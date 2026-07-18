//! Enumeration / type introspection: lets a generic (reflection-driven)
//! caller enumerate an unknown FonCollection. `fon_collection_size` (in
//! `collection.rs`) gives the entry count; `fon_collection_key_at` gets the
//! key at a position; `fon_collection_kind_of` (by key) gives the packed
//! type tag needed to pick the right typed getter.

use std::ffi::{c_char, c_void};
use std::slice;

use fon::types::{
    FonCollection, FonValue, TYPE_BOOL, TYPE_BYTE, TYPE_DOUBLE, TYPE_FLOAT, TYPE_INT, TYPE_LONG,
    TYPE_OBJECT, TYPE_RAW, TYPE_SHORT, TYPE_STRING, TYPE_UINT, TYPE_ULONG,
};

use crate::error::{cstr_to_str, set_error, FonError, FON_ERROR_INVALID_ARGUMENT, FON_OK};


// Packs a FonValue's type tag + array flag into a single i32 for FFI, so
// generic (reflection-driven) callers can discover a value's shape without a
// separate call per candidate type. Layout: low byte = type char (TYPE_*),
// bit 8 = 1 if the value is an array of that type.
fn value_kind(v: &FonValue) -> (u8, bool) {
    match v {
        FonValue::Byte(_) => (TYPE_BYTE, false),
        FonValue::Short(_) => (TYPE_SHORT, false),
        FonValue::Int(_) => (TYPE_INT, false),
        FonValue::UInt(_) => (TYPE_UINT, false),
        FonValue::Long(_) => (TYPE_LONG, false),
        FonValue::ULong(_) => (TYPE_ULONG, false),
        FonValue::Float(_) => (TYPE_FLOAT, false),
        FonValue::Double(_) => (TYPE_DOUBLE, false),
        FonValue::Bool(_) => (TYPE_BOOL, false),
        FonValue::String(_) => (TYPE_STRING, false),
        FonValue::Raw(_) => (TYPE_RAW, false),
        FonValue::Object(_) => (TYPE_OBJECT, false),
        FonValue::ByteArray(_) => (TYPE_BYTE, true),
        FonValue::ShortArray(_) => (TYPE_SHORT, true),
        FonValue::IntArray(_) => (TYPE_INT, true),
        FonValue::UIntArray(_) => (TYPE_UINT, true),
        FonValue::LongArray(_) => (TYPE_LONG, true),
        FonValue::ULongArray(_) => (TYPE_ULONG, true),
        FonValue::FloatArray(_) => (TYPE_FLOAT, true),
        FonValue::DoubleArray(_) => (TYPE_DOUBLE, true),
        FonValue::BoolArray(_) => (TYPE_BOOL, true),
        FonValue::StringArray(_) => (TYPE_STRING, true),
        FonValue::ObjectArray(_) => (TYPE_OBJECT, true),
    }
}


fn pack_kind(t: u8, is_array: bool) -> i32 {
    (t as i32) | if is_array { 0x100 } else { 0 }
}


/// Gets the key name at iteration position `index` (0-based, `0..fon_collection_size`).
/// The key is written nul-terminated and truncated to fit `buffer_size`.
#[no_mangle]
pub extern "C" fn fon_collection_key_at(
    collection: *mut c_void,
    index: i64,
    buffer: *mut u8,
    buffer_size: i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || buffer.is_null() || buffer_size <= 0 || index < 0 {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let entry = match c.iter().nth(index as usize) {
            Some(e) => e,
            None => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Index out of range");
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        let key_bytes = entry.0.as_bytes();
        let dst = slice::from_raw_parts_mut(buffer, buffer_size as usize);
        let n = key_bytes.len().min(dst.len() - 1);
        dst[..n].copy_from_slice(&key_bytes[..n]);
        dst[n] = 0;
        FON_OK
    }
}


/// Gets the packed type tag (low byte = TYPE_* char, bit 8 = is-array flag) of the
/// value stored under `key`. Returns -1 if the key is missing or invalid.
#[no_mangle]
pub extern "C" fn fon_collection_kind_of(collection: *mut c_void, key: *const c_char) -> i32 {
    if collection.is_null() || key.is_null() {
        return -1;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s,
            Err(_) => return -1,
        };
        match c.get(k) {
            Some(v) => {
                let (t, arr) = value_kind(v);
                pack_kind(t, arr)
            }
            None => -1,
        }
    }
}
