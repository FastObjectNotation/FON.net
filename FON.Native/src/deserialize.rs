//! Deserialization: from a file, and from a caller-supplied UTF-8 buffer
//! (dump — multi-line, or a single collection — one line).

use std::ffi::{c_char, c_void};
use std::path::PathBuf;
use std::ptr;
use std::slice;
use std::sync::atomic::Ordering;

use fon::deserialize::{deserialize_dump_from_bytes, deserialize_from_file, deserialize_line};
use fon::DeserializeOptions;

use crate::error::{
    cstr_to_str, err_code, set_error, FonError, DESERIALIZE_RAW_UNPACK, FON_ERROR_INVALID_ARGUMENT,
    MAX_DEPTH,
};


#[no_mangle]
pub extern "C" fn fon_deserialize_from_file(
    path: *const c_char,
    max_threads: i32,
    error: *mut FonError,
) -> *mut c_void {
    if path.is_null() {
        set_error(
            error,
            FON_ERROR_INVALID_ARGUMENT,
            "Invalid argument: path is null",
        );
        return ptr::null_mut();
    }
    let path_str = match unsafe { cstr_to_str(path) } {
        Ok(s) => s,
        Err(e) => {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
            return ptr::null_mut();
        }
    };

    let opts = DeserializeOptions {
        max_depth: MAX_DEPTH.load(Ordering::Relaxed),
        unpack_raw: DESERIALIZE_RAW_UNPACK.load(Ordering::Relaxed),
    };
    match deserialize_from_file(&PathBuf::from(path_str), max_threads, &opts) {
        Ok(dump) => Box::into_raw(Box::new(dump)) as *mut c_void,
        Err(e) => {
            let code = err_code(&e);
            set_error(error, code, &e.to_string());
            ptr::null_mut()
        }
    }
}


#[no_mangle]
pub extern "C" fn fon_deserialize_dump_from_buffer(
    data: *const u8,
    size: i64,
    max_threads: i32,
    error: *mut FonError,
) -> *mut c_void {
    if (data.is_null() && size > 0) || size < 0 {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return ptr::null_mut();
    }
    let bytes = if size == 0 {
        &[][..]
    } else {
        unsafe { slice::from_raw_parts(data, size as usize) }
    };
    let opts = DeserializeOptions {
        max_depth: MAX_DEPTH.load(Ordering::Relaxed),
        unpack_raw: DESERIALIZE_RAW_UNPACK.load(Ordering::Relaxed),
    };
    match deserialize_dump_from_bytes(bytes, max_threads, &opts) {
        Ok(dump) => Box::into_raw(Box::new(dump)) as *mut c_void,
        Err(e) => {
            let code = err_code(&e);
            set_error(error, code, &e.to_string());
            ptr::null_mut()
        }
    }
}


#[no_mangle]
pub extern "C" fn fon_deserialize_collection_from_buffer(
    data: *const u8,
    size: i64,
    error: *mut FonError,
) -> *mut c_void {
    if (data.is_null() && size > 0) || size < 0 {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return ptr::null_mut();
    }
    let bytes = if size == 0 {
        &[][..]
    } else {
        unsafe { slice::from_raw_parts(data, size as usize) }
    };
    let opts = DeserializeOptions {
        max_depth: MAX_DEPTH.load(Ordering::Relaxed),
        unpack_raw: DESERIALIZE_RAW_UNPACK.load(Ordering::Relaxed),
    };
    match deserialize_line(bytes, &opts) {
        Ok(c) => Box::into_raw(Box::new(c)) as *mut c_void,
        Err(e) => {
            let code = err_code(&e);
            set_error(error, code, &e.to_string());
            ptr::null_mut()
        }
    }
}
