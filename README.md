# FON — Fast Object Notation

A fast, human-readable, line-oriented serialization format — a compact alternative to JSON for record-style data; each line is one record; values are typed and can nest.

## Features

- **Compact, readable wire format** — `key=type:value` pairs, one record per line.
- **Typed values** — integers of every width, floats, bool, string, binary blobs, nested objects, and arrays of them.
- **Editable in memory** — add, set, replace and remove fields and records.
- **Z85 binary encoding** for raw blobs (5 ASCII characters per 4 bytes).
- **Nested objects with a depth limit** — nesting depth is configurable through `Fon.MaxDepth` (default 64).
- **Parallel by default** — serialization and parsing of files spread across CPU cores; `maxDegreeOfParallelism` caps the thread count.
- **Deterministic memory management** — `FonDump` and `FonCollection` implement `IDisposable`; a `using` frees their memory as soon as you are done.
- **Cross-platform** — Windows, Linux (glibc and musl), and macOS, on x64 and ARM64.

## Format

A record is a line of comma-separated `key=type:value` pairs:

```
key=type:value,key2=type2:value2
```

A file holds one record per line. Records are identified by their line number (0-based) when the file is loaded.

### Examples

```
# Simple values
name=s:"John",age=i:30,balance=d:1234.56

# Arrays
scores=i:[95,87,92,88]

# Binary data (Z85 encoded)
data=r:"0rJu61"
```

```
# Nested objects
user=o:{id=i:42,name=s:"Bob",addr=o:{city=s:"NY",zip=i:10001}}

# Arrays of objects
items=o:[{id=i:1,qty=i:5},{id=i:2,qty=i:3}]

# Empty object and empty array of objects
empty=o:{},none=o:[]
```

### Type codes

| Code | C# type | Example |
|------|---------|---------|
| `e` | `byte` | `count=e:255` |
| `t` | `short` | `year=t:2024` |
| `i` | `int` | `id=i:42` |
| `u` | `uint` | `flags=u:12345` |
| `l` | `long` | `timestamp=l:1234567890` |
| `g` | `ulong` | `bignum=g:18446744073709551615` |
| `f` | `float` | `ratio=f:3.14` |
| `d` | `double` | `precise=d:3.141592653589793` |
| `s` | `string` | `name=s:"Hello"` |
| `b` | `bool` | `active=b:1` |
| `r` | `RawData` | `data=r:"0rJu61"` |
| `o` | `FonCollection` | `user=o:{id=i:1,name=s:"Bob"}` |

Every type except `RawData` has an array form written as `[...]` after the colon: `values=i:[1,2,3,4,5]`, `tags=s:["sale","featured"]`, `flags=b:[1,0]`. Arrays of objects use the `o` code with `{...}` items: `items=o:[{id=i:1},{id=i:2}]`.

## Install

```bash
dotnet add package FastObjectNotation
```

One package. NuGet selects the binaries for your project's runtime identifier at restore time; see [Platform support](#platform-support).

## Usage

### A single record

```csharp
using FON.Core;
using FON.Types;

using var collection = new FonCollection();
collection.Add("id", 42);
collection.Add("name", "Test Item");
collection.Add("price", 99.99);
collection.Add("active", true);
collection.Add("tags", new List<string> { "sale", "featured" });

string text = Fon.SerializeToString(collection);
// id=i:42,name=s:"Test Item",price=d:99.99,active=b:1,tags=s:["sale","featured"]

using var parsed = Fon.DeserializeCollection(text);
int id = parsed.Get<int>("id");
```

### Many records

```csharp
using var dump = new FonDump();
dump.TryAdd(0, new FonCollection { { "id", 0 }, { "text", "Hi" } });
dump.TryAdd(1, new FonCollection { { "id", 1 }, { "text", "Hello" } });
dump.TryAdd(2, new FonCollection { { "id", 2 }, { "text", "World" } });

// Write to a file (auto-selects the best method)
await Fon.SerializeToFileAutoAsync(dump, new FileInfo("data.fon"));

// Read it back
using var loaded = await Fon.DeserializeFromFileAutoAsync(new FileInfo("data.fon"));

foreach (var (id, record) in loaded) {
    Console.WriteLine($"Record {id}: id={record.Get<int>("id")}");
}

// In memory
using var fromText = Fon.Deserialize(Fon.SerializeToString(dump));
```

