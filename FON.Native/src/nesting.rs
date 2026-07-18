//! Nested collections: a single child object, or an array of child objects.
//! Adding always transfers ownership of the child handle(s) into the parent;
//! getting always returns a borrowed handle owned by the parent.

use std::ffi::{c_char, c_void};
use std::ptr;
use std::slice;

use fon::types::{FonCollection, FonValue};

use crate::error::{cstr_to_str, set_error, FonError, FON_ERROR_INVALID_ARGUMENT, FON_OK};


#[no_mangle]
pub extern "C" fn fon_collection_add_collection(
    parent: *mut c_void,
    key: *const c_char,
    child: *mut c_void,
    error: *mut FonError,
) -> i32 {
    if parent.is_null() || key.is_null() || child.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    let k = match unsafe { cstr_to_str(key) } {
        Ok(s) => s.to_owned(),
        Err(e) => {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
            return FON_ERROR_INVALID_ARGUMENT;
        }
    };
    unsafe {
        let p = &mut *(parent as *mut FonCollection);
        // Take ownership of child handle.
        let c = Box::from_raw(child as *mut FonCollection);
        p.add(k, FonValue::Object(c));
    }
    FON_OK
}


#[no_mangle]
pub extern "C" fn fon_collection_add_collection_array(
    parent: *mut c_void,
    key: *const c_char,
    children: *const *mut c_void,
    count: i64,
    error: *mut FonError,
) -> i32 {
    if parent.is_null() || key.is_null() || count < 0 || (count > 0 && children.is_null()) {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    let k = match unsafe { cstr_to_str(key) } {
        Ok(s) => s.to_owned(),
        Err(e) => {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
            return FON_ERROR_INVALID_ARGUMENT;
        }
    };
    unsafe {
        let p = &mut *(parent as *mut FonCollection);
        let mut vec: Vec<Box<FonCollection>> = Vec::with_capacity(count as usize);
        let raw_children = slice::from_raw_parts(children, count as usize);
        for &handle in raw_children {
            // Take ownership of each child handle.
            vec.push(Box::from_raw(handle as *mut FonCollection));
        }
        p.add(k, FonValue::ObjectArray(vec));
    }
    FON_OK
}


/// Gets a borrowed handle to a nested collection under `key`. Ownership: the returned
/// handle is owned by `parent`. The caller MUST NOT call `fon_collection_free` on it.
/// Returns null if the key is missing or the value is not a nested collection.
#[no_mangle]
pub extern "C" fn fon_collection_get_collection(
    parent: *mut c_void,
    key: *const c_char,
    error: *mut FonError,
) -> *mut c_void {
    if parent.is_null() || key.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return ptr::null_mut();
    }
    unsafe {
        let p = &mut *(parent as *mut FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s.to_owned(),
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return ptr::null_mut();
            }
        };
        match p.get_mut(&k) {
            Some(FonValue::Object(boxed)) => {
                // Return borrowed pointer to the heap-allocated FonCollection inside the Box.
                // Stable as long as the parent FonCollection outlives the borrowed handle.
                let raw: *mut FonCollection = &mut **boxed;
                raw as *mut c_void
            }
            _ => {
                set_error(
                    error,
                    FON_ERROR_INVALID_ARGUMENT,
                    "Key not found or not a nested collection",
                );
                ptr::null_mut()
            }
        }
    }
}


/// Gets borrowed handles for an array of nested collections. Pass `buffer=null` with
/// `buffer_size=0` to query `actual_size` only. Ownership: every returned handle is
/// owned by `parent`. The caller MUST NOT call `fon_collection_free` on any of them.
#[no_mangle]
pub extern "C" fn fon_collection_get_collection_array(
    parent: *mut c_void,
    key: *const c_char,
    buffer: *mut *mut c_void,
    buffer_size: i64,
    actual_size: *mut i64,
    error: *mut FonError,
) -> i32 {
    if parent.is_null() || key.is_null() || actual_size.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let p = &mut *(parent as *mut FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s.to_owned(),
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        let arr = match p.get_mut(&k) {
            Some(FonValue::ObjectArray(v)) => v,
            _ => {
                set_error(
                    error,
                    FON_ERROR_INVALID_ARGUMENT,
                    "Key not found or not an array of nested collections",
                );
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        *actual_size = arr.len() as i64;
        if !buffer.is_null() && buffer_size > 0 {
            let copy_count = (buffer_size as usize).min(arr.len());
            let dst = slice::from_raw_parts_mut(buffer, copy_count);
            for (i, child) in arr.iter_mut().take(copy_count).enumerate() {
                let raw: *mut FonCollection = &mut **child;
                dst[i] = raw as *mut c_void;
            }
        }
        FON_OK
    }
}
