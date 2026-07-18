//! Dump lifecycle, lookup/enumeration, and taking ownership of a Collection.

use std::ffi::c_void;
use std::ptr;

use fon::types::{FonCollection, FonDump};

use crate::error::{set_error, FonError, FON_ERROR_INVALID_ARGUMENT, FON_OK};


#[no_mangle]
pub extern "C" fn fon_dump_create() -> *mut c_void {
    Box::into_raw(Box::new(FonDump::new())) as *mut c_void
}


#[no_mangle]
pub extern "C" fn fon_dump_free(dump: *mut c_void) {
    if dump.is_null() {
        return;
    }
    unsafe {
        drop(Box::from_raw(dump as *mut FonDump));
    }
}


#[no_mangle]
pub extern "C" fn fon_dump_size(dump: *mut c_void) -> i64 {
    if dump.is_null() {
        return 0;
    }
    unsafe { (*(dump as *const FonDump)).len() as i64 }
}


#[no_mangle]
pub extern "C" fn fon_dump_get(dump: *mut c_void, index: u64) -> *mut c_void {
    if dump.is_null() {
        return ptr::null_mut();
    }
    unsafe {
        let d = &mut *(dump as *mut FonDump);
        match d.get_mut(index) {
            Some(c) => c as *mut FonCollection as *mut c_void,
            None => ptr::null_mut(),
        }
    }
}


/// Gets the record id stored at iteration position `position` (0-based, `0..fon_dump_size(dump)`).
/// Used to enumerate a Dump without assuming ids are contiguous. Combine with
/// <c>fon_dump_get</c> to fetch the corresponding collection.
#[no_mangle]
pub extern "C" fn fon_dump_id_at(dump: *mut c_void, position: i64, id_out: *mut u64) -> i32 {
    if dump.is_null() || id_out.is_null() || position < 0 {
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let d = &*(dump as *const FonDump);
        match d.iter().nth(position as usize) {
            Some((id, _)) => {
                *id_out = id;
                FON_OK
            }
            None => FON_ERROR_INVALID_ARGUMENT,
        }
    }
}


#[no_mangle]
pub extern "C" fn fon_dump_add(
    dump: *mut c_void,
    id: u64,
    collection: *mut c_void,
    error: *mut FonError,
) -> i32 {
    if dump.is_null() || collection.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let d = &mut *(dump as *mut FonDump);
        // Take ownership of the collection — caller must not free it again.
        let c = Box::from_raw(collection as *mut FonCollection);
        d.add(id, *c);
    }
    FON_OK
}
