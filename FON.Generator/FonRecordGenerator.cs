using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FON.Generator;


/// <summary>
/// Implements IFonRecord on every partial type marked with [FonRecord], so that it maps to and
/// from a FonCollection without reflection.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class FonRecordGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        IncrementalValuesProvider<GenerationResult> results = context.SyntaxProvider.ForAttributeWithMetadataName(
            RecordAnalyzer.AttributeName,
            static (node, cancel) => node is TypeDeclarationSyntax,
            static (attributeContext, cancel) => RecordAnalyzer.Analyze(attributeContext, cancel)
        );
        context.RegisterSourceOutput(results, static (output, result) => Produce(output, result));
    }


    private static void Produce(SourceProductionContext output, GenerationResult result) {
        foreach (DiagnosticModel diagnostic in result.Diagnostics) {
            output.ReportDiagnostic(diagnostic.ToDiagnostic());
        }
        if (result.Model is not null) {
            output.AddSource(result.Model.HintName, SourceText.From(RecordEmitter.Emit(result.Model), System.Text.Encoding.UTF8));
        }
    }
}
