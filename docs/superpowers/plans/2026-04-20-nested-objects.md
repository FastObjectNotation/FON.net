# Nested Objects and Arrays of Objects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a single new value type `o` (object) that holds a nested `FonCollection`, and reuse the existing array machinery to support arrays of objects, in both the managed C# implementation and the native C++ implementation.

**Architecture:** `FonCollection` becomes another supported type. Managed serialization adds one branch (`'o'`) in `SerializeBaseObject`; deserialization adds one branch in `DeserializeValueOptimized` plus a `FindClosingBrace` helper that mirrors `FindClosingBracket`. The C++ side adds two `std::variant` arms (`std::shared_ptr<FonCollection>` and `std::vector<std::shared_ptr<FonCollection>>` — `shared_ptr` is required because `FonCollection` is recursive through `FonValue`) and matching parse/serialize branches. Both implementations gain a configurable `MaxDepth` (default 64) and depth-tracked recursive parsing. New C ABI exports + P/Invoke bindings expose the nested API to managed code.

**Tech Stack:** C# (.NET 10, preview LangVersion), C++20, CMake, xUnit 2.9.3, Span/stackalloc/ArrayPool for zero-allocation parsing, std::variant for native value model, P/Invoke via [DllImport].

**Reference spec:** `docs/superpowers/specs/2026-04-20-nested-objects-design.md`

---

## File structure

### Managed (C#)

| File | Responsibility | Action |
|------|----------------|--------|
| `FON/Core/Fon.cs` | Type-char registry and `MaxDepth` config | Modify |
| `FON/Core/FonSerialize.cs` | Serialize `FonCollection`/`FonDump` to text/file | Modify (extract `SerializeBody`, add `'o'` case) |
| `FON/Core/FonDeserialize.cs` | Parse FON text/file into `FonCollection`/`FonDump` | Modify (extract `ParseCollectionBody` with depth, add `FindClosingBrace`, `DeserializeObjectOptimized`) |
| `FON/Types/FonCollection.cs` | The nested type itself + transitive Dispose | Modify (Dispose walks lists, XML doc on cycles) |
| `FON.Tests/FON.Test/NestedObjectTests.cs` | Comprehensive managed tests | Create |

### Native (C++)

| File | Responsibility | Action |
|------|----------------|--------|
| `FON.Native/include/fon_types.hpp` | `FonValue` variant, type chars, `FonCollection` | Modify (variant arms, `TYPE_OBJECT`) |
| `FON.Native/include/fon.hpp` | Serializer/parser, `Fon::max_depth` | Modify (refactor `serialize_body`/`parse_collection_body`, add `find_closing_brace`/`parse_object`/`parse_object_array`) |
| `FON.Native/include/fon_export.h` | C ABI declarations | Modify (5 new exports) |
| `FON.Native/src/fon_export.cpp` | C ABI implementations | Modify (5 new function bodies) |
| `FON.Native.Runtime/NativeBindings.cs` | P/Invoke declarations | Modify (5 new `[DllImport]`) |
| `FON.Tests/FON.Native.Test/NativeNestedTests.cs` | Native nested tests via P/Invoke | Create |
| `FON.Tests/FON.Native.Test/NativeFileTests.cs` | Cross-impl roundtrip tests | Modify (extend) |

### Documentation

| File | Action |
|------|--------|
| `README.md` | Modify (add `o` row, examples, `MaxDepth` mention) |

---

## Conventions for every task

- **K&R braces, no `_` prefix on identifiers** (matches existing codebase).
- **C# property names use PascalCase** for public statics like `Fon.MaxDepth` (matches `Fon.DeserializeRawUnpack`, `Fon.ParallelMethodThreshold`).
- **Test command** for managed: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~<TestClass>.<TestMethod>"`.
- **Test command** for native: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~<TestClass>.<TestMethod>"`.
- **Native build command** (run before any native test): `cmake -B build -S FON.Native -DCMAKE_BUILD_TYPE=Release && cmake --build build --config Release`.
- **Commit style:** match repo history (`Add ...`, `Fix ...`, `Bump ...`); no AI attribution per repo convention.
- **Each task ends with a single commit.** Stage only files listed in the Files block of the task.

---

# Phase 1 — Managed (C#)

## Task 1: Add `Fon.MaxDepth` configuration

**Files:**
- Modify: `FON/Core/Fon.cs`
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `FON.Tests/FON.Test/NestedObjectTests.cs`:

```csharp
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
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.MaxDepth"`

Expected: FAIL — `Fon.MaxDepth` does not exist (compile error).

- [ ] **Step 3: Add `MaxDepth` property in `FON/Core/Fon.cs`**

Insert after the existing `DeserializeRawUnpack` property:

```csharp
private static int maxDepth = 64;

/// <summary>
/// Maximum number of nested brackets ({} or []) the parser will accept
/// before throwing FormatException. Must be at least 1. Default: 64.
/// </summary>
public static int MaxDepth {
    get => maxDepth;
    set {
        if (value < 1) {
            throw new ArgumentOutOfRangeException(nameof(value), "MaxDepth must be at least 1");
        }
        maxDepth = value;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.MaxDepth"`

