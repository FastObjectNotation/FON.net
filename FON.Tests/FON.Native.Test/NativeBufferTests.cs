using System.Text;
using FON.Native;
using Xunit;


namespace FON.Native.Test;


/// <summary>
/// Tests for native string/buffer-based serialization and deserialization.
/// Covers both raw NativeBindings (two-call pattern) and high-level NativeApi helpers.
/// </summary>
public class NativeBufferTests {
    [Fact]
    public void SerializeDump_AndDeserializeDump_RoundTrips_ViaNativeApi() {
        var dump = NativeBindings.fon_dump_create();
        try {
            var error = new FonError();

            var c1 = NativeBindings.fon_collection_create();
            NativeBindings.fon_collection_add_int(c1, "id", 1, ref error);
            NativeBindings.fon_collection_add_string(c1, "name", "first", ref error);
            NativeBindings.fon_dump_add(dump, 0, c1, ref error);

            var c2 = NativeBindings.fon_collection_create();
            NativeBindings.fon_collection_add_int(c2, "id", 2, ref error);
            NativeBindings.fon_collection_add_double(c2, "ratio", 3.14, ref error);
            NativeBindings.fon_dump_add(dump, 1, c2, ref error);

            string serialized = NativeApi.SerializeDump(dump);
            Assert.NotEmpty(serialized);
            Assert.Contains("first", serialized);

            IntPtr loaded = NativeApi.DeserializeDump(serialized);
            try {
                Assert.Equal(2, NativeBindings.fon_dump_size(loaded));

                var l1 = NativeBindings.fon_dump_get(loaded, 0);
                NativeBindings.fon_collection_get_int(l1, "id", out int id1, ref error);
                Assert.Equal(1, id1);

                var l2 = NativeBindings.fon_dump_get(loaded, 1);
                NativeBindings.fon_collection_get_double(l2, "ratio", out double ratio, ref error);
                Assert.Equal(3.14, ratio);
            } finally {
                NativeBindings.fon_dump_free(loaded);
            }
        } finally {
            NativeBindings.fon_dump_free(dump);
        }
    }


    [Fact]
    public void SerializeCollection_AndDeserializeCollection_RoundTrips_ViaNativeApi() {
        var c = NativeBindings.fon_collection_create();
        try {
            var error = new FonError();
            NativeBindings.fon_collection_add_int(c, "x", 42, ref error);
            NativeBindings.fon_collection_add_bool(c, "flag", 1, ref error);
            NativeBindings.fon_collection_add_string(c, "label", "hello", ref error);

            string line = NativeApi.SerializeCollection(c);
            Assert.NotEmpty(line);

            IntPtr loaded = NativeApi.DeserializeCollection(line);
            try {
                NativeBindings.fon_collection_get_int(loaded, "x", out int x, ref error);
                Assert.Equal(42, x);
                NativeBindings.fon_collection_get_bool(loaded, "flag", out int flag, ref error);
                Assert.Equal(1, flag);

                var buf = new byte[64];
                NativeBindings.fon_collection_get_string(loaded, "label", buf, buf.Length, ref error);
                int len = Array.IndexOf(buf, (byte)0);
                string label = Encoding.UTF8.GetString(buf, 0, len < 0 ? buf.Length : len);
                Assert.Equal("hello", label);
            } finally {
                NativeBindings.fon_collection_free(loaded);
            }
        } finally {
            NativeBindings.fon_collection_free(c);
        }
    }


    [Fact]
    public unsafe void SerializeDump_TwoCallPattern_ReturnsRequiredSizeFirst() {
        var dump = NativeBindings.fon_dump_create();
        try {
            var error = new FonError();
            var c = NativeBindings.fon_collection_create();
            NativeBindings.fon_collection_add_int(c, "n", 7, ref error);
            NativeBindings.fon_dump_add(dump, 0, c, ref error);

            int rc = NativeBindings.fon_serialize_dump_to_buffer(dump, null, 0, out long required, 1, ref error);
            Assert.Equal(FonResultCode.OK, rc);
            Assert.True(required > 0);

            byte[] buf = new byte[required];
            fixed (byte* p = buf) {
                rc = NativeBindings.fon_serialize_dump_to_buffer(dump, p, buf.Length, out required, 1, ref error);
            }
            Assert.Equal(FonResultCode.OK, rc);

            string text = Encoding.UTF8.GetString(buf);
            Assert.Contains("n=", text);
        } finally {
            NativeBindings.fon_dump_free(dump);
        }
    }


    [Fact]
    public void TrySerializeCollection_ReturnsFalseWhenBufferTooSmall_AndWritesRequiredSize() {
        var c = NativeBindings.fon_collection_create();
        try {
            var error = new FonError();
            NativeBindings.fon_collection_add_string(c, "k", "this string needs space", ref error);

            Span<byte> tooSmall = stackalloc byte[4];
            bool ok = NativeApi.TrySerializeCollection(c, tooSmall, out int written);

            Assert.False(ok);
            Assert.True(written > tooSmall.Length);
        } finally {
            NativeBindings.fon_collection_free(c);
        }
    }


    [Fact]
    public void DeserializeDump_FromSpan_ZeroCopy() {
        var dump = NativeBindings.fon_dump_create();
        string serialized;
        try {
            var error = new FonError();
            var c = NativeBindings.fon_collection_create();
            NativeBindings.fon_collection_add_long(c, "v", 9_000_000_000L, ref error);
            NativeBindings.fon_dump_add(dump, 0, c, ref error);
            serialized = NativeApi.SerializeDump(dump);
        } finally {
            NativeBindings.fon_dump_free(dump);
        }

        ReadOnlySpan<byte> utf8 = Encoding.UTF8.GetBytes(serialized);
        IntPtr loaded = NativeApi.DeserializeDump(utf8);
        try {
            var l = NativeBindings.fon_dump_get(loaded, 0);
            var error = new FonError();
            NativeBindings.fon_collection_get_long(l, "v", out long v, ref error);
            Assert.Equal(9_000_000_000L, v);
        } finally {
            NativeBindings.fon_dump_free(loaded);
        }
    }


    [Fact]
    public void DeserializeDump_OnInvalidInput_ThrowsFonNativeException() {
        Assert.Throws<FonNativeException>(() => NativeApi.DeserializeDump("key=garbage_without_type_separator"));
    }
}
