using System.Collections.Immutable;
using FON.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FON.Generator.Test;


/// <summary>
/// Types the generator cannot map are compile-time errors that name the type or member, and
/// valid types compile with no error.
/// </summary>
public class GeneratorDiagnosticsTests {
    private const string Usings = "using System; using System.Collections.Generic; using FON.Records; using FON.Types;\n";

    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest);

    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.Length > 0)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray();


    [Fact]
    public void ValidRecords_CompileWithoutErrors() {
        GeneratorRun run = Run("""
            namespace Probe;

            public enum Mood { calm, angry }

            [FonRecord]
            public sealed partial record Point(int x, int y);

            [FonRecord]
            public readonly partial record struct Size(double width, double height);

            public static partial class Outer {
                [FonRecord]
                internal sealed partial record Inner(string name, Point at, IReadOnlyList<Point> path, Size? size, int? count, Mood mood, Mood[] moods);
            }

            [FonRecord]
            public sealed partial class Settings {
                public string name { get; set; } = "";
                public List<string> tags { get; init; } = new List<string>();
                public HashSet<int> Evens => new HashSet<int> { 2, 4 };
                public FonCollection? extra { get; set; }
                public RawData? blob { get; set; }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
        Assert.Equal(4, run.GeneratedSources);
    }


    [Fact]
    public void NonPartialType_IsFON001() {
        GeneratorRun run = Run("""
            [FonRecord]
            public sealed record Point(int x, int y);
            """);

        Diagnostic diagnostic = Assert.Single(run.GeneratorDiagnostics);
        Assert.Equal("FON001", diagnostic.Id);
        Assert.Contains("Point", diagnostic.GetMessage());
        Assert.Equal(0, run.GeneratedSources);
    }


    [Fact]
    public void NonPartialContainingType_IsFON001() {
        GeneratorRun run = Run("""
            public static class Outer {
                [FonRecord]
                public sealed partial record Point(int x, int y);
            }
            """);

        Assert.Equal("FON001", Assert.Single(run.GeneratorDiagnostics).Id);
    }


    [Theory]
    [InlineData("DateTime when", "when")]
    [InlineData("Dictionary<string, int> map", "map")]
    [InlineData("object anything", "anything")]
    [InlineData("List<int?> numbers", "numbers")]
    [InlineData("RawData[] blobs", "blobs")]
    [InlineData("int[,] grid", "grid")]
    [InlineData("HashSet<int> unique", "unique")]
    [InlineData("string? [] maybe", null)]
    [InlineData("Plain nested", "nested")]
    [InlineData("List<Plain> nestedList", "nestedList")]
    [InlineData("decimal price", "price")]
    public void UnsupportedMemberType_IsFON002_NamingTheMember(string parameter, string? member) {
        GeneratorRun run = Run($$"""
            public sealed class Plain {
                public int value { get; set; }
            }

            [FonRecord]
            public sealed partial record Holder({{parameter}});
            """);

        if (member is null) {
            Assert.Empty(run.GeneratorDiagnostics);
            Assert.Empty(run.Errors);
            return;
        }
        Assert.NotEmpty(run.GeneratorDiagnostics);
        Assert.All(run.GeneratorDiagnostics, diagnostic => Assert.Equal("FON002", diagnostic.Id));
        Assert.Contains($"'{member}'", run.GeneratorDiagnostics[0].GetMessage());
        Assert.Equal(member, run.Source.Substring(run.GeneratorDiagnostics[0].Location.SourceSpan.Start, member.Length));
    }


    [Fact]
    public void UnmarkedClass_SuggestsTheAttribute() {
        GeneratorRun run = Run("""
            public sealed class Plain {
                public int value { get; set; }
            }

            [FonRecord]
            public sealed partial record Holder(Plain nested);
            """);

        Assert.Contains("mark it with [FonRecord]", Assert.Single(run.GeneratorDiagnostics).GetMessage());
    }


    [Fact]
    public void ReadOnlyUnsupportedList_IsFine_WhenItIsOnlyWritten() {
        GeneratorRun run = Run("""
            [FonRecord]
            public sealed partial record Holder(int count) {
                public HashSet<int> Evens => new HashSet<int> { count * 2 };
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
    }


    [Fact]
    public void NoUsableConstructor_IsFON003() {
        GeneratorRun run = Run("""
            [FonRecord]
            public sealed partial class Renamed {
                public Renamed(int other) {
                    value = other;
                }

                public int value { get; }
            }
            """);

        Diagnostic diagnostic = Assert.Single(run.GeneratorDiagnostics);
        Assert.Equal("FON003", diagnostic.Id);
        Assert.Contains("Renamed", diagnostic.GetMessage());
    }


    [Fact]
    public void StructWithoutConstructors_IsFON003() {
        GeneratorRun run = Run("""
            [FonRecord]
            public partial struct Counter {
                public int hits { get; set; }
            }
            """);

        Assert.Equal("FON003", Assert.Single(run.GeneratorDiagnostics).Id);
    }


    [Fact]
    public void RequiredMembersNotSetByTheConstructor_IsFON003() {
        GeneratorRun run = Run("""
            [FonRecord]
            public sealed partial record Named(int id) {
                public required string name { get; init; }
            }
            """);

        Assert.Equal("FON003", Assert.Single(run.GeneratorDiagnostics).Id);
    }


    [Fact]
    public void RequiredMembersSetThroughTheInitializer_AreFine() {
        GeneratorRun run = Run("""
            [FonRecord]
            public sealed partial class Named {
                public required string name { get; init; }
                public int id { get; set; }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
    }


    [Theory]
    [InlineData("public sealed partial record Box<T>(T value);")]
    [InlineData("public abstract partial record Shape(int sides);")]
    [InlineData("public static partial class Tools { }")]
    [InlineData("public ref partial struct Span { public int length { get; set; } }")]
    public void UnsupportedType_IsFON004(string declaration) {
        GeneratorRun run = Run("[FonRecord]\n" + declaration);

        Assert.Equal("FON004", Assert.Single(run.GeneratorDiagnostics).Id);
    }


    [Fact]
    public void InaccessibleInheritedSetter_IsFON005() {
        GeneratorRun run = Run("""
            public class Base {
                public int id { get; private set; }
            }

            [FonRecord]
            public sealed partial class Derived : Base {
                public string name { get; set; } = "";
            }
            """);

        Diagnostic diagnostic = Assert.Single(run.GeneratorDiagnostics);
        Assert.Equal("FON005", diagnostic.Id);
        Assert.Contains("'id'", diagnostic.GetMessage());
    }


    [Fact]
    public void PrivateSetterOfTheTypeItself_IsFine() {
        GeneratorRun run = Run("""
            [FonRecord]
            public sealed partial class Counter {
                public int hits { get; private set; }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
    }


    [Fact]
    public void PropertyHiddenWithAnotherType_IsFON006() {
        GeneratorRun run = Run("""
            public class Base {
                public int id { get; set; }
            }

            [FonRecord]
            public sealed partial class Derived : Base {
                public new string id { get; set; } = "";
            }
            """);

        Assert.Contains(run.GeneratorDiagnostics, diagnostic => diagnostic.Id == "FON006");
    }



    private static GeneratorRun Run(string source) {
        string text = Usings + source;
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(text, ParseOptions)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new FonRecordGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);
        int generated = driver.GetRunResult().Results.Sum(result => result.GeneratedSources.Length);
        ImmutableArray<Diagnostic> errors = output.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        return new GeneratorRun(text, diagnostics, errors, generated);
    }


    private sealed record GeneratorRun(string Source, ImmutableArray<Diagnostic> GeneratorDiagnostics, ImmutableArray<Diagnostic> Errors, int GeneratedSources);
}
