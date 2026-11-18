using System.Text;
using System.Text.Json;
using FON.Core;
using FON.Records;
using FON.Types;

namespace FON.Test.Records;


/// <summary>
/// Generated codecs against the reflection mapper they replace: the same values write the same
/// bytes, each reads what the other wrote, and text with missing or mismatched keys reads to the
/// same result or fails in both.
/// </summary>
public class RecordCompatibilityTests {
    private const int LongTextLength = 100_000;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions();


    // ==================== SAME BYTES, BOTH DIRECTIONS ====================

    [Fact]
    public void DockSnapshot_IsCompatible() {
        DockSnapshot snapshot = new DockSnapshot(
            DockSnapshot.CurrentVersion,
            "explorer",
            ["output", "terminal"],
            [
                new DockSnapshotNode(DockSnapshotNode.Split, "horizontal", 0.25, null, null, false, []),
                new DockSnapshotNode(DockSnapshotNode.Group, null, 0.5, null, "explorer", true, ["explorer", "search"]),
                new DockSnapshotNode(DockSnapshotNode.Panel, null, 1, "editor", null, false, ["editor"])
            ]
        );

        AssertCompatible(snapshot);
    }


    [Fact]
    public void DockSnapshot_WithNoActivePanelAndEmptyLists_IsCompatible() {
        AssertCompatible(new DockSnapshot(1, null, [], []));
    }


    [Fact]
    public void RecentDocument_IsCompatible() {
        RecentDocument document = new RecentDocument(
            [
                new RecentEntry("C:\\work\\one.sln", 3, 638_000_000_000_000_000L, true),
                new RecentEntry("/home/user/two.sln", 0, long.MaxValue, false)
            ],
            ["C:\\work", "/home/user"]
        );

        AssertCompatible(document);
    }


    [Fact]
    public void ExplorerExpansionDocument_IsCompatible() {
        ExplorerExpansionDocument document = new ExplorerExpansionDocument([
            new ExplorerExpansionEntry("one.sln", 1L, "src/Core", true),
            new ExplorerExpansionEntry("one.sln", 2L, "src/Ui \"quoted\"", false)
        ]);

        AssertCompatible(document);
    }


    [Fact]
    public void GrantTableSnapshot_IsCompatible() {
        AssertCompatible(new GrantTableSnapshot([
            new GrantEntry("plugin.alpha", "files.read"),
            new GrantEntry("plugin.beta", "net.connect")
        ]));
        AssertCompatible(new GrantTableSnapshot([]));
    }


    [Fact]
    public void StoredValues_AreCompatible() {
        AssertCompatible(new StoredCopies(["a", "b", "c"]));
        AssertCompatible(new StoredCopies([]));
        AssertCompatible(new StoredPort(8443));
        AssertCompatible(new StoredKey("k3y=="));
        AssertCompatible(new StoredThumbprint("AB:CD:EF"));
    }


    [Fact]
    public void CodecTestShapes_AreCompatible() {
        AssertCompatible(new TestConfig("main", 2));
        AssertCompatible(new AsyncConfig("async", -7));
        AssertCompatible(new Point(3, 4));
        AssertCompatible(new Bag([new Pair("a", 1), new Pair("b", 2), new Pair("c", 3)]));
        AssertCompatible(new Bag([]));
        AssertCompatible(new BagArray([new Pair("x", 9), new Pair("y", 18)]));
        AssertCompatible(new Line("segment", new Point(7, 11)));
        AssertCompatible(new NumberBag([10, 20, 30]));
        AssertCompatible(new TextBody(new string('a', LongTextLength)));
        AssertCompatible(new TextBody("abc\0def"));
        AssertCompatible(new TextBody("quote \" backslash \\ newline \n tab \t emoji \U0001F600 cyrillic \u0436"));
        AssertCompatible(new GrownRecord("a", 1, "labelled"));
        AssertCompatible(new GrownRecord("a", 1));
    }


    [Fact]
    public void Scalars_AreCompatible() {
        AssertCompatible(new Scalars(255, short.MinValue, int.MinValue, uint.MaxValue, long.MinValue, ulong.MaxValue, 1.5f, Math.PI, true, "text"));
        AssertCompatible(new Scalars(0, 0, 0, 0, 0, 0, -0.0f, double.Epsilon, false, ""));
    }


    [Fact]
    public void ScalarLists_AreCompatible() {
        ScalarLists lists = new ScalarLists(
            [1, 2, 255],
            [-1, short.MaxValue],
            [1, 2, 3],
            [uint.MaxValue],
            [long.MinValue, 0],
            [ulong.MaxValue],
            new List<float> { 0.5f, -2f }.Select(item => item * 2),
            [1.25, 2.5],
            [true, false, true],
            ["one", "two \"quoted\""]
        );

        AssertCompatible(lists);
        AssertCompatible(new ScalarLists([], [], [], [], [], [], [], [], [], []));
    }


    [Fact]
    public void OptionalLists_AreCompatible() {
        AssertCompatible(new OptionalLists(["a"], [1, 2], [new Pair("p", 1)], [new Pair("q", 2)]));
        AssertSameBytes(new OptionalLists(null, null, null, null));
    }


