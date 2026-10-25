using FON.Core;
using FON.Types;
using Xunit;

namespace FON.Test;


/// <summary>
/// Every value kind (23 in total) round-trips through the in-memory API and through text,
/// including the minimum and maximum of each integer type and NaN/infinity for floats.
/// </summary>
public class KindRoundTripTests {
    [Fact]
    public void ScalarKinds_MinAndMax_AddThenGet() {
        using var collection = new FonCollection();
        collection.Add("byte-max", byte.MaxValue);
        collection.Add("byte-min", byte.MinValue);
        collection.Add("short-max", short.MaxValue);
        collection.Add("short-min", short.MinValue);
        collection.Add("int-max", int.MaxValue);
        collection.Add("int-min", int.MinValue);
        collection.Add("uint-max", uint.MaxValue);
        collection.Add("uint-min", uint.MinValue);
        collection.Add("long-max", long.MaxValue);
        collection.Add("long-min", long.MinValue);
        collection.Add("ulong-max", ulong.MaxValue);
        collection.Add("ulong-min", ulong.MinValue);

        Assert.Equal(byte.MaxValue, collection.Get<byte>("byte-max"));
        Assert.Equal(byte.MinValue, collection.Get<byte>("byte-min"));
        Assert.Equal(short.MaxValue, collection.Get<short>("short-max"));
        Assert.Equal(short.MinValue, collection.Get<short>("short-min"));
        Assert.Equal(int.MaxValue, collection.Get<int>("int-max"));
        Assert.Equal(int.MinValue, collection.Get<int>("int-min"));
        Assert.Equal(uint.MaxValue, collection.Get<uint>("uint-max"));
        Assert.Equal(uint.MinValue, collection.Get<uint>("uint-min"));
        Assert.Equal(long.MaxValue, collection.Get<long>("long-max"));
        Assert.Equal(long.MinValue, collection.Get<long>("long-min"));
        Assert.Equal(ulong.MaxValue, collection.Get<ulong>("ulong-max"));
        Assert.Equal(ulong.MinValue, collection.Get<ulong>("ulong-min"));
    }


    [Fact]
    public void FloatKinds_NaNAndInfinity_AddThenGet() {
        using var collection = new FonCollection();
        collection.Add("float-nan", float.NaN);
        collection.Add("float-inf", float.PositiveInfinity);
        collection.Add("float-neg-inf", float.NegativeInfinity);
        collection.Add("double-nan", double.NaN);
        collection.Add("double-inf", double.PositiveInfinity);
        collection.Add("double-neg-inf", double.NegativeInfinity);
        collection.Add("double-max", double.MaxValue);
        collection.Add("float-min", float.Epsilon);

        Assert.True(float.IsNaN(collection.Get<float>("float-nan")));
        Assert.Equal(float.PositiveInfinity, collection.Get<float>("float-inf"));
        Assert.Equal(float.NegativeInfinity, collection.Get<float>("float-neg-inf"));
        Assert.True(double.IsNaN(collection.Get<double>("double-nan")));
        Assert.Equal(double.PositiveInfinity, collection.Get<double>("double-inf"));
        Assert.Equal(double.NegativeInfinity, collection.Get<double>("double-neg-inf"));
        Assert.Equal(double.MaxValue, collection.Get<double>("double-max"));
        Assert.Equal(float.Epsilon, collection.Get<float>("float-min"));
    }


    [Fact]
    public void BoolRawStringAndNestedKinds_AddThenGet() {
        var inner = new FonCollection { { "x", 1 } };
        using var collection = new FonCollection();
        collection.Add("yes", true);
        collection.Add("no", false);
        collection.Add("text", "plain");
        collection.Add("blob", new RawData(new byte[] { 0, 1, 2, 254, 255 }));
        collection.Add("inner", inner);

        Assert.True(collection.Get<bool>("yes"));
        Assert.False(collection.Get<bool>("no"));
        Assert.Equal("plain", collection.Get<string>("text"));
        Assert.Equal(new byte[] { 0, 1, 2, 254, 255 }, collection.Get<RawData>("blob").data);
        Assert.Equal(1, collection.Get<FonCollection>("inner").Get<int>("x"));
    }


