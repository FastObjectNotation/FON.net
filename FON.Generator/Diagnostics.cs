using Microsoft.CodeAnalysis;

namespace FON.Generator;


internal static class Diagnostics {
    private const string Category = "FON";


    public static readonly DiagnosticDescriptor NotPartial = new DiagnosticDescriptor(
        "FON001",
        "FON record type is not partial",
        "'{0}' must be partial, and so must every type that contains it, so that the FON generator can implement IFonRecord",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );


    public static readonly DiagnosticDescriptor UnsupportedMember = new DiagnosticDescriptor(
        "FON002",
        "FON cannot store a member",
        "Cannot map member '{0}' of '{1}': {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );


    public static readonly DiagnosticDescriptor NoConstructor = new DiagnosticDescriptor(
        "FON003",
        "FON record has no usable constructor",
        "Cannot read '{0}': {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );


    public static readonly DiagnosticDescriptor UnsupportedType = new DiagnosticDescriptor(
        "FON004",
        "Type cannot be a FON record",
        "'{0}' cannot be a FON record: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );


    public static readonly DiagnosticDescriptor InaccessibleMember = new DiagnosticDescriptor(
        "FON005",
        "FON record member is not accessible",
        "Cannot map member '{0}' of '{1}': its {2} is not accessible from '{1}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );


    public static readonly DiagnosticDescriptor DuplicateKey = new DiagnosticDescriptor(
        "FON006",
        "FON record has two properties with the same name",
        "'{0}' has two public properties named '{1}', but each FON key must be unique",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