    [Fact]
    public void MutableSettings_IsCompatible() {
        MutableSettings settings = new MutableSettings {
            name = "main",
            comment = "a comment",
            level = 3,
            tags = ["x", "y"],
            anchor = new Point(1, 2),
            ratio = 0.75
        };
        settings.Hide(42);

        AssertCompatible(settings);
        AssertCompatible(new MutableSettings());
    }


    [Fact]
    public void DerivedShape_WritesPropertiesInReflectionOrder() {
        DerivedShape shape = new DerivedShape {
            id = "shape-1",
            title = "a title",
            weight = 9
        };

        AssertCompatible(shape);
    }


    /// <summary>
    /// A property outside the constructor is written but not read back when a constructor is
    /// used, in both mappers.
    /// </summary>
    [Fact]
    public void RecordWithBodyMembers_WritesAllButReadsOnlyTheConstructor() {
        WithBody value = new WithBody(1, "two") { third = 3 };

        AssertSameBytes(value);
        AssertSameDecode<WithBody>(OracleText(value));
        Assert.Equal(0, Fon.DeserializeRecord<WithBody>(OracleText(value)).third);
    }


    [Fact]
    public void OtherShapes_AreCompatible() {
        AssertCompatible(new Empty());
        AssertCompatible(new Keyworded("klass", 5));
        AssertCompatible(new Holder.Inner("inner", new Point(5, 6)));
        AssertCompatible(new AttributedSettings { label = "l", note = "n" });
        AssertCompatible(new Widened(1, 2, 3, 4));
        AssertCompatible(new WidenedArrays([1, 2], [0.5]));
        AssertCompatible(new WidenedList([7, 8]));
    }



    // ==================== MISSING AND MISMATCHED KEYS ====================

    [Fact]
    public void MissingRequiredScalar_FailsWithTheSameMessage() {
        AssertSameDecode<TestConfig>(Text(collection => collection.Add("count", 1)));
    }


    [Fact]
    public void MissingRequiredLists_FailWithTheSameMessage() {
        AssertSameDecode<DockSnapshot>(Text(collection => {
            collection.Add("version", 1);
            collection.Add("nodes", new List<FonCollection>());
        }));
        AssertSameDecode<DockSnapshot>(Text(collection => {
            collection.Add("version", 1);
            collection.Add("closedPanelIds", new List<string>());
        }));
        AssertSameDecode<StoredCopies>(string.Empty);
    }


    [Fact]
    public void MissingNullableMembers_ReadAsNullOrEmpty() {
        AssertSameDecode<DockSnapshot>(Text(collection => {
            collection.Add("version", 1);
            collection.Add("closedPanelIds", new List<string>());
            collection.Add("nodes", new List<FonCollection>());
        }));
        AssertSameDecode<OptionalLists>(string.Empty);
        AssertSameDecode<GrownRecord>(Text(collection => {
            collection.Add("name", "a");
            collection.Add("value", 1);
        }));

        OptionalLists lists = Fon.DeserializeRecord<OptionalLists>(string.Empty);
        Assert.Empty(lists.names!);
        Assert.Empty(lists.numbers!);
        Assert.Empty(lists.pairs!);
        Assert.Empty(lists.pairArray!);
    }


    [Fact]
    public void NestedRecord_MissingOrNotAnObject_ReadsAsMissing() {
        AssertSameDecode<Line>(Text(collection => collection.Add("name", "n")));
        AssertSameDecode<Line>(Text(collection => {
            collection.Add("name", "n");
            collection.Add("origin", 5);
        }));
        AssertSameDecode<MutableSettings>(Text(collection => {
            collection.Add("name", "n");
            collection.Add("level", 1);
            collection.Add("tags", new List<string>());
            collection.Add("anchor", 5);
            collection.Add("ratio", 1.0);
            collection.Add("hidden", 2L);
        }));
    }


    [Fact]
    public void SettableProperties_FollowWriteNullability() {
        AssertSameDecode<MutableSettings>(Text(collection => collection.Add("level", 1)));
        AssertSameDecode<MutableSettings>(Text(collection => {
            collection.Add("name", "n");
            collection.Add("tags", new List<string>());
            collection.Add("ratio", 1.0);
            collection.Add("hidden", 2L);
        }));
        AssertSameDecode<AttributedSettings>(string.Empty);
        AssertSameDecode<AttributedSettings>(Text(collection => collection.Add("label", "l")));
        AssertSameDecode<AttributedSettings>(Text(collection => collection.Add("note", "n")));
    }


