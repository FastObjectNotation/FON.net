using FON.Native;
using Xunit;

namespace FON.Native.Test;


/// <summary>
/// Raw ABI tests for collection scalars, put modes, result codes and keys/kinds.
/// </summary>
public unsafe class NativeSerializationTests {
    [Fact]
    public void NativeCollection_CreateAndFree() {
        IntPtr collection = NativeBindings.fon_collection_create();
        Assert.NotEqual(IntPtr.Zero, collection);
        Assert.Equal(0, NativeBindings.fon_collection_size(collection));
        NativeBindings.fon_collection_free(collection);
    }


    [Fact]
    public void NativeCollection_AddAndGetInt() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, "n", 123, FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeKit.GetInt(collection, "n", out int value, &error));
            Assert.Equal(123, value);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_AddExistingKey_ReturnsAlreadyExists() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, "n", 1, FonPutMode.Add, &error));
            int rc = NativeKit.PutInt(collection, "n", 2, FonPutMode.Add, &error);
            Assert.Equal(FonResultCode.AlreadyExists, rc);
            Assert.Contains("n", error.Message);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_SetAbsentKey_ReturnsNotFound() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            int rc = NativeKit.PutInt(collection, "missing", 1, FonPutMode.Set, &error);
            Assert.Equal(FonResultCode.NotFound, rc);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_UpsertReplacesAndChangesType() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, "k", 1, FonPutMode.Upsert, &error));
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(collection, "k", "text", FonPutMode.Upsert, &error));
            Assert.Equal("text", NativeKit.GetString(collection, "k"));
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_GetWrongType_ReturnsTypeMismatch() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(collection, "s", "x", FonPutMode.Add, &error));
            int rc = NativeKit.GetInt(collection, "s", out _, &error);
            Assert.Equal(FonResultCode.TypeMismatch, rc);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_GetAbsentKey_ReturnsNotFound() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            int rc = NativeKit.GetInt(collection, "nope", out _, &error);
            Assert.Equal(FonResultCode.NotFound, rc);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_KindOf_ReportsTypeAndArrayFlag() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        byte[] key = NativeKit.Utf8("i");
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, "i", 1, FonPutMode.Add, &error));
            fixed (byte* keyPtr = key) {
                Assert.Equal((int)FonTypeCode.Int, NativeBindings.fon_collection_kind_of(collection, keyPtr, key.Length));
            }
            byte[] absent = NativeKit.Utf8("absent");
            fixed (byte* absentPtr = absent) {
                Assert.Equal(-1, NativeBindings.fon_collection_kind_of(collection, absentPtr, absent.Length));
            }
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_Keys_InsertionOrderWithKinds() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutString(collection, "zeta", "z", FonPutMode.Add, &error));
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, "alpha", 1, FonPutMode.Add, &error));

            List<(string Key, int Kind)> keys = NativeKit.KeysWithKinds(collection);

            Assert.Equal(2, keys.Count);
            Assert.Equal("zeta", keys[0].Key);
            Assert.Equal((int)FonTypeCode.String, keys[0].Kind);
            Assert.Equal("alpha", keys[1].Key);
            Assert.Equal((int)FonTypeCode.Int, keys[1].Kind);
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_RemoveReturnsOneThenZero() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        byte[] key = NativeKit.Utf8("k");
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, "k", 1, FonPutMode.Add, &error));
            fixed (byte* keyPtr = key) {
                Assert.Equal(1, NativeBindings.fon_collection_remove(collection, keyPtr, key.Length));
                Assert.Equal(0, NativeBindings.fon_collection_remove(collection, keyPtr, key.Length));
            }
            Assert.Equal(0, NativeBindings.fon_collection_size(collection));
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }


    [Fact]
    public void NativeCollection_Clone_IsDeepAndIndependent() {
        IntPtr collection = NativeBindings.fon_collection_create();
        FonError error = default;
        try {
            Assert.Equal(FonResultCode.OK, NativeKit.PutInt(collection, "n", 1, FonPutMode.Add, &error));
            IntPtr clone = NativeBindings.fon_collection_clone(collection, &error);
            Assert.NotEqual(IntPtr.Zero, clone);
            try {
                Assert.Equal(FonResultCode.OK, NativeKit.PutInt(clone, "other", 2, FonPutMode.Add, &error));
                Assert.Equal(1, NativeBindings.fon_collection_size(collection));
                Assert.Equal(2, NativeBindings.fon_collection_size(clone));
            } finally {
                NativeBindings.fon_collection_free(clone);
            }
        } finally {
            NativeBindings.fon_collection_free(collection);
        }
    }
}