    [Fact]
    public void ArrayKinds_MinAndMaxElements_AddThenGet() {
        using var collection = new FonCollection();
        collection.Add("bytes", new List<byte> { byte.MinValue, byte.MaxValue });
        collection.Add("shorts", new List<short> { short.MinValue, short.MaxValue });
        collection.Add("ints", new List<int> { int.MinValue, int.MaxValue });
        collection.Add("uints", new List<uint> { uint.MinValue, uint.MaxValue });
        collection.Add("longs", new List<long> { long.MinValue, long.MaxValue });
        collection.Add("ulongs", new List<ulong> { ulong.MinValue, ulong.MaxValue });
        collection.Add("floats", new List<float> { float.NaN, float.NegativeInfinity, 1.5f });
        collection.Add("doubles", new List<double> { double.NaN, double.PositiveInfinity, -2.25 });
        collection.Add("bools", new List<bool> { true, false, true });
        collection.Add("strings", new List<string> { "a", "b" });

        Assert.Equal(new List<byte> { byte.MinValue, byte.MaxValue }, collection.Get<List<byte>>("bytes"));
        Assert.Equal(new List<short> { short.MinValue, short.MaxValue }, collection.Get<List<short>>("shorts"));
        Assert.Equal(new List<int> { int.MinValue, int.MaxValue }, collection.Get<List<int>>("ints"));
        Assert.Equal(new List<uint> { uint.MinValue, uint.MaxValue }, collection.Get<List<uint>>("uints"));
        Assert.Equal(new List<long> { long.MinValue, long.MaxValue }, collection.Get<List<long>>("longs"));
        Assert.Equal(new List<ulong> { ulong.MinValue, ulong.MaxValue }, collection.Get<List<ulong>>("ulongs"));
        var floats = collection.Get<List<float>>("floats");
        Assert.True(float.IsNaN(floats[0]));
        Assert.Equal(float.NegativeInfinity, floats[1]);
        Assert.Equal(1.5f, floats[2]);
        var doubles = collection.Get<List<double>>("doubles");
        Assert.True(double.IsNaN(doubles[0]));
        Assert.Equal(double.PositiveInfinity, doubles[1]);
        Assert.Equal(-2.25, doubles[2]);
        Assert.Equal(new List<bool> { true, false, true }, collection.Get<List<bool>>("bools"));
        Assert.Equal(new List<string> { "a", "b" }, collection.Get<List<string>>("strings"));
    }


