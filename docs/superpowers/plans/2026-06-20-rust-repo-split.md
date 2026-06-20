# Rust Repo Split (FON.rust) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the FON algorithm into a standalone idiomatic Rust crate `fon` in a new local repo `FON.rust`, and reduce `FON.net/FON.Native` to a thin C-ABI shim (`fon_native` cdylib) that consumes `fon` as a git submodule.

**Architecture:** `fon` (FON.rust) is pure Rust — types + serialize + deserialize, no FFI, no global config. `fon_native` (FON.net/FON.Native) keeps the entire `extern "C"` surface and the ambient `fon_set_*` state, calling into `fon` via a path dependency on the submodule at `FON.Native/fon-rust`. Native binaries are built from source in FON.net CI. The only cross-repo link is the submodule gitlink (a pinned commit hash).

**Tech Stack:** Rust 2021 (rayon), cargo; .NET 10 / C# P/Invoke; GitHub Actions; git submodules.

## Global Constraints

- The cdylib MUST stay named `fon_native` (load-bearing for `DllImport("fon_native")` and every CI artifact path `fon_native.dll` / `libfon_native.so` / `libfon_native.dylib`).
- Version `0.2.1` appears verbatim in three files: `Directory.Build.props` `<VersionPrefix>` (unchanged), `FON.Native/Cargo.toml`, `FON.Native/fon-rust/Cargo.toml`.
- Wire format is unchanged — relocated code is the same code; behavior must be byte-identical.
- `fon` crate: `crate-type = ["lib"]`, edition `2021`, license MIT, no `extern "C"`, no `#[no_mangle]`, no global/ambient configuration.
- Submodule path is `FON.Native/fon-rust`; `.gitmodules` `url = https://github.com/FastObjectNotation/FON.rust.git`.
- This pass is LOCAL ONLY: commit locally in both repos, do NOT push. Later push order is FON.rust first, then FON.net.
- Do NOT modify: `FON.Native.Runtime/*`, `FON.Native.Platforms/*`, `FON/*`, `FON.Tests/*` (except verification runs), `FON.Native/FON.Native.csproj`, `FON.Native/_._`, `Directory.Build.props`.
- Local paths: FON.rust = `D:/Personal/VeyProjects/C#/FON/FON.rust`, FON.net = `D:/Personal/VeyProjects/C#/FON/FON.net`.
- Commit messages: plain imperative English, no AI attribution.

---

## File map

**FON.rust (new repo, currently empty dir):**
- `Cargo.toml` — crate `fon`, version 0.2.1, rayon dep, lib crate-type.
- `.gitignore` — `/target`.
- `src/types.rs` — moved verbatim from FON.net.
- `src/raw_data.rs` — moved; error type renamed.
- `src/serialize.rs` — moved; error type renamed.
- `src/error.rs` — moved; `FonNativeError` → `FonError`.
- `src/deserialize.rs` — moved; globals → `DeserializeOptions`; error renamed.
- `src/lib.rs` — NEW public API (module decls + re-exports).
- `tests/roundtrip.rs` — NEW cargo tests.
- `README.md`, `LICENSE`, `.github/workflows/ci.yml` — NEW.

**FON.net (existing repo):**
- `FON.Native/Cargo.toml` — rewritten as the shim (dep `fon = { path = "fon-rust" }`).
- `FON.Native/src/lib.rs` — rewritten over `fon::*` (keeps FFI globals + C ABI).
- `FON.Native/src/{types,serialize,deserialize,raw_data,error}.rs` — DELETED.
- `FON.Native/fon-rust/` — NEW git submodule → FON.rust.
- `.gitmodules` — NEW.
- `.github/workflows/publish.yml` — `submodules: recursive` + version guard.

---

## Task 1: Create the `fon` crate (compiles + lints clean)

**Files:**
- Create: `D:/Personal/VeyProjects/C#/FON/FON.rust/Cargo.toml`
- Create: `D:/Personal/VeyProjects/C#/FON/FON.rust/.gitignore`
- Create: `.../FON.rust/src/{lib,types,serialize,deserialize,raw_data,error}.rs`

