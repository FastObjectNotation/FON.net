using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Raw ABI tests for nesting, borrowed handles, ownership-iff-OK and dump-level put/ids/gens.
/// </summary>
public unsafe class NativeNestedTests {
    [Fact]
    public void NativePutCollection_TransfersOwnership_ParentFreesChild() {
        IntPtr parent = NativeBindings.fon_collection_create();
        IntPtr child = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(child, "inner", 7, FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeKit.PutCollection(parent, "child", child, FonPutMode.Add, &error));
        } finally {
            NativeBindings.fon_collection_free(parent);
        }
    }


    [Fact]
    public void NativePutCollection_FailedAdd_LeavesChildOwnedAndUsable() {
        IntPtr parent = NativeBindings.fon_collection_create();
        IntPtr child = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(parent, "taken", 1, FonPutMode.Add, &error));
            int rc = NativeKit.PutCollection(parent, "taken", child, FonPutMode.Add, &error);
            Assert.Equal(FonResultCode.AlreadyExists, rc);
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(child, "still", 2, FonPutMode.Add, &error));
            Assert.Equal(1, NativeBindings.fon_collection_size(child));
        } finally {
            NativeBindings.fon_collection_free(child);
            NativeBindings.fon_collection_free(parent);
        }
    }


    [Fact]
    public void NativePutCollection_SelfChild_IsRejectedAndNothingIsConsumed() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            int rc = NativeKit.PutCollection(collection, "self", collection, FonPutMode.Add, &error);
            Assert.Equal(FonResultCode.InvalidArgument, rc);
            Assert.Equal(0, NativeBindings.fon_collection_size(collection));
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativePutCollectionArray_DuplicateChild_IsRejectedAndChildStaysOwned() {
        IntPtr parent = NativeBindings.fon_collection_create();
        IntPtr child = NativeBindings.fon_collection_create();
        FonError error = default;
        byte[] key = NativeKit.Utf8("arr");
        IntPtr[] children = { child, child };
        try {
            fixed (byte* keyPtr = key) {
                fixed (IntPtr* childrenPtr = children) {
                    int rc = NativeBindings.fon_collection_put_collection_array(parent, keyPtr, key.Length, childrenPtr, children.Length, FonPutMode.Add, &error);
                    Assert.Equal(FonResultCode.InvalidArgument, rc);
                }
            }
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(child, "usable", 1, FonPutMode.Add, &error));
        } finally {
            NativeBindings.fon_collection_free(child);
            NativeBindings.fon_collection_free(parent);
        }
    }


    [Fact]
    public void NativeGetCollection_BorrowedChildReadsThroughParent() {
        IntPtr parent = NativeBindings.fon_collection_create();
        IntPtr child = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(child, "v", 99, FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeKit.PutCollection(parent, "c", child, FonPutMode.Add, &error));

            byte[] key = NativeKit.Utf8("c");
            IntPtr borrowed;
            fixed (byte* keyPtr = key) {
                borrowed = NativeBindings.fon_collection_get_collection(parent, keyPtr, key.Length, &error);
            }
            Assert.NotEqual(IntPtr.Zero, borrowed);
            Assert.Equal(FonResultCode.OK, NativeKit.GetInt(borrowed, "v", out int value, &error));
            Assert.Equal(99, value);
        } finally {
            NativeBindings.fon_collection_free(parent);
        }
    }


    [Fact]
    public void NativeGetCollection_AbsentKey_ReturnsNullWithNotFound() {
        IntPtr parent = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            byte[] key = NativeKit.Utf8("none");
            IntPtr borrowed;
            fixed (byte* keyPtr = key) {
                borrowed = NativeBindings.fon_collection_get_collection(parent, keyPtr, key.Length, &error);
            }
            Assert.Equal(IntPtr.Zero, borrowed);
            Assert.Equal(FonResultCode.NotFound, error.Code);
        } finally {
            NativeBindings.fon_collection_free(parent);
        }
    }


    [Fact]
    public void NativeDumpPut_AddExistingId_ReturnsAlreadyExistsAndKeepsChild() {
        IntPtr dump = NativeBindings.fon_dump_create();
        IntPtr first = NativeBindings.fon_collection_create();
        IntPtr second = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_put(dump, 3, first, FonPutMode.Add, &error));
            int rc = NativeBindings.fon_dump_put(dump, 3, second, FonPutMode.Add, &error);
            Assert.Equal(FonResultCode.AlreadyExists, rc);
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(second, "still", 1, FonPutMode.Add, &error));
        } finally {
            NativeBindings.fon_collection_free(second);
            NativeBindings.fon_dump_free(dump);
        }
    }


    [Fact]
    public void NativeDumpIds_AscendingAfterSparseAddsAndRemovals() {
        IntPtr dump = NativeBindings.fon_dump_create();
        FonError error = default;
        try {
            foreach (ulong id in new ulong[] { 9, 2, 5, 0 }) {
                IntPtr record = NativeBindings.fon_collection_create();
                Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_put(dump, id, record, FonPutMode.Add, &error));
            }
            Assert.Equal(1, NativeBindings.fon_dump_remove(dump, 5));
            Assert.Equal(0, NativeBindings.fon_dump_remove(dump, 5));

            long count = 0;
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_ids(dump, null, 0, &count, &error));
            ulong[] ids = new ulong[count];
            fixed (ulong* idPtr = ids) {
                Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_ids(dump, idPtr, count, &count, &error));
            }
            Assert.Equal(new ulong[] { 0, 2, 9 }, ids);
        } finally {
            NativeBindings.fon_dump_free(dump);
        }
    }


    [Fact]
    public void NativeDumpGens_TouchChangesTreeGenOnly_ReplaceChangesRecordGen() {
        IntPtr dump = NativeBindings.fon_dump_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_put(dump, 1, NativeBindings.fon_collection_create(), FonPutMode.Add, &error));
            uint recordBefore = 0;
            uint treeBefore = 0;
            uint recordAfter = 0;
            uint treeAfter = 0;

            Assert.Equal(1, NativeBindings.fon_dump_gens(dump, 1, &recordBefore, &treeBefore));
            Assert.Equal(1, NativeBindings.fon_dump_touch(dump, 1));
            Assert.Equal(1, NativeBindings.fon_dump_gens(dump, 1, &recordAfter, &treeAfter));
            Assert.Equal(recordBefore, recordAfter);
            Assert.NotEqual(treeBefore, treeAfter);

            IntPtr replacement = NativeBindings.fon_collection_create();
            Assert.Equal(FonResultCode.OK, NativeBindings.fon_dump_put(dump, 1, replacement, FonPutMode.Set, &error));
            Assert.Equal(1, NativeBindings.fon_dump_gens(dump, 1, &recordAfter, &treeAfter));
            Assert.NotEqual(recordBefore, recordAfter);
        } finally {
            NativeBindings.fon_dump_free(dump);
        }
    }
}