    [Fact]
    public void CollectionArray_AddThenGet_KeepsOrderAndContent() {
        using var collection = new FonCollection {
            { "rows", new List<FonCollection> { new FonCollection { { "n", 1 } }, new FonCollection { { "n", 2 } } } }
        };

        var rows = collection.Get<List<FonCollection>>("rows");

        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows[0].Get<int>("n"));
        Assert.Equal(2, rows[1].Get<int>("n"));
    }


    [Fact]
    public void AllTwentyThreeKinds_SurviveSerializeAndDeserialize() {
        using var source = new FonCollection {
            { "byte", byte.MaxValue },
            { "short", short.MinValue },
            { "int", int.MinValue },
            { "uint", uint.MaxValue },
            { "long", long.MinValue },
            { "ulong", ulong.MaxValue },
            { "float", 0.5f },
            { "double", -1234.5678 },
            { "bool", true },
            { "string", "value" },
            { "raw", new RawData(new byte[] { 9, 8, 7 }) },
            { "object", new FonCollection { { "k", "v" } } },
            { "bytes", new List<byte> { 1, 255 } },
            { "shorts", new List<short> { -1, 1 } },
            { "ints", new List<int> { int.MinValue } },
            { "uints", new List<uint> { uint.MaxValue } },
            { "longs", new List<long> { long.MaxValue } },
            { "ulongs", new List<ulong> { 0 } },
            { "floats", new List<float> { 1.25f } },
            { "doubles", new List<double> { 2.5 } },
            { "bools", new List<bool> { false, true } },
            { "strings", new List<string> { "x", "" } },
            { "objects", new List<FonCollection> { new FonCollection { { "z", 3 } } } }
        };

        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(source));

        Assert.Equal(23, loaded.Count);
        Assert.Equal(byte.MaxValue, loaded.Get<byte>("byte"));
        Assert.Equal(short.MinValue, loaded.Get<short>("short"));
        Assert.Equal(int.MinValue, loaded.Get<int>("int"));
        Assert.Equal(uint.MaxValue, loaded.Get<uint>("uint"));
        Assert.Equal(long.MinValue, loaded.Get<long>("long"));
        Assert.Equal(ulong.MaxValue, loaded.Get<ulong>("ulong"));
        Assert.Equal(0.5f, loaded.Get<float>("float"));
        Assert.Equal(-1234.5678, loaded.Get<double>("double"));
        Assert.True(loaded.Get<bool>("bool"));
        Assert.Equal("value", loaded.Get<string>("string"));
        Assert.Equal(new byte[] { 9, 8, 7 }, loaded.Get<RawData>("raw").data);
        Assert.Equal("v", loaded.Get<FonCollection>("object").Get<string>("k"));
        Assert.Equal(new List<byte> { 1, 255 }, loaded.Get<List<byte>>("bytes"));
        Assert.Equal(new List<short> { -1, 1 }, loaded.Get<List<short>>("shorts"));
        Assert.Equal(new List<int> { int.MinValue }, loaded.Get<List<int>>("ints"));
        Assert.Equal(new List<uint> { uint.MaxValue }, loaded.Get<List<uint>>("uints"));
        Assert.Equal(new List<long> { long.MaxValue }, loaded.Get<List<long>>("longs"));
        Assert.Equal(new List<ulong> { 0 }, loaded.Get<List<ulong>>("ulongs"));
        Assert.Equal(new List<float> { 1.25f }, loaded.Get<List<float>>("floats"));
        Assert.Equal(new List<double> { 2.5 }, loaded.Get<List<double>>("doubles"));
        Assert.Equal(new List<bool> { false, true }, loaded.Get<List<bool>>("bools"));
        Assert.Equal(new List<string> { "x", "" }, loaded.Get<List<string>>("strings"));
        Assert.Equal("z", loaded.Get<List<FonCollection>>("objects")[0].Keys[0]);
    }


    [Fact]
    public void ExtremeValues_SurviveSerializeAndDeserialize() {
        using var source = new FonCollection {
            { "byte-min", byte.MinValue },
            { "byte-max", byte.MaxValue },
            { "short-min", short.MinValue },
            { "short-max", short.MaxValue },
            { "int-min", int.MinValue },
            { "int-max", int.MaxValue },
            { "uint-min", uint.MinValue },
            { "uint-max", uint.MaxValue },
            { "long-min", long.MinValue },
            { "long-max", long.MaxValue },
            { "ulong-min", ulong.MinValue },
            { "ulong-max", ulong.MaxValue },
            { "float-nan", float.NaN },
            { "float-inf", float.PositiveInfinity },
            { "float-neg-inf", float.NegativeInfinity },
            { "double-nan", double.NaN },
            { "double-inf", double.PositiveInfinity },
            { "double-neg-inf", double.NegativeInfinity },
            { "floats", new List<float> { float.NaN, float.PositiveInfinity, float.NegativeInfinity } },
            { "doubles", new List<double> { double.NaN, double.PositiveInfinity, double.NegativeInfinity } }
        };

        using var loaded = Fon.DeserializeCollection(Fon.SerializeToString(source));

        Assert.Equal(byte.MinValue, loaded.Get<byte>("byte-min"));
        Assert.Equal(byte.MaxValue, loaded.Get<byte>("byte-max"));
        Assert.Equal(short.MinValue, loaded.Get<short>("short-min"));
        Assert.Equal(short.MaxValue, loaded.Get<short>("short-max"));
        Assert.Equal(int.MinValue, loaded.Get<int>("int-min"));
        Assert.Equal(int.MaxValue, loaded.Get<int>("int-max"));
        Assert.Equal(uint.MinValue, loaded.Get<uint>("uint-min"));
        Assert.Equal(uint.MaxValue, loaded.Get<uint>("uint-max"));
        Assert.Equal(long.MinValue, loaded.Get<long>("long-min"));
        Assert.Equal(long.MaxValue, loaded.Get<long>("long-max"));
        Assert.Equal(ulong.MinValue, loaded.Get<ulong>("ulong-min"));
        Assert.Equal(ulong.MaxValue, loaded.Get<ulong>("ulong-max"));
        Assert.True(float.IsNaN(loaded.Get<float>("float-nan")));
        Assert.Equal(float.PositiveInfinity, loaded.Get<float>("float-inf"));
        Assert.Equal(float.NegativeInfinity, loaded.Get<float>("float-neg-inf"));
        Assert.True(double.IsNaN(loaded.Get<double>("double-nan")));
        Assert.Equal(double.PositiveInfinity, loaded.Get<double>("double-inf"));
        Assert.Equal(double.NegativeInfinity, loaded.Get<double>("double-neg-inf"));

        var floats = loaded.Get<List<float>>("floats");
        Assert.True(float.IsNaN(floats[0]));
        Assert.Equal(float.PositiveInfinity, floats[1]);
        Assert.Equal(float.NegativeInfinity, floats[2]);
        var doubles = loaded.Get<List<double>>("doubles");
        Assert.True(double.IsNaN(doubles[0]));
        Assert.Equal(double.PositiveInfinity, doubles[1]);
        Assert.Equal(double.NegativeInfinity, doubles[2]);
    }


    [Fact]
    public void AllTwentyThreeKinds_ReportTheirKind() {
        using var collection = new FonCollection {
            { "byte", (byte)1 }, { "short", (short)1 }, { "int", 1 }, { "uint", 1u },
            { "long", 1L }, { "ulong", 1UL }, { "float", 1f }, { "double", 1d },
            { "bool", true }, { "string", "s" }, { "raw", new RawData(new byte[] { 1 }) },
            { "object", new FonCollection() },
            { "bytes", new List<byte> { 1 } }, { "shorts", new List<short> { 1 } },
            { "ints", new List<int> { 1 } }, { "uints", new List<uint> { 1 } },
            { "longs", new List<long> { 1 } }, { "ulongs", new List<ulong> { 1 } },
            { "floats", new List<float> { 1 } }, { "doubles", new List<double> { 1 } },
            { "bools", new List<bool> { true } }, { "strings", new List<string> { "s" } },
            { "objects", new List<FonCollection> { new FonCollection() } }
        };

        Assert.Equal(new FonKind(FonType.Byte, false), collection.KindOf("byte"));
        Assert.Equal(new FonKind(FonType.Short, false), collection.KindOf("short"));
        Assert.Equal(new FonKind(FonType.Int, false), collection.KindOf("int"));
        Assert.Equal(new FonKind(FonType.UInt, false), collection.KindOf("uint"));
        Assert.Equal(new FonKind(FonType.Long, false), collection.KindOf("long"));
        Assert.Equal(new FonKind(FonType.ULong, false), collection.KindOf("ulong"));
        Assert.Equal(new FonKind(FonType.Float, false), collection.KindOf("float"));
        Assert.Equal(new FonKind(FonType.Double, false), collection.KindOf("double"));
        Assert.Equal(new FonKind(FonType.Bool, false), collection.KindOf("bool"));
        Assert.Equal(new FonKind(FonType.String, false), collection.KindOf("string"));
        Assert.Equal(new FonKind(FonType.Raw, false), collection.KindOf("raw"));
        Assert.Equal(new FonKind(FonType.Object, false), collection.KindOf("object"));
        Assert.Equal(new FonKind(FonType.Byte, true), collection.KindOf("bytes"));
        Assert.Equal(new FonKind(FonType.Short, true), collection.KindOf("shorts"));
        Assert.Equal(new FonKind(FonType.Int, true), collection.KindOf("ints"));
        Assert.Equal(new FonKind(FonType.UInt, true), collection.KindOf("uints"));
        Assert.Equal(new FonKind(FonType.Long, true), collection.KindOf("longs"));
        Assert.Equal(new FonKind(FonType.ULong, true), collection.KindOf("ulongs"));
        Assert.Equal(new FonKind(FonType.Float, true), collection.KindOf("floats"));
        Assert.Equal(new FonKind(FonType.Double, true), collection.KindOf("doubles"));
        Assert.Equal(new FonKind(FonType.Bool, true), collection.KindOf("bools"));
        Assert.Equal(new FonKind(FonType.String, true), collection.KindOf("strings"));
        Assert.Equal(new FonKind(FonType.Object, true), collection.KindOf("objects"));
        Assert.Null(collection.KindOf("missing"));
    }
}