**Interfaces:**
- Produces (public API other code relies on):
  - `fon::types::{FonCollection, FonDump, FonValue}` (and `pub const TYPE_*` in `types`).
  - `fon::error::FonError` (`enum { Parse(String), Write(String), InvalidArgument(String) }`, `impl Display`).
  - `fon::raw_data::RawData`.
  - `fon::serialize::{serialize_to_string(&FonCollection)->String, serialize_dump_lines(&FonDump,i32)->Vec<String>, serialize_dump_to_string(&FonDump,i32)->String, serialize_to_file(&FonDump,&Path,i32)->Result<(),FonError>}`.
  - `fon::deserialize::{DeserializeOptions{max_depth:i32,unpack_raw:bool}, deserialize_line(&[u8],&DeserializeOptions)->Result<FonCollection,FonError>, deserialize_dump_from_bytes(&[u8],i32,&DeserializeOptions)->Result<FonDump,FonError>, deserialize_from_file(&Path,i32,&DeserializeOptions)->Result<FonDump,FonError>}`.
  - All of the above re-exported at crate root via `fon::`.

- [ ] **Step 1: Init the repo and ignore the build dir**

Run in `D:/Personal/VeyProjects/C#/FON/FON.rust`:
```bash
git init
```
Create `.gitignore`:
```gitignore
/target
```

- [ ] **Step 2: Write `Cargo.toml`**

`D:/Personal/VeyProjects/C#/FON/FON.rust/Cargo.toml`:
```toml
[package]
name = "fon"
version = "0.2.1"
edition = "2021"
description = "Fast Object Notation - high-performance serialization core (Rust)"
license = "MIT"
repository = "https://github.com/FastObjectNotation/FON.rust"

[lib]
name = "fon"
path = "src/lib.rs"

[dependencies]
rayon = "1"

[profile.release]
opt-level = 3
lto = true
codegen-units = 1
```

- [ ] **Step 3: Copy `types.rs` verbatim**

Copy `FON.net/FON.Native/src/types.rs` → `FON.rust/src/types.rs` with NO changes (it only `use crate::raw_data::RawData;` and defines `FonValue`, `FonCollection`, `FonDump`, `TYPE_*`).

- [ ] **Step 4: Copy `error.rs` and rename the type**

`FON.rust/src/error.rs` — same as source but `FonNativeError` → `FonError` (4 occurrences: the `enum` name and 3 in the `Display` impl):
```rust
use std::fmt;


pub enum FonError {
    Parse(String),
    Write(String),
    InvalidArgument(String),
}


impl fmt::Display for FonError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            FonError::Parse(m) => write!(f, "{}", m),
            FonError::Write(m) => write!(f, "{}", m),
            FonError::InvalidArgument(m) => write!(f, "{}", m),
        }
    }
}
```

- [ ] **Step 5: Copy `raw_data.rs` with error rename**

Copy `raw_data.rs` verbatim EXCEPT: `use crate::error::FonNativeError;` → `use crate::error::FonError;`, and both `FonNativeError::Parse(...)` → `FonError::Parse(...)` (2 occurrences in `unpack`). Signature `pub fn unpack(&mut self) -> Result<&mut Self, FonError>`.

- [ ] **Step 6: Copy `serialize.rs` with error rename**

Copy `serialize.rs` verbatim EXCEPT: `use crate::error::FonNativeError;` → `use crate::error::FonError;`, and every `FonNativeError::Write(...)` → `FonError::Write(...)` (3 occurrences in `serialize_to_file`). `serialize_to_file` returns `Result<(), FonError>`.

- [ ] **Step 7: Copy `deserialize.rs` and replace globals with `DeserializeOptions`**

Start from the source file, then apply exactly these edits:

