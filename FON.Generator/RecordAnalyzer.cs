using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace FON.Generator;


/// <summary>
/// Turns a type marked with [FonRecord] into a <see cref="RecordModel"/>, following the rules
/// of a reflection mapper: public instance properties with a getter in reflection order, a
/// constructor whose parameter names all match property names, or a parameterless constructor
/// and settable properties.
/// </summary>
internal sealed class RecordAnalyzer {
    public const string AttributeName = "FON.Records.FonRecordAttribute";

    private const string InterfaceName = "FON.Records.IFonRecord`1";
    private const string CollectionName = "FON.Types.FonCollection";
    private const string RawDataName = "FON.Types.RawData";
    private const string CodeAnalysisNamespace = "System.Diagnostics.CodeAnalysis";

    private readonly Compilation compilation;
    private readonly INamedTypeSymbol type;
    private readonly Location typeLocation;
    private readonly INamedTypeSymbol? attributeSymbol;
    private readonly INamedTypeSymbol? interfaceSymbol;
    private readonly INamedTypeSymbol? collectionSymbol;
    private readonly INamedTypeSymbol? rawDataSymbol;
    private readonly INamedTypeSymbol enumerableSymbol;
    private readonly INamedTypeSymbol? listSymbol;
    private readonly List<DiagnosticModel> diagnostics = new List<DiagnosticModel>();
    private bool hasErrorTypes;


