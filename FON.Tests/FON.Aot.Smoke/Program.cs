using FON.Core;
using FON.Records;
using FON.Types;

namespace FON.Aot.Smoke;


public enum Mood {
    calm,
    angry
}


[FonRecord]
public sealed partial record Node(string kind, string? panelId, double ratio, bool visible, IReadOnlyList<string> tabIds);


[FonRecord]
public sealed partial record Snapshot(int version, string? activePanelId, IReadOnlyList<string> closedPanelIds, IReadOnlyList<Node> nodes);


[FonRecord]
public readonly partial record struct Point(int x, int y);


[FonRecord]
internal sealed partial record Extras(int? count, Mood mood, Mood? maybeMood, Point at, Point? maybeAt, long[] totals, List<Mood> moods);


[FonRecord]
public sealed partial class Settings {
    public string name { get; set; } = "";

    public int level { get; set; }

    public List<string> tags { get; set; } = new List<string>();
}


public sealed class LegacySettings {
    public string name { get; set; } = "";

    public int level { get; set; }
}



public static class Program {
    public static int Main() {
        List<string> failures = new List<string>();

        Snapshot snapshot = new Snapshot(
            1,
            "explorer",
            ["output"],
            [new Node("panel", "explorer", 0.5, true, ["explorer", "search"]), new Node("split", null, 0.25, false, [])]
        );
        string snapshotText = Fon.SerializeRecord(snapshot);
        Snapshot snapshotBack = Fon.DeserializeRecord<Snapshot>(snapshotText);
        Check(failures, "snapshot text", Fon.SerializeRecord(snapshotBack) == snapshotText);
        Check(failures, "snapshot nodes", snapshotBack.nodes.Count == 2 && snapshotBack.nodes[0].tabIds[1] == "search");
        Check(failures, "snapshot null member", snapshotBack.nodes[1].panelId is null);

        Extras extras = new Extras(null, Mood.angry, Mood.calm, new Point(1, 2), null, [1, 2, 3], [Mood.calm, Mood.angry]);
        string extrasText = Fon.SerializeRecord(extras);
        Extras extrasBack = Fon.DeserializeRecord<Extras>(extrasText);
        Check(failures, "enum by name", extrasText.Contains("mood=s:\"angry\""));
        Check(failures, "nullable skipped", !extrasText.Contains("count="));
        Check(failures, "extras values", extrasBack.mood == Mood.angry && extrasBack.maybeMood == Mood.calm && extrasBack.at == new Point(1, 2));
        Check(failures, "extras lists", extrasBack.totals.SequenceEqual(new long[] { 1, 2, 3 }) && extrasBack.moods.SequenceEqual(extras.moods));
        Check(failures, "extras nulls", extrasBack.count is null && extrasBack.maybeAt is null);

        Settings settings = new Settings {
            name = "main",
            level = 3,
            tags = ["a"]
        };
        Settings settingsBack = Fon.DeserializeRecord<Settings>(Fon.SerializeRecord(settings));
        Check(failures, "settings", settingsBack.name == "main" && settingsBack.level == 3 && settingsBack.tags.Single() == "a");

        try {
            Fon.DeserializeRecord<Snapshot>("version=i:1");
            failures.Add("missing key did not throw");
        } catch (FonRecordException error) {
            Check(failures, "missing key message", error.Message == "FON decode: required list field 'closedPanelIds' is missing.");
        }

        using FonCollection legacy = FonCollection.Serialize(new LegacySettings { name = "legacy", level = 2 });
        LegacySettings legacyBack = legacy.Deserialize<LegacySettings>();
        Check(failures, "legacy reflection helpers", legacyBack.name == "legacy" && legacyBack.level == 2);

        foreach (string failure in failures) {
            Console.Error.WriteLine("FAILED: " + failure);
        }
        Console.WriteLine(failures.Count == 0 ? "FON AOT smoke: ok" : $"FON AOT smoke: {failures.Count} failed");
        return failures.Count == 0 ? 0 : 1;
    }


    private static void Check(List<string> failures, string name, bool passed) {
        if (!passed) {
            failures.Add(name);
        }
    }
}