(a) Imports — drop the atomics, keep the rest:
```rust
// REMOVE: use std::sync::atomic::{AtomicBool, AtomicI32, Ordering};
use std::fs;
use std::path::Path;
use std::str::FromStr;

use rayon::prelude::*;

use crate::error::FonError;            // was FonNativeError
use crate::raw_data::RawData;
use crate::types::{
    FonCollection, FonDump, FonValue, TYPE_BOOL, TYPE_BYTE, TYPE_DOUBLE, TYPE_FLOAT, TYPE_INT,
    TYPE_LONG, TYPE_OBJECT, TYPE_RAW, TYPE_SHORT, TYPE_STRING, TYPE_UINT, TYPE_ULONG,
};
```

(b) Replace the two `pub static` globals with the options struct:
```rust
pub struct DeserializeOptions {
    pub max_depth: i32,
    pub unpack_raw: bool,
}

impl Default for DeserializeOptions {
    fn default() -> Self {
        Self { max_depth: 64, unpack_raw: false }
    }
}
```

(c) Rename `FonNativeError` → `FonError` everywhere in the file (all `Result<_, FonNativeError>` and `FonNativeError::Parse/Write` constructions).

(d) Thread `opts: &DeserializeOptions` through; the changed signatures and bodies:
```rust
pub fn deserialize_from_file(
    path: &Path,
    max_threads: i32,
    opts: &DeserializeOptions,
) -> Result<FonDump, FonError> {
    let content = fs::read(path)
        .map_err(|e| FonError::Parse(format!("Failed to open file: {}", e)))?;
    deserialize_dump_from_bytes(&content, max_threads, opts)
}

pub fn deserialize_dump_from_bytes(
    content: &[u8],
    _max_threads: i32,
    opts: &DeserializeOptions,
) -> Result<FonDump, FonError> {
    let lines = split_lines(content);
    let collections: Vec<Result<FonCollection, FonError>> = lines
        .par_iter()
        .map(|line| {
            if line.is_empty() {
                Ok(FonCollection::new())
            } else {
                deserialize_line(line, opts)
            }
        })
        .collect();
    let mut dump = FonDump::with_capacity(lines.len());
    for (i, c) in collections.into_iter().enumerate() {
        let c = c?;
        if c.len() > 0 {
            dump.add(i as u64, c);
        }
    }
    Ok(dump)
}

pub fn deserialize_line(line: &[u8], opts: &DeserializeOptions) -> Result<FonCollection, FonError> {
    parse_collection_body(line, 0, opts)
}
```

(e) Change the private helpers to take `opts: &DeserializeOptions` instead of `max_depth: i32`, replacing `max_depth` reads with `opts.max_depth` and the raw-unpack global read with `opts.unpack_raw`:

- `fn parse_collection_body(line: &[u8], depth: i32, opts: &DeserializeOptions) -> Result<FonCollection, FonError>` — its internal `parse_value(remaining, type_char, depth, max_depth)?` call becomes `parse_value(remaining, type_char, depth, opts)?`.
- `fn parse_value(data: &[u8], type_char: u8, depth: i32, opts: &DeserializeOptions) -> Result<(FonValue, usize), FonError>`:
  - object branch: `parse_object(data, depth + 1, opts)?` and `parse_object_array(data, depth + 1, opts)?`.
  - raw branch: replace
    ```rust
    if DESERIALIZE_RAW_UNPACK.load(Ordering::Relaxed) {
        raw.unpack()?;
    }
    ```
    with
    ```rust
    if opts.unpack_raw {
        raw.unpack()?;
    }
    ```
- `fn parse_object(data: &[u8], depth: i32, opts: &DeserializeOptions) -> Result<(Box<FonCollection>, usize), FonError>` — guard becomes `if depth > opts.max_depth`; inner call `parse_collection_body(body, depth, opts)?`.
- `fn parse_object_array(data: &[u8], depth: i32, opts: &DeserializeOptions) -> Result<(Vec<Box<FonCollection>>, usize), FonError>` — guard becomes `if depth > opts.max_depth`; inner call `parse_object(remaining, depth, opts)?`.

