//! Error codes, the FFI error struct, version reporting, and the global
//! config (raw-unpack / max-depth) shared across the other FFI modules.

use std::ffi::{c_char, CStr};
use std::sync::atomic::{AtomicBool, AtomicI32, Ordering};

use fon::FonError as FonLibError;


// Result codes mirror fon_export.h. FON_ERROR_FILE_NOT_FOUND mirrors
// FonResultCode.FileNotFound on the C# side for completeness; no function
// here currently returns it (file-not-found surfaces as FON_ERROR_PARSE_FAILED
// via the underlying I/O error), hence the explicit dead_code allowance.
pub const FON_OK: i32 = 0;
#[allow(dead_code)]
pub const FON_ERROR_FILE_NOT_FOUND: i32 = 1;
pub const FON_ERROR_PARSE_FAILED: i32 = 2;
pub const FON_ERROR_WRITE_FAILED: i32 = 3;
pub const FON_ERROR_INVALID_ARGUMENT: i32 = 4;


pub static DESERIALIZE_RAW_UNPACK: AtomicBool = AtomicBool::new(false);
pub static MAX_DEPTH: AtomicI32 = AtomicI32::new(64);


#[repr(C)]
pub struct FonError {
    pub code: i32,
    pub message: [u8; 256],
}


pub fn set_error(error: *mut FonError, code: i32, message: &str) {
    if error.is_null() {
        return;
    }
    unsafe {
        (*error).code = code;
        let buf = &mut (*error).message;
        for b in buf.iter_mut() {
            *b = 0;
        }
        let bytes = message.as_bytes();
        let n = bytes.len().min(buf.len() - 1);
        buf[..n].copy_from_slice(&bytes[..n]);
        buf[n] = 0;
    }
}


pub fn err_code(e: &FonLibError) -> i32 {
    match e {
        FonLibError::Parse(_) => FON_ERROR_PARSE_FAILED,
        FonLibError::Write(_) => FON_ERROR_WRITE_FAILED,
        FonLibError::InvalidArgument(_) => FON_ERROR_INVALID_ARGUMENT,
    }
}


pub unsafe fn cstr_to_str<'a>(p: *const c_char) -> Result<&'a str, FonLibError> {
    if p.is_null() {
        return Err(FonLibError::InvalidArgument("null pointer".into()));
    }
    CStr::from_ptr(p)
        .to_str()
        .map_err(|_| FonLibError::InvalidArgument("invalid UTF-8".into()))
}


fn version_cstr() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), "\0").as_ptr() as *const c_char
}


// ==================== VERSION ====================

#[no_mangle]
pub extern "C" fn fon_version() -> *const c_char {
    version_cstr()
}


// ==================== CONFIGURATION ====================

#[no_mangle]
pub extern "C" fn fon_set_raw_unpack(enable: i32) {
    DESERIALIZE_RAW_UNPACK.store(enable != 0, Ordering::Relaxed);
}


#[no_mangle]
pub extern "C" fn fon_set_max_depth(depth: i32) {
    let d = if depth < 1 { 1 } else { depth };
    MAX_DEPTH.store(d, Ordering::Relaxed);
}
