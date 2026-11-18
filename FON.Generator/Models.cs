using System.Collections;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FON.Generator;


/// <summary>What a single stored value is, element type of a list included.</summary>
internal enum ValueKind {
    Number,
    String,
    RawData,
    Collection,
    Enum,
    Record
}


/// <summary>How a member holds its value.</summary>
internal enum ShapeKind {
    Single,
    Nullable,
    Array,
    List
}


/// <param name="TypeName">The value or element type, fully qualified, without a nullable annotation.</param>
internal sealed record ValueModel(ValueKind Kind, string TypeName, bool IsValueType);


/// <param name="Key">The FON key: the property or parameter name as declared.</param>
/// <param name="Identifier">The property name as a C# identifier, escaped when it is a keyword.</param>
/// <param name="Optional">A missing key reads as null, or as an empty list, instead of throwing.</param>
internal sealed record MemberModel(string Key, string Identifier, ShapeKind Shape, ValueModel Value, bool Optional);


/// <param name="Keyword">The declaration keyword: class, struct, record, record struct or interface.</param>
internal sealed record TypeDeclarationModel(string Keyword, string Name);


/// <param name="Containing">The types that contain the record, outermost first.</param>
/// <param name="FullName">The record type, fully qualified with global::.</param>
/// <param name="Written">The members written, in the order of their keys.</param>
/// <param name="UsesConstructor">Reading passes <paramref name="Read"/> to a constructor; otherwise it sets them as properties.</param>
/// <param name="Read">The constructor parameters in order, or the properties to set.</param>
internal sealed record RecordModel(
    string? Namespace,
    EquatableArray<TypeDeclarationModel> Containing,
    TypeDeclarationModel Declaration,
    string FullName,
    bool IsValueType,
    string HintName,
    EquatableArray<MemberModel> Written,
    bool UsesConstructor,
    EquatableArray<MemberModel> Read
);


internal sealed record LocationModel(string FilePath, TextSpan Span, LinePositionSpan LineSpan) {
    public static LocationModel? From(Location? location) {
        if (location is null || !location.IsInSource || location.SourceTree is null) {
            return null;
        }
        return new LocationModel(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }


    public Location ToLocation() {
        return Location.Create(FilePath, Span, LineSpan);
    }
}


internal sealed record DiagnosticModel(DiagnosticDescriptor Descriptor, LocationModel? Location, EquatableArray<string> Arguments) {
    public Diagnostic ToDiagnostic() {
        object[] arguments = Arguments.AsArray().Cast<object>().ToArray();
        return Diagnostic.Create(Descriptor, Location?.ToLocation(), arguments);
    }
}


/// <summary>The result for one marked type: a model to emit, or the diagnostics that stop it.</summary>
internal sealed record GenerationResult(RecordModel? Model, EquatableArray<DiagnosticModel> Diagnostics);


/// <summary>An immutable array compared by its elements, so that models cache between runs.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T> {
    private readonly T[]? items;


    public EquatableArray(T[] items) {
        this.items = items;
    }


    public int Count => items?.Length ?? 0;


    public T[] AsArray() {
        return items ?? Array.Empty<T>();
    }


    public bool Equals(EquatableArray<T> other) {
        return AsArray().SequenceEqual(other.AsArray());
    }


    public override bool Equals(object? obj) {
        return obj is EquatableArray<T> other && Equals(other);
    }


    public override int GetHashCode() {
        int hash = 17;
        foreach (T item in AsArray()) {
            hash = unchecked((hash * 31) + (item?.GetHashCode() ?? 0));
        }
        return hash;
    }


    public IEnumerator<T> GetEnumerator() {
        return ((IEnumerable<T>)AsArray()).GetEnumerator();
    }


    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}



internal static class EquatableArray {
    public static EquatableArray<T> From<T>(IEnumerable<T> items) {
        return new EquatableArray<T>(items.ToArray());
    }


    public static EquatableArray<T> From<T>(ImmutableArray<T> items) {
        return new EquatableArray<T>(items.ToArray());
    }
}