`split_lines`, `parse_num`, `parse_number_array`, `parse_string`, `find_value_end`, `find_closing_bracket`, `find_closing_brace`, `find_byte` are unchanged except the `FonNativeError` → `FonError` rename.

- [ ] **Step 8: Write the public API `src/lib.rs`**

`FON.rust/src/lib.rs`:
```rust
//! FON (Fast Object Notation) — high-performance serialization core.
//!
//! Idiomatic Rust library: types, serialization, and deserialization for the
//! FON wire format. This crate is consumer-agnostic; FFI / C-ABI bindings live
//! in the consuming projects (for example FON.net).

pub mod deserialize;
pub mod error;
pub mod raw_data;
pub mod serialize;
pub mod types;

pub use deserialize::{
    deserialize_dump_from_bytes, deserialize_from_file, deserialize_line, DeserializeOptions,
};
pub use error::FonError;
pub use raw_data::RawData;
pub use serialize::{
    serialize_dump_lines, serialize_dump_to_string, serialize_to_file, serialize_to_string,
};
pub use types::{FonCollection, FonDump, FonValue};
```

- [ ] **Step 9: Build and lint**

Run in `FON.rust`:
```bash
cargo build
cargo clippy --all-targets -- -D warnings
```
Expected: both succeed (clippy clean). Do NOT run `cargo fmt` — the project house
style uses two blank lines between top-level members and methods (per the user's
global style rules), which rustfmt's defaults would collapse. Maintain that
spacing by hand; match the spacing of the original FON.net source files.

- [ ] **Step 10: Commit**

Run in `FON.rust`:
```bash
git add -A
git commit -m "Add fon crate: pure Rust FON core (types, serialize, deserialize, raw_data)"
```

---

## Task 2: Cargo roundtrip tests for `fon`

**Files:**
- Create: `D:/Personal/VeyProjects/C#/FON/FON.rust/tests/roundtrip.rs`

**Interfaces:**
- Consumes: `fon::{serialize_to_string, deserialize_line, deserialize_dump_from_bytes, DeserializeOptions}`, `fon::types::{FonCollection, FonValue}` (from Task 1).

- [ ] **Step 1: Write the tests**

`FON.rust/tests/roundtrip.rs`:
```rust
use fon::types::{FonCollection, FonValue};
use fon::{deserialize_dump_from_bytes, deserialize_line, serialize_to_string, DeserializeOptions};

fn roundtrip(c: &FonCollection) -> FonCollection {
    let s = serialize_to_string(c);
    deserialize_line(s.as_bytes(), &DeserializeOptions::default()).unwrap()
}

#[test]
fn primitives_roundtrip() {
    let mut c = FonCollection::new();
    c.add("id".into(), FonValue::Int(42));
    c.add("name".into(), FonValue::String("Test".into()));
    c.add("active".into(), FonValue::Bool(true));
    let back = roundtrip(&c);
    assert!(matches!(back.get("id"), Some(FonValue::Int(42))));
    assert!(matches!(back.get("active"), Some(FonValue::Bool(true))));
    match back.get("name") {
        Some(FonValue::String(s)) => assert_eq!(s.as_str(), "Test"),
        _ => panic!("expected string value for name"),
    }
}

#[test]
fn int_array_roundtrip() {
    let mut c = FonCollection::new();
    c.add("xs".into(), FonValue::IntArray(vec![1, 2, 3]));
    let back = roundtrip(&c);
    match back.get("xs") {
        Some(FonValue::IntArray(v)) => assert_eq!(v, &vec![1, 2, 3]),
        _ => panic!("expected int array"),
    }
}

#[test]
fn nested_object_roundtrip() {
    let mut addr = FonCollection::new();
    addr.add("zip".into(), FonValue::Int(10001));
    let mut c = FonCollection::new();
    c.add("addr".into(), FonValue::Object(Box::new(addr)));
    let back = roundtrip(&c);
    match back.get("addr") {
        Some(FonValue::Object(b)) => assert!(matches!(b.get("zip"), Some(FonValue::Int(10001)))),
        _ => panic!("expected nested object"),
    }
}

#[test]
fn max_depth_enforced() {
    let s = "a=o:{b=o:{c=i:1}}";
    let shallow = DeserializeOptions { max_depth: 1, unpack_raw: false };
    assert!(deserialize_line(s.as_bytes(), &shallow).is_err());
    assert!(deserialize_line(s.as_bytes(), &DeserializeOptions::default()).is_ok());
}

#[test]
fn bom_is_stripped_in_dump() {
    let mut c = FonCollection::new();
    c.add("id".into(), FonValue::Int(7));
    let line = serialize_to_string(&c);
    let mut bytes = vec![0xEF, 0xBB, 0xBF];
    bytes.extend_from_slice(line.as_bytes());
    let dump = deserialize_dump_from_bytes(&bytes, 0, &DeserializeOptions::default()).unwrap();
    let first = dump.get(0).expect("record 0");
    assert!(matches!(first.get("id"), Some(FonValue::Int(7))));
}
```

