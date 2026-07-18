//! FON native library — C ABI shim over the `fon` crate.
//!
//! Exposes a C ABI matching the original `fon_export.h` so the .NET P/Invoke
//! bindings (`NativeBindings.cs`, in the `FON` project) work without any
//! change. Split by concern:
//!
//! - `error` — error codes, the FFI error struct, version, and global config
//! - `collection` / `dump` — lifecycle (create/free/size) for each container
//! - `scalar` — single-value add/get (ints, floats, bool, string, raw binary)
//! - `array` — array-value add/get
//! - `nesting` — nested collections (single child, or an array of children)
//! - `enumerate` — generic (reflection-driven) enumeration / type introspection
//! - `serialize` / `deserialize` — text, buffer, and file I/O
//!
//! Every `#[no_mangle] extern "C"` function keeps its original exported
//! symbol name regardless of which module it lives in.

mod array;
mod collection;
mod deserialize;
mod dump;
mod enumerate;
mod error;
mod nesting;
mod scalar;
mod serialize;