### Nested objects and arrays of objects

```csharp
var address = new FonCollection {
    { "city", "NY" },
    { "zip", 10001 }
};

// The address now belongs to user; do not use it directly afterwards.
using var user = new FonCollection {
    { "id", 42 },
    { "addr", address }
};

var items = new List<FonCollection> {
    new() { { "id", 1 }, { "qty", 5 } },
    new() { { "id", 2 }, { "qty", 3 } }
};
user.Add("items", items);

FonCollection addr = user.Get<FonCollection>("addr");
string city = addr.Get<string>("city");

foreach (FonCollection item in user.Get<List<FonCollection>>("items")) {
    Console.WriteLine(item.Get<int>("qty"));
}
```

### Editing records

Fields of a collection can be added, replaced, and removed:

```csharp
using var record = new FonCollection { { "id", 1 }, { "price", 10 } };

record.Set("price", 12.5);        // upsert: replaces the value and its type
record.Set("note", "new field");  // upsert: adds the key when it is absent
record.Remove("note");            // true; false if the key was absent
record.Add("tag", "sale");        // InvalidOperationException if the key exists
```

Records in a dump are added, replaced, and removed by id:

```csharp
using var dump = new FonDump();
dump.Add(10, new FonCollection { { "name", "a" } });   // InvalidOperationException if the id exists
dump.Set(10, new FonCollection { { "name", "b" } });   // upsert
dump.Replace(10, new FonCollection { { "name", "c" } }); // KeyNotFoundException if the id is absent
dump.Remove(10);                                       // true; false if the id was absent

dump.Set(1, new FonCollection { { "name", "x" } });
dump.Set(2, new FonCollection { { "name", "y" } });
int removed = dump.RemoveMany(new ulong[] { 1, 2, 3 }); // 2; id 3 is absent and is ignored

dump.Set(5, new FonCollection { { "name", "z" } });
dump[5].Set("name", "edited");   // a record can be edited in place through its view
```

Ids are stable while the dump is in memory. Serializing the dump renumbers the records 0, 1, 2, … in ascending id order, so the ids of a loaded dump are its line numbers.

### Binary data and parse options

```csharp
using var blob = new FonCollection {
    { "data", new RawData(new byte[] { 1, 2, 3 }) }
};

string text = Fon.SerializeToString(blob);   // data=r:"0rJu61"

byte[] bytes = blob.Get<RawData>("data").data;
```

Parse options:

```csharp
Fon.DeserializeRawUnpack = true;  // decode RawData blobs while parsing
Fon.MaxDepth = 64;                // maximum bracket nesting; default 64

// Limit the threads used for file serialization and parsing
await Fon.SerializeToFileAutoAsync(dump, new FileInfo("data.fon"), maxDegreeOfParallelism: 4);
```

## Memory and lifetime

There are three kinds of collection objects:

- **Owned collection** — created with `new FonCollection()`, returned by `Clone()`, or parsed from a single line with `Fon.DeserializeCollection`. It owns native memory. Call `Dispose()` (or use `using`) when you are done.
- **Dump** — created with `new FonDump()` or parsed with `Fon.Deserialize` and the file readers. It owns native memory and is disposed the same way.
- **View** — a `FonCollection` read from a dump (`dump.Get(id)`, `dump[id]`, `TryGet`, enumeration) or from a nested collection (`Get<FonCollection>`, `Get<List<FonCollection>>`). A view owns nothing. It keeps its dump or owned root collection alive, so the memory stays valid for as long as the view is used.

A view is valid until the data it refers to changes:

```csharp
using var dump = Fon.Deserialize("id=i:1\nid=i:2");
FonCollection record = dump.Get(0);

record.Set("id", 5);   // a view survives edits to its own fields
dump.Remove(0);
record.Get<int>("id"); // FonStaleViewException: the record was removed
```

