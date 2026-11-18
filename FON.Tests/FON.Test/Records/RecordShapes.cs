using System.Diagnostics.CodeAnalysis;
using FON.Records;
using FON.Types;

namespace FON.Test.Records;


// ==================== SHAPES DECERIUM STORES ====================

[FonRecord]
public sealed partial record DockSnapshot(
    int version,
    string? activePanelId,
    IReadOnlyList<string> closedPanelIds,
    IReadOnlyList<DockSnapshotNode> nodes
) {
    public const int CurrentVersion = 1;
}


[FonRecord]
public sealed partial record DockSnapshotNode(
    string kind,
    string? orientation,
    double ratio,
    string? panelId,
    string? selectedPanelId,
    bool isTabStripVisible,
    IReadOnlyList<string> tabIds
) {
    public const string Split = "split";

    public const string Panel = "panel";

    public const string Group = "group";
}


[FonRecord]
internal sealed partial record RecentDocument(IReadOnlyList<RecentEntry> solutions, IReadOnlyList<string> locations);


[FonRecord]
internal sealed partial record RecentEntry(string path, int boardCount, long lastOpenedTicks, bool isPinned);


[FonRecord]
internal sealed partial record ExplorerExpansionDocument(IReadOnlyList<ExplorerExpansionEntry> entries);


[FonRecord]
internal sealed partial record ExplorerExpansionEntry(string solution, long touchedTicks, string row, bool expanded);


[FonRecord]
public sealed partial record GrantTableSnapshot(IReadOnlyList<GrantEntry> grants);


[FonRecord]
public sealed partial record GrantEntry(string pluginId, string permissionId);


[FonRecord]
public sealed partial record StoredCopies(List<string> copies);


[FonRecord]
public sealed partial record StoredPort(int port);


[FonRecord]
public sealed partial record StoredKey(string key);


[FonRecord]
public sealed partial record StoredThumbprint(string thumbprint);



// ==================== SHAPES OF DECERIUM'S CODEC TESTS ====================

[FonRecord]
public sealed partial record TestConfig(string name, int count);


[FonRecord]
public sealed partial record AsyncConfig(string name, int count);


[FonRecord]
public sealed partial record Point(int x, int y);


[FonRecord]
public sealed partial record Pair(string key, int value);


[FonRecord]
public sealed partial record Bag(IReadOnlyList<Pair> items);


[FonRecord]
public sealed partial record BagArray(Pair[] items);


[FonRecord]
public sealed partial record Line(string name, Point origin);


[FonRecord]
public sealed partial record NumberBag(IReadOnlyList<int> numbers);


[FonRecord]
public sealed partial record TextBody(string body);


[FonRecord]
public sealed partial record GrownRecord(string name, int value, string? description = null);



// ==================== MORE SHAPES FOR THE RULES ====================

[FonRecord]
public sealed partial record Scalars(
    byte b,
    short s,
    int i,
    uint u,
    long l,
    ulong g,
    float f,
    double d,
    bool flag,
    string text
);


[FonRecord]
public sealed partial record ScalarLists(
    byte[] bytes,
    short[] shorts,
    List<int> ints,
    IReadOnlyList<uint> uints,
    IList<long> longs,
    ICollection<ulong> ulongs,
    IEnumerable<float> floats,
    IReadOnlyCollection<double> doubles,
    bool[] flags,
    string[] texts
);


[FonRecord]
public sealed partial record OptionalLists(IReadOnlyList<string>? names, int[]? numbers, IReadOnlyList<Pair>? pairs, Pair[]? pairArray);


/// <summary>Read through the parameterless constructor and its settable properties.</summary>
[FonRecord]
public sealed partial class MutableSettings {
    public string name { get; set; } = "";

    public string? comment { get; set; }

    public int level { get; set; }

    public List<string> tags { get; set; } = new List<string>();

    public Point? anchor { get; set; }

    public string label => "label-" + level;

    public double ratio { get; init; }

    public long hidden { get; private set; }


    public void Hide(long value) {
        hidden = value;
    }
}


public class BaseShape {
    public string id { get; set; } = "";

    public virtual int weight { get; set; }
}


[FonRecord]
public sealed partial class DerivedShape : BaseShape {
    public string title { get; set; } = "";

    public override int weight { get; set; }
}


[FonRecord]
public sealed partial record WithBody(int first, string second) {
    public int third { get; init; }

    public string fourth => second + first;
}


[FonRecord]
public sealed partial record Empty();


[FonRecord]
public sealed partial record Keyworded(string @class, int @event);


public static partial class Holder {
    [FonRecord]
    public sealed partial record Inner(string name, Point at);
}


[FonRecord]
public sealed partial class AttributedSettings {
    [AllowNull]
    public string label { get; set; } = "";

    [DisallowNull]
    public string? note { get; set; } = "";
}


[FonRecord]
public sealed partial record Widened(long count, double ratio, float scale, ulong big);


[FonRecord]
public sealed partial record WidenedArrays(long[] totals, double[] ratios);


[FonRecord]
public sealed partial record WidenedList(List<long> totals);



// ==================== ADDED SUPPORT: NULLABLE VALUES, ENUMS, STRUCT RECORDS ====================

public enum Mood {
    calm,
    angry,
    sleepy
}


[Flags]
public enum Access {
    none = 0,
    read = 1,
    write = 2
}


[FonRecord]
public readonly partial record struct Point3(int x, int y, int z);


[FonRecord]
public sealed partial record Extended(
    int? count,
    double? ratio,
    Mood mood,
    Mood? maybeMood,
    Access access,
    IReadOnlyList<Mood> moods,
    Mood[] moodArray,
    Point3 at,
    Point3? maybeAt,
    IReadOnlyList<Point3> path
);


[FonRecord]
public sealed partial record ReadmeSnapshot(int version, string? activePanelId, IReadOnlyList<string> closedPanelIds, IReadOnlyList<ReadmeNode> nodes);


[FonRecord]
public sealed partial record ReadmeNode(string kind, double ratio, IReadOnlyList<string> tabIds);


[FonRecord]
public sealed partial record Blob(RawData data, FonCollection extra, IReadOnlyList<FonCollection> items, RawData? maybeData);
