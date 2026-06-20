# Split the Rust native implementation into its own repository (FON.rust)

**Status:** Approved
**Date:** 2026-06-20

## Problem

`FON.Native/` in the `FON.net` repository currently mixes two concerns in one
directory: the full Rust implementation of FON (the `fon_native` crate:
`Cargo.toml` + `src/{lib,types,serialize,deserialize,raw_data,error}.rs`) and a
C# meta-package project (`FON.Native.csproj`). The Rust code is both the FON
algorithm *and* the C ABI (FFI) layer, welded together.

FON is being developed as a family of per-language implementations, each in its
own repository under the `FastObjectNotation` org (`FON.net`, `FON.rust`,
`FON.python`, `FON.swift`, `FON.js`). The Rust implementation should be a
standalone, idiomatic Rust library that other consumers can use directly — and
that does not know or care that it is also consumed over FFI by the .NET
bindings.

## Goal

Extract the FON algorithm into a standalone Rust library crate `fon` living in a
new repository `FastObjectNotation/FON.rust`. Keep the C ABI / FFI shim (the
"handles") on the .NET side, inside `FON.net/FON.Native`, consuming `fon` as a
git submodule. The split must:

- Make `fon` a clean, idiomatic Rust library: no `extern "C"`, no `#[no_mangle]`,
  no ambient global configuration, no awareness of any FFI consumer.
- Preserve the existing wire format byte-for-byte (the relocated code is the same
  code).
- Preserve the native binary name `fon_native` and the entire C ABI surface, so
  the C# P/Invoke bindings (`NativeBindings.cs`) and the platform packaging are
  **unchanged**.
- Keep versions synchronized across the FON family via the submodule commit hash
  plus a CI guard.
- Build native binaries from source in `FON.net` CI (no prebuilt-binary
  distribution from `FON.rust`).

## Non-goals

