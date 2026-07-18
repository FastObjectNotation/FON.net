# FON — Fast Object Notation

FON is a fast, human-readable serialization format — a compact alternative to JSON for record-style data. Each line in a `.fon` file is one self-describing record of typed `key=type:value` pairs. This library provides native-speed serialization and deserialization for .NET, backed by a native core that parallelizes across CPU cores.

## Features

- **High performance** — parallel serialization/deserialization across CPU cores, ~2 GB/s throughput
- **Simple, human-readable format** — typed `key=type:value` pairs, one record per line
- **Cross-platform** — Windows, Linux, macOS (x64 and ARM64)
- **Deterministic memory management** — `FonDump` and `FonCollection` are `IDisposable` and report their native memory footprint to the GC, so they're reclaimed promptly even without an explicit `Dispose`

## Installation

```bash
dotnet add package FastObjectNotation
```

One package. It ships with native binaries for every supported platform (Windows, Linux, Linux musl, macOS — x64 and ARM64); NuGet picks the right one for your project's RID at restore time.

## Quick Start

```csharp
using FON.Core;
using FON.Types;

// Create a collection
using var collection = new FonCollection();
collection.Add("id", 42);
collection.Add("name", "Test Item");
collection.Add("price", 99.99);
collection.Add("active", true);
collection.Add("tags", new List<string> { "sale", "featured" });

// Serialize to string
var text = Fon.SerializeToString(collection);
// Result: id=i:42,name=s:"Test Item",price=d:99.99,active=b:1,tags=s:["sale","featured"]

// Create a dump (multiple records)
using var dump = new FonDump();
dump.TryAdd(0, collection);
dump.TryAdd(1, new FonCollection { { "id", 1 }, { "text", "Hello" } });
dump.TryAdd(2, new FonCollection { { "id", 2 }, { "text", "World" } });

// Serialize to file (auto-selects the best method)
await Fon.SerializeToFileAutoAsync(dump, new FileInfo("data.fon"));

// Deserialize from file
using var loaded = await Fon.DeserializeFromFileAutoAsync(new FileInfo("data.fon"));

// Access data
foreach (var (key, record) in loaded) {
    var id = record.Get<int>("id");
    Console.WriteLine($"Record {key}: id={id}");
}
```

You can also parse and serialize in memory, without touching the filesystem:

```csharp
using var dump = Fon.Deserialize(text);
using var record = Fon.DeserializeCollection("id=i:1,name=s:\"Bob\"");
```

### Disposal

`FonDump` and `FonCollection` wrap a native handle and implement `IDisposable`. Wrap them in `using` so the native memory is freed deterministically:

```csharp
using var dump = await Fon.DeserializeFromFileAutoAsync(file);
```

If you forget, the handle's critical finalizer still frees the native memory eventually, and the GC is told about the native memory via `GC.AddMemoryPressure` so it collects promptly rather than waiting on managed-heap pressure alone — but an explicit `using` is always preferable for predictable cleanup.

A `FonCollection` obtained from another one — via `Get<FonCollection>(key)`, a `List<FonCollection>` element, or dump enumeration — is a *borrowed view* into its parent's native memory. Don't use it after the parent is disposed. Nesting a `FonCollection` into another (`Add(key, childCollection)`, or into a `FonDump`) transfers ownership: the child becomes part of the parent's tree and disposing it separately afterward is a safe no-op.

The native store is append-only: overwriting an existing key/id or calling `Remove` throws `NotSupportedException`.

## Supported Types

| Type | Code | Example |
|------|------|---------|
| `byte` | `e` | `count=e:255` |
| `short` | `t` | `year=t:2024` |
| `int` | `i` | `id=i:42` |
| `uint` | `u` | `flags=u:12345` |
| `long` | `l` | `timestamp=l:1234567890` |
| `ulong` | `g` | `bignum=g:18446744073709551615` |
| `float` | `f` | `ratio=f:3.14` |
| `double` | `d` | `precise=d:3.141592653589793` |
| `string` | `s` | `name=s:"Hello"` |
| `bool` | `b` | `active=b:1` |
| `RawData` | `r` | `data=r:"nm=QNzv..."` |
| `FonCollection` | `o` | `user=o:{id=i:1,name=s:"Bob"}` |