- [ ] **Step 2: Run tests, verify they pass**

Run in `FON.rust`:
```bash
cargo test
```
Expected: 5 tests pass. (If `type_char` is not callable in the panic arm, simplify that arm to `_ => panic!("unexpected")` — `type_char` is `pub fn type_char(&self) -> u8` on `FonValue`, so it should compile.)

- [ ] **Step 3: Commit**

Run in `FON.rust`:
```bash
git add -A
git commit -m "Add roundtrip tests for fon crate"
```

---

## Task 3: FON.rust repo polish (README, LICENSE, CI)

**Files:**
- Create: `FON.rust/README.md`, `FON.rust/LICENSE`, `FON.rust/.github/workflows/ci.yml`

- [ ] **Step 1: Copy the license from FON.net**

Copy `D:/Personal/VeyProjects/C#/FON/FON.net/LICENSE` → `D:/Personal/VeyProjects/C#/FON/FON.rust/LICENSE` verbatim.

- [ ] **Step 2: Write `README.md`**

`FON.rust/README.md`:
```markdown
# FON (Rust)

Fast Object Notation — the high-performance serialization core, implemented as an
idiomatic Rust library. A fast, human-readable key-value alternative to JSON.

This crate is consumer-agnostic: it exposes a normal Rust API and knows nothing
about FFI. C-ABI bindings for other languages (for example .NET via `FON.net`)
wrap this crate in their own repositories.

Part of the [FastObjectNotation](https://github.com/FastObjectNotation) family.

## Usage

```rust
use fon::types::{FonCollection, FonValue};
use fon::{serialize_to_string, deserialize_line, DeserializeOptions};

let mut c = FonCollection::new();
c.add("id".into(), FonValue::Int(42));
c.add("name".into(), FonValue::String("Test".into()));

let text = serialize_to_string(&c);
let back = deserialize_line(text.as_bytes(), &DeserializeOptions::default()).unwrap();
```

## License

MIT
```

- [ ] **Step 3: Write the CI workflow**

`FON.rust/.github/workflows/ci.yml`:
```yaml
name: CI

on:
  push:
    branches: [main, master]
  pull_request:

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
        with:
          components: rustfmt, clippy
      - uses: Swatinem/rust-cache@v2
      - name: Clippy
        run: cargo clippy --all-targets -- -D warnings
      - name: Test
        run: cargo test --all
```

(No `cargo fmt --check` step: the house style — two blank lines between top-level
members — diverges from rustfmt defaults and is maintained by hand.)

- [ ] **Step 4: Commit**

Run in `FON.rust`:
```bash
git add -A
git commit -m "Add README, LICENSE, gitignore, and CI for fon crate"
```

---

## Task 4: Reduce FON.Native to the `fon_native` shim over the submodule

