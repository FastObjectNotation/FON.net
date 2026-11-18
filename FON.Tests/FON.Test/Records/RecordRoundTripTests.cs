using System.Text.Json;
using FON.Core;
using FON.Records;
using FON.Types;

namespace FON.Test.Records;


/// <summary>
/// What the generated code adds beyond the reflection mapper: nullable values, enums and record
/// structs, copies of nested collections, and the entry points on <see cref="Fon"/>.
/// </summary>
public class RecordRoundTripTests {
    [Fact]
    public void Extended_RoundTripsEveryValue() {
        Extended original = new Extended(
            5,
            0.5,
            Mood.angry,
            Mood.sleepy,
            Access.read | Access.write,
            [Mood.calm, Mood.sleepy],
            [Mood.angry],
            new Point3(1, 2, 3),
            new Point3(4, 5, 6),
            [new Point3(7, 8, 9)]
        );

        Extended decoded = Fon.DeserializeRecord<Extended>(Fon.SerializeRecord(original));

        Assert.Equal(Json(original), Json(decoded));
    }


    [Fact]
    public void ReadmeExample_WritesTheDocumentedText() {
        ReadmeSnapshot snapshot = new ReadmeSnapshot(1, "explorer", ["output"], [new ReadmeNode("panel", 0.5, ["explorer"])]);

        string text = Fon.SerializeRecord(snapshot);

        Assert.Equal("version=i:1,activePanelId=s:\"explorer\",closedPanelIds=s:[\"output\"],nodes=o:[{kind=s:\"panel\",ratio=d:0.5,tabIds=s:[\"explorer\"]}]", text);
        Assert.Equal(Json(snapshot), Json(Fon.DeserializeRecord<ReadmeSnapshot>(text)));
    }


    [Fact]
    public void NullableValues_SkipTheKeyWhenNull() {
        Extended original = new Extended(null, null, Mood.calm, null, Access.none, [], [], new Point3(0, 0, 0), null, []);

        string text = Fon.SerializeRecord(original);
        Extended decoded = Fon.DeserializeRecord<Extended>(text);

        Assert.DoesNotContain("count=", text);
        Assert.DoesNotContain("ratio=", text);
        Assert.DoesNotContain("maybeMood=", text);
        Assert.DoesNotContain("maybeAt=", text);
        Assert.Null(decoded.count);
        Assert.Null(decoded.ratio);
        Assert.Null(decoded.maybeMood);
        Assert.Null(decoded.maybeAt);
    }


    [Fact]
    public void NullableValues_WriteTheUnderlyingValue() {
        Extended original = new Extended(7, 1.5, Mood.calm, Mood.angry, Access.read, [], [], new Point3(0, 0, 0), new Point3(1, 1, 1), []);

        using FonCollection collection = Fon.ToCollection(original);

        Assert.Equal(7, collection.Get<int>("count"));
        Assert.Equal(1.5, collection.Get<double>("ratio"));
        Assert.Equal("angry", collection.Get<string>("maybeMood"));
        Assert.IsType<FonCollection>(collection.Get("maybeAt"));
    }


    [Fact]
    public void Enums_AreWrittenByName() {
        Extended original = new Extended(null, null, Mood.sleepy, null, Access.read | Access.write, [Mood.calm, Mood.angry], [Mood.sleepy], new Point3(0, 0, 0), null, []);

        using FonCollection collection = Fon.ToCollection(original);

        Assert.Equal("sleepy", collection.Get<string>("mood"));
        Assert.Equal("read, write", collection.Get<string>("access"));
        Assert.Equal(new List<string> { "calm", "angry" }, collection.Get<List<string>>("moods"));
        Assert.Equal(new List<string> { "sleepy" }, collection.Get<List<string>>("moodArray"));
    }


    [Fact]
    public void Enum_UndefinedValue_RoundTripsAsItsNumber() {
        Extended original = new Extended(null, null, (Mood)42, null, (Access)8, [], [], new Point3(0, 0, 0), null, []);

        Extended decoded = Fon.DeserializeRecord<Extended>(Fon.SerializeRecord(original));

        Assert.Equal((Mood)42, decoded.mood);
        Assert.Equal((Access)8, decoded.access);
    }