Expected: 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add FON/Core/Fon.cs FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Add Fon.MaxDepth configuration with validation"
```

---

## Task 2: Register `FonCollection -> 'o'` in `SupportTypes`

**Files:**
- Modify: `FON/Core/Fon.cs`
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `NestedObjectTests.cs`:

```csharp
[Fact]
public void SupportTypes_RegistersFonCollectionAsObject() {
    Assert.True(Fon.SupportTypes.ContainsKey(typeof(FonCollection)));
    Assert.Equal('o', Fon.SupportTypes[typeof(FonCollection)]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.SupportTypes_RegistersFonCollectionAsObject"`

Expected: FAIL — `KeyNotFoundException`.

- [ ] **Step 3: Add the entry to `SupportTypes` in `FON/Core/Fon.cs`**

Edit the dictionary literal (insert as the last entry to keep diffs minimal):

```csharp
public static readonly Dictionary<Type, char> SupportTypes = new() {
    { typeof(byte),         'e' },
    { typeof(short),        't' },
    { typeof(int),          'i' },
    { typeof(uint),         'u' },
    { typeof(long),         'l' },
    { typeof(ulong),        'g' },
    { typeof(float),        'f' },
    { typeof(double),       'd' },
    { typeof(string),       's' },
    { typeof(bool),         'b' },
    { typeof(RawData),      'r' },
    { typeof(FonCollection),'o' }
};
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.SupportTypes_RegistersFonCollectionAsObject"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FON/Core/Fon.cs FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Register FonCollection as 'o' type in SupportTypes"
```

---

## Task 3: Implement nested object serialization

**Files:**
- Modify: `FON/Core/FonSerialize.cs`
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs`

This task refactors `SerializeToString` to extract `SerializeBody` (so it can be reused for nested bodies) and adds the `'o'` case to `SerializeBaseObject`.

- [ ] **Step 1: Write the failing test**

Append to `NestedObjectTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.Serialize_"`

Expected: FAIL — `InvalidOperationException` (existing serializer falls through to "Unsupported type" because there is no switch arm for `'o'`).

- [ ] **Step 3: Refactor `SerializeToString` to extract `SerializeBody`**

In `FON/Core/FonSerialize.cs`, replace the existing `SerializeToString` with:

```csharp
public static string SerializeToString(FonCollection fonCollection) {
    var sb = new StringBuilder(fonCollection.Count() * 200);
    SerializeBody(sb, fonCollection);
    return sb.ToString();
}


private static void SerializeBody(StringBuilder sb, FonCollection fonCollection) {
    bool isFirst = true;
    foreach (var kvp in fonCollection) {
        if (isFirst) {
            isFirst = false;
        } else {
            sb.Append(',');
        }
        SerializeKeyValue(sb, kvp.Key, kvp.Value);
    }
}
```

- [ ] **Step 4: Add `'o'` case to `SerializeBaseObject`**

In `FON/Core/FonSerialize.cs`, in the `switch (shortType)` block of `SerializeBaseObject`, insert before the `default:` arm:

```csharp
case 'o':
    sb.Append('{');
    SerializeBody(sb, (FonCollection)value);
    sb.Append('}');
break;
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.Serialize_"`

Expected: 3 tests PASS.

- [ ] **Step 6: Run the full managed test suite to confirm no regression from the refactor**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj`

Expected: all existing tests still PASS.

- [ ] **Step 7: Commit**

```bash
git add FON/Core/FonSerialize.cs FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Add nested object serialization (type 'o')"
```

---

## Task 4: Implement nested object deserialization

**Files:**
- Modify: `FON/Core/FonDeserialize.cs`
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs`

This task extracts `ParseCollectionBody(span, depth)` from `DeserializeLineOptimized`, threads `int depth` through every `parse_*` helper, adds `FindClosingBrace`, adds `DeserializeObjectOptimized`, and adds the `'o'` dispatch to `DeserializeValueOptimized`.

- [ ] **Step 1: Write the failing test**

Append to `NestedObjectTests.cs`:

```csharp
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
public void Deserialize_NestedObject_NotFollowedByBrace_Throws() {
    var dump = new FonDump();
    dump.TryAdd(0, new FonCollection { { "id", 1 } });
    var tempFile = new FileInfo(Path.GetTempFileName());
    try {
        File.WriteAllText(tempFile.FullName, "x=o:42\n");
        Assert.ThrowsAsync<FormatException>(() => Fon.DeserializeFromFileAutoAsync(tempFile)).GetAwaiter().GetResult();
    } finally {
        tempFile.Delete();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.Deserialize_"`

Expected: FAIL — `NotSupportedException` from `DeserializeValueOptimized` (no arm for `'o'`).

- [ ] **Step 3: Refactor `DeserializeLineOptimized` to extract `ParseCollectionBody`, threading depth**

In `FON/Core/FonDeserialize.cs`, replace `DeserializeLineOptimized` with:

```csharp
[MethodImpl(MethodImplOptions.AggressiveOptimization)]
private static FonCollection DeserializeLineOptimized(ReadOnlySpan<char> chars) {
    return ParseCollectionBody(chars, 0);
}


[MethodImpl(MethodImplOptions.AggressiveOptimization)]
private static FonCollection ParseCollectionBody(ReadOnlySpan<char> chars, int depth) {
    var fonList = new FonCollection();
    int position = 0;

    while (position < chars.Length) {
        var remaining = chars.Slice(position);

        var eqIndex = remaining.IndexOf('=');
        if (eqIndex < 0) {
            break;
        }

        var key = remaining.Slice(0, eqIndex).ToString();
        position += eqIndex + 1;
        remaining = chars.Slice(position);

        if (remaining.Length < 2 || remaining[1] != ':') {
            throw new FormatException($"Invalid format at position {position}");
        }

        var typeChar = remaining[0];
        var type = GetType(typeChar);
        if (type == null) {
            throw new FormatException($"Unknown type '{typeChar}' at position {position}");
        }

        position += 2;
        remaining = chars.Slice(position);

        object data;
        int consumed;

        if (remaining.Length > 0 && remaining[0] == '[') {
            (data, consumed) = DeserializeArrayOptimized(remaining, type, typeChar, depth + 1);
        } else {
            (data, consumed) = DeserializeValueOptimized(remaining, type, typeChar, depth);
        }

        fonList.Add(key, data);
        position += consumed;

        if (position < chars.Length && chars[position] == ',') {
            position++;
        }
    }

    return fonList;
}
```

- [ ] **Step 4: Update `DeserializeValueOptimized` to take depth and dispatch `'o'`**

Replace the existing `DeserializeValueOptimized` signature and body:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static (object data, int consumed) DeserializeValueOptimized(ReadOnlySpan<char> chars, Type type, char typeChar, int depth) {
    if (typeChar == 'o') {
        if (chars.Length == 0 || chars[0] != '{') {
            throw new FormatException("Object must start with '{'");
        }
        var (obj, consumed) = DeserializeObjectOptimized(chars, depth + 1);
        return (obj, consumed);
    }

    if (typeChar == 's') {
        return DeserializeStringOptimized(chars);
    }

    if (typeChar == 'r') {
        return DeserializeRawOptimized(chars);
    }

    var endIndex = FindValueEnd(chars);
    var valueSpan = chars.Slice(0, endIndex);
    var consumed2 = endIndex;

    if (consumed2 < chars.Length && (chars[consumed2] == ',' || chars[consumed2] == ']')) {
        if (chars[consumed2] == ',') {
            consumed2++;
        }
    }

    object value = typeChar switch {
        'e' => byte.Parse(valueSpan),
        't' => short.Parse(valueSpan),
        'i' => int.Parse(valueSpan),
        'u' => uint.Parse(valueSpan),
        'l' => long.Parse(valueSpan),
        'g' => ulong.Parse(valueSpan),
        'f' => float.Parse(valueSpan, CultureInfo.InvariantCulture),
        'd' => double.Parse(valueSpan, CultureInfo.InvariantCulture),
        'b' => valueSpan[0] != '0',
        _ => throw new NotSupportedException($"Type '{typeChar}' is not supported")
    };

    return (value, consumed2);
}
```

- [ ] **Step 5: Update `DeserializeArrayOptimized` signature to take depth and forward to value parser**

Replace `DeserializeArrayOptimized` with:

```csharp
private static (IList data, int consumed) DeserializeArrayOptimized(ReadOnlySpan<char> chars, Type elementType, char typeChar, int depth) {
    if (chars[0] != '[') {
        throw new FormatException("Array must start with '['");
    }

    var closeIndex = FindClosingBracket(chars);
    var arrayContent = chars.Slice(1, closeIndex - 1);

    var list = CreateTypedList(elementType, typeChar);

    if (arrayContent.Length == 0) {
        var consumed = closeIndex + 1;
        if (consumed < chars.Length && chars[consumed] == ',') {
            consumed++;
        }
        return (list, consumed);
    }

    int position = 0;
    while (position < arrayContent.Length) {
        var remaining = arrayContent.Slice(position);
        var (value, valueConsumed) = DeserializeValueOptimized(remaining, elementType, typeChar, depth);
        list.Add(value);
        position += valueConsumed;
    }

    var totalConsumed = closeIndex + 1;
    if (totalConsumed < chars.Length && chars[totalConsumed] == ',') {
        totalConsumed++;
    }

    return (list, totalConsumed);
}
```

- [ ] **Step 6: Add `FindClosingBrace` and `DeserializeObjectOptimized`**

Insert these helpers after `FindClosingBracket` in `FON/Core/FonDeserialize.cs`:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static int FindClosingBrace(ReadOnlySpan<char> chars) {
    int depth = 0;
    bool inString = false;

    for (int i = 0; i < chars.Length; i++) {
        var c = chars[i];

        if (c == '"' && (i == 0 || chars[i - 1] != '\\')) {
            inString = !inString;
        } else if (!inString) {
            if (c == '{') {
                depth++;
            } else if (c == '}') {
                depth--;
                if (depth == 0) {
                    return i;
                }
            }
        }
    }

    throw new FormatException("Closing brace not found");
}


private static (FonCollection data, int consumed) DeserializeObjectOptimized(ReadOnlySpan<char> chars, int depth) {
    if (chars[0] != '{') {
        throw new FormatException("Object must start with '{'");
    }

    var closeIndex = FindClosingBrace(chars);
    var body = chars.Slice(1, closeIndex - 1);

    var collection = ParseCollectionBody(body, depth);

    var consumed = closeIndex + 1;
    if (consumed < chars.Length && chars[consumed] == ',') {
        consumed++;
    }

    return (collection, consumed);
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.Deserialize_"`

Expected: 4 tests PASS.

- [ ] **Step 8: Run the full managed test suite to confirm no regression**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj`

Expected: all existing tests still PASS.

- [ ] **Step 9: Commit**

```bash
git add FON/Core/FonDeserialize.cs FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Add nested object deserialization with depth tracking"
```

---

## Task 5: Implement arrays of nested objects

**Files:**
- Modify: `FON/Core/FonDeserialize.cs`
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs`

The serialization side already handles `List<FonCollection>` because the existing `IList` branch in `SerializeObject` looks at `value.GetType().GenericTypeArguments[0]` and routes to `SerializeBaseObject(sb, 'o', item)`, which Task 3 already implemented. Only `CreateTypedList` needs an entry for `'o'`.

- [ ] **Step 1: Write the failing test**

Append to `NestedObjectTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.RoundTrip_"`

Expected: FAIL — `CreateTypedList` falls into the default `Activator.CreateInstance` branch and produces a list, but `DeserializeValueOptimized` for `'o'` will be called inside the array body — that already works from Task 4 — so the failure is more subtle. Most likely the failure is that the deserialized list has type `List<object>` (from Activator over `FonCollection`), causing the cast in `Get<List<FonCollection>>("items")` to throw.

Actually re-check: in `ParseCollectionBody` we call `GetType(typeChar)` which returns `typeof(FonCollection)` (registered in Task 2), so `Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(FonCollection)))` would produce a `List<FonCollection>`. The test may actually pass via the fallback. Run it first to confirm — the explicit branch in step 3 is still preferred for symmetry with the other types and to avoid reflection in the hot path.

- [ ] **Step 3: Add explicit `'o'` branch to `CreateTypedList`**

In `FON/Core/FonDeserialize.cs`, edit `CreateTypedList`:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static IList CreateTypedList(Type elementType, char typeChar) {
    return typeChar switch {
        'e' => new List<byte>(),
        't' => new List<short>(),
        'i' => new List<int>(),
        'u' => new List<uint>(),
        'l' => new List<long>(),
        'g' => new List<ulong>(),
        'f' => new List<float>(),
        'd' => new List<double>(),
        'b' => new List<bool>(),
        's' => new List<string>(),
        'r' => new List<RawData>(),
        'o' => new List<FonCollection>(),
        _ => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.RoundTrip_"`

Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add FON/Core/FonDeserialize.cs FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Add arrays of nested objects (List<FonCollection>)"
```

---

## Task 6: Enforce nesting depth limit

**Files:**
- Modify: `FON/Core/FonDeserialize.cs`
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs`

Task 4 already threads `depth` through every helper. This task adds the actual `depth > Fon.MaxDepth` check at the top of `DeserializeObjectOptimized` and `DeserializeArrayOptimized`.

- [ ] **Step 1: Write the failing test**

Append to `NestedObjectTests.cs`:

```csharp
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
public void Deserialize_AboveMaxDepth_Throws() {
    var original = Fon.MaxDepth;
    try {
        Fon.MaxDepth = 5;
        var nested = BuildNested(6);
        var dump = new FonDump();
        dump.TryAdd(0, nested);

        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            Fon.SerializeToFileAuto(dump, tempFile);
            Assert.ThrowsAsync<FormatException>(
                () => Fon.DeserializeFromFileAutoAsync(tempFile)
            ).GetAwaiter().GetResult();
        } finally {
            tempFile.Delete();
        }
    } finally {
        Fon.MaxDepth = original;
    }
}


[Fact]
public void Deserialize_DeepArray_AboveMaxDepth_Throws() {
    var original = Fon.MaxDepth;
    try {
        Fon.MaxDepth = 2;
        // Construct depth-3 array nesting via raw text: outer array contains object containing array.
        var line = "a=i:[1,2,3]";
        var tempFile = new FileInfo(Path.GetTempFileName());
        try {
            File.WriteAllText(tempFile.FullName, line + "\n");
            // depth-1 array under MaxDepth=2 should pass
            var loaded = Fon.DeserializeFromFileAutoAsync(tempFile).GetAwaiter().GetResult();
            Assert.Equal(1, loaded.Count);

            // Now a deeper construction: array of objects each containing an array (depth = 3)
            var deep = "items=o:[{vals=i:[1,2]}]";
            File.WriteAllText(tempFile.FullName, deep + "\n");
            Assert.ThrowsAsync<FormatException>(
                () => Fon.DeserializeFromFileAutoAsync(tempFile)
            ).GetAwaiter().GetResult();
        } finally {
            tempFile.Delete();
        }
    } finally {
        Fon.MaxDepth = original;
    }
}
```

- [ ] **Step 2: Run tests to verify the depth-violation tests fail**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.Deserialize_AboveMaxDepth_Throws|FullyQualifiedName~NestedObjectTests.Deserialize_DeepArray_AboveMaxDepth_Throws"`

Expected: FAIL — no depth check exists yet, so deep nesting deserializes silently.

- [ ] **Step 3: Add depth check to `DeserializeObjectOptimized`**

In `FON/Core/FonDeserialize.cs`, add the check at the very top of the method:

```csharp
private static (FonCollection data, int consumed) DeserializeObjectOptimized(ReadOnlySpan<char> chars, int depth) {
    if (depth > Fon.MaxDepth) {
        throw new FormatException($"Maximum nesting depth exceeded ({Fon.MaxDepth})");
    }

    if (chars[0] != '{') {
        throw new FormatException("Object must start with '{'");
    }

    var closeIndex = FindClosingBrace(chars);
    var body = chars.Slice(1, closeIndex - 1);

    var collection = ParseCollectionBody(body, depth);

    var consumed = closeIndex + 1;
    if (consumed < chars.Length && chars[consumed] == ',') {
        consumed++;
    }

    return (collection, consumed);
}
```

- [ ] **Step 4: Add depth check to `DeserializeArrayOptimized`**

Add the same check at the very top of `DeserializeArrayOptimized`:

```csharp
private static (IList data, int consumed) DeserializeArrayOptimized(ReadOnlySpan<char> chars, Type elementType, char typeChar, int depth) {
    if (depth > Fon.MaxDepth) {
        throw new FormatException($"Maximum nesting depth exceeded ({Fon.MaxDepth})");
    }

    if (chars[0] != '[') {
        throw new FormatException("Array must start with '['");
    }
    // ... rest unchanged
}
```

- [ ] **Step 5: Run all depth tests to verify they pass**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.Deserialize_AtExactlyMaxDepth_Succeeds|FullyQualifiedName~NestedObjectTests.Deserialize_AboveMaxDepth_Throws|FullyQualifiedName~NestedObjectTests.Deserialize_DeepArray_AboveMaxDepth_Throws"`

Expected: all 3 PASS.

- [ ] **Step 6: Run the full managed test suite to confirm no regression**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj`

Expected: all existing tests still PASS (default `MaxDepth = 64` permits everything used in older tests).

- [ ] **Step 7: Commit**

```bash
git add FON/Core/FonDeserialize.cs FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Enforce MaxDepth in object and array parsers"
```

---

## Task 7: Transitive Dispose for nested collections

**Files:**
- Modify: `FON/Types/FonCollection.cs`
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs`

The current `Dispose` only walks top-level `IDisposable` values. With nested collections this misses lists of `RawData` and lists of `FonCollection`. Walk lists, dispose each element if disposable.

- [ ] **Step 1: Write the failing test**

Append to `NestedObjectTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.Dispose_"`

Expected: the first test PASSES already (inner FonCollection is itself IDisposable, current `Dispose` walks `OfType<IDisposable>`). The other two FAIL because lists are not walked.

- [ ] **Step 3: Extend `Dispose` to walk `IList` values transitively**

In `FON/Types/FonCollection.cs`, replace `Dispose`:

```csharp
public void Dispose() {
    foreach (var value in Collection.Values) {
        if (value is IDisposable disposable) {
            disposable.Dispose();
        } else if (value is System.Collections.IList list) {
            foreach (var item in list) {
                if (item is IDisposable d) {
                    d.Dispose();
                }
            }
        }
    }
    Collection.Clear();
}
```

- [ ] **Step 4: Add XML doc warning about cycles to the class**

In `FON/Types/FonCollection.cs`, replace the `public class FonCollection ...` line with this doc-commented version (preserve the existing class signature):

```csharp
/// <summary>
/// Key-value collection that can hold primitives, strings, RawData, and other
/// FonCollections (nested objects) or lists of any supported type.
/// </summary>
/// <remarks>
/// Cycles are the caller's responsibility: placing a FonCollection inside
/// itself (directly or transitively through nested values or lists) is not
/// detected. Serialization will recurse until the call stack overflows.
/// </remarks>
public class FonCollection : IEnumerable<KeyValuePair<string, object>>, IDisposable {
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.Dispose_"`

Expected: 3 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add FON/Types/FonCollection.cs FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Walk nested IList values in FonCollection.Dispose"
```

---

## Task 8: String metacharacters and escapes inside nested objects

**Files:**
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs`

The parser already handles `inString` for `]` (via `FindClosingBracket`) and Task 4 added the same for `}` (via `FindClosingBrace`). This task pins that behavior with explicit tests.

- [ ] **Step 1: Write the failing test**

Append to `NestedObjectTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they pass already**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.RoundTrip_NestedString_"`

Expected: all PASS — the existing `inString` handling in `FindClosingBrace` and `FindClosingBracket` (and string escape logic) makes these tests pass. If any fail, debug `FindClosingBrace`'s `inString` toggle.

- [ ] **Step 3: Commit**

```bash
git add FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Pin nested-string metacharacter handling with regression tests"
```

---

## Task 9: I/O strategy parity and parallelism

**Files:**
- Test: `FON.Tests/FON.Test/NestedObjectTests.cs`

Verify nested data roundtrips identically across all four serialize methods and all three deserialize methods, and survives parallel execution at scale.

- [ ] **Step 1: Write the failing test**

Append to `NestedObjectTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj --filter "FullyQualifiedName~NestedObjectTests.RoundTrip_All|FullyQualifiedName~NestedObjectTests.RoundTrip_LargeNestedDump"`

Expected: 3 tests PASS. If any fail, the failure points to a serialize/deserialize strategy that handles the body differently from `SerializeToString`/`DeserializeLineOptimized` (in which case the strategy needs investigation — they should all share the line-level helpers).

- [ ] **Step 3: Run the full managed test suite as the final phase-1 gate**

Run: `dotnet test FON.Tests/FON.Test/FON.Test.csproj`

Expected: every test PASS.

- [ ] **Step 4: Commit**

```bash
git add FON.Tests/FON.Test/NestedObjectTests.cs
git commit -m "Verify I/O strategy parity and parallelism for nested data"
```

---

# Phase 2 — Native (C++)

> **Build before any native test:** `cmake -B build -S FON.Native -DCMAKE_BUILD_TYPE=Release && cmake --build build --config Release`

## Task 10: Add native types and configuration

**Files:**
- Modify: `FON.Native/include/fon_types.hpp`
- Modify: `FON.Native/include/fon.hpp`

This task only adds types — no parser/serializer changes yet, so there is no end-to-end behavior to test from C#. The validation is that the project still builds.

- [ ] **Step 1: Add `TYPE_OBJECT` and the two new variant arms in `fon_types.hpp`**

In `FON.Native/include/fon_types.hpp`, replace the `using FonValue = std::variant<...>` block with:

```cpp
using FonValue = std::variant<
    uint8_t,                                          // 'e' - byte
    int16_t,                                          // 't' - short
    int32_t,                                          // 'i' - int
    uint32_t,                                         // 'u' - uint
    int64_t,                                          // 'l' - long
    uint64_t,                                         // 'g' - ulong
    float,                                            // 'f' - float
    double,                                           // 'd' - double
    bool,                                             // 'b' - bool
    std::string,                                      // 's' - string
    std::shared_ptr<RawData>,                         // 'r' - raw data
    std::shared_ptr<FonCollection>,                   // 'o' - nested object
    std::vector<uint8_t>,
    std::vector<int16_t>,
    std::vector<int32_t>,
    std::vector<uint32_t>,
    std::vector<int64_t>,
    std::vector<uint64_t>,
    std::vector<float>,
    std::vector<double>,
    std::vector<bool>,
    std::vector<std::string>,
    std::vector<std::shared_ptr<FonCollection>>       // 'o' - array of objects
>;
```

Then add the type code constant just below the existing `TYPE_RAW`:

```cpp
constexpr char TYPE_OBJECT = 'o';
```

- [ ] **Step 2: Add `Fon::max_depth` configuration in `fon.hpp`**

In `FON.Native/include/fon.hpp`, inside the `class Fon { public:` section, immediately after `static inline int parallel_threshold = 2000;`, add:

```cpp
static inline int max_depth = 64;
```

- [ ] **Step 3: Build to verify the project still compiles**

Run: `cmake -B build -S FON.Native -DCMAKE_BUILD_TYPE=Release && cmake --build build --config Release`

Expected: build SUCCESS. (The existing `serialize_value` / `get_type_char` / `parse_value` use `if constexpr` chains that gracefully ignore unknown variant arms — the new arms compile-fall through to `else return '?';` in `get_type_char` until we add explicit branches in the next tasks.)

- [ ] **Step 4: Commit**

```bash
git add FON.Native/include/fon_types.hpp FON.Native/include/fon.hpp
git commit -m "Add nested-object variant arms and Fon::max_depth in native"
```

---

## Task 11: Native serialization for nested objects

**Files:**
- Modify: `FON.Native/include/fon.hpp`
- Modify: `FON.Native/include/fon_export.h`
- Modify: `FON.Native/src/fon_export.cpp`
- Modify: `FON.Native.Runtime/NativeBindings.cs`
- Test: `FON.Tests/FON.Native.Test/NativeNestedTests.cs` (create)

This task refactors `serialize_to_string`, adds the two nested branches in `serialize_value`, updates `get_type_char`, and exposes `fon_collection_add_collection` + `fon_collection_add_collection_array` so the test can populate nested data and verify the on-disk output.

- [ ] **Step 1: Write the failing test**

Create `FON.Tests/FON.Native.Test/NativeNestedTests.cs`:

```csharp
using FON.Native;
using Xunit;

namespace FON.Native.Test;


public class NativeNestedTests : IDisposable {
    private readonly string testDir;

    public NativeNestedTests() {
        testDir = Path.Combine(Path.GetTempPath(), $"fon_native_nested_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
    }


    public void Dispose() {
        if (Directory.Exists(testDir)) {
            Directory.Delete(testDir, recursive: true);
        }
    }


    [Fact]
    public void NativeSerialize_NestedObject_WritesObjectLiteral() {
        var filePath = Path.Combine(testDir, "nested.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();
        var inner = NativeBindings.fon_collection_create();

        NativeBindings.fon_collection_add_int(inner, "x", 7, ref error);
        NativeBindings.fon_collection_add_collection(outer, "wrap", inner, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);

        var result = NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        Assert.Equal(FonResultCode.OK, result);

        var text = File.ReadAllText(filePath).TrimEnd();
        Assert.Equal("wrap=o:{x=i:7}", text);

        NativeBindings.fon_dump_free(dump);
    }


    [Fact]
    public void NativeSerialize_ArrayOfObjects_WritesArrayLiteral() {
        var filePath = Path.Combine(testDir, "array.fon");
        var error = new FonError();

        var dump = NativeBindings.fon_dump_create();
        var outer = NativeBindings.fon_collection_create();

        var c1 = NativeBindings.fon_collection_create();
        var c2 = NativeBindings.fon_collection_create();
        NativeBindings.fon_collection_add_int(c1, "id", 1, ref error);
        NativeBindings.fon_collection_add_int(c2, "id", 2, ref error);

        var children = new IntPtr[] { c1, c2 };
        NativeBindings.fon_collection_add_collection_array(outer, "items", children, children.LongLength, ref error);
        NativeBindings.fon_dump_add(dump, 0, outer, ref error);

        var result = NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
        Assert.Equal(FonResultCode.OK, result);

        var text = File.ReadAllText(filePath).TrimEnd();
        Assert.Equal("items=o:[{id=i:1},{id=i:2}]", text);

        NativeBindings.fon_dump_free(dump);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail at compile time**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeNestedTests.NativeSerialize_"`

Expected: FAIL — `fon_collection_add_collection` and `fon_collection_add_collection_array` are not declared in `NativeBindings`.

- [ ] **Step 3: Refactor `serialize_to_string` and add `serialize_body`**

In `FON.Native/include/fon.hpp`, in the `private:` section of `class Fon`, declare:

```cpp
static void serialize_body(std::string& out, const FonCollection& collection);
```

Then replace the existing `inline std::string Fon::serialize_to_string(...)` with:

```cpp
inline std::string Fon::serialize_to_string(const FonCollection& collection) {
    std::string result;
    result.reserve(4096);
    serialize_body(result, collection);
    return result;
}


inline void Fon::serialize_body(std::string& out, const FonCollection& collection) {
    bool first = true;
    for (const auto& [key, value] : collection) {
        if (!first) {
            out += ',';
        }
        first = false;
        out += key;
        out += '=';
        out += get_type_char(value);
        out += ':';
        serialize_value(out, value);
    }
}
```

- [ ] **Step 4: Add nested branches to `serialize_value`**

In the `serialize_value` `std::visit` lambda in `fon.hpp`, immediately before the closing `}, value);`, add:

```cpp
else if constexpr (std::is_same_v<T, std::shared_ptr<FonCollection>>) {
    out += '{';
    if (v) {
        serialize_body(out, *v);
    }
    out += '}';
}
else if constexpr (std::is_same_v<T, std::vector<std::shared_ptr<FonCollection>>>) {
    out += '[';
    for (size_t i = 0; i < v.size(); ++i) {
        if (i > 0) {
            out += ',';
        }
        out += '{';
        if (v[i]) {
            serialize_body(out, *v[i]);
        }
        out += '}';
    }
    out += ']';
}
```

- [ ] **Step 5: Add `TYPE_OBJECT` returns in `get_type_char`**

In `get_type_char`, before the final `else return '?';`, add:

```cpp
else if constexpr (std::is_same_v<T, std::shared_ptr<FonCollection>>) return TYPE_OBJECT;
else if constexpr (std::is_same_v<T, std::vector<std::shared_ptr<FonCollection>>>) return TYPE_OBJECT;
```

- [ ] **Step 6: Add C ABI declarations in `fon_export.h`**

In `FON.Native/include/fon_export.h`, before the `// ==================== CONFIGURATION ====================` block, add:

```c
    /**
     * Add a nested collection to a parent collection.
     * Ownership: parent takes ownership of child. After this call, the child handle
     * is invalidated; the caller must not use it again or call fon_collection_free on it.
     */
    FON_API int32_t fon_collection_add_collection(
        FonCollectionHandle parent,
        const char* key,
        FonCollectionHandle child,
        FonError* error
    );


    /**
     * Add an array of nested collections to a parent collection.
     * Ownership: parent takes ownership of every handle in the array. All children
     * handles are invalidated after the call.
     */
    FON_API int32_t fon_collection_add_collection_array(
        FonCollectionHandle parent,
        const char* key,
        const FonCollectionHandle* children,
        int64_t count,
        FonError* error
    );
```

- [ ] **Step 7: Implement the two C ABI exports in `fon_export.cpp`**

In `FON.Native/src/fon_export.cpp`, after the existing `fon_collection_add_string` implementation (or any sensible spot in the ADD block), add:

```cpp
FON_API int32_t fon_collection_add_collection(FonCollectionHandle parent, const char* key, FonCollectionHandle child, FonError* error) {
    if (!parent || !key || !child) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* p = static_cast<fon::FonCollection*>(parent);
    auto* c = static_cast<fon::FonCollection*>(child);
    p->add(key, std::shared_ptr<fon::FonCollection>(c));
    return FON_OK;
}


FON_API int32_t fon_collection_add_collection_array(FonCollectionHandle parent, const char* key, const FonCollectionHandle* children, int64_t count, FonError* error) {
    if (!parent || !key || (count > 0 && !children) || count < 0) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    auto* p = static_cast<fon::FonCollection*>(parent);
    std::vector<std::shared_ptr<fon::FonCollection>> vec;
    vec.reserve(static_cast<size_t>(count));
    for (int64_t i = 0; i < count; ++i) {
        auto* c = static_cast<fon::FonCollection*>(children[i]);
        vec.emplace_back(c);
    }
    p->add(key, std::move(vec));
    return FON_OK;
}
```

- [ ] **Step 8: Add P/Invoke declarations in `NativeBindings.cs`**

In `FON.Native.Runtime/NativeBindings.cs`, in the `// ==================== COLLECTION ADD OPERATIONS ====================` block, add:

```csharp
[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public static extern int fon_collection_add_collection(
    IntPtr parent,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
    IntPtr child,
    ref FonError error
);


[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public static extern int fon_collection_add_collection_array(
    IntPtr parent,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
    IntPtr[] children,
    long count,
    ref FonError error
);
```

- [ ] **Step 9: Rebuild native and run tests**

Run: `cmake --build build --config Release && dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeNestedTests.NativeSerialize_"`

Expected: 2 tests PASS.

- [ ] **Step 10: Run the full native test suite to confirm no regression**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj`

Expected: all existing tests still PASS.

- [ ] **Step 11: Commit**

```bash
git add FON.Native/include/fon.hpp FON.Native/include/fon_export.h FON.Native/src/fon_export.cpp FON.Native.Runtime/NativeBindings.cs FON.Tests/FON.Native.Test/NativeNestedTests.cs
git commit -m "Add native serialization and C ABI for nested objects"
```

---

## Task 12: Native deserialization for nested objects

**Files:**
- Modify: `FON.Native/include/fon.hpp`
- Modify: `FON.Native/include/fon_export.h`
- Modify: `FON.Native/src/fon_export.cpp`
- Modify: `FON.Native.Runtime/NativeBindings.cs`
- Test: `FON.Tests/FON.Native.Test/NativeNestedTests.cs`

Refactor `deserialize_line` into `parse_collection_body(line, depth)`, thread `depth` through `parse_value` and `parse_array<T>`, add `find_closing_brace`, `parse_object`, `parse_object_array`, and the `'o'` dispatch in `parse_value`. Expose `fon_collection_get_collection` and `fon_collection_get_collection_array` so the test can read nested data back.

- [ ] **Step 1: Write the failing test**

Append to `NativeNestedTests.cs`:

```csharp
[Fact]
public void NativeRoundTrip_NestedObject_ReadsBack() {
    var filePath = Path.Combine(testDir, "rt-nested.fon");
    var error = new FonError();

    var dump = NativeBindings.fon_dump_create();
    var outer = NativeBindings.fon_collection_create();
    var inner = NativeBindings.fon_collection_create();
    NativeBindings.fon_collection_add_int(inner, "x", 99, ref error);
    NativeBindings.fon_collection_add_collection(outer, "wrap", inner, ref error);
    NativeBindings.fon_dump_add(dump, 0, outer, ref error);
    NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
    NativeBindings.fon_dump_free(dump);

    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    Assert.NotEqual(IntPtr.Zero, loaded);

    var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
    var loadedInner = NativeBindings.fon_collection_get_collection(loadedOuter, "wrap", ref error);
    Assert.NotEqual(IntPtr.Zero, loadedInner);

    NativeBindings.fon_collection_get_int(loadedInner, "x", out var x, ref error);
    Assert.Equal(99, x);

    NativeBindings.fon_dump_free(loaded);
}


[Fact]
public void NativeRoundTrip_ArrayOfObjects_ReadsBack() {
    var filePath = Path.Combine(testDir, "rt-array.fon");
    var error = new FonError();

    var dump = NativeBindings.fon_dump_create();
    var outer = NativeBindings.fon_collection_create();
    var c1 = NativeBindings.fon_collection_create();
    var c2 = NativeBindings.fon_collection_create();
    NativeBindings.fon_collection_add_int(c1, "id", 10, ref error);
    NativeBindings.fon_collection_add_int(c2, "id", 20, ref error);
    var children = new IntPtr[] { c1, c2 };
    NativeBindings.fon_collection_add_collection_array(outer, "items", children, 2, ref error);
    NativeBindings.fon_dump_add(dump, 0, outer, ref error);
    NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
    NativeBindings.fon_dump_free(dump);

    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);

    NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", null, 0, out var actualSize, ref error);
    Assert.Equal(2, actualSize);

    var buffer = new IntPtr[2];
    NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", buffer, buffer.LongLength, out actualSize, ref error);
    Assert.Equal(2, actualSize);

    NativeBindings.fon_collection_get_int(buffer[0], "id", out var id0, ref error);
    NativeBindings.fon_collection_get_int(buffer[1], "id", out var id1, ref error);
    Assert.Equal(10, id0);
    Assert.Equal(20, id1);

    NativeBindings.fon_dump_free(loaded);
}


[Fact]
public void NativeRoundTrip_EmptyNestedObject_ReadsBack() {
    var filePath = Path.Combine(testDir, "rt-empty.fon");
    var error = new FonError();

    var dump = NativeBindings.fon_dump_create();
    var outer = NativeBindings.fon_collection_create();
    var inner = NativeBindings.fon_collection_create();
    NativeBindings.fon_collection_add_collection(outer, "empty", inner, ref error);
    NativeBindings.fon_dump_add(dump, 0, outer, ref error);
    NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
    NativeBindings.fon_dump_free(dump);

    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
    var loadedInner = NativeBindings.fon_collection_get_collection(loadedOuter, "empty", ref error);
    Assert.NotEqual(IntPtr.Zero, loadedInner);
    Assert.Equal(0, NativeBindings.fon_collection_size(loadedInner));

    NativeBindings.fon_dump_free(loaded);
}
```

- [ ] **Step 2: Run tests to verify they fail at compile time**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeNestedTests.NativeRoundTrip_"`

Expected: FAIL — `fon_collection_get_collection` and `fon_collection_get_collection_array` are not declared.

- [ ] **Step 3: Refactor `deserialize_line` into `parse_collection_body` and thread depth through helpers**

In `FON.Native/include/fon.hpp`, in the `private:` section of `class Fon`, replace the existing `parse_value` and `parse_array` declarations with depth-aware variants and add new helpers:

```cpp
static FonCollection parse_collection_body(std::string_view line, int depth);
static std::pair<FonValue, size_t> parse_value(std::string_view data, char type_char, int depth);
static std::pair<std::string, size_t> parse_string(std::string_view data);
static size_t find_closing_brace(std::string_view data);
static std::pair<std::shared_ptr<FonCollection>, size_t> parse_object(std::string_view data, int depth);
static std::pair<std::vector<std::shared_ptr<FonCollection>>, size_t> parse_object_array(std::string_view data, int depth);

template<typename T>
static std::pair<T, size_t> parse_number(std::string_view data);

template<typename T>
static std::pair<std::vector<T>, size_t> parse_array(std::string_view data, char type_char, int depth);
```

Replace the existing `inline FonCollection Fon::deserialize_line(...)` with:

```cpp
inline FonCollection Fon::deserialize_line(std::string_view line) {
    return parse_collection_body(line, 0);
}


inline FonCollection Fon::parse_collection_body(std::string_view line, int depth) {
    FonCollection collection;
    size_t pos = 0;

    while (pos < line.size()) {
        auto eq_pos = line.find('=', pos);
        if (eq_pos == std::string_view::npos) {
            break;
        }

        std::string key(line.substr(pos, eq_pos - pos));
        pos = eq_pos + 1;

        if (pos >= line.size() || pos + 1 >= line.size() || line[pos + 1] != ':') {
            throw std::runtime_error("Invalid format: expected type:value");
        }

        char type_char = line[pos];
        pos += 2;

        auto remaining = line.substr(pos);
        auto [value, consumed] = parse_value(remaining, type_char, depth);

        collection.add(key, std::move(value));
        pos += consumed;

        if (pos < line.size() && line[pos] == ',') {
            ++pos;
        }
    }

    return collection;
}
```

- [ ] **Step 4: Update `parse_value` to take depth and dispatch `'o'`**

Replace `inline std::pair<FonValue, size_t> Fon::parse_value(...)` with:

```cpp
inline std::pair<FonValue, size_t> Fon::parse_value(std::string_view data, char type_char, int depth) {
    if (data.empty()) {
        throw std::runtime_error("Empty value");
    }

    if (type_char == TYPE_OBJECT) {
        if (data[0] == '{') {
            auto [obj, consumed] = parse_object(data, depth + 1);
            return {std::move(obj), consumed};
        }
        if (data[0] == '[') {
            auto [arr, consumed] = parse_object_array(data, depth + 1);
            return {std::move(arr), consumed};
        }
        throw std::runtime_error("Object must start with '{' or '['");
    }

    if (data[0] == '[') {
        switch (type_char) {
            case TYPE_BYTE:   { auto [v, c] = parse_array<uint8_t>(data, type_char, depth + 1); return {std::move(v), c}; }
            case TYPE_SHORT:  { auto [v, c] = parse_array<int16_t>(data, type_char, depth + 1); return {std::move(v), c}; }
            case TYPE_INT:    { auto [v, c] = parse_array<int32_t>(data, type_char, depth + 1); return {std::move(v), c}; }
            case TYPE_UINT:   { auto [v, c] = parse_array<uint32_t>(data, type_char, depth + 1); return {std::move(v), c}; }
            case TYPE_LONG:   { auto [v, c] = parse_array<int64_t>(data, type_char, depth + 1); return {std::move(v), c}; }
            case TYPE_ULONG:  { auto [v, c] = parse_array<uint64_t>(data, type_char, depth + 1); return {std::move(v), c}; }
            case TYPE_FLOAT:  { auto [v, c] = parse_array<float>(data, type_char, depth + 1); return {std::move(v), c}; }
            case TYPE_DOUBLE: { auto [v, c] = parse_array<double>(data, type_char, depth + 1); return {std::move(v), c}; }
            default: throw std::runtime_error("Unsupported array type");
        }
    }

    if (type_char == TYPE_STRING) {
        auto [str, consumed] = parse_string(data);
        return {std::move(str), consumed};
    }

    if (type_char == TYPE_RAW) {
        auto [str, consumed] = parse_string(data);
        auto raw = std::make_shared<RawData>(str);
        if (deserialize_raw_unpack) {
            raw->unpack();
        }
        return {raw, consumed};
    }

    size_t end = find_value_end(data);
    auto value_str = data.substr(0, end);
    size_t consumed = end;
    if (consumed < data.size() && data[consumed] == ',') ++consumed;

    switch (type_char) {
        case TYPE_BYTE:   { auto [v, c] = parse_number<uint8_t>(value_str); return {v, consumed}; }
        case TYPE_SHORT:  { auto [v, c] = parse_number<int16_t>(value_str); return {v, consumed}; }
        case TYPE_INT:    { auto [v, c] = parse_number<int32_t>(value_str); return {v, consumed}; }
        case TYPE_UINT:   { auto [v, c] = parse_number<uint32_t>(value_str); return {v, consumed}; }
        case TYPE_LONG:   { auto [v, c] = parse_number<int64_t>(value_str); return {v, consumed}; }
        case TYPE_ULONG:  { auto [v, c] = parse_number<uint64_t>(value_str); return {v, consumed}; }
        case TYPE_FLOAT:  { auto [v, c] = parse_number<float>(value_str); return {v, consumed}; }
        case TYPE_DOUBLE: { auto [v, c] = parse_number<double>(value_str); return {v, consumed}; }
        case TYPE_BOOL:   return {value_str[0] != '0', consumed};
        default: throw std::runtime_error("Unknown type");
    }
}
```

- [ ] **Step 5: Update `parse_array<T>` signature to accept depth**

Replace `template<typename T> inline std::pair<std::vector<T>, size_t> Fon::parse_array(std::string_view data, char type_char)` with:

```cpp
template<typename T>
inline std::pair<std::vector<T>, size_t> Fon::parse_array(std::string_view data, char type_char, int depth) {
    (void)depth;
    (void)type_char;
    if (data[0] != '[') {
        throw std::runtime_error("Array must start with '['");
    }

    size_t close = find_closing_bracket(data);
    auto content = data.substr(1, close - 1);

    std::vector<T> result;
    result.reserve(content.size() / 4);

    size_t pos = 0;
    while (pos < content.size()) {
        auto remaining = content.substr(pos);
        auto [value, consumed] = parse_number<T>(remaining);
        result.push_back(value);
        pos += consumed;
        if (pos < content.size() && content[pos] == ',') ++pos;
    }

    size_t total_consumed = close + 1;
    if (total_consumed < data.size() && data[total_consumed] == ',') ++total_consumed;

    return {std::move(result), total_consumed};
}
```

(`depth` is unused inside; depth checks live in `parse_object` / `parse_object_array` for bracket-symmetric enforcement matching the C# spec.)

- [ ] **Step 6: Add `find_closing_brace`, `parse_object`, `parse_object_array`**

In `FON.Native/include/fon.hpp`, add after the existing `find_closing_bracket`:

```cpp
inline size_t Fon::find_closing_brace(std::string_view data) {
    int depth = 0;
    bool in_string = false;

    for (size_t i = 0; i < data.size(); ++i) {
        char c = data[i];

        if (c == '"' && (i == 0 || data[i - 1] != '\\')) {
            in_string = !in_string;
        } else if (!in_string) {
            if (c == '{') {
                ++depth;
            } else if (c == '}') {
                --depth;
                if (depth == 0) {
                    return i;
                }
            }
        }
    }

    throw std::runtime_error("Closing brace not found");
}


inline std::pair<std::shared_ptr<FonCollection>, size_t> Fon::parse_object(std::string_view data, int depth) {
    if (data[0] != '{') {
        throw std::runtime_error("Object must start with '{'");
    }

    size_t close = find_closing_brace(data);
    auto body = data.substr(1, close - 1);

    auto collection = std::make_shared<FonCollection>(parse_collection_body(body, depth));

    size_t consumed = close + 1;
    if (consumed < data.size() && data[consumed] == ',') {
        ++consumed;
    }

    return {std::move(collection), consumed};
}


inline std::pair<std::vector<std::shared_ptr<FonCollection>>, size_t> Fon::parse_object_array(std::string_view data, int depth) {
    if (data[0] != '[') {
        throw std::runtime_error("Object array must start with '['");
    }

    size_t close = find_closing_bracket(data);
    auto content = data.substr(1, close - 1);

    std::vector<std::shared_ptr<FonCollection>> result;

    size_t pos = 0;
    while (pos < content.size()) {
        auto remaining = content.substr(pos);
        if (remaining[0] != '{') {
            throw std::runtime_error("Object array element must start with '{'");
        }
        auto [obj, consumed] = parse_object(remaining, depth);
        result.push_back(std::move(obj));
        pos += consumed;
    }

    size_t total_consumed = close + 1;
    if (total_consumed < data.size() && data[total_consumed] == ',') {
        ++total_consumed;
    }

    return {std::move(result), total_consumed};
}
```

- [ ] **Step 7: Add C ABI declarations in `fon_export.h`**

Insert after the two `add_collection*` declarations from Task 11:

```c
    /**
     * Get a borrowed handle to a nested collection. Returns NULL if the key is missing
     * or the value is not a nested object. The returned handle is owned by the parent;
     * the caller must not call fon_collection_free on it.
     */
    FON_API FonCollectionHandle fon_collection_get_collection(
        FonCollectionHandle parent,
        const char* key,
        FonError* error
    );


    /**
     * Get an array of borrowed nested-collection handles. Pass buffer=NULL with
     * buffer_size=0 to query actual_size only. Returns FON_OK on success.
     */
    FON_API int32_t fon_collection_get_collection_array(
        FonCollectionHandle parent,
        const char* key,
        FonCollectionHandle* buffer,
        int64_t buffer_size,
        int64_t* actual_size,
        FonError* error
    );
```

- [ ] **Step 8: Implement the two C ABI exports in `fon_export.cpp`**

Add to the GET block:

```cpp
FON_API FonCollectionHandle fon_collection_get_collection(FonCollectionHandle parent, const char* key, FonError* error) {
    if (!parent || !key) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return nullptr;
    }

    try {
        auto* p = static_cast<fon::FonCollection*>(parent);
        auto* shared = p->try_get<std::shared_ptr<fon::FonCollection>>(key);
        if (!shared || !*shared) {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or not a nested collection");
            return nullptr;
        }
        return shared->get();
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return nullptr;
    }
}


FON_API int32_t fon_collection_get_collection_array(FonCollectionHandle parent, const char* key, FonCollectionHandle* buffer, int64_t buffer_size, int64_t* actual_size, FonError* error) {
    if (!parent || !key || !actual_size) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto* p = static_cast<fon::FonCollection*>(parent);
        auto* vec = p->try_get<std::vector<std::shared_ptr<fon::FonCollection>>>(key);
        if (!vec) {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or not an array of nested collections");
            return FON_ERROR_INVALID_ARGUMENT;
        }

        *actual_size = static_cast<int64_t>(vec->size());

        if (buffer && buffer_size > 0) {
            int64_t copy_count = std::min(buffer_size, *actual_size);
            for (int64_t i = 0; i < copy_count; ++i) {
                buffer[i] = (*vec)[i].get();
            }
        }
        return FON_OK;
    } catch (const std::exception& e) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, e.what());
        return FON_ERROR_INVALID_ARGUMENT;
    }
}
```

- [ ] **Step 9: Add P/Invoke declarations**

In `NativeBindings.cs`, in the `// ==================== COLLECTION GET OPERATIONS ====================` block:

```csharp
[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public static extern IntPtr fon_collection_get_collection(
    IntPtr parent,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
    ref FonError error
);


[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public static extern int fon_collection_get_collection_array(
    IntPtr parent,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
    [Out] IntPtr[]? buffer,
    long bufferSize,
    out long actualSize,
    ref FonError error
);
```

- [ ] **Step 10: Rebuild native and run tests**

Run: `cmake --build build --config Release && dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeNestedTests.NativeRoundTrip_"`

Expected: 3 tests PASS.

- [ ] **Step 11: Run the full native test suite**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj`

Expected: all PASS.

- [ ] **Step 12: Commit**

```bash
git add FON.Native/include/fon.hpp FON.Native/include/fon_export.h FON.Native/src/fon_export.cpp FON.Native.Runtime/NativeBindings.cs FON.Tests/FON.Native.Test/NativeNestedTests.cs
git commit -m "Add native deserialization and C ABI for nested objects"
```

---

## Task 13: Native depth-limit enforcement

**Files:**
- Modify: `FON.Native/include/fon.hpp`
- Modify: `FON.Native/include/fon_export.h`
- Modify: `FON.Native/src/fon_export.cpp`
- Modify: `FON.Native.Runtime/NativeBindings.cs`
- Test: `FON.Tests/FON.Native.Test/NativeNestedTests.cs`

Adds `depth > max_depth` checks at the top of `parse_object` and `parse_object_array`, exposes `fon_set_max_depth(int32_t)` over the C ABI, and verifies parsing fails when the limit is breached.

- [ ] **Step 1: Write the failing test**

Append to `NativeNestedTests.cs`:

```csharp
[Fact]
public void NativeMaxDepth_BeyondLimit_ParseFails() {
    var filePath = Path.Combine(testDir, "deep.fon");
    var error = new FonError();

    // Build a depth-3 nested object as raw text.
    File.WriteAllText(filePath, "a=o:{b=o:{c=o:{d=i:1}}}\n");

    NativeBindings.fon_set_max_depth(2);

    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    Assert.Equal(IntPtr.Zero, loaded);
    Assert.Equal(FonResultCode.ParseFailed, error.Code);

    // Reset for other tests
    NativeBindings.fon_set_max_depth(64);
}


[Fact]
public void NativeMaxDepth_AtLimit_Succeeds() {
    var filePath = Path.Combine(testDir, "atlimit.fon");
    var error = new FonError();

    File.WriteAllText(filePath, "a=o:{b=o:{c=i:1}}\n");

    NativeBindings.fon_set_max_depth(2);

    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    Assert.NotEqual(IntPtr.Zero, loaded);

    NativeBindings.fon_set_max_depth(64);
    NativeBindings.fon_dump_free(loaded);
}


[Fact]
public void NativeMaxDepth_NegativeValue_ClampedToOne() {
    var filePath = Path.Combine(testDir, "clamp.fon");
    var error = new FonError();

    File.WriteAllText(filePath, "a=o:{x=i:1}\n");

    NativeBindings.fon_set_max_depth(-5);

    // depth=1 is at the clamped limit; should still parse
    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    Assert.NotEqual(IntPtr.Zero, loaded);

    NativeBindings.fon_set_max_depth(64);
    NativeBindings.fon_dump_free(loaded);
}
```

- [ ] **Step 2: Run tests to verify they fail at compile time**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeNestedTests.NativeMaxDepth_"`

Expected: FAIL — `fon_set_max_depth` not declared.

- [ ] **Step 3: Add depth checks at the top of `parse_object` and `parse_object_array`**

In `FON.Native/include/fon.hpp`, modify `parse_object` to check at the very top:

```cpp
inline std::pair<std::shared_ptr<FonCollection>, size_t> Fon::parse_object(std::string_view data, int depth) {
    if (depth > max_depth) {
        throw std::runtime_error("Maximum nesting depth exceeded");
    }
    if (data[0] != '{') {
        throw std::runtime_error("Object must start with '{'");
    }
    // ... rest unchanged
}
```

And `parse_object_array`:

```cpp
inline std::pair<std::vector<std::shared_ptr<FonCollection>>, size_t> Fon::parse_object_array(std::string_view data, int depth) {
    if (depth > max_depth) {
        throw std::runtime_error("Maximum nesting depth exceeded");
    }
    if (data[0] != '[') {
        throw std::runtime_error("Object array must start with '['");
    }
    // ... rest unchanged
}
```

- [ ] **Step 4: Add `fon_set_max_depth` C ABI declaration**

In `FON.Native/include/fon_export.h`, in the `// ==================== CONFIGURATION ====================` block, after `fon_set_raw_unpack`:

```c
    /**
     * Set the maximum nesting depth for the parser. Values less than 1 are
     * silently clamped to 1. Default: 64.
     */
    FON_API void fon_set_max_depth(int32_t depth);
```

- [ ] **Step 5: Implement `fon_set_max_depth` in `fon_export.cpp`**

In `FON.Native/src/fon_export.cpp`, in the CONFIGURATION block (right after `fon_set_raw_unpack`):

```cpp
FON_API void fon_set_max_depth(int32_t depth) {
    if (depth < 1) {
        depth = 1;
    }
    fon::Fon::max_depth = depth;
}
```

- [ ] **Step 6: Add P/Invoke declaration**

In `NativeBindings.cs`, in the CONFIGURATION block (after `fon_set_raw_unpack`):

```csharp
[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
public static extern void fon_set_max_depth(int depth);
```

- [ ] **Step 7: Rebuild native and run tests**

Run: `cmake --build build --config Release && dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeNestedTests.NativeMaxDepth_"`

Expected: 3 tests PASS.

- [ ] **Step 8: Run the full native test suite as the phase-2 gate**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj`

Expected: every test PASS.

- [ ] **Step 9: Commit**

```bash
git add FON.Native/include/fon.hpp FON.Native/include/fon_export.h FON.Native/src/fon_export.cpp FON.Native.Runtime/NativeBindings.cs FON.Tests/FON.Native.Test/NativeNestedTests.cs
git commit -m "Enforce native MaxDepth and expose fon_set_max_depth"
```

---

## Task 14: Additional native nested-object coverage

**Files:**
- Test: `FON.Tests/FON.Native.Test/NativeNestedTests.cs`

Lock in coverage for empty arrays, nested-string metacharacters, and the documented child-handle invalidation contract.

- [ ] **Step 1: Write the failing test**

Append to `NativeNestedTests.cs`:

```csharp
[Fact]
public void NativeRoundTrip_EmptyObjectArray_ReadsBack() {
    var filePath = Path.Combine(testDir, "rt-empty-array.fon");
    var error = new FonError();

    var dump = NativeBindings.fon_dump_create();
    var outer = NativeBindings.fon_collection_create();
    NativeBindings.fon_collection_add_collection_array(outer, "items", Array.Empty<IntPtr>(), 0, ref error);
    NativeBindings.fon_dump_add(dump, 0, outer, ref error);
    NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
    NativeBindings.fon_dump_free(dump);

    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
    NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", null, 0, out var size, ref error);
    Assert.Equal(0, size);

    NativeBindings.fon_dump_free(loaded);
}


[Fact]
public void NativeRoundTrip_NestedStringWithBraces_ReadsBack() {
    var filePath = Path.Combine(testDir, "rt-meta.fon");
    var error = new FonError();

    var dump = NativeBindings.fon_dump_create();
    var outer = NativeBindings.fon_collection_create();
    var inner = NativeBindings.fon_collection_create();
    NativeBindings.fon_collection_add_string(inner, "txt", "has}brace,and{open[bracket]", ref error);
    NativeBindings.fon_collection_add_collection(outer, "wrap", inner, ref error);
    NativeBindings.fon_dump_add(dump, 0, outer, ref error);
    NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
    NativeBindings.fon_dump_free(dump);

    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
    var loadedInner = NativeBindings.fon_collection_get_collection(loadedOuter, "wrap", ref error);

    var buffer = new byte[256];
    NativeBindings.fon_collection_get_string(loadedInner, "txt", buffer, buffer.LongLength, ref error);
    var got = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');
    Assert.Equal("has}brace,and{open[bracket]", got);

    NativeBindings.fon_dump_free(loaded);
}


[Fact]
public void NativeAdd_ChildHandleInvalidatedAfterAdd() {
    // Documents the ownership contract from the spec: once add_collection succeeds,
    // the child handle is owned by the parent. Calling fon_collection_free on it
    // afterward would be a double-free, so this test demonstrates the correct usage
    // pattern: do NOT free child after add.
    var error = new FonError();
    var parent = NativeBindings.fon_collection_create();
    var child = NativeBindings.fon_collection_create();
    NativeBindings.fon_collection_add_int(child, "x", 1, ref error);

    var rc = NativeBindings.fon_collection_add_collection(parent, "c", child, ref error);
    Assert.Equal(FonResultCode.OK, rc);

    // child is now invalidated — only free the parent.
    NativeBindings.fon_collection_free(parent);
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeNestedTests.NativeRoundTrip_EmptyObjectArray_ReadsBack|FullyQualifiedName~NativeNestedTests.NativeRoundTrip_NestedStringWithBraces_ReadsBack|FullyQualifiedName~NativeNestedTests.NativeAdd_ChildHandleInvalidatedAfterAdd"`

Expected: 3 tests PASS.

- [ ] **Step 3: Commit**

```bash
git add FON.Tests/FON.Native.Test/NativeNestedTests.cs
git commit -m "Add native coverage for empty arrays, metacharacters, ownership"
```

---

# Phase 3 — Cross-cutting

## Task 15: Cross-implementation roundtrip — managed writes, native reads

**Files:**
- Test: `FON.Tests/FON.Native.Test/NativeFileTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `FON.Tests/FON.Native.Test/NativeFileTests.cs` (inside the existing `NativeFileTests` class):

```csharp
[Fact]
public async Task CrossImpl_ManagedWritesNested_NativeReads() {
    var filePath = Path.Combine(_testDir, "cross-mn.fon");
    var error = new FonError();

    var inner = new global::FON.Types.FonCollection { { "x", 17 }, { "name", "with}brace,here" } };
    var arrItems = new List<global::FON.Types.FonCollection> {
        new global::FON.Types.FonCollection { { "id", 1 } },
        new global::FON.Types.FonCollection { { "id", 2 } }
    };
    var outer = new global::FON.Types.FonCollection {
        { "wrap", inner },
        { "items", arrItems }
    };

    var dump = new global::FON.Types.FonDump();
    dump.TryAdd(0, outer);

    await global::FON.Core.Fon.SerializeToFileAutoAsync(dump, new FileInfo(filePath));

    var loaded = NativeBindings.fon_deserialize_from_file(filePath, 1, ref error);
    Assert.NotEqual(IntPtr.Zero, loaded);

    var loadedOuter = NativeBindings.fon_dump_get(loaded, 0);
    var loadedInner = NativeBindings.fon_collection_get_collection(loadedOuter, "wrap", ref error);
    NativeBindings.fon_collection_get_int(loadedInner, "x", out var x, ref error);
    Assert.Equal(17, x);

    var nameBuf = new byte[256];
    NativeBindings.fon_collection_get_string(loadedInner, "name", nameBuf, nameBuf.LongLength, ref error);
    Assert.Equal("with}brace,here", System.Text.Encoding.UTF8.GetString(nameBuf).TrimEnd('\0'));

    NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", null, 0, out var size, ref error);
    Assert.Equal(2, size);

    var buf = new IntPtr[2];
    NativeBindings.fon_collection_get_collection_array(loadedOuter, "items", buf, 2, out _, ref error);
    NativeBindings.fon_collection_get_int(buf[0], "id", out var id0, ref error);
    NativeBindings.fon_collection_get_int(buf[1], "id", out var id1, ref error);
    Assert.Equal(1, id0);
    Assert.Equal(2, id1);

    NativeBindings.fon_dump_free(loaded);
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeFileTests.CrossImpl_ManagedWritesNested_NativeReads"`

Expected: PASS. If it fails, the on-disk format produced by C# differs from what the native parser expects — write out the file content and diff against the expected `wrap=o:{x=i:17,name=s:"with}brace,here"},items=o:[{id=i:1},{id=i:2}]`.

- [ ] **Step 3: Commit**

```bash
git add FON.Tests/FON.Native.Test/NativeFileTests.cs
git commit -m "Add cross-impl test: managed writes nested, native reads"
```

---

## Task 16: Cross-implementation roundtrip — native writes, managed reads

**Files:**
- Test: `FON.Tests/FON.Native.Test/NativeFileTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `NativeFileTests`:

```csharp
[Fact]
public async Task CrossImpl_NativeWritesNested_ManagedReads() {
    var filePath = Path.Combine(_testDir, "cross-nm.fon");
    var error = new FonError();

    var dump = NativeBindings.fon_dump_create();
    var outer = NativeBindings.fon_collection_create();
    var inner = NativeBindings.fon_collection_create();
    NativeBindings.fon_collection_add_int(inner, "x", 31, ref error);
    NativeBindings.fon_collection_add_string(inner, "msg", "}meta,test", ref error);
    NativeBindings.fon_collection_add_collection(outer, "wrap", inner, ref error);

    var c1 = NativeBindings.fon_collection_create();
    var c2 = NativeBindings.fon_collection_create();
    NativeBindings.fon_collection_add_int(c1, "id", 100, ref error);
    NativeBindings.fon_collection_add_int(c2, "id", 200, ref error);
    var children = new IntPtr[] { c1, c2 };
    NativeBindings.fon_collection_add_collection_array(outer, "items", children, 2, ref error);

    NativeBindings.fon_dump_add(dump, 0, outer, ref error);
    NativeBindings.fon_serialize_to_file(dump, filePath, 1, ref error);
    NativeBindings.fon_dump_free(dump);

    var loaded = await global::FON.Core.Fon.DeserializeFromFileAutoAsync(new FileInfo(filePath));
    Assert.Equal(1, loaded.Count);

    var loadedOuter = loaded[0];
    var loadedInner = loadedOuter.Get<global::FON.Types.FonCollection>("wrap");
    Assert.Equal(31, loadedInner.Get<int>("x"));
    Assert.Equal("}meta,test", loadedInner.Get<string>("msg"));

    var loadedItems = loadedOuter.Get<List<global::FON.Types.FonCollection>>("items");
    Assert.Equal(2, loadedItems.Count);
    Assert.Equal(100, loadedItems[0].Get<int>("id"));
    Assert.Equal(200, loadedItems[1].Get<int>("id"));
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test FON.Tests/FON.Native.Test/FON.Native.Test.csproj --filter "FullyQualifiedName~NativeFileTests.CrossImpl_NativeWritesNested_ManagedReads"`

Expected: PASS.

- [ ] **Step 3: Run the entire repository test suite as the final integration gate**

Run: `dotnet test`

Expected: every test in every project PASS.

- [ ] **Step 4: Commit**

```bash
git add FON.Tests/FON.Native.Test/NativeFileTests.cs
git commit -m "Add cross-impl test: native writes nested, managed reads"
```

---

## Task 17: Update README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add the `o` row to the Supported Types table**

In `README.md`, in the `## Supported Types` table, after the `RawData` row and before the line `All types support arrays: ...`, insert:

```markdown
| `FonCollection` | `o` | `user=o:{id=i:1,name=s:"Bob"}` |
```

- [ ] **Step 2: Update the array note immediately below the table**

Replace the line `All types support arrays: \`values=i:[1,2,3,4,5]\`` with:

```markdown
All primitive and string types support arrays (`values=i:[1,2,3,4,5]`). Nested objects also support arrays of objects (`items=o:[{id=i:1},{id=i:2}]`).
```

- [ ] **Step 3: Add nested-object examples in the Format Specification section**

In `README.md`, under `### Examples`, append a new code block:

```markdown
```
# Nested objects
user=o:{id=i:42,name=s:"Bob",addr=o:{city=s:"NY",zip=i:10001}}

# Arrays of objects
items=o:[{id=i:1,qty=i:5},{id=i:2,qty=i:3}]

# Empty object and empty array of objects
empty=o:{},none=o:[]
```
```

- [ ] **Step 4: Add `MaxDepth` to the Configuration section**

In `README.md`, in the `### Configuration` code block, add this line after `Fon.ParallelMethodThreshold = 2000;`:

```csharp
// Maximum bracket nesting depth (default: 64)
Fon.MaxDepth = 64;
```

- [ ] **Step 5: Verify the README renders sensibly**

Run: `dotnet build` (no behavior change, but confirms nothing else is broken)

Expected: build SUCCESS.

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "Document nested object support and MaxDepth in README"
```

---

# Final gate

Run the entire test suite one more time end-to-end:

```bash
cmake --build build --config Release && dotnet test
```

Expected: every test in every project PASS. The branch now ships nested-object and array-of-object support symmetrically across managed and native implementations, with depth limiting, transitive disposal, and full cross-implementation parity.