**Files:**
- Create: `FON.net/.gitmodules`, `FON.net/FON.Native/fon-rust/` (submodule)
- Modify: `FON.net/FON.Native/Cargo.toml` (full rewrite)
- Modify: `FON.net/FON.Native/src/lib.rs`
- Delete: `FON.net/FON.Native/src/{types,serialize,deserialize,raw_data,error}.rs`

**Interfaces:**
- Consumes: the `fon::*` public API from Task 1.
- Produces: the unchanged C ABI (`fon_version`, `fon_set_*`, `fon_dump_*`, `fon_collection_*`, `fon_serialize_*`, `fon_deserialize_*`) — same symbols, same signatures as today (NativeBindings.cs must keep working untouched).

- [ ] **Step 1: Add the submodule from the local FON.rust repo**

Run in `FON.net`:
```bash
git -c protocol.file.allow=always submodule add "D:/Personal/VeyProjects/C#/FON/FON.rust" FON.Native/fon-rust
```
If git rejects the path because of the `#` (treats it as a URL fragment), use the encoded file URL instead:
```bash
git -c protocol.file.allow=always submodule add "file:///D:/Personal/VeyProjects/C%23/FON/FON.rust" FON.Native/fon-rust
```
Then point `.gitmodules` at the eventual GitHub URL and sync:
```bash
git config -f .gitmodules submodule.FON.Native/fon-rust.url https://github.com/FastObjectNotation/FON.rust.git
git submodule sync
```
Verify:
```bash
git submodule status            # shows a SHA and FON.Native/fon-rust
cat .gitmodules                 # url = https://github.com/FastObjectNotation/FON.rust.git
ls FON.Native/fon-rust/src      # lib.rs, types.rs, ... present
```

- [ ] **Step 2: Rewrite `FON.Native/Cargo.toml` as the shim**

`FON.net/FON.Native/Cargo.toml`:
```toml
[package]
name = "fon_native"
version = "0.2.1"
edition = "2021"
publish = false

[lib]
name = "fon_native"
crate-type = ["cdylib"]
path = "src/lib.rs"

[dependencies]
fon = { path = "fon-rust" }

[profile.release]
opt-level = 3
lto = true
codegen-units = 1
panic = "abort"
strip = "debuginfo"
```

- [ ] **Step 3: Delete the moved Rust modules**

Run in `FON.net`:
```bash
git rm FON.Native/src/types.rs FON.Native/src/serialize.rs FON.Native/src/deserialize.rs FON.Native/src/raw_data.rs FON.Native/src/error.rs
```
(`FON.Native/src/lib.rs` stays — it is rewritten next.)

- [ ] **Step 4: Rewrite `FON.Native/src/lib.rs` to consume `fon`**

Apply these edits to the existing `FON.Native/src/lib.rs`:

(a) Replace the module declarations and crate-internal imports:
```rust
// REMOVE these five lines:
//   mod deserialize;
//   mod error;
//   mod raw_data;
//   mod serialize;
//   mod types;
//
// REMOVE the old crate-internal `use crate::...` imports and REPLACE with:
use fon::deserialize::{deserialize_dump_from_bytes, deserialize_from_file, deserialize_line};
use fon::serialize::{serialize_dump_to_string, serialize_to_file, serialize_to_string};
use fon::types::{FonCollection, FonDump, FonValue};
use fon::{DeserializeOptions, FonError as FonLibError};
```

(b) Keep the FFI-side ambient config here (these used to live in `deserialize.rs`). Add near the top of the file:
```rust
use std::sync::atomic::{AtomicBool, AtomicI32, Ordering};

static DESERIALIZE_RAW_UNPACK: AtomicBool = AtomicBool::new(false);
static MAX_DEPTH: AtomicI32 = AtomicI32::new(64);
```
The bodies of `fon_set_raw_unpack` and `fon_set_max_depth` are unchanged — they still `.store()` into these now-local statics.