- A record view goes stale when its record is replaced or removed. Views of other records are not affected.
- A nested view goes stale when any field in its record changes, or when the root collection it was read from is changed. The collection that was changed is still valid.
- `FonStaleViewException` is thrown on use. Read the value again from its owner to get a valid view.
- Using a view, or an owned collection, after `Dispose()` (of it or of its owner) throws `ObjectDisposedException`.

Putting a collection into another collection or dump moves it:

```csharp
var child = new FonCollection { { "x", 1 } };
using var parent = new FonCollection { { "child", child } };

child.Get<int>("x");   // ObjectDisposedException: child now belongs to parent
```

- `Add`, `Set`, a dump's `Add`, `Set`, and `Replace`, and each element of a list all take ownership of the collection passed in.
- A failed put leaves the collection with the caller, still usable. This includes adding to an existing key or id.
- A view cannot be put into a container (`ArgumentException`). Call `Clone()` first and put the copy.
- A collection cannot be put into itself, into its own nested view, or twice in the same list (`ArgumentException`).
- `Clone()` returns a deep copy as a new owned collection. It works on views too.

## API

### `FonCollection` (namespace `FON.Types`)

| Member | Description |
|--------|-------------|
| `new FonCollection()` | Creates an empty owned collection. |
| `this[string key]` | Reads the value; `KeyNotFoundException` if the key is absent. Assigning is `Set`. |
| `Count` | Number of keys. |
| `Keys` | All keys in insertion order (`IReadOnlyList<string>`). |
| `Add(string key, object value)` | Adds a new key; `InvalidOperationException` if it exists. |
| `TryAdd(string key, object value)` | Adds a new key; returns `false` if it exists. |
| `Set(string key, object value)` | Stores the value, replacing any existing value and its type. |
| `Remove(string key)` | Removes the key; returns `false` if it was absent. |
| `ContainsKey(string key)` | Returns whether the key exists. |
| `KindOf(string key)` | Returns the `FonKind?` of the value, or `null` if the key is absent. |
| `Get(string key)` | Reads the value as `object`. |
| `Get<T>(string key)` | Reads the value as `T`; `KeyNotFoundException` if absent, `InvalidCastException` if the type differs. |
| `TryGet(string key)` | Reads the value as `object?`; `null` if absent. |
| `TryGet<T>(string key)` | Reads a reference-type value as `T?`; `null` if absent or of another type. |
| `TryGetNullable<T>(string key)` | Reads a value-type value as `T?`; `null` if absent or of another type. |
| `Clone()` | Returns a deep copy as a new owned collection. |
| `Serialize<T>(T obj)` | Static. Copies the public, non-null properties of `obj` into a new collection. |
| `Deserialize<T>()` | Creates a `T` and sets its public properties from the matching keys. |
| `Dispose()` | Frees the native memory of an owned collection. |

Enumeration yields `KeyValuePair<string, object>` in insertion order.

### `FonKind` and `FonType` (namespace `FON.Types`)

`FonKind` is `readonly record struct FonKind(FonType Type, bool IsArray)`. For an array, `Type` is the element type. `FonType` has the values `Byte`, `Short`, `Int`, `UInt`, `Long`, `ULong`, `Float`, `Double`, `Bool`, `String`, `Raw`, and `Object`.

### `FonDump` (namespace `FON.Types`)

| Member | Description |
|--------|-------------|
| `new FonDump()` | Creates an empty owned dump. |
| `new FonDump(int capacity)` | Same as `new FonDump()`; the capacity is not used. |
| `this[ulong id]` | Reads the record as a view; `KeyNotFoundException` if the id is absent. Assigning is `Set`. |
| `Count` | Number of records. |
| `Ids` | All ids in ascending order (`IReadOnlyList<ulong>`). |
| `Add(ulong id, FonCollection record)` | Adds a record; `InvalidOperationException` if the id exists. |
| `TryAdd(ulong id, FonCollection record)` | Adds a record; returns `false` if the id exists. |
| `Set(ulong id, FonCollection record)` | Stores a record under the id, replacing any existing one. |
| `Replace(ulong id, FonCollection record)` | Replaces an existing record; `KeyNotFoundException` if the id is absent. |
| `Remove(ulong id)` | Removes the record; returns `false` if the id was absent. |
| `RemoveMany(IEnumerable<ulong> ids)` | Removes the records; returns how many were removed. Absent and duplicate ids are ignored. |
| `ContainsId(ulong id)` | Returns whether a record with the id exists. |
| `Get(ulong id)` | Reads the record as a view; `KeyNotFoundException` if the id is absent. |
| `TryGet(ulong id)` | Reads the record as a view; `null` if the id is absent. |
| `Dispose()` | Frees the native memory of the dump. |

