//! Collection lifecycle: create, free, size.

use std::ffi::c_void;

use fon::types::FonCollection;


#[no_mangle]
pub extern "C" fn fon_collection_create() -> *mut c_void {
    Box::into_raw(Box::new(FonCollection::new())) as *mut c_void
}


#[no_mangle]
pub extern "C" fn fon_collection_free(collection: *mut c_void) {
    if collection.is_null() {
        return;
    }
    unsafe {
        drop(Box::from_raw(collection as *mut FonCollection));
    }
}


#[no_mangle]
pub extern "C" fn fon_collection_size(collection: *mut c_void) -> i64 {
    if collection.is_null() {
        return 0;
    }
    unsafe { (*(collection as *const FonCollection)).len() as i64 }
}
