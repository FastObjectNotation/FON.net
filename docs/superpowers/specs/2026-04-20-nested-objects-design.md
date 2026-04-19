# Nested Objects and Arrays of Objects in FON

**Status:** Draft
**Date:** 2026-04-20

## Problem

The FON format currently supports only flat collections: a value can be a primitive, a string, raw binary, or an array of those. Hierarchical data (an object containing another object, or an array of objects) cannot be expressed without flattening keys or encoding nested structures as escaped strings.

## Goal

Add a single new value type `o` (object) representing a nested `FonCollection`, and reuse the existing array machinery to support arrays of objects. The change must:

- Keep all existing files readable.
- Apply symmetrically to both the managed (C#) and native (C++) implementations so files written by one are readable by the other.
- Stay within the existing zero-allocation parsing style (`Span<T>`, `stackalloc`, `ArrayPool`).

## Non-goals

- POCO/reflection support for nested classes (`Serialize<T>` with nested properties). Out of scope; the API stays at `FonCollection` granularity.
- Cycle detection in serialization. Documented as a user responsibility.
- Performance benchmarks for the new path.
- Fuzz testing the parser.

## Format

A new type literal `o` is added. Its value is one of:

- A nested object: `{` followed by zero or more `key=type:value` pairs separated by `,`, followed by `}`.
- An array of objects: existing array syntax `[ ... ]` whose elements are objects.

Grammar (extension):

```
value     := primitive | string | raw | array | object
object    := '{' [ pair (',' pair)* ] '}'
pair      := key '=' typechar ':' value
array     := '[' [ value (',' value)* ] ']'
```

Examples:

```
user=o:{id=i:42,name=s:"Bob",addr=o:{city=s:"NY",zip=i:10001}}
items=o:[{id=i:1,qty=i:5},{id=i:2,qty=i:3}]
empty=o:{}
none=o:[]
```

Existing literals (`e t i u l g f d s b r`), key whitelist, and the line-as-record top-level structure of `FonDump` files are unchanged.

Arrays of objects are heterogeneous by construction: each element is an independent `FonCollection` and may contain different keys.

A `}` appearing inside a string value must not terminate the enclosing object. The parser tracks an `inString` flag (the same approach already used for `]` in arrays).

## Approach

`FonCollection` is treated as just another supported type:

- Managed: add `typeof(FonCollection) -> 'o'` to `Fon.SupportTypes`. The existing dispatch by `typeChar` gets one new branch on each side (serialize, deserialize). The existing `IList` branch handles arrays of objects without any new code path.
- Native: add `std::shared_ptr<FonCollection>` and `std::vector<std::shared_ptr<FonCollection>>` to the `FonValue` variant. `shared_ptr` is required because `std::variant` requires complete types and `FonCollection` is recursive through `FonValue`.

Two alternatives were considered and rejected:

- A separate "nested API" with a distinct value type and dedicated `AddNested`/`GetNested` methods. Rejected: extra surface area, breaks symmetry with how strings and arrays already work.
- A full refactor of the deserializer into an explicit recursive-descent parser. Rejected: large diff for a single feature that fits the existing dispatch shape; risks regressing the hot path.

## Managed design (C#)

### `FON/Core/Fon.cs`
Add one entry to `SupportTypes`:

```
{ typeof(FonCollection), 'o' }
```

Add a public configuration property:

```
public static int MaxDepth { get; set; } = 64;
```

The setter throws `ArgumentOutOfRangeException` for values less than 1.

### `FON/Core/FonSerialize.cs`
- Extract a private `SerializeBody(StringBuilder, FonCollection)` from the body of `SerializeToString`. `SerializeToString` becomes a thin wrapper that allocates the `StringBuilder` and calls `SerializeBody`.
- Add `case 'o'` in `SerializeBaseObject` that writes `{`, calls `SerializeBody(sb, (FonCollection)value)`, writes `}`.
- The existing `IList` branch already iterates elements and dispatches to `SerializeBaseObject`. With the new case, `List<FonCollection>` serializes as `o:[{...},{...}]` without any change to that branch.

### `FON/Core/FonDeserialize.cs`
- Extract a private `ParseCollectionBody(ReadOnlySpan<char>, int depth) -> (FonCollection, int consumed)` from `DeserializeLineOptimized`. The current `DeserializeLineOptimized` becomes a thin wrapper that calls `ParseCollectionBody` for the whole line.
- All private parse helpers (`DeserializeValueOptimized`, `DeserializeArrayOptimized`, `DeserializeStringOptimized`, `DeserializeRawOptimized`, the new object helpers) gain an `int depth` parameter. `DeserializeObjectOptimized` and `DeserializeArrayOptimized` check `depth > Fon.MaxDepth` on entry and throw `FormatException` on violation.
- New private `DeserializeObjectOptimized(ReadOnlySpan<char>, int depth) -> (FonCollection, int consumed)`: validates the leading `{`, locates the matching `}` via a new `FindClosingBrace`, slices the body, calls `ParseCollectionBody(body, depth)`, returns the consumed count including a trailing `,` if present.
- New private `FindClosingBrace(ReadOnlySpan<char>) -> int`: mirror of `FindClosingBracket` with brace counting and an `inString` flag that tracks unescaped `"`.
- The `[`-vs-value dispatch in `ParseCollectionBody` (the call site that today routes arrays to `DeserializeArrayOptimized` and primitives to `DeserializeValueOptimized`) needs no change: when `typeChar == 'o'` and the next character is `[`, control already flows into `DeserializeArrayOptimized`, which produces a `List<FonCollection>` via `CreateTypedList`.
- Inside `DeserializeValueOptimized`, when `typeChar == 'o'`, the next character must be `{` (anything else -> `FormatException`); the call dispatches to `DeserializeObjectOptimized`.
- `CreateTypedList`: add `'o' => new List<FonCollection>()`.
- Depth-tracking convention: callers pass `depth + 1` when descending into a nested object or an array. The check at the top of `DeserializeObjectOptimized` and `DeserializeArrayOptimized` is `depth > Fon.MaxDepth` (so `MaxDepth = N` permits `N` levels of bracket nesting, matching `System.Text.Json` semantics).

### `FON/Types/FonCollection.cs`
- Public API unchanged. Nested values are accessed via `Get<FonCollection>("user")` and `Get<List<FonCollection>>("items")`, consistent with existing typed accessors.
- `Dispose()`: extend the `IDisposable` walk to also enumerate `IList` values and dispose disposable elements. This ensures arrays of `FonCollection` and arrays of `RawData` are disposed transitively.
- XML doc note on the class: putting a `FonCollection` inside itself (or creating a cycle through nested values) is the user's responsibility; serialization does not detect cycles and will recurse until the call stack overflows.

## Native design (C++)

The managed and native implementations are independent (managed code does not call into native today). Both must accept and produce the same wire format.

### `FON.Native/include/fon_types.hpp`
- Add to `FonValue` variant:
  - `std::shared_ptr<FonCollection>` (single nested object)
  - `std::vector<std::shared_ptr<FonCollection>>` (array of objects)
- Add `constexpr char TYPE_OBJECT = 'o';`.
- `FonCollection` and `FonDump` definitions are unchanged.

### `FON.Native/include/fon.hpp`
- `serialize_value` (`std::visit` lambda): two new branches.
  - `std::shared_ptr<FonCollection>`: emit `{`, call `serialize_body(*v)`, emit `}`.
  - `std::vector<std::shared_ptr<FonCollection>>`: emit `[`, loop with `,` separators emitting each as `{...}`, emit `]`.
- Extract `serialize_body(const FonCollection&)` from the body of `serialize_to_string`. `serialize_to_string` becomes a wrapper that constructs the result string and calls `serialize_body`.
- `get_type_char`: two new branches return `TYPE_OBJECT` for the nested-collection variants.
- `parse_value`: when `type_char == 'o'`:
  - `data[0] == '{'` -> `parse_object(data, depth + 1)`.
  - `data[0] == '['` -> `parse_object_array(data, depth + 1)`.
  - otherwise -> throw `std::runtime_error`.
- New `find_closing_brace(std::string_view) -> size_t`: mirror of `find_closing_bracket` with brace counting and `in_string` tracking.
- New `parse_object(std::string_view, int depth) -> std::pair<std::shared_ptr<FonCollection>, size_t>`: validates `{`, finds the matching `}`, calls `parse_collection_body` on the body, wraps the result in a `shared_ptr`.
- New `parse_object_array(std::string_view, int depth) -> std::pair<std::vector<std::shared_ptr<FonCollection>>, size_t>`: same shape as `parse_array` but elements are objects.
- Extract `parse_collection_body(std::string_view, int depth) -> FonCollection` from `deserialize_line`. `deserialize_line` becomes a wrapper that calls `parse_collection_body(line, 0)`.
- All `parse_*` helpers take `int depth`. `parse_object` and `parse_object_array` check `depth > Fon::max_depth` on entry.
- Configuration: `static inline int max_depth = 64;` next to the existing `parallel_threshold`.

### `FON.Native/include/fon_export.h` and `src/fon_export.cpp`

New C ABI exports:

```c
FON_API int32_t fon_collection_add_collection(
    FonCollectionHandle parent,
    const char* key,
    FonCollectionHandle child,
    FonError* error);

FON_API FonCollectionHandle fon_collection_get_collection(
    FonCollectionHandle parent,
    const char* key,
    FonError* error);

FON_API int32_t fon_collection_add_collection_array(
    FonCollectionHandle parent,
    const char* key,
    const FonCollectionHandle* children,
    int64_t count,
    FonError* error);

FON_API int32_t fon_collection_get_collection_array(
    FonCollectionHandle parent,
    const char* key,
    FonCollectionHandle* buffer,
    int64_t buffer_size,
    int64_t* actual_size,
    FonError* error);

FON_API void fon_set_max_depth(int32_t depth);
```

Ownership semantics:

- `fon_collection_add_collection(parent, key, child)`: the parent takes ownership. After this call the `child` handle is invalidated; the caller must not use it again and must not call `fon_collection_free` on it.
- `fon_collection_add_collection_array(parent, key, children, count)`: the parent takes ownership of every handle in the array. All `children` handles are invalidated after the call.
- `fon_collection_get_collection` and `fon_collection_get_collection_array` return read-only borrows owned by the parent. The caller must not free them.

`fon_set_max_depth` follows the signature style of the existing `fon_set_raw_unpack` (no error parameter). Values less than 1 are silently clamped to 1.

### `FON.Native.Runtime/NativeBindings.cs`
Add `[DllImport]` declarations for the five new exports above. No other changes.

## Error handling

| Case | Managed | Native (over C ABI) |
|------|---------|---------------------|
| `o:` not followed by `{` or `[` | `FormatException` | `FonError{ code = FON_ERROR_PARSE_FAILED }` |
| Unmatched `{` (EOF before `}`) | `FormatException("Closing brace not found")` | `FonError{ code = FON_ERROR_PARSE_FAILED }` |
| Depth > `MaxDepth` | `FormatException("Maximum nesting depth exceeded ({N})")` | `FonError{ code = FON_ERROR_PARSE_FAILED }` |
| Invalid pair inside object (no `=`) | Same as today (loop terminates at the missing `=`) | Same as today |
| `MaxDepth` set to 0 or negative | `ArgumentOutOfRangeException` | Silently clamped to 1 (no error path on `fon_set_max_depth`) |

No new error codes are introduced in the C ABI.

Cycles in user data (a collection placed inside itself, directly or through nested values) are not detected. Serialization recurses until the call stack overflows. Documented in the `FonCollection` XML doc.

`MaxDepth` defaults to 64 in both implementations.

## Testing

### `FON.Tests/FON.Test/NestedObjectTests.cs` (new)

- Roundtrip:
  - One level of nesting.
  - Two and three levels of nesting.
  - Array of three objects with different keys (heterogeneous shapes).
  - Object inside an array inside an object.
  - Empty object `o:{}`.
  - Empty array `o:[]`.
- String values containing metacharacters inside nested objects:
  - `}`, `{`, `[`, `]`, `,`, `=` inside string values do not break parsing.
  - All standard escapes (`\n`, `\\`, `\"`) inside nested strings.
- Depth limits:
  - Nesting at exactly `MaxDepth` succeeds.
  - Nesting at `MaxDepth + 1` throws `FormatException`.
  - With `Fon.MaxDepth = 5`, a file at depth 6 throws.
  - Setting `Fon.MaxDepth` to 0 or a negative value throws `ArgumentOutOfRangeException`.
- Backward compatibility:
  - Existing fixtures without `o:` parse identically.
  - A mixed file with `o:` and existing types parses correctly.
- I/O strategy parity (parameterized):
  - Roundtrip with nesting through `SerializeToFileAsync`, `SerializeToFilePipelineAsync`, `SerializeToFileChunkedAsync`, `SerializeToFileAutoAsync` produces byte-identical output.
  - Same for `DeserializeFromFileAsync`, `DeserializeFromFileChunkedAsync`, `DeserializeFromFileAutoAsync`.
- Parallelism: a `FonDump` of 5000 nested records roundtrips correctly under `Parallel.For`.
- Disposal: nested `IDisposable` values (including `RawData` and nested `FonCollection`) are disposed when the parent is disposed; lists of disposables are walked.

### `FON.Tests/FON.Native.Test/NativeNestedTests.cs` (new)

- Same roundtrip scenarios driven through `NativeBindings` (`fon_collection_add_collection`, `fon_collection_get_collection`, the `_array` variants).
- `fon_set_max_depth(5)` followed by parsing a file of depth 6 returns `FonError{ code = FON_ERROR_PARSE_FAILED }`.
- After `fon_collection_add_collection`, the child handle is invalidated; this is documented and verified by a single test that does not call `fon_collection_free` on the child after handing it over.

### Cross-implementation roundtrip (`FON.Tests/FON.Native.Test/NativeFileTests.cs`)

- Managed writes a file with nesting; native reads it back via `fon_deserialize_from_file`; values match.
- Native writes; managed reads; values match.
- Fixtures cover: deep nesting, arrays of objects, strings with metacharacters.

### Out of scope

- Performance benchmarks for the new path.
- Fuzz testing the parser.

## Files touched

Managed:
- `FON/Core/Fon.cs`
- `FON/Core/FonSerialize.cs`
- `FON/Core/FonDeserialize.cs`
- `FON/Types/FonCollection.cs`
- `FON.Tests/FON.Test/NestedObjectTests.cs` (new)

Native:
- `FON.Native/include/fon_types.hpp`
- `FON.Native/include/fon.hpp`
- `FON.Native/include/fon_export.h`
- `FON.Native/src/fon_export.cpp`
- `FON.Native.Runtime/NativeBindings.cs`
- `FON.Tests/FON.Native.Test/NativeNestedTests.cs` (new)
- `FON.Tests/FON.Native.Test/NativeFileTests.cs` (extend with cross-impl tests)

Documentation:
- `README.md` (add `o` row to the type table, examples for nested objects and arrays of objects, mention of `MaxDepth`)
