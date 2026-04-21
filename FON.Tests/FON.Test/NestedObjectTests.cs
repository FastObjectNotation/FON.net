using FON.Core;
using FON.Types;

namespace FON.Test;


public class NestedObjectTests {
    [Fact]
    public void MaxDepth_DefaultsTo64() {
        Assert.Equal(64, Fon.MaxDepth);
    }


    [Fact]
    public void MaxDepth_RejectsZero() {
        var original = Fon.MaxDepth;
        try {
            Assert.Throws<ArgumentOutOfRangeException>(() => Fon.MaxDepth = 0);
        } finally {
            Fon.MaxDepth = original;
        }
    }


    [Fact]
    public void MaxDepth_RejectsNegative() {
        var original = Fon.MaxDepth;
        try {
            Assert.Throws<ArgumentOutOfRangeException>(() => Fon.MaxDepth = -1);
        } finally {
            Fon.MaxDepth = original;
        }
    }


    [Fact]
    public void MaxDepth_AcceptsOne() {
        var original = Fon.MaxDepth;
        try {
            Fon.MaxDepth = 1;
            Assert.Equal(1, Fon.MaxDepth);
        } finally {
            Fon.MaxDepth = original;
        }
    }


    [Fact]
    public void SupportTypes_RegistersFonCollectionAsObject() {
        Assert.True(Fon.SupportTypes.ContainsKey(typeof(FonCollection)));
        Assert.Equal('o', Fon.SupportTypes[typeof(FonCollection)]);
    }


    [Fact]
    public void Serialize_NestedObject_ProducesObjectLiteral() {
        var inner = new FonCollection { { "x", 1 } };
        var outer = new FonCollection { { "inner", inner } };

        var text = Fon.SerializeToString(outer);

        Assert.Equal("inner=o:{x=i:1}", text);
    }


    [Fact]
    public void Serialize_TwoLevelNested_ProducesNestedLiteral() {
        var deepest = new FonCollection { { "v", 99 } };
        var middle = new FonCollection { { "deep", deepest } };
        var outer = new FonCollection { { "mid", middle } };

        var text = Fon.SerializeToString(outer);

        Assert.Equal("mid=o:{deep=o:{v=i:99}}", text);
    }


    [Fact]
    public void Serialize_EmptyNestedObject_ProducesEmptyBraces() {
        var outer = new FonCollection { { "empty", new FonCollection() } };

        var text = Fon.SerializeToString(outer);

        Assert.Equal("empty=o:{}", text);
    }