(c) Change `err_code` and `cstr_to_str` to the aliased library error:
```rust
fn err_code(e: &FonLibError) -> i32 {
    match e {
        FonLibError::Parse(_) => FON_ERROR_PARSE_FAILED,
        FonLibError::Write(_) => FON_ERROR_WRITE_FAILED,
        FonLibError::InvalidArgument(_) => FON_ERROR_INVALID_ARGUMENT,
    }
}

unsafe fn cstr_to_str<'a>(p: *const c_char) -> Result<&'a str, FonLibError> {
    if p.is_null() {
        return Err(FonLibError::InvalidArgument("null pointer".into()));
    }
    CStr::from_ptr(p)
        .to_str()
        .map_err(|_| FonLibError::InvalidArgument("invalid UTF-8".into()))
}
```
(The `#[repr(C)] struct FonError`, `set_error`, and the `FON_OK`/`FON_ERROR_*` consts are UNCHANGED — `FonError` here is the C ABI struct, not the library enum.)

(d) Auto-report the version from Cargo:
```rust
fn version_cstr() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), "\0").as_ptr() as *const c_char
}
```

(e) Build `DeserializeOptions` at each deserialize entry point and pass it in:
- `fon_deserialize_from_file`: replace `deserialize_from_file(&PathBuf::from(path_str), max_threads)` with
  ```rust
  let opts = DeserializeOptions {
      max_depth: MAX_DEPTH.load(Ordering::Relaxed),
      unpack_raw: DESERIALIZE_RAW_UNPACK.load(Ordering::Relaxed),
  };
  deserialize_from_file(&PathBuf::from(path_str), max_threads, &opts)
  ```
- `fon_deserialize_dump_from_buffer`: replace `deserialize_dump_from_bytes(bytes, max_threads)` with the same `opts` built as above, then `deserialize_dump_from_bytes(bytes, max_threads, &opts)`.
- `fon_deserialize_collection_from_buffer`: replace
  ```rust
  let max_depth = MAX_DEPTH.load(Ordering::Relaxed);
  match deserialize_line(bytes, max_depth) {
  ```
  with
  ```rust
  let opts = DeserializeOptions {
      max_depth: MAX_DEPTH.load(Ordering::Relaxed),
      unpack_raw: DESERIALIZE_RAW_UNPACK.load(Ordering::Relaxed),
  };
  match deserialize_line(bytes, &opts) {
  ```

All other exports (serialize, memory management, collection add/get) keep their bodies; they now resolve `FonCollection`/`FonDump`/`FonValue` from `fon::types` via the new `use`.

- [ ] **Step 5: Build the cdylib from the submodule**

Run in `FON.net`:
```bash
cargo build --release --manifest-path FON.Native/Cargo.toml
```
Expected: compiles; produces `FON.Native/target/release/fon_native.dll` (Windows). If a `FonError` name collision error appears, confirm the library enum is imported as `FonLibError` and the `#[repr(C)] struct FonError` is left named `FonError`.

- [ ] **Step 6: Commit**

Run in `FON.net`:
```bash
git add .gitmodules FON.Native/fon-rust FON.Native/Cargo.toml FON.Native/src/lib.rs
git add -u FON.Native/src
git commit -m "Consume fon via submodule; reduce FON.Native to the FFI shim"
```

---

## Task 5: Wire the submodule into the release workflow + version guard

**Files:**
- Modify: `FON.net/.github/workflows/publish.yml`

- [ ] **Step 1: Check out submodules in every job that builds or reads the crate**

In `publish.yml`, for the `check-version` job and ALL eight native build jobs (`build-native-windows-x64`, `build-native-windows-arm64`, `build-native-linux-x64`, `build-native-linux-arm64`, `build-native-linux-musl-x64`, `build-native-linux-musl-arm64`, `build-native-macos-x64`, `build-native-macos-arm64`), change each:
```yaml
    - uses: actions/checkout@v4
```
to:
```yaml
    - uses: actions/checkout@v4
      with:
        submodules: recursive
```
(The `publish` job consumes downloaded artifacts and does not need the submodule; leaving its checkout as-is is fine.)

- [ ] **Step 2: Add the version guard to `check-version`**