All primitive types support arrays — numeric (`values=i:[1,2,3,4,5]`), `string` (`tags=s:["sale","featured"]`), and `bool` (`flags=b:[1,0]`) — and nested objects support arrays of objects (`items=o:[{id=i:1},{id=i:2}]`). Every array form round-trips through both the in-memory API and the text format.

## Format Specification

FON uses a simple, human-readable format:

```
key=type:value,key2=type2:value2
```

Each line in a `.fon` file represents one record. Records are indexed by line number (0-based).

### Examples

```
# Simple values
name=s:"John",age=i:30,balance=d:1234.56

# Arrays
scores=i:[95,87,92,88]

# Binary data (Z85 encoded)
image=r:"nm=QNzv..."
```

```
# Nested objects
user=o:{id=i:42,name=s:"Bob",addr=o:{city=s:"NY",zip=i:10001}}

# Arrays of objects
items=o:[{id=i:1,qty=i:5},{id=i:2,qty=i:3}]

# Empty object and empty array of objects
empty=o:{},none=o:[]
```

## API Reference

### Serialization

```csharp
// Serialize a single record to a string
string text = Fon.SerializeToString(collection);

// Serialize an entire dump to a string
string text = Fon.SerializeToString(dump);

// Auto-select best method based on data size (recommended)
await Fon.SerializeToFileAutoAsync(dump, file);

// Pipeline / Chunked / plain variants also exist for source compatibility;
// all route through the same native file writer.
await Fon.SerializeToFilePipelineAsync(dump, file);
await Fon.SerializeToFileChunkedAsync(dump, file, chunkSize: 1000);
await Fon.SerializeToFileAsync(dump, file);
```

### Deserialization

```csharp
// In memory
FonDump dump = Fon.Deserialize(text);
FonDump dump = Fon.Deserialize(utf8Bytes);
FonCollection record = Fon.DeserializeCollection(text);

// Auto-select best method based on file size (recommended)
var dump = await Fon.DeserializeFromFileAutoAsync(file);

// Async variants also exist for source compatibility; all route through
// the same native file parser.
var dump = await Fon.DeserializeFromFileAsync(file);
var dump = await Fon.DeserializeFromFileChunkedAsync(file, chunkSize: 10000);
```

### Configuration

```csharp
// Automatically decompress RawData during deserialization
Fon.DeserializeRawUnpack = true;

// Maximum bracket nesting depth (default: 64)
Fon.MaxDepth = 64;
```

## Platform Support

The `FastObjectNotation` package embeds native binaries for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, and `osx-arm64` under `runtimes/{rid}/native/`. NuGet copies the one matching your project's runtime identifier at restore/publish time — nothing else to configure.

If the native library can't be found or loaded at runtime, calls into `FonDump`/`FonCollection`/`Fon` throw a `DllNotFoundException` — this almost always means the project was published for a RID this package doesn't cover, or (in Docker/Alpine images) that the app was published as `linux-x64` instead of `linux-musl-x64`.

## Performance Tips

1. **Use Auto methods** — `SerializeToFileAutoAsync` and `DeserializeFromFileAutoAsync` let the native core pick the best strategy for the data size.
2. **Adjust parallelism** — pass `maxDegreeOfParallelism` to control how many threads the native core uses:
   ```csharp
   await Fon.SerializeToFileAutoAsync(dump, file, maxDegreeOfParallelism: 4);
   ```
3. **Use RawData for binary** — more efficient than encoding large binary blobs as strings.
4. **Dispose promptly** — `using` a `FonDump`/`FonCollection` frees native memory immediately instead of waiting for a GC pass.

## Building from Source

```bash
git clone --recurse-submodules https://github.com/FastObjectNotation/FON.net.git
cd FON.net

# Build the native core
cargo build --release --manifest-path FON.Native/Cargo.toml

# Build the library
dotnet build

# Run tests
dotnet test
```