    [Fact]
    public void Deserialize_NestedObject_RoundTrips() {
        var inner = new FonCollection { { "x", 1 } };
        var outer = new FonCollection { { "inner", inner } };

        var text = Fon.SerializeToString(outer);
        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            Assert.Equal(1, loaded.Count);
            var loadedOuter = loaded[0];
            var loadedInner = loadedOuter.Get<FonCollection>("inner");
            Assert.Equal(1, loadedInner.Get<int>("x"));
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void Deserialize_ThreeLevelNested_RoundTrips() {
        var l3 = new FonCollection { { "v", 42 } };
        var l2 = new FonCollection { { "l3", l3 } };
        var l1 = new FonCollection { { "l2", l2 } };
        var l0 = new FonCollection { { "l1", l1 } };

        var dump = new FonDump();
        dump.TryAdd(0, l0);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var got = loaded[0]
                .Get<FonCollection>("l1")
                .Get<FonCollection>("l2")
                .Get<FonCollection>("l3")
                .Get<int>("v");
            Assert.Equal(42, got);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void Deserialize_EmptyNestedObject_RoundTrips() {
        var outer = new FonCollection { { "empty", new FonCollection() } };

        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var inner = loaded[0].Get<FonCollection>("empty");
            Assert.Equal(0, inner.Count());
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public async Task Deserialize_NestedObject_NotFollowedByBrace_Throws() {
        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            File.WriteAllText(tempFile.FullName, "x=o:42\n");
            var ex = await Assert.ThrowsAsync<AggregateException>(() => Fon.DeserializeFromFileAutoAsync(tempFile));
            Assert.IsType<FormatException>(ex.InnerException);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void RoundTrip_ArrayOfObjects_HeterogeneousShapes() {
        var items = new List<FonCollection> {
            new FonCollection { { "id", 1 }, { "qty", 5 } },
            new FonCollection { { "id", 2 }, { "qty", 3 } },
            new FonCollection { { "id", 3 }, { "name", "third" } }
        };
        var outer = new FonCollection { { "items", items } };

        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var loadedItems = loaded[0].Get<List<FonCollection>>("items");
            Assert.Equal(3, loadedItems.Count);
            Assert.Equal(1, loadedItems[0].Get<int>("id"));
            Assert.Equal(5, loadedItems[0].Get<int>("qty"));
            Assert.Equal(2, loadedItems[1].Get<int>("id"));
            Assert.Equal(3, loadedItems[2].Get<int>("id"));
            Assert.Equal("third", loadedItems[2].Get<string>("name"));
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void RoundTrip_EmptyArrayOfObjects() {
        var outer = new FonCollection { { "items", new List<FonCollection>() } };

        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var items = loaded[0].Get<List<FonCollection>>("items");
            Assert.Empty(items);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void RoundTrip_ObjectInsideArrayInsideObject() {
        var inner = new FonCollection { { "tag", "leaf" } };
        var arr = new List<FonCollection> { inner };
        var middle = new FonCollection { { "list", arr } };
        var outer = new FonCollection { { "wrap", middle } };

        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var loadedTag = loaded[0]
                .Get<FonCollection>("wrap")
                .Get<List<FonCollection>>("list")[0]
                .Get<string>("tag");
            Assert.Equal("leaf", loadedTag);
        } finally {
            tempFile.Delete();
        }
    }


    private static FonCollection BuildNested(int depth) {
        var current = new FonCollection { { "leaf", 1 } };
        for (int i = 0; i < depth; i++) {
            var wrap = new FonCollection { { "n", current } };
            current = wrap;
        }
        return current;
    }


    [Fact]
    public void Deserialize_AtExactlyMaxDepth_Succeeds() {
        var original = Fon.MaxDepth;
        try {
            Fon.MaxDepth = 5;
            var nested = BuildNested(5);
            var dump = new FonDump();
            dump.TryAdd(0, nested);

            var tempFile = new FileInfo(Path.GetTempFileName());
            try {
                Fon.SerializeToFileAuto(dump, tempFile);
                var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();
                Assert.Equal(1, loaded.Count);
            } finally {
                tempFile.Delete();
            }
        } finally {
            Fon.MaxDepth = original;
        }
    }


    [Fact]
    public async Task Deserialize_AboveMaxDepth_Throws() {
        var original = Fon.MaxDepth;
        try {
            Fon.MaxDepth = 5;
            var nested = BuildNested(6);
            var dump = new FonDump();
            dump.TryAdd(0, nested);

            var tempFile = new FileInfo(Path.GetTempFileName());
            try {
                Fon.SerializeToFileAuto(dump, tempFile);
                var ex = await Assert.ThrowsAsync<AggregateException>(
                    () => Fon.DeserializeFromFileAutoAsync(tempFile)
                );
                Assert.IsType<FormatException>(ex.InnerException);
            } finally {
                tempFile.Delete();
            }
        } finally {
            Fon.MaxDepth = original;
        }
    }


    [Fact]
    public async Task Deserialize_DeepArray_AboveMaxDepth_Throws() {
        var original = Fon.MaxDepth;
        try {
            Fon.MaxDepth = 2;
            var tempFile = new FileInfo(Path.GetTempFileName());
            try {
                // Depth-1 array: should still succeed at MaxDepth=2
                File.WriteAllText(tempFile.FullName, "a=i:[1,2,3]\n");
                var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();
                Assert.Equal(1, loaded.Count);

                // Depth-3 construction: array of objects each containing an array. Must throw.
                File.WriteAllText(tempFile.FullName, "items=o:[{vals=i:[1,2]}]\n");
                var ex = await Assert.ThrowsAsync<AggregateException>(
                    () => Fon.DeserializeFromFileAutoAsync(tempFile)
                );
                Assert.IsType<FormatException>(ex.InnerException);
            } finally {
                tempFile.Delete();
            }
        } finally {
            Fon.MaxDepth = original;
        }
    }


    private sealed class TrackingDisposable : IDisposable {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }


    [Fact]
    public void Dispose_DisposesNestedFonCollections() {
        var inner = new FonCollection();
        var outer = new FonCollection { { "inner", inner } };

        // Add a tracker into inner so we can detect its Dispose
        var tracker = new TrackingDisposable();
        inner.Add("tracker", tracker);

        outer.Dispose();

        Assert.True(tracker.Disposed);
    }


    [Fact]
    public void Dispose_DisposesElementsOfDisposableLists() {
        var t1 = new TrackingDisposable();
        var t2 = new TrackingDisposable();

        var collection = new FonCollection {
            { "items", new List<TrackingDisposable> { t1, t2 } }
        };

        collection.Dispose();

        Assert.True(t1.Disposed);
        Assert.True(t2.Disposed);
    }


    [Fact]
    public void Dispose_DisposesNestedCollectionsInsideLists() {
        var c1 = new FonCollection();
        var c2 = new FonCollection();
        var t1 = new TrackingDisposable();
        var t2 = new TrackingDisposable();
        c1.Add("t", t1);
        c2.Add("t", t2);

        var outer = new FonCollection {
            { "items", new List<FonCollection> { c1, c2 } }
        };

        outer.Dispose();

        Assert.True(t1.Disposed);
        Assert.True(t2.Disposed);
    }


    [Theory]
    [InlineData("contains}brace")]
    [InlineData("contains{open")]
    [InlineData("contains[bracket")]
    [InlineData("contains]closebr")]
    [InlineData("contains,comma")]
    [InlineData("contains=equals")]
    [InlineData("multi}{][,=mix")]
    public void RoundTrip_NestedString_WithMetacharacter(string payload) {
        var inner = new FonCollection { { "txt", payload } };
        var outer = new FonCollection { { "wrap", inner } };

        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var got = loaded[0].Get<FonCollection>("wrap").Get<string>("txt");
            Assert.Equal(payload, got);
        } finally {
            tempFile.Delete();
        }
    }


    [Fact]
    public void RoundTrip_NestedString_WithStandardEscapes() {
        var payload = "line1\nline2\twith\\backslash\"quote";
        var inner = new FonCollection { { "txt", payload } };
        var outer = new FonCollection { { "wrap", inner } };

        var dump = new FonDump();
        dump.TryAdd(0, outer);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();

            var got = loaded[0].Get<FonCollection>("wrap").Get<string>("txt");
            Assert.Equal(payload, got);
        } finally {
            tempFile.Delete();
        }
    }


    private static FonDump BuildNestedDump(int recordCount) {
        var dump = new FonDump();
        for (int i = 0; i < recordCount; i++) {
            var inner = new FonCollection {
                { "id", i },
                { "name", $"item-{i}" }
            };
            var outer = new FonCollection {
                { "i", i },
                { "nested", inner },
                { "items", new List<FonCollection> {
                    new FonCollection { { "x", i + 1 } },
                    new FonCollection { { "x", i + 2 } }
                } }
            };
            dump.TryAdd((ulong)i, outer);
        }
        return dump;
    }


    private static void AssertDumpEquals(FonDump expected, FonDump actual) {
        Assert.Equal(expected.Count, actual.Count);
        for (ulong i = 0; i < (ulong)expected.Count; i++) {
            var e = expected[i];
            var a = actual[i];
            Assert.Equal(e.Get<int>("i"), a.Get<int>("i"));
            Assert.Equal(e.Get<FonCollection>("nested").Get<int>("id"),
                         a.Get<FonCollection>("nested").Get<int>("id"));
            Assert.Equal(e.Get<FonCollection>("nested").Get<string>("name"),
                         a.Get<FonCollection>("nested").Get<string>("name"));
            var ei = e.Get<List<FonCollection>>("items");
            var ai = a.Get<List<FonCollection>>("items");
            Assert.Equal(ei.Count, ai.Count);
            for (int j = 0; j < ei.Count; j++) {
                Assert.Equal(ei[j].Get<int>("x"), ai[j].Get<int>("x"));
            }
        }
    }


    [Fact]
    public async Task RoundTrip_AllSerializeMethods_NestedData() {
        var dump = BuildNestedDump(10);

        var f1 = new FileInfo(Path.GetTempFileName());
        var f2 = new FileInfo(Path.GetTempFileName());
        var f3 = new FileInfo(Path.GetTempFileName());
        var f4 = new FileInfo(Path.GetTempFileName());

        try {
            await Fon.SerializeToFileAsync(dump, f1);
            await Fon.SerializeToFilePipelineAsync(dump, f2);
            await Fon.SerializeToFileChunkedAsync(dump, f3, chunkSize: 4);
            await Fon.SerializeToFileAutoAsync(dump, f4);

            var b1 = await File.ReadAllBytesAsync(f1.FullName);
            var b2 = await File.ReadAllBytesAsync(f2.FullName);
            var b3 = await File.ReadAllBytesAsync(f3.FullName);
            var b4 = await File.ReadAllBytesAsync(f4.FullName);

            Assert.Equal(b1, b2);
            Assert.Equal(b1, b3);
            Assert.Equal(b1, b4);
        } finally {
            f1.Delete();
            f2.Delete();
            f3.Delete();
            f4.Delete();
        }
    }


    [Fact]
    public async Task RoundTrip_AllDeserializeMethods_NestedData() {
        var dump = BuildNestedDump(10);

        var file = new FileInfo(Path.GetTempFileName());
        try {
            await Fon.SerializeToFileAutoAsync(dump, file);

            var loaded1 = await Fon.DeserializeFromFileAsync(file);
            var loaded2 = await Fon.DeserializeFromFileChunkedAsync(file, chunkSize: 4);
            var loaded3 = await Fon.DeserializeFromFileAutoAsync(file);

            AssertDumpEquals(dump, loaded1);
            AssertDumpEquals(dump, loaded2);
            AssertDumpEquals(dump, loaded3);
        } finally {
            file.Delete();
        }
    }


    [Fact]
    public async Task RoundTrip_LargeNestedDump_UnderParallelism() {
        var dump = BuildNestedDump(5000);

        var file = new FileInfo(Path.GetTempFileName());
        try {
            await Fon.SerializeToFileAutoAsync(dump, file);
            var loaded = await Fon.DeserializeFromFileAutoAsync(file);
            AssertDumpEquals(dump, loaded);
        } finally {
            file.Delete();
        }
    }
}