Enumeration yields `KeyValuePair<ulong, FonCollection>` in ascending id order.

### Module functions (`Fon`, namespace `FON.Core`)

| Function | Description |
|----------|-------------|
| `Fon.SerializeToString(FonCollection)` | Serializes one record to a line, without a trailing newline. |
| `Fon.SerializeToString(FonDump)` | Serializes a dump, one line per record. |
| `Fon.Deserialize(string)` / `Fon.Deserialize(ReadOnlySpan<byte>)` | Parses multi-line UTF-8 text into a new `FonDump`. |
| `Fon.DeserializeCollection(string)` / `Fon.DeserializeCollection(ReadOnlySpan<byte>)` | Parses one line into a new owned `FonCollection`. |
| `Fon.SerializeToFileAutoAsync(dump, FileInfo, int? maxDegreeOfParallelism = null)` | Writes a dump to a file. Recommended. |
| `Fon.SerializeToFileAuto(dump, FileInfo, int? maxDegreeOfParallelism = null)` | Synchronous version of the above. |
| `Fon.SerializeToFileAsync`, `Fon.SerializeToFilePipelineAsync`, `Fon.SerializeToFileChunkedAsync(dump, FileInfo, int chunkSize = 1000, …)` | Same as `SerializeToFileAutoAsync`; kept for compatibility. |
| `Fon.DeserializeFromFileAutoAsync(FileInfo, int? maxDegreeOfParallelism = null)` | Reads a file into a new `FonDump`. Recommended. |
| `Fon.DeserializeFromFileAsync`, `Fon.DeserializeFromFileChunkedAsync(FileInfo, int chunkSize = 10000, …)` | Same as `DeserializeFromFileAutoAsync`; kept for compatibility. |
| `Fon.DeserializeRawUnpack` | `bool`. When `true`, `RawData` blobs are decoded while parsing. Default `false`. |
| `Fon.MaxDepth` | `int`. Maximum nesting of `{}` and `[]`. Must be at least 1; default `64`. |

### Exceptions

| Exception | Raised when |
|-----------|-------------|
| `ArgumentException` | A key contains `=`, `"`, `{`, `}`, `[`, `]`, CR or LF; a view or a repeated collection is put into a container. |
| `KeyNotFoundException` | A key or id is absent on read, or on `Replace`. |
| `InvalidCastException` | `Get<T>` finds a value of another type. |
| `InvalidOperationException` | `Add` finds an existing key or id. |
| `FonStaleViewException` | A view outlived the data it refers to. Derives from `FonNativeException`. |
| `ObjectDisposedException` | An owned collection or dump is used after `Dispose()`, after it was moved into a container, or through a view of a disposed owner. |
| `FonNativeException` | Parse, write, and file errors. The `Code` property holds the result code, for example `1` for a missing file. |

## Platform support

The `FastObjectNotation` package embeds binaries for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, and `osx-arm64` under `runtimes/{rid}/native/`. NuGet copies the one matching your project's runtime identifier at restore and publish time, so nothing else needs configuring.

If the library cannot be found or loaded at runtime, calls into `FonDump`, `FonCollection`, and `Fon` throw `DllNotFoundException`. This almost always means the project was published for a runtime identifier the package does not cover, or, in Docker or Alpine images, that the app was published as `linux-x64` instead of `linux-musl-x64`.

## Build and test

```bash
git clone --recurse-submodules https://github.com/FastObjectNotation/FON.net.git
cd FON.net

# Build the native library
cargo build --release --manifest-path FON.Native/Cargo.toml

# Build and run the tests
dotnet test FON.slnx -c Release
```

The `FON` project copies `FON.Native/target/release/fon_native.dll` (or `libfon_native.so` / `libfon_native.dylib` on other platforms) next to the assembly, so build the library before the .NET projects.
