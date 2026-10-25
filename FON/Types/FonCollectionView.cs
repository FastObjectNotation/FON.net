namespace FON.Types;


/// <summary>
/// Bookkeeping of a view: the root that keeps the native memory alive, the generations or
/// versions captured at the last successful validation, and the cached native pointer.
/// Owned collections have no view.
/// </summary>
internal sealed class FonCollectionView {
    /// <summary>Dump owning the record (record views and nested views inside a record).</summary>
    public FonDump? dump;

    /// <summary>Owned root collection (nested views inside a collection tree).</summary>
    public FonCollection? root;

    /// <summary>True for a record view (a collection obtained from a dump by id).</summary>
    public bool isRecord;

    public ulong recordId;

    public uint recordGen;

    public uint treeGen;

    public long treeVersion;

    public long capturedVersion;

    public IntPtr pointer;
}