In the `check-version` job, immediately after the `Extract version from Directory.Build.props` step, add:
```yaml
    - name: Verify native crate versions match product version
      run: |
        VERSION="${{ steps.get_version.outputs.version }}"
        SHIM=$(grep -m1 -oP '(?<=^version = ")[^"]+' FON.Native/Cargo.toml)
        LIB=$(grep -m1 -oP '(?<=^version = ")[^"]+' FON.Native/fon-rust/Cargo.toml)
        echo "product=$VERSION shim=$SHIM lib=$LIB"
        if [ "$SHIM" != "$VERSION" ] || [ "$LIB" != "$VERSION" ]; then
          echo "::error::Native version mismatch (product=$VERSION shim=$SHIM lib=$LIB)"
          exit 1
        fi
```

- [ ] **Step 3: Sanity-check the edits**

Run in `FON.net`:
```bash
grep -c "submodules: recursive" .github/workflows/publish.yml   # expect 9
grep -n "Verify native crate versions" .github/workflows/publish.yml
```
Expected: count is 9 (check-version + 8 build jobs); the guard step is present.

- [ ] **Step 4: Commit**

Run in `FON.net`:
```bash
git add .github/workflows/publish.yml
git commit -m "Wire fon-rust submodule into release workflow with version guard"
```

---

## Task 6: End-to-end verification (relocation preserved behavior)

**Files:** none modified (verification only; commit only if a fix is required).

**Interfaces:**
- Consumes: the built `fon_native` cdylib + the unchanged C# `FON.Native.Test` suite, which is the regression net.

- [ ] **Step 1: Build the native binary (release)**

Run in `FON.net`:
```bash
cargo build --release --manifest-path FON.Native/Cargo.toml
```
Expected: `FON.Native/target/release/fon_native.dll` exists. (The `CopyNativeLib` MSBuild target copies it into the test output automatically after `dotnet build`.)

- [ ] **Step 2: Run the .NET test suites**

Run in `FON.net`:
```bash
dotnet test --configuration Release
```
Expected: PASS for both `FON.Test` and `FON.Native.Test`. Key native checks that must pass:
- `NativeAvailabilityTests.NativeLibrary_IsAvailable` and `...HasVersion` (verifies `fon_version()` → `"0.2.1"`).
- `NativeNestedTests` (nested-object add/get/roundtrip through the shim).
- `NativeFileTests` cross-impl (managed writes / native reads and vice versa).
- `NativeSerializationTests`, `NativeBufferTests` (buffer serialize/deserialize, `fon_set_raw_unpack` / `fon_set_max_depth` behavior threaded through `DeserializeOptions`).

If a native test fails, debug the shim/options threading (most likely `fon_deserialize_collection_from_buffer` not passing `unpack_raw`, or a missed `FonError`→`FonLibError` rename) before proceeding. Do not edit `fon` to match a bug — the wire format must be identical.

- [ ] **Step 3: Confirm clean trees and the gitlink**

Run:
```bash
# FON.net
git -C "D:/Personal/VeyProjects/C#/FON/FON.net" status -s
git -C "D:/Personal/VeyProjects/C#/FON/FON.net" submodule status
# FON.rust
git -C "D:/Personal/VeyProjects/C#/FON/FON.rust" status -s
git -C "D:/Personal/VeyProjects/C#/FON/FON.rust" log --oneline
```
Expected: FON.net working tree clean except the two pre-existing `docs/superpowers/*` deletions (untouched); `submodule status` shows the FON.rust HEAD SHA; FON.rust has 3 commits and a clean tree. **No push is performed.**

---

## Post-plan: handing off (for the user, when ready to publish)

Not part of execution. When the user approves publishing:
1. Push `FON.rust` to `https://github.com/FastObjectNotation/FON.rust.git` (publishes the pinned SHA).
2. Push `FON.net` (whose gitlink references that SHA).
3. The FON.net `publish.yml` run will check out the submodule, build native binaries from source, run the version guard, and pack/publish as before.