    [Fact]
    public void Enum_UnknownName_Throws() {
        string text = Fon.SerializeRecord(new Extended(null, null, Mood.calm, null, Access.none, [], [], new Point3(0, 0, 0), null, []));

        FonRecordException failure = Assert.Throws<FonRecordException>(() => Fon.DeserializeRecord<Extended>(text.Replace("\"calm\"", "\"furious\"")));

        Assert.Contains("'mood'", failure.Message);
        Assert.Contains("furious", failure.Message);
    }


    [Fact]
    public void MissingRequiredEnum_Throws() {
        FonRecordException failure = Assert.Throws<FonRecordException>(() => Fon.DeserializeRecord<Extended>(string.Empty));

        Assert.Equal("FON decode: required field 'mood' is missing.", failure.Message);
    }


    [Fact]
    public void Blob_CopiesRawDataAndCollections() {
        FonCollection extra = new FonCollection();
        extra.Add("inner", 1);
        FonCollection item = new FonCollection();
        item.Add("name", "item");
        Blob original = new Blob(new RawData([1, 2, 3, 0, 255]), extra, [item], null);

        string text = Fon.SerializeRecord(original);
        Blob decoded = Fon.DeserializeRecord<Blob>(text);

        Assert.Equal(1, extra.Get<int>("inner"));
        Assert.Equal("item", item.Get<string>("name"));
        Assert.Equal(new byte[] { 1, 2, 3, 0, 255 }, decoded.data.data);
        Assert.Equal(1, decoded.extra.Get<int>("inner"));
        Assert.Equal("item", Assert.Single(decoded.items).Get<string>("name"));
        Assert.Null(decoded.maybeData);
        decoded.extra.Dispose();
        decoded.items[0].Dispose();
        extra.Dispose();
        item.Dispose();
    }


    [Fact]
    public void ReadFromADumpRecord_StaysValidAfterTheDumpIsDisposed() {
        Bag bag = new Bag([new Pair("a", 1), new Pair("b", 2)]);
        string text = Fon.SerializeRecord(bag) + "\n" + Fon.SerializeRecord(new Bag([])) + "\n";

        Bag decoded;
        using (FonDump dump = Fon.Deserialize(text)) {
            decoded = Fon.FromCollection<Bag>(dump[0]);
        }

        Assert.Equal(2, decoded.items.Count);
        Assert.Equal(new Pair("b", 2), decoded.items[1]);
    }


    [Fact]
    public void DeserializeRecord_ReadsUtf8() {
        DockSnapshot snapshot = new DockSnapshot(1, "a", ["b"], [new DockSnapshotNode("panel", null, 1, "p", null, false, ["t"])]);
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(Fon.SerializeRecord(snapshot));

        DockSnapshot decoded = Fon.DeserializeRecord<DockSnapshot>(utf8);

        Assert.Equal(Json(snapshot), Json(decoded));
    }


    [Fact]
    public void GenericCode_ReachesTheGeneratedMembers() {
        StoredPort decoded = RoundTrip(new StoredPort(8080));

        Assert.Equal(8080, decoded.port);
    }


    [Fact]
    public void NullRoot_Throws() {
        Assert.Throws<ArgumentNullException>(() => Fon.ToCollection<Point>(null!));
        Assert.Throws<ArgumentNullException>(() => Fon.FromCollection<Point>(null!));
    }


    [Fact]
    public void NullElementInARecordList_Throws() {
        Bag bag = new Bag([new Pair("a", 1), null!]);

        Assert.Throws<ArgumentNullException>(() => Fon.SerializeRecord(bag));
    }


    [Fact]
    public void LegacyReflectionHelpers_StillWork() {
        using FonCollection collection = FonCollection.Serialize(new LegacySettings { name = "n", count = 2 });

        LegacySettings decoded = collection.Deserialize<LegacySettings>();

        Assert.Equal("n", decoded.name);
        Assert.Equal(2, decoded.count);
    }



    private static T RoundTrip<T>(T value) where T : IFonRecord<T> {
        using FonCollection collection = T.ToFon(value);
        return T.FromFon(collection);
    }


    private static string Json(object? value) {
        return JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object));
    }


    public sealed class LegacySettings {
        public string name { get; set; } = "";

        public int count { get; set; }
    }
}