- Publishing `fon` to crates.io. (Possible later; out of scope here.)
- Merging `FON.Native.Runtime` (C# P/Invoke) into `FON.Native`. It stays a
  separate project and NuGet package.
- Any change to the FON wire format, the managed C# implementation, the platform
  packaging projects, or the other language ports.
- Cleaning up the stale, prunable `.worktrees/` directories.
- Changing the `max_threads` semantics (today native serialize/deserialize accept
  the parameter but rely on the global rayon pool; this behavior is preserved).

## Target topology

```
FON.rust  (FastObjectNotation/FON.rust)        FON.net  (FastObjectNotation/FON.net)
  crate `fon` — pure Rust library                FON.Native/
  ├─ Cargo.toml      (crate-type = lib)            ├─ Cargo.toml          shim crate `fon_native` (cdylib)
  ├─ src/lib.rs      (public API)                  ├─ src/lib.rs          all extern "C" + FonError + handles
  ├─ src/types.rs                                  ├─ fon-rust/           git submodule -> FON.rust (pinned hash)
  ├─ src/serialize.rs                              ├─ FON.Native.csproj   meta-package  (UNCHANGED)
  ├─ src/deserialize.rs                            └─ _._                 (UNCHANGED)
  ├─ src/raw_data.rs                             FON.Native.Runtime/      C# P/Invoke   (UNCHANGED)
  ├─ src/error.rs                                FON.Native.Platforms/*   packaging     (UNCHANGED)
  ├─ tests/roundtrip.rs                          FON/                     managed core  (UNCHANGED)
  ├─ README.md / LICENSE / .gitignore            FON.Tests/               C# tests      (UNCHANGED)
  └─ .github/workflows/ci.yml
```

The only coupling between the two repositories is the git submodule **gitlink**:
`FON.net` records an exact commit SHA of `FON.rust`. `FON.net` always builds
against that fixed commit — deterministic and reproducible. The `fon` version is
a property of the pinned commit; there is no separate cross-repo version channel.

## FON.rust — the `fon` crate

### Module moves

`types.rs`, `serialize.rs`, `deserialize.rs`, `raw_data.rs`, `error.rs` move
verbatim from `FON.net/FON.Native/src/` into `FON.rust/src/`, except for the two
edits below.

### Edit 1 — remove ambient globals (the one real code change)

Today `deserialize.rs` holds:

```rust
pub static DESERIALIZE_RAW_UNPACK: AtomicBool = AtomicBool::new(false);
pub static MAX_DEPTH: AtomicI32 = AtomicI32::new(64);
```

These are FFI ergonomics (set by `fon_set_raw_unpack` / `fon_set_max_depth`) and
do not belong in a pure library. Replace them with an explicit options struct:

```rust
pub struct DeserializeOptions {
    pub max_depth: i32,   // default 64
    pub unpack_raw: bool, // default false
}
impl Default for DeserializeOptions { /* 64, false */ }
```

Thread `&DeserializeOptions` through the deserialize call chain
(`deserialize_dump_from_bytes`, `deserialize_line`, `parse_collection_body`,
`parse_value`, `parse_object`, `parse_object_array`). `parse_value` reads
`opts.unpack_raw` instead of the global; depth checks read `opts.max_depth`. The
ambient `AtomicBool`/`AtomicI32` move to the FFI shim (see below), which builds a
`DeserializeOptions` per call.

### Edit 2 — rename the error type

`FonNativeError` -> `FonError`. It is the library's error, not a "native" error.
Variants unchanged: `Parse(String)`, `Write(String)`, `InvalidArgument(String)`.

### Public API (`src/lib.rs`)

```rust
pub mod types; pub mod serialize; pub mod deserialize; pub mod raw_data; pub mod error;

pub use error::FonError;
pub use types::{FonCollection, FonDump, FonValue};
pub use raw_data::RawData;
pub use deserialize::DeserializeOptions;
pub use serialize::{serialize_to_string, serialize_dump_to_string, serialize_dump_lines, serialize_to_file};
pub use deserialize::{deserialize_line, deserialize_dump_from_bytes, deserialize_from_file};
```

Signatures after the refactor:

```rust
fn serialize_to_string(c: &FonCollection) -> String;
fn serialize_dump_to_string(d: &FonDump, threads: i32) -> String;
fn serialize_to_file(d: &FonDump, path: &Path, threads: i32) -> Result<(), FonError>;
fn deserialize_line(line: &[u8], opts: &DeserializeOptions) -> Result<FonCollection, FonError>;
fn deserialize_dump_from_bytes(bytes: &[u8], threads: i32, opts: &DeserializeOptions) -> Result<FonDump, FonError>;
fn deserialize_from_file(path: &Path, threads: i32, opts: &DeserializeOptions) -> Result<FonDump, FonError>;
```

### Cargo.toml

```toml
[package]
name = "fon"
version = "0.2.1"           # synchronized with the FON family (Directory.Build.props)
edition = "2021"
description = "Fast Object Notation - high-performance serialization core (Rust)"
license = "MIT"
repository = "https://github.com/FastObjectNotation/FON.rust"

[lib]
name = "fon"
path = "src/lib.rs"          # default crate-type = ["lib"] (rlib)

[dependencies]
rayon = "1"

[profile.release]
opt-level = 3
lto = true
codegen-units = 1
# NOTE: no panic = "abort" here — the crate's own tests need unwind. When built
# as a dependency of the cdylib shim, the shim's release profile (panic = abort)
# governs the whole graph.
```

### Repo scaffolding

- `tests/roundtrip.rs` — new cargo integration tests (the crate has none today):
  primitive/array/nested-object/raw roundtrips, `max_depth` boundary, UTF-8 BOM
  stripping.
- `README.md` — Rust-focused: what FON is, a usage snippet, link to the family.
- `LICENSE` — MIT (copied from `FON.net`).
- `.gitignore` — `/target`. `Cargo.lock` is committed (reproducible CI).
- `.github/workflows/ci.yml` — see CI section.

## FON.net — the `fon_native` FFI shim

`FON.Native/src/lib.rs` keeps its entire role as the C ABI. Changes:

- Drop `mod deserialize; mod error; mod raw_data; mod serialize; mod types;`.
- `use fon::{FonCollection, FonDump, FonValue, DeserializeOptions, ...}` and the
  `fon` serialize/deserialize free functions. **Name clash:** the shim already
  defines a `#[repr(C)]` struct `FonError` (the C ABI out-parameter, name is
  load-bearing) — import the library enum aliased, `use fon::FonError as
  FonLibError;`, to avoid colliding with it.
- Keep the ambient setters' backing state **here** (this is FFI-specific):
  ```rust
  static MAX_DEPTH: AtomicI32 = AtomicI32::new(64);
  static DESERIALIZE_RAW_UNPACK: AtomicBool = AtomicBool::new(false);
  ```
  `fon_set_max_depth` / `fon_set_raw_unpack` write them; each deserialize export
  builds `DeserializeOptions { max_depth: MAX_DEPTH.load(..), unpack_raw: ... }`
  and passes it into `fon`. External C ABI behavior is unchanged.
- `err_code(&FonLibError)` mapping (library error -> C result code) stays here
  (FFI concern); the `#[repr(C)] FonError` struct and `set_error` are unchanged.
- `version_cstr()` returns `concat!(env!("CARGO_PKG_VERSION"), "\0")` instead of
  the hard-coded `"1.0.0"`. This fixes the current version drift and makes
  `fon_version()` auto-report the shim's Cargo version.

`FON.Native/Cargo.toml`:

```toml
[package]
name = "fon_native"
version = "0.2.1"
edition = "2021"
publish = false

[lib]
name = "fon_native"          # MUST stay "fon_native": DllImport + artifact names
crate-type = ["cdylib"]
path = "src/lib.rs"

[dependencies]
fon = { path = "fon-rust" }  # the git submodule; rayon arrives transitively

[profile.release]
opt-level = 3
lto = true
codegen-units = 1
panic = "abort"
strip = "debuginfo"
```

`src/{types,serialize,deserialize,raw_data,error}.rs` are **deleted** from
`FON.Native` (now sourced from the submodule).

Unchanged in `FON.net`: `FON.Native/FON.Native.csproj`, `_._`,
`FON.Native.Runtime/*` (incl. `NativeBindings.cs`, `NativeApi.cs`,
`NativeLoader.cs`), `FON.Native.Platforms/*`, `FON/*`, `FON.Tests/*`,
`Directory.Build.props` (stays `0.2.1`).

## Version synchronization

- Single value `0.2.1` across: `Directory.Build.props` `<VersionPrefix>`,
  `FON.Native/Cargo.toml`, and `fon-rust/Cargo.toml`.
- `fon_version()` derives from `env!("CARGO_PKG_VERSION")` (one Rust-side source
  of truth = the shim's `Cargo.toml`).
- Release flow: commit the new version in `FON.rust`; in `FON.net` move the
  submodule gitlink to that SHA and bump `Directory.Build.props` +
  `FON.Native/Cargo.toml` to match.
- CI guard (in the `check-version` job, after checking out submodules): read the
  `version = "..."` line from `FON.Native/Cargo.toml` and
  `FON.Native/fon-rust/Cargo.toml`, compare both to the extracted
  `<VersionPrefix>`, fail the build on any mismatch. Because the submodule is
  pinned by hash, this read is deterministic.

## CI

### FON.net `publish.yml`

- Add `with: { submodules: recursive }` to `actions/checkout@v4` in the
  `check-version` job (needed for the version guard) and all eight native build
  jobs (needed so `cargo build` can resolve the `fon` path dependency).
- Add the version-guard step described above to `check-version`.
- Everything else is unchanged: build command stays
  `cargo build --release --manifest-path FON.Native/Cargo.toml [--target ...]`;
  artifact paths stay `fon_native.dll` / `libfon_native.so` / `libfon_native.dylib`;
  the `publish` job (pack/push) is untouched because it consumes downloaded
  artifacts, not cargo.

### FON.rust `ci.yml` (new)

`push` (main/master) + `pull_request`: `cargo clippy --all-targets -- -D warnings`
and `cargo test --all`, with `Swatinem/rust-cache@v2`. No binary publishing.

`rustfmt` is intentionally NOT enforced: the project house style (two blank lines
between top-level members and methods, per the user's global style rules)
diverges from rustfmt's defaults, and stable rustfmt cannot be configured to
preserve it. Formatting is maintained by hand to the house style; clippy guards
correctness/idiom.

## Migration mechanics (local only, no push)

The chosen execution scope is local preparation with local commits; nothing is
pushed to GitHub in this pass.

1. **FON.rust** at `D:\Personal\VeyProjects\C#\FON\FON.rust`: `git init`, add the
   moved/edited crate + scaffolding, local commit -> `SHA_R`. This is the
   canonical repo the user later pushes to `FastObjectNotation/FON.rust`.
2. **FON.net**: add the submodule from the local canonical repo, then point
   `.gitmodules` at the eventual GitHub URL:
   ```
   git -c protocol.file.allow=always submodule add \
       "D:/Personal/VeyProjects/C#/FON/FON.rust" FON.Native/fon-rust
   # then set url -> https://github.com/FastObjectNotation/FON.rust.git in .gitmodules
   git submodule sync
   ```
   The gitlink records `SHA_R`. The working tree is populated, so local builds
   and CI-equivalent runs work immediately.
3. Rewrite `FON.Native/Cargo.toml` and `FON.Native/src/lib.rs`; delete the moved
   modules; update `publish.yml`; commit `FON.net` locally.
4. Verify (see Testing).
5. **No push.** When the user is ready: push `FON.rust` first (publishes `SHA_R`),
   then push `FON.net` (whose gitlink references `SHA_R`).

## Testing

- **FON.rust:** `cargo build`, `cargo clippy -D warnings`, `cargo fmt --check`,
  `cargo test` (new `tests/roundtrip.rs`).
- **FON.net:** `cargo build --manifest-path FON.Native/Cargo.toml` (builds the
  shim, resolving `fon` via the submodule). Then the existing C#
  `FON.Native.Test` suite — unchanged — runs against the freshly built
  `fon_native` binary and is the regression net proving the relocation did not
  change behavior (FFI, nested objects, cross-impl file roundtrips, buffer I/O).
  `dotnet build` / `dotnet test` as the environment allows.

## Risks and constraints

- **`fon_native` name is load-bearing.** The cdylib must stay named `fon_native`
  or P/Invoke (`DllImport("fon_native")`) and every artifact path in the workflow
  break. Preserved.
- **Submodule checkout is now required.** Local builds need
  `git submodule update --init --recursive`; CI needs `submodules: recursive`.
- **Version guard parses `Cargo.toml` textually.** Keep the version line in the
  canonical `version = "x.y.z"` form.
- **panic strategy.** `panic = "abort"` lives only in the shim profile and
  governs the whole graph when building the cdylib; `fon`'s own tests build with
  unwind. Consistent.
- **Pending working-tree state.** `docs/superpowers/*` are already deleted in the
  working tree (a prior user change); this work only adds the new spec file and
  does not touch those deletions. The stale `.worktrees/` dirs are left as-is.

## Files touched

**FON.rust (new repo):** `Cargo.toml`, `Cargo.lock`, `src/lib.rs` (new public
API), `src/types.rs`, `src/serialize.rs`, `src/deserialize.rs` (globals ->
options), `src/raw_data.rs`, `src/error.rs` (renamed type), `tests/roundtrip.rs`
(new), `README.md`, `LICENSE`, `.gitignore`, `.github/workflows/ci.yml`.

**FON.net:** `FON.Native/Cargo.toml` (rewritten as shim), `FON.Native/src/lib.rs`
(rewritten over `fon`), delete `FON.Native/src/{types,serialize,deserialize,raw_data,error}.rs`,
`FON.Native/fon-rust/` (new submodule), `.gitmodules` (new),
`FON.Native/Cargo.lock` (regenerated), `.github/workflows/publish.yml`
(submodules + version guard), `docs/superpowers/specs/2026-06-20-rust-repo-split-design.md`
(this spec).