    private RecordAnalyzer(Compilation compilation, INamedTypeSymbol type, Location typeLocation) {
        this.compilation = compilation;
        this.type = type;
        this.typeLocation = typeLocation;
        attributeSymbol = compilation.GetTypeByMetadataName(AttributeName);
        interfaceSymbol = compilation.GetTypeByMetadataName(InterfaceName);
        collectionSymbol = compilation.GetTypeByMetadataName(CollectionName);
        rawDataSymbol = compilation.GetTypeByMetadataName(RawDataName);
        enumerableSymbol = compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T);
        listSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
    }


    public static GenerationResult Analyze(GeneratorAttributeSyntaxContext context, CancellationToken cancel) {
        if (context.TargetSymbol is not INamedTypeSymbol type) {
            return new GenerationResult(null, default);
        }
        Location location = context.TargetNode is TypeDeclarationSyntax declaration
            ? declaration.Identifier.GetLocation()
            : context.TargetNode.GetLocation();
        RecordAnalyzer analyzer = new RecordAnalyzer(context.SemanticModel.Compilation, type, location);
        RecordModel? model = analyzer.Build(cancel);
        return new GenerationResult(model, EquatableArray.From(analyzer.diagnostics));
    }


    private RecordModel? Build(CancellationToken cancel) {
        string? typeProblem = UnsupportedTypeReason();
        if (typeProblem is not null) {
            Report(Diagnostics.UnsupportedType, typeLocation, Display(type), typeProblem);
            return null;
        }
        if (!IsPartialChain(cancel)) {
            Report(Diagnostics.NotPartial, typeLocation, Display(type));
            return null;
        }
        if (interfaceSymbol is null || collectionSymbol is null || rawDataSymbol is null || listSymbol is null) {
            return null;
        }

        List<IPropertySymbol> readable = new List<IPropertySymbol>();
        HashSet<string> readableNames = new HashSet<string>(StringComparer.Ordinal);
        CollectProperties(readable, readableNames);
        ReportDuplicateKeys(readable);

        List<MemberModel> written = new List<MemberModel>();
        foreach (IPropertySymbol property in readable) {
            cancel.ThrowIfCancellationRequested();
            if (!IsAccessible(property.GetMethod!)) {
                Report(Diagnostics.InaccessibleMember, LocationOf(property), property.Name, Display(type), "getter");
                continue;
            }
            MemberModel? member = Classify(property.Name, property.Type, forRead: false, optional: false, property);
            if (member is not null) {
                written.Add(member);
            }
        }

        List<MemberModel> read = new List<MemberModel>();
        bool usesConstructor = AnalyzeConstruction(readable, readableNames, read);

        if (diagnostics.Count > 0 || hasErrorTypes) {
            return null;
        }
        return new RecordModel(
            NamespaceOf(type),
            EquatableArray.From(ContainingDeclarations()),
            Declaration(type),
            FullName(type),
            type.IsValueType,
            HintName(),
            EquatableArray.From(written),
            usesConstructor,
            EquatableArray.From(read)
        );
    }



    // ==================== THE TYPE ====================

    private string? UnsupportedTypeReason() {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) {
            if (current.IsGenericType) {
                return "generic types are not supported";
            }
        }
        if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct) {
            return "only classes, structs and records are supported";
        }
        if (type.IsStatic) {
            return "a static class has no instances";
        }
        if (type.IsAbstract) {
            return "an abstract class cannot be created when reading";
        }
        if (type.IsRefLikeType) {
            return "ref structs are not supported";
        }
        return null;
    }


    private bool IsPartialChain(CancellationToken cancel) {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) {
            foreach (SyntaxReference reference in current.DeclaringSyntaxReferences) {
                if (reference.GetSyntax(cancel) is not TypeDeclarationSyntax declaration) {
                    return false;
                }
                if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword)) {
                    return false;
                }
            }
        }
        return true;
    }



    // ==================== PROPERTIES ====================

    /// <summary>
    /// Collects the public instance properties with a getter in the order reflection returns
    /// them: the type's own in declaration order, then each base type's. A base property with
    /// the same name and signature as a property of a more derived type is hidden by it.
    /// </summary>
    /// <param name="readableNames">Every readable property name, indexers included, for matching constructor parameters.</param>
    private void CollectProperties(List<IPropertySymbol> readable, HashSet<string> readableNames) {
        List<IPropertySymbol> moreDerived = new List<IPropertySymbol>();
        for (INamedTypeSymbol? current = type; current is not null && !IsRootType(current); current = current.BaseType) {
            List<IPropertySymbol> declared = new List<IPropertySymbol>();
            foreach (ISymbol symbol in current.GetMembers()) {
                if (symbol is not IPropertySymbol property || property.IsStatic) {
                    continue;
                }
                if (IsHidden(property, moreDerived)) {
                    continue;
                }
                declared.Add(property);
                if (property.DeclaredAccessibility != Accessibility.Public || property.GetMethod is null) {
                    continue;
                }
                readableNames.Add(property.MetadataName);
                if (!property.IsIndexer) {
                    readable.Add(property);
                }
            }
            moreDerived.AddRange(declared);
        }
    }


    private static bool IsRootType(INamedTypeSymbol candidate) {
        return candidate.SpecialType == SpecialType.System_Object || candidate.SpecialType == SpecialType.System_ValueType;
    }


    private static bool IsHidden(IPropertySymbol property, List<IPropertySymbol> moreDerived) {
        foreach (IPropertySymbol other in moreDerived) {
            if (other.MetadataName == property.MetadataName && SameSignature(other, property)) {
                return true;
            }
        }
        return false;
    }


    private static bool SameSignature(IPropertySymbol first, IPropertySymbol second) {
        if (!SymbolEqualityComparer.Default.Equals(first.Type, second.Type)) {
            return false;
        }
        if (first.Parameters.Length != second.Parameters.Length) {
            return false;
        }
        for (int index = 0; index < first.Parameters.Length; index++) {
            if (!SymbolEqualityComparer.Default.Equals(first.Parameters[index].Type, second.Parameters[index].Type)) {
                return false;
            }
        }
        return true;
    }


    private void ReportDuplicateKeys(List<IPropertySymbol> readable) {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (IPropertySymbol property in readable) {
            if (!seen.Add(property.Name)) {
                Report(Diagnostics.DuplicateKey, LocationOf(property), Display(type), property.Name);
            }
        }
    }


    private bool IsAccessible(ISymbol symbol) {
        return compilation.IsSymbolAccessibleWithin(symbol, type, type);
    }



    // ==================== CONSTRUCTION ====================

    /// <summary>
    /// Chooses how to read the type and fills <paramref name="read"/>: the public constructor
    /// with the most parameters whose names all match readable property names, or the public
    /// parameterless constructor and every settable property.
    /// </summary>
    /// <returns>True when reading calls a constructor with parameters.</returns>
    private bool AnalyzeConstruction(List<IPropertySymbol> readable, HashSet<string> readableNames, List<MemberModel> read) {
        List<IMethodSymbol> constructors = new List<IMethodSymbol>();
        foreach (IMethodSymbol constructor in type.InstanceConstructors) {
            if (constructor.DeclaredAccessibility != Accessibility.Public) {
                continue;
            }
            bool isImplicitStructDefault = type.IsValueType && constructor.IsImplicitlyDeclared && constructor.Parameters.Length == 0;
            if (!isImplicitStructDefault) {
                constructors.Add(constructor);
            }
        }

        IMethodSymbol? best = null;
        foreach (IMethodSymbol constructor in constructors) {
            if (!constructor.Parameters.All(parameter => readableNames.Contains(parameter.Name))) {
                continue;
            }
            if (best is null || constructor.Parameters.Length > best.Parameters.Length) {
                best = constructor;
            }
        }

        if (best is not null && best.Parameters.Length > 0) {
            if (!SetsRequiredMembers(best) && RequiredMembers().Count > 0) {
                Report(
                    Diagnostics.NoConstructor,
                    typeLocation,
                    Display(type),
                    "it has required members, and the constructor it would be read with does not set them"
                );
                return true;
            }
            foreach (IParameterSymbol parameter in best.Parameters) {
                MemberModel? member = Classify(parameter.Name, parameter.Type, forRead: true, IsNullableParameter(parameter), parameter);
                if (member is not null) {
                    read.Add(member);
                }
            }
            return true;
        }

        IMethodSymbol? parameterless = constructors.FirstOrDefault(constructor => constructor.Parameters.Length == 0);
        if (parameterless is null) {
            Report(
                Diagnostics.NoConstructor,
                typeLocation,
                Display(type),
                "it has no public constructor whose parameter names all match public property names, and no public parameterless constructor"
            );
            return false;
        }

        HashSet<string> assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (IPropertySymbol property in readable) {
            if (property.SetMethod is null) {
                continue;
            }
            if (!IsAccessible(property.SetMethod)) {
                Report(Diagnostics.InaccessibleMember, LocationOf(property), property.Name, Display(type), "setter");
                continue;
            }
            MemberModel? member = Classify(property.Name, property.Type, forRead: true, IsNullableProperty(property), property);
            if (member is not null) {
                read.Add(member);
                assigned.Add(property.Name);
            }
        }
        if (!SetsRequiredMembers(parameterless) && RequiredMembers().Any(name => !assigned.Contains(name))) {
            Report(
                Diagnostics.NoConstructor,
                typeLocation,
                Display(type),
                "it has required members that are not public settable properties"
            );
        }
        return false;
    }


    private static bool SetsRequiredMembers(IMethodSymbol constructor) {
        return constructor.GetAttributes().Any(attribute => IsCodeAnalysisAttribute(attribute, "SetsRequiredMembersAttribute"));
    }


    private List<string> RequiredMembers() {
        List<string> names = new List<string>();
        for (INamedTypeSymbol? current = type; current is not null && !IsRootType(current); current = current.BaseType) {
            foreach (ISymbol symbol in current.GetMembers()) {
                bool required = symbol switch {
                    IPropertySymbol property => property.IsRequired,
                    IFieldSymbol field => field.IsRequired,
                    _ => false
                };
                if (required) {
                    names.Add(symbol.Name);
                }
            }
        }
        return names;
    }



    // ==================== NULLABILITY ====================

    /// <summary>
    /// Whether a missing key may read as null for a constructor parameter: the parameter's
    /// nullable read state, as reflection's NullabilityInfoContext reports it.
    /// </summary>
    private static bool IsNullableParameter(IParameterSymbol parameter) {
        if (IsNullableValueType(parameter.Type)) {
            return true;
        }
        if (parameter.Type.IsValueType) {
            return false;
        }
        ImmutableArray<AttributeData> attributes = parameter.GetAttributes();
        if (attributes.Any(attribute => IsCodeAnalysisAttribute(attribute, "NotNullAttribute"))) {
            return false;
        }
        if (attributes.Any(attribute => IsCodeAnalysisAttribute(attribute, "MaybeNullAttribute") || IsCodeAnalysisAttribute(attribute, "MaybeNullWhenAttribute"))) {
            return true;
        }
        return parameter.NullableAnnotation == NullableAnnotation.Annotated;
    }


    /// <summary>
    /// Whether a missing key may read as null for a settable property: the property's nullable
    /// write state, as reflection's NullabilityInfoContext reports it.
    /// </summary>
    private static bool IsNullableProperty(IPropertySymbol property) {
        if (IsNullableValueType(property.Type)) {
            return true;
        }
        if (property.Type.IsValueType) {
            return false;
        }
        List<AttributeData> attributes = new List<AttributeData>(property.GetAttributes());
        if (property.SetMethod is { Parameters.Length: > 0 } setter) {
            attributes.AddRange(setter.Parameters[setter.Parameters.Length - 1].GetAttributes());
        }
        if (attributes.Any(attribute => IsCodeAnalysisAttribute(attribute, "DisallowNullAttribute"))) {
            return false;
        }
        if (attributes.Any(attribute => IsCodeAnalysisAttribute(attribute, "AllowNullAttribute"))) {
            return true;
        }
        return property.NullableAnnotation == NullableAnnotation.Annotated;
    }


    private static bool IsNullableValueType(ITypeSymbol candidate) {
        return candidate.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }


    private static bool IsCodeAnalysisAttribute(AttributeData attribute, string name) {
        INamedTypeSymbol? attributeClass = attribute.AttributeClass;
        return attributeClass is not null
            && attributeClass.Name == name
            && attributeClass.ContainingNamespace.ToDisplayString() == CodeAnalysisNamespace;
    }



    // ==================== MEMBER TYPES ====================

    /// <summary>Classifies a member type, or reports why FON cannot store it.</summary>
    /// <param name="owner">The property or parameter, for the diagnostic.</param>
    private MemberModel? Classify(string key, ITypeSymbol memberType, bool forRead, bool optional, ISymbol owner) {
        if (ContainsErrorType(memberType)) {
            hasErrorTypes = true;
            return null;
        }
        MemberModel? member = TryClassify(key, memberType, forRead, optional, out string? problem);
        if (member is null) {
            Report(Diagnostics.UnsupportedMember, LocationOf(owner), owner.Name, Display(type), problem ?? "its type is not supported");
        }
        return member;
    }


    /// <summary>
    /// Classifies in the order of the reflection mapper: a type FON stores directly, then an
    /// array or a generic enumerable, then a nested record. Nullable value types and enums
    /// are added on top.
    /// </summary>
    private MemberModel? TryClassify(string key, ITypeSymbol memberType, bool forRead, bool optional, out string? problem) {
        problem = null;
        string identifier = Escape(key);

        if (memberType is INamedTypeSymbol nullable && IsNullableValueType(nullable)) {
            ITypeSymbol underlying = nullable.TypeArguments[0];
            ValueModel? inner = ClassifyNamed(underlying) ?? ClassifyStored(underlying);
            if (inner is null || !inner.IsValueType) {
                problem = $"'{Display(memberType)}' is not supported; a nullable value must be a number, a bool, an enum or a record struct";
                return null;
            }
            return new MemberModel(key, identifier, ShapeKind.Nullable, inner, Optional: true);
        }

        ValueModel? stored = ClassifyStored(memberType);
        if (stored is not null) {
            return new MemberModel(key, identifier, ShapeKind.Single, stored, optional);
        }

        if (memberType is IArrayTypeSymbol array) {
            if (array.Rank != 1) {
                problem = "multi-dimensional arrays are not supported";
                return null;
            }
            ValueModel? element = ClassifyElement(array.ElementType, out problem);
            if (element is null) {
                return null;
            }
            return new MemberModel(key, identifier, ShapeKind.Array, element, optional);
        }

        if (memberType is INamedTypeSymbol { IsGenericType: true } generic) {
            List<ITypeSymbol> elements = EnumerableElements(generic);
            if (elements.Count > 1) {
                problem = "it implements IEnumerable<T> for more than one T";
                return null;
            }
            if (elements.Count == 1) {
                if (IsKeyValuePair(elements[0])) {
                    problem = "dictionaries are not supported";
                    return null;
                }
                ValueModel? element = ClassifyElement(elements[0], out problem);
                if (element is null) {
                    return null;
                }
                if (forRead && !AcceptsList(generic, elements[0])) {
                    problem = $"a list is read back as List<{Display(elements[0])}>, which cannot be assigned to '{Display(memberType)}'";
                    return null;
                }
                return new MemberModel(key, identifier, ShapeKind.List, element, optional);
            }
        }

        ValueModel? named = ClassifyNamed(memberType);
        if (named is not null) {
            return new MemberModel(key, identifier, ShapeKind.Single, named, optional);
        }

        problem = UnsupportedReason(memberType);
        return null;
    }


    /// <summary>A type that a FonCollection stores as is.</summary>
    private ValueModel? ClassifyStored(ITypeSymbol candidate) {
        string? number = candidate.SpecialType switch {
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Boolean => "bool",
            _ => null
        };
        if (number is not null) {
            return new ValueModel(ValueKind.Number, number, IsValueType: true);
        }
        if (candidate.SpecialType == SpecialType.System_String) {
            return new ValueModel(ValueKind.String, "string", IsValueType: false);
        }
        if (SymbolEqualityComparer.Default.Equals(candidate, rawDataSymbol)) {
            return new ValueModel(ValueKind.RawData, FullName(candidate), IsValueType: false);
        }
        if (SymbolEqualityComparer.Default.Equals(candidate, collectionSymbol)) {
            return new ValueModel(ValueKind.Collection, FullName(candidate), IsValueType: false);
        }
        return null;
    }


    /// <summary>An enum, or a type that maps itself as a FON record.</summary>
    private ValueModel? ClassifyNamed(ITypeSymbol candidate) {
        if (candidate.TypeKind == TypeKind.Enum) {
            return new ValueModel(ValueKind.Enum, FullName(candidate), IsValueType: true);
        }
        if (candidate is INamedTypeSymbol named && IsFonRecord(named)) {
            return new ValueModel(ValueKind.Record, FullName(named), named.IsValueType);
        }
        return null;
    }


    private ValueModel? ClassifyElement(ITypeSymbol element, out string? problem) {
        problem = null;
        if (IsNullableValueType(element)) {
            problem = "lists of nullable values are not supported";
            return null;
        }
        ValueModel? stored = ClassifyStored(element);
        if (stored is not null) {
            if (stored.Kind == ValueKind.RawData) {
                problem = "lists of raw values are not supported";
                return null;
            }
            return stored;
        }
        ValueModel? named = ClassifyNamed(element);
        if (named is not null) {
            return named;
        }
        problem = "its elements cannot be stored: " + UnsupportedReason(element);
        return null;
    }


    private bool IsFonRecord(INamedTypeSymbol candidate) {
        if (candidate.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))) {
            return true;
        }
        foreach (INamedTypeSymbol implemented in candidate.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, interfaceSymbol)
                && SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], candidate)) {
                return true;
            }
        }
        return false;
    }


    /// <summary>The distinct T of every IEnumerable&lt;T&gt; that the type is or implements.</summary>
    private List<ITypeSymbol> EnumerableElements(INamedTypeSymbol generic) {
        List<ITypeSymbol> elements = new List<ITypeSymbol>();
        List<INamedTypeSymbol> candidates = new List<INamedTypeSymbol> { generic };
        candidates.AddRange(generic.AllInterfaces);
        foreach (INamedTypeSymbol candidate in candidates) {
            if (!SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, enumerableSymbol)) {
                continue;
            }
            ITypeSymbol element = candidate.TypeArguments[0];
            if (!elements.Any(known => SymbolEqualityComparer.Default.Equals(known, element))) {
                elements.Add(element);
            }
        }
        return elements;
    }


    private static bool IsKeyValuePair(ITypeSymbol candidate) {
        return candidate is INamedTypeSymbol { Name: "KeyValuePair", Arity: 2 } named
            && named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic";
    }


    /// <summary>Whether a List&lt;element&gt; can be assigned to the member type.</summary>
    private bool AcceptsList(INamedTypeSymbol target, ITypeSymbol element) {
        INamedTypeSymbol list = listSymbol!.Construct(element);
        CommonConversion conversion = compilation.ClassifyCommonConversion(list, target);
        return conversion.IsIdentity || (conversion.IsImplicit && conversion.IsReference);
    }


    private static string UnsupportedReason(ITypeSymbol candidate) {
        bool declaredInSource = candidate.Locations.Any(location => location.IsInSource);
        if (candidate is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct, IsGenericType: false } && declaredInSource) {
            return $"'{Display(candidate)}' is not a FON record; mark it with [FonRecord] and make it partial";
        }
        return $"FON cannot store a value of type '{Display(candidate)}'";
    }


    private static bool ContainsErrorType(ITypeSymbol candidate) {
        if (candidate.TypeKind == TypeKind.Error) {
            return true;
        }
        if (candidate is IArrayTypeSymbol array) {
            return ContainsErrorType(array.ElementType);
        }
        if (candidate is INamedTypeSymbol named) {
            return named.TypeArguments.Any(ContainsErrorType);
        }
        return false;
    }



    // ==================== NAMES ====================

    private static string? NamespaceOf(INamedTypeSymbol symbol) {
        if (symbol.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace) {
            return null;
        }
        SymbolDisplayFormat format = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);
        return symbol.ContainingNamespace.ToDisplayString(format);
    }


    private List<TypeDeclarationModel> ContainingDeclarations() {
        List<TypeDeclarationModel> containing = new List<TypeDeclarationModel>();
        for (INamedTypeSymbol? current = type.ContainingType; current is not null; current = current.ContainingType) {
            containing.Insert(0, Declaration(current));
        }
        return containing;
    }


    private static TypeDeclarationModel Declaration(INamedTypeSymbol symbol) {
        string keyword;
        if (symbol.IsRecord) {
            keyword = symbol.IsValueType ? "record struct" : "record";
        } else if (symbol.TypeKind == TypeKind.Struct) {
            keyword = "struct";
        } else if (symbol.TypeKind == TypeKind.Interface) {
            keyword = "interface";
        } else {
            keyword = "class";
        }
        return new TypeDeclarationModel(keyword, Escape(symbol.Name));
    }


    private string HintName() {
        List<string> names = new List<string>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) {
            names.Insert(0, current.Name);
        }
        string prefix = type.ContainingNamespace is null || type.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : type.ContainingNamespace.ToDisplayString() + ".";
        return prefix + string.Join("+", names) + ".FonRecord.g.cs";
    }


    private static string FullName(ITypeSymbol symbol) {
        return symbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }


    private static string Display(ISymbol symbol) {
        return symbol.ToDisplayString();
    }


    private static string Escape(string name) {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
    }



    // ==================== DIAGNOSTICS ====================

    private Location LocationOf(ISymbol symbol) {
        foreach (Location location in symbol.Locations) {
            if (location.IsInSource && location.SourceTree is not null && compilation.ContainsSyntaxTree(location.SourceTree)) {
                return location;
            }
        }
        return typeLocation;
    }


    /// <summary>
    /// Records a diagnostic once: the property and the constructor parameter of a positional
    /// record share a location and would report the same problem twice.
    /// </summary>
    private void Report(DiagnosticDescriptor descriptor, Location location, params string[] arguments) {
        DiagnosticModel diagnostic = new DiagnosticModel(descriptor, LocationModel.From(location), new EquatableArray<string>(arguments));
        if (!diagnostics.Contains(diagnostic)) {
            diagnostics.Add(diagnostic);
        }
    }
}
