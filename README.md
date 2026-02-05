# FON - Fast Object Notation

High-performance serialization library for .NET with parallel processing and optional native acceleration. A fast alternative to JSON for .NET applications.

## Features

- **High Performance** - Parallel serialization/deserialization with SIMD-optimized parsing
- **Zero-allocation parsing** - Uses `Span<T>`, `stackalloc`, and `ArrayPool`
- **Native Acceleration** - Optional C++ native library for even faster processing
- **Cross-platform** - Windows, Linux, macOS (x64 and ARM64)
- **Simple Format** - Human-readable key-value format

## Installation

### Basic (Managed Only)

```bash
dotnet add package FON
```

### With Native Acceleration (Recommended for large datasets)

```bash
dotnet add package FON
dotnet add package FON.Native
```

The `FON.Native` package automatically includes native binaries for all supported platforms.

## Quick Start

```csharp
using FON.Core;
using FON.Types;

// Create a collection
var collection = new FonCollection();
collection.Add("id", 42);
collection.Add("name", "Test Item");
collection.Add("price", 99.99);
collection.Add("active", true);
collection.Add("tags", new List<string> { "sale", "featured" });

// Serialize to string
var text = Fon.SerializeToString(collection);
// Result: id=i:42,name=s:"Test Item",price=d:99.99,active=b:1,tags=s:["sale","featured"]

// Create a dump (multiple records)
var dump = new FonDump();
dump.TryAdd(0, collection);
dump.TryAdd(1, new FonCollection { { "id", 1 }, { "text", "Hello" } });
dump.TryAdd(2, new FonCollection { { "id", 2 }, { "text", "World" } });

// Serialize to file (auto-selects best method)
await Fon.SerializeToFileAutoAsync(dump, new FileInfo("data.fon"));

// Deserialize from file
var loaded = await Fon.DeserializeFromFileAutoAsync(new FileInfo("data.fon"));

// Access data
foreach (var (key, record) in loaded.FonObjects) {
    var id = record.Get<int>("id");
    Console.WriteLine($"Record {key}: id={id}");
}
```

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
| `RawData` | `r` | `data=r:"base64..."` |

All types support arrays: `values=i:[1,2,3,4,5]`

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
scores=i:[95,87,92,88],tags=s:["admin","user"]

# Binary data (base64 encoded)
image=r:"iVBORw0KGgoAAAANSUhEUg..."
```

## API Reference

### Serialization

```csharp
// Serialize collection to string
string text = Fon.SerializeToString(collection);

// Auto-select best method based on data size (recommended)
await Fon.SerializeToFileAutoAsync(dump, file);

// Pipeline method - best for small datasets (<2000 records)
await Fon.SerializeToFilePipelineAsync(dump, file);

// Chunked method - best for large datasets, lower memory pressure
await Fon.SerializeToFileChunkedAsync(dump, file, chunkSize: 1000);

// Basic parallel serialization
await Fon.SerializeToFileAsync(dump, file);
```

### Deserialization

```csharp
// Auto-select best method based on file size (recommended)
var dump = await Fon.DeserializeFromFileAutoAsync(file);

// Load entire file, parse in parallel - best for files <500MB
var dump = await Fon.DeserializeFromFileAsync(file);

// Stream file in chunks - best for very large files
var dump = await Fon.DeserializeFromFileChunkedAsync(file, chunkSize: 10000);
```

### Configuration

```csharp
// Automatically decompress RawData during deserialization
Fon.DeserializeRawUnpack = true;

// Adjust threshold for auto method selection (default: 2000)
Fon.ParallelMethodThreshold = 2000;
```

## Native Acceleration

FON includes optional native acceleration via a C++ library for maximum performance.

### Package Structure

| Package | Description |
|---------|-------------|
| `FON` | Core library (managed, works everywhere) |
| `FON.Native` | Meta-package that includes all native binaries |
| `FON.Native.Runtime` | P/Invoke bindings (included by FON.Native) |
| `FON.Native.win-x64` | Windows x64 native binary |
| `FON.Native.win-arm64` | Windows ARM64 native binary |
| `FON.Native.linux-x64` | Linux x64 native binary |
| `FON.Native.linux-arm64` | Linux ARM64 native binary |
| `FON.Native.linux-musl-x64` | Alpine Linux x64 native binary |
| `FON.Native.linux-musl-arm64` | Alpine Linux ARM64 native binary |
| `FON.Native.osx-x64` | macOS x64 native binary |
| `FON.Native.osx-arm64` | macOS ARM64 (Apple Silicon) native binary |

### Checking Native Availability

```csharp
using FON.Acceleration;

if (FonAccelerator.IsAvailable) {
    Console.WriteLine($"Native acceleration enabled, version: {FonAccelerator.Version}");
} else {
    Console.WriteLine("Using managed implementation");
}
```

### When to Use Native Acceleration

| Scenario | Recommendation |
|----------|----------------|
| Small datasets (<1000 records) | Managed is sufficient |
| Large datasets (>10000 records) | Native recommended |
| Memory-constrained environments | Native (lower allocations) |
| Docker/Alpine containers | Use `linux-musl-*` packages |
| Cross-platform deployment | Install `FON.Native` for all platforms |
| Single platform deployment | Install specific `FON.Native.{rid}` package |

### Platform-Specific Installation

If you only need native support for specific platforms (smaller deployment):

```bash
# Windows x64 only
dotnet add package FON.Native.Runtime
dotnet add package FON.Native.win-x64

# Linux x64 only
dotnet add package FON.Native.Runtime
dotnet add package FON.Native.linux-x64

# Multiple specific platforms
dotnet add package FON.Native.Runtime
dotnet add package FON.Native.win-x64
dotnet add package FON.Native.linux-x64
```

## Performance Tips

1. **Use Auto methods** - `SerializeToFileAutoAsync` and `DeserializeFromFileAutoAsync` automatically select the best strategy

2. **Adjust parallelism** - Pass `maxDegreeOfParallelism` parameter to control CPU usage:
   ```csharp
   await Fon.SerializeToFileAutoAsync(dump, file, maxDegreeOfParallelism: 4);
   ```

3. **Reuse FonDump** - Clear and reuse instead of creating new instances

4. **Use RawData for binary** - More efficient than base64 strings for large binary data

## Building from Source

```bash
git clone https://github.com/FastObjectNotation/FON.net.git
cd FON.net

# Build managed library
dotnet build

# Build native library (requires CMake)
cmake -B build -S FON.Native -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release

# Run tests
dotnet test
```