    [Fact]
    public void Numbers_WidenAsReflectionDoes() {
        AssertSameDecode<Widened>(Text(collection => {
            collection.Add("count", 5);
            collection.Add("ratio", 2);
            collection.Add("scale", (byte)3);
            collection.Add("big", 4u);
        }));
        AssertSameDecode<Widened>(Text(collection => {
            collection.Add("count", (short)-5);
            collection.Add("ratio", 2.5f);
            collection.Add("scale", long.MaxValue);
            collection.Add("big", (byte)1);
        }));
        AssertSameDecode<WidenedArrays>(Text(collection => {
            collection.Add("totals", new List<int> { 1, -2 });
            collection.Add("ratios", new List<float> { 0.5f });
        }));

        Widened widened = Fon.DeserializeRecord<Widened>(Text(collection => {
            collection.Add("count", 5);
            collection.Add("ratio", 2);
            collection.Add("scale", (byte)3);
            collection.Add("big", 4u);
        }));
        Assert.Equal(new Widened(5, 2, 3, 4), widened);
    }


    [Fact]
    public void NarrowingOrUnrelatedTypes_FailInBoth() {
        AssertSameDecode<Widened>(Text(collection => {
            collection.Add("count", 1.5);
            collection.Add("ratio", 2.0);
            collection.Add("scale", 3f);
            collection.Add("big", 4UL);
        }));
        AssertSameDecode<Widened>(Text(collection => {
            collection.Add("count", 1L);
            collection.Add("ratio", 2.0);
            collection.Add("scale", 3f);
            collection.Add("big", -4);
        }));
        AssertSameDecode<WidenedList>(Text(collection => collection.Add("totals", new List<int> { 1 })));
        AssertSameDecode<TestConfig>(Text(collection => {
            collection.Add("name", 5);
            collection.Add("count", 1);
        }));
        AssertSameDecode<TestConfig>(Text(collection => {
            collection.Add("name", "n");
            collection.Add("count", "1");
        }));
        AssertSameDecode<NumberBag>(Text(collection => collection.Add("numbers", 5)));
        AssertSameDecode<Bag>(Text(collection => collection.Add("items", new List<string>())));
        AssertSameDecode<Bag>(Text(collection => collection.Add("items", new List<int> { 1 })));
    }


    [Fact]
    public void ScalarList_AcceptsAnEmptyListOfAnyType_AsReflectionDoes() {
        AssertSameDecode<NumberBag>(Text(collection => collection.Add("numbers", new List<string>())));
        AssertSameDecode<NumberBag>(Text(collection => collection.Add("numbers", new List<string> { "1" })));
    }


    [Fact]
    public void UnknownKeys_AreIgnored() {
        AssertSameDecode<TestConfig>(Text(collection => {
            collection.Add("name", "n");
            collection.Add("count", 1);
            collection.Add("extra", "ignored");
        }));
    }



    // ==================== HELPERS ====================

    private static void AssertCompatible<T>(T value) where T : IFonRecord<T> {
        AssertSameBytes(value);

        string oracleText = OracleText(value);
        T generatedRead = Fon.DeserializeRecord<T>(oracleText);
        Assert.Equal(Json(value), Json(generatedRead));

        T oracleRead = new ReflectionOracle<T>().Decode(Encoding.UTF8.GetBytes(Fon.SerializeRecord(value)));
        Assert.Equal(Json(value), Json(oracleRead));
    }


    private static void AssertSameBytes<T>(T value) where T : IFonRecord<T> {
        byte[] oracle = new ReflectionOracle<T>().Encode(value);
        byte[] generated = Encoding.UTF8.GetBytes(Fon.SerializeRecord(value));
        Assert.Equal(Encoding.UTF8.GetString(oracle), Encoding.UTF8.GetString(generated));
        Assert.Equal(oracle, generated);
    }


    /// <summary>
    /// Reads the text with both mappers. Either both succeed with equal values, or both fail;
    /// a missing required key fails with the same message.
    /// </summary>
    private static void AssertSameDecode<T>(string text) where T : IFonRecord<T> {
        T? oracleValue = default;
        Exception? oracleError = null;
        try {
            oracleValue = new ReflectionOracle<T>().Decode(Encoding.UTF8.GetBytes(text));
        } catch (Exception error) {
            oracleError = error;
        }

        T? generatedValue = default;
        Exception? generatedError = null;
        try {
            generatedValue = Fon.DeserializeRecord<T>(text);
        } catch (Exception error) {
            generatedError = error;
        }

        if (oracleError is null) {
            Assert.True(generatedError is null, $"The reflection mapper read '{text}', the generated code threw: {generatedError}");
            Assert.Equal(Json(oracleValue), Json(generatedValue));
            return;
        }
        Assert.True(generatedError is not null, $"The reflection mapper failed on '{text}' ({oracleError.Message}), the generated code read it");
        if (oracleError is OracleException { InnerException: null }) {
            FonRecordException recordError = Assert.IsType<FonRecordException>(generatedError);
            Assert.Equal(oracleError.Message, recordError.Message);
        }
    }


    private static string OracleText<T>(T value) {
        return Encoding.UTF8.GetString(new ReflectionOracle<T>().Encode(value));
    }


    private static string Text(Action<FonCollection> fill) {
        using FonCollection collection = new FonCollection();
        fill(collection);
        return Fon.SerializeToString(collection);
    }


    private static string Json(object? value) {
        return JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), JsonOptions);
    }
}
