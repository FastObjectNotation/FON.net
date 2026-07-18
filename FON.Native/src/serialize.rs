//! Serialization: to a file, and to a caller-supplied UTF-8 buffer (two-call
//! query-size-then-fill pattern, matching the array getters).

use std::ffi::{c_char, c_void};
use std::path::PathBuf;
use std::slice;

use fon::serialize::{serialize_dump_to_string, serialize_to_file, serialize_to_string};
use fon::types::{FonCollection, FonDump};

use crate::error::{cstr_to_str, err_code, set_error, FonError, FON_ERROR_INVALID_ARGUMENT, FON_OK};


#[no_mangle]
pub extern "C" fn fon_serialize_to_file(
    dump: *mut c_void,
    path: *const c_char,
    max_threads: i32,
    error: *mut FonError,
) -> i32 {
    if dump.is_null() || path.is_null() {
        set_error(
            error,
            FON_ERROR_INVALID_ARGUMENT,
            "Invalid argument: dump or path is null",
        );
        return FON_ERROR_INVALID_ARGUMENT;
    }
    let path_str = match unsafe { cstr_to_str(path) } {
        Ok(s) => s,
        Err(e) => {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
            return FON_ERROR_INVALID_ARGUMENT;
        }
    };

    let d = unsafe { &*(dump as *const FonDump) };
    match serialize_to_file(d, &PathBuf::from(path_str), max_threads) {
        Ok(()) => FON_OK,
        Err(e) => {
            let code = err_code(&e);
            set_error(error, code, &e.to_string());
            code
        }
    }
}


// Two-call pattern (matches fon_collection_get_int_array):
//   1. Pass buffer=null, buffer_size=0 to read required size into *required_size.
//   2. Allocate buffer of required_size bytes, call again to receive UTF-8 bytes.
// Output is NOT null-terminated; *required_size is the exact byte count.

unsafe fn write_buffer(
    bytes: &[u8],
    buffer: *mut u8,
    buffer_size: i64,
    required_size: *mut i64,
) -> i32 {
    if !required_size.is_null() {
        *required_size = bytes.len() as i64;
    }
    if !buffer.is_null() && buffer_size > 0 {
        let copy_count = (buffer_size as usize).min(bytes.len());
        let dst = slice::from_raw_parts_mut(buffer, copy_count);
        dst.copy_from_slice(&bytes[..copy_count]);
    }
    FON_OK
}


#[no_mangle]
pub extern "C" fn fon_serialize_dump_to_buffer(
    dump: *mut c_void,
    buffer: *mut u8,
    buffer_size: i64,
    required_size: *mut i64,
    max_threads: i32,
    error: *mut FonError,
) -> i32 {
    if dump.is_null() || required_size.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    let d = unsafe { &*(dump as *const FonDump) };
    let s = serialize_dump_to_string(d, max_threads);
    unsafe { write_buffer(s.as_bytes(), buffer, buffer_size, required_size) }
}


#[no_mangle]
pub extern "C" fn fon_serialize_collection_to_buffer(
    collection: *mut c_void,
    buffer: *mut u8,
    buffer_size: i64,
    required_size: *mut i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || required_size.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    let c = unsafe { &*(collection as *const FonCollection) };
    let s = serialize_to_string(c);
    unsafe { write_buffer(s.as_bytes(), buffer, buffer_size, required_size) }
}
