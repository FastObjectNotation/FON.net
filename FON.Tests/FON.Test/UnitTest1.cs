using FON.Core;
using FON.Types;

namespace FON.Test;


public class FonSerializationTests {
    [Fact]
    public async Task SerializeAndDeserialize_SimpleCollection_RoundTrips() {
        using var collection = new FonCollection {
            { "name", "test" },
            { "value", 42 },
            { "price", 99.99f },
            { "active", true }
        };

        using var dump = new FonDump();
        dump.TryAdd(0, collection);

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAutoAsync(dump, tempFile);
            using var result = await Fon.DeserializeFromFileAutoAsync(tempFile);

            Assert.Equal(1, result.Count);
            var loaded = result[0];
            Assert.Equal("test", loaded.Get<string>("name"));
            Assert.Equal(42, loaded.Get<int>("value"));
            Assert.Equal(99.99f, loaded.Get<float>("price"));
            Assert.True(loaded.Get<bool>("active"));
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task SerializeAndDeserialize_Arrays_RoundTrips() {
        // Note: string[] and bool[] are deliberately not included here — the pinned native
        // fon-rust text parser only understands array syntax for numeric element types
        // ('s:[...]'/'b:[...]' fail to parse back from text; see FonCollection_AddAndSerialize
        // tests below for the one-way in-memory-to-text behavior of those two).
        using var collection = new FonCollection {
            { "numbers", new List<int> { 1, 2, 3, 4, 5 } },
            { "values", new List<float> { 1.1f, 2.2f, 3.3f } }
        };

        using var dump = new FonDump();
        dump.TryAdd(0, collection);

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAutoAsync(dump, tempFile);
            using var result = await Fon.DeserializeFromFileAutoAsync(tempFile);

            var loaded = result[0];
            Assert.Equal(new List<int> { 1, 2, 3, 4, 5 }, loaded.Get<List<int>>("numbers"));
            Assert.Equal(new List<float> { 1.1f, 2.2f, 3.3f }, loaded.Get<List<float>>("values"));
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void StringArray_And_BoolArray_SerializeToText_ButCannotBeParsedBack() {
        // Documents a known limitation of the pinned native fon-rust parser: string[] and
        // bool[] can be built and read back in-memory (no text round-trip involved), and
        // they serialize to valid-looking FON text, but that text cannot be parsed back —
        // the native parser only supports array syntax for numeric element types.
        using var collection = new FonCollection {
            { "names", new List<string> { "Alice", "Bob" } },
            { "flags", new List<bool> { true, false } }
        };

        // In-memory add/get works without going through the text format at all.
        Assert.Equal(new List<string> { "Alice", "Bob" }, collection.Get<List<string>>("names"));
        Assert.Equal(new List<bool> { true, false }, collection.Get<List<bool>>("flags"));

        var text = Fon.SerializeToString(collection);
        Assert.Contains("names=s:[", text);
        Assert.Contains("flags=b:[", text);

        var ex = Assert.Throws<FON.Native.FonNativeException>(() => Fon.DeserializeCollection(text));
        Assert.Equal(FON.Native.FonResultCode.ParseFailed, ex.Code);
    }


    [Fact]
    public async Task SerializeAndDeserialize_AllNumericTypes_RoundTrips() {
        using var collection = new FonCollection {
            { "byte", (byte)255 },
            { "short", (short)-1000 },
            { "int", -123456 },
            { "uint", 123456u },
            { "long", -9876543210L },
            { "ulong", 9876543210UL },
            { "float", 3.14159f },
            { "double", 3.141592653589793 }
        };

        using var dump = new FonDump();
        dump.TryAdd(0, collection);

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAutoAsync(dump, tempFile);
            using var result = await Fon.DeserializeFromFileAutoAsync(tempFile);

            var loaded = result[0];
            Assert.Equal((byte)255, loaded.Get<byte>("byte"));
            Assert.Equal((short)-1000, loaded.Get<short>("short"));
            Assert.Equal(-123456, loaded.Get<int>("int"));
            Assert.Equal(123456u, loaded.Get<uint>("uint"));
            Assert.Equal(-9876543210L, loaded.Get<long>("long"));
            Assert.Equal(9876543210UL, loaded.Get<ulong>("ulong"));
            Assert.Equal(3.14159f, loaded.Get<float>("float"), 5);
            Assert.Equal(3.141592653589793, loaded.Get<double>("double"), 10);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task SerializeAndDeserialize_StringEscaping_RoundTrips() {
        using var collection = new FonCollection {
            { "text", "Hello \"World\"\nNew line\tTab\\Backslash" }
        };

        using var dump = new FonDump();
        dump.TryAdd(0, collection);

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAutoAsync(dump, tempFile);
            using var result = await Fon.DeserializeFromFileAutoAsync(tempFile);

            var loaded = result[0];
            Assert.Equal("Hello \"World\"\nNew line\tTab\\Backslash", loaded.Get<string>("text"));
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task SerializeAndDeserialize_RawData_RoundTrips() {
        var originalData = new byte[] { 0, 1, 2, 3, 255, 254, 253 };
        var rawData = new RawData(originalData);

        using var collection = new FonCollection {
            { "binary", rawData }
        };

        using var dump = new FonDump();
        dump.TryAdd(0, collection);

        var tempFile = new FileInfo(Path.GetTempFileName());
        var originalUnpackSetting = Fon.DeserializeRawUnpack;

        try {
            Fon.DeserializeRawUnpack = true;
            await Fon.SerializeToFileAutoAsync(dump, tempFile);
            using var result = await Fon.DeserializeFromFileAutoAsync(tempFile);

            var loaded = result[0];
            var loadedRaw = loaded.Get<RawData>("binary");
            Assert.Equal(originalData, loadedRaw!.data);
        } finally {
            Fon.DeserializeRawUnpack = originalUnpackSetting;
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task SerializeAndDeserialize_MultipleRecords_RoundTrips() {
        using var dump = new FonDump();
        for (ulong i = 0; i < 100; i++) {
            var collection = new FonCollection {
                { "id", $"item_{i}" },
                { "index", (int)i }
            };
            dump.TryAdd(i, collection);
        }

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAutoAsync(dump, tempFile);
            using var result = await Fon.DeserializeFromFileAutoAsync(tempFile);

            Assert.Equal(100, result.Count);
            for (ulong i = 0; i < 100; i++) {
                var loaded = result[i];
                Assert.Equal($"item_{i}", loaded.Get<string>("id"));
                Assert.Equal((int)i, loaded.Get<int>("index"));
            }
        } finally {
            tempFile.Delete();
        }
    }
}


public class FonStringSerializationTests {
    [Fact]
    public void SerializeToString_SimpleCollection_ProducesValidFormat() {
        using var collection = new FonCollection {
            { "id", 42 },
            { "name", "test" }
        };

        var result = Fon.SerializeToString(collection);

        Assert.Contains("id=i:42", result);
        Assert.Contains("name=s:\"test\"", result);
    }


    [Fact]
    public void SerializeToString_EmptyCollection_ProducesEmptyString() {
        using var collection = new FonCollection();
        var result = Fon.SerializeToString(collection);
        Assert.Equal("", result);
    }


    [Fact]
    public void SerializeToString_Array_ProducesValidFormat() {
        using var collection = new FonCollection {
            { "nums", new List<int> { 1, 2, 3 } }
        };

        var result = Fon.SerializeToString(collection);

        Assert.Contains("nums=i:[1,2,3]", result);
    }


    [Fact]
    public void SerializeToString_EmptyArray_ProducesValidFormat() {
        using var collection = new FonCollection {
            { "empty", new List<int>() }
        };

        var result = Fon.SerializeToString(collection);

        Assert.Contains("empty=i:[]", result);
    }


    [Fact]
    public void SerializeToString_SpecialCharacters_EscapesCorrectly() {
        using var collection = new FonCollection {
            { "text", "line1\nline2\ttab\"quote" }
        };

        var result = Fon.SerializeToString(collection);

        Assert.Contains("\\n", result);
        Assert.Contains("\\t", result);
        Assert.Contains("\\\"", result);
    }


    [Fact]
    public void SerializeToString_Unicode_PreservesCharacters() {
        using var collection = new FonCollection {
            { "emoji", "Hello 🌍 World" },
            { "cyrillic", "Привет мир" },
            { "chinese", "你好世界" }
        };

        var result = Fon.SerializeToString(collection);

        Assert.Contains("🌍", result);
        Assert.Contains("Привет", result);
        Assert.Contains("你好", result);
    }
}


public class FonCollectionTests {
    [Fact]
    public void FonCollection_Add_StoresValue() {
        using var collection = new FonCollection();
        collection.Add("key", "value");

        Assert.Equal("value", collection.Get<string>("key"));
    }


    [Fact]
    public void FonCollection_Get_ThrowsForMissingKey() {
        using var collection = new FonCollection();

        Assert.Throws<KeyNotFoundException>(() => collection.Get<string>("missing"));
    }


    [Fact]
    public void FonCollection_TryGet_ReturnsNullForMissingKey() {
        using var collection = new FonCollection();

        var result = collection.TryGet<string>("missing");

        Assert.Null(result);
    }


    [Fact]
    public void FonCollection_TryGetNullable_ReturnsNullForMissingValueType() {
        using var collection = new FonCollection();

        var result = collection.TryGetNullable<int>("missing");

        Assert.Null(result);
    }


    [Fact]
    public void FonCollection_Indexer_WorksCorrectly() {
        using var collection = new FonCollection {
            { "key", 123 }
        };

        Assert.Equal(123, collection["key"]);
    }


    [Fact]
    public void FonCollection_Count_ReturnsCorrectCount() {
        using var collection = new FonCollection {
            { "a", 1 },
            { "b", 2 },
            { "c", 3 }
        };

        Assert.Equal(3, collection.Count());
    }


    [Fact]
    public void FonCollection_Enumeration_WorksCorrectly() {
        using var collection = new FonCollection {
            { "a", 1 },
            { "b", 2 }
        };

        var keys = collection.Select(kvp => kvp.Key).ToList();

        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }


    [Fact]
    public void FonCollection_Add_ThrowsForDuplicateKey() {
        using var collection = new FonCollection { { "key", 1 } };

        Assert.Throws<InvalidOperationException>(() => collection.Add("key", 2));
    }


    [Fact]
    public void FonCollection_Remove_ThrowsNotSupported() {
        using var collection = new FonCollection { { "key", 1 } };

        Assert.Throws<NotSupportedException>(() => collection.Remove("key"));
    }


    [Fact]
    public void FonCollection_Dispose_ThenAccess_ThrowsObjectDisposed() {
        var collection = new FonCollection { { "key", 1 } };
        collection.Dispose();

        Assert.Throws<ObjectDisposedException>(() => collection.Get<int>("key"));
    }
}


public class FonDumpTests {
    [Fact]
    public void FonDump_TryAdd_AddsRecord() {
        using var dump = new FonDump();
        var collection = new FonCollection { { "test", 1 } };

        var result = dump.TryAdd(0, collection);

        Assert.True(result);
        Assert.Equal(1, dump.Count);
    }


    [Fact]
    public void FonDump_TryAdd_ReturnsFalseForDuplicate() {
        using var dump = new FonDump();
        dump.TryAdd(0, new FonCollection { { "first", 1 } });

        var result = dump.TryAdd(0, new FonCollection { { "second", 2 } });

        Assert.False(result);
    }


    [Fact]
    public void FonDump_Indexer_ReturnsCorrectCollection() {
        using var dump = new FonDump();
        var collection = new FonCollection { { "value", 42 } };
        dump.TryAdd(5, collection);

        var result = dump[5];

        Assert.Equal(42, result.Get<int>("value"));
    }


    [Fact]
    public void FonDump_TryGet_ReturnsCorrectly() {
        using var dump = new FonDump();
        dump.TryAdd(10, new FonCollection { { "x", 1 } });

        Assert.NotNull(dump.TryGet(10));
        Assert.Null(dump.TryGet(20));
    }


    [Fact]
    public void FonDump_Remove_ThrowsNotSupported() {
        using var dump = new FonDump();
        dump.TryAdd(0, new FonCollection { { "x", 1 } });

        Assert.Throws<NotSupportedException>(() => dump.Remove(0));
    }
}


public class FonMethodsTests {
    [Fact]
    public async Task SerializeToFileAsync_CreatesFile() {
        using var dump = new FonDump();
        dump.TryAdd(0, new FonCollection { { "test", 1 } });

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAsync(dump, tempFile);

            Assert.True(tempFile.Exists);
            Assert.True(tempFile.Length > 0);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task SerializeToFilePipelineAsync_CreatesFile() {
        using var dump = new FonDump();
        dump.TryAdd(0, new FonCollection { { "test", 1 } });

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFilePipelineAsync(dump, tempFile);

            Assert.True(tempFile.Exists);
            Assert.True(tempFile.Length > 0);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task SerializeToFileChunkedAsync_CreatesFile() {
        using var dump = new FonDump();
        for (ulong i = 0; i < 50; i++) {
            dump.TryAdd(i, new FonCollection { { "id", (int)i } });
        }

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileChunkedAsync(dump, tempFile, chunkSize: 10);

            Assert.True(tempFile.Exists);

            using var result = await Fon.DeserializeFromFileAutoAsync(tempFile);
            Assert.Equal(50, result.Count);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task DeserializeFromFileAsync_LoadsCorrectly() {
        using var dump = new FonDump();
        dump.TryAdd(0, new FonCollection { { "value", 123 } });

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAutoAsync(dump, tempFile);
            using var result = await Fon.DeserializeFromFileAsync(tempFile);

            Assert.Equal(1, result.Count);
            Assert.Equal(123, result[0].Get<int>("value"));
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task DeserializeFromFileChunkedAsync_LoadsCorrectly() {
        using var dump = new FonDump();
        for (ulong i = 0; i < 50; i++) {
            dump.TryAdd(i, new FonCollection { { "id", (int)i } });
        }

        var tempFile = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAutoAsync(dump, tempFile);
            using var result = await Fon.DeserializeFromFileChunkedAsync(tempFile, chunkSize: 10);

            Assert.Equal(50, result.Count);
        } finally {
            tempFile.Delete();
        }
    }
}


public class RawDataTests {
    [Fact]
    public void RawData_Constructor_StoresData() {
        var data = new byte[] { 1, 2, 3 };
        var raw = new RawData(data);

        Assert.Equal(data, raw.data);
    }


    [Fact]
    public void RawData_Pack_ProducesBase64() {
        var data = new byte[] { 1, 2, 3 };
        var raw = new RawData(data);

        var packed = raw.Pack();

        Assert.NotNull(packed.encoded);
        Assert.True(packed.encoded.Length > 0);
    }


    [Fact]
    public void RawData_PackUnpack_RoundTrips() {
        var originalData = new byte[] { 0, 127, 255, 1, 2, 3 };
        var raw = new RawData(originalData);

        var packed = raw.Pack();
        var unpacked = new RawData(packed.encoded.AsSpan());
        unpacked.Unpack();

        Assert.Equal(originalData, unpacked.data);
    }
}
