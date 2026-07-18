//! Scalar value add/get: the fixed single-value types (ints, floats, bool,
//! string, raw binary) that are neither arrays nor nested collections.

use std::ffi::{c_char, c_void};
use std::slice;

use fon::types::{FonCollection, FonValue};
use fon::RawData;

use crate::error::{cstr_to_str, set_error, FonError, FON_ERROR_INVALID_ARGUMENT, FON_ERROR_PARSE_FAILED, FON_OK};


// ==================== ADD ====================

unsafe fn add_to_collection(
    collection: *mut c_void,
    key: *const c_char,
    error: *mut FonError,
    f: impl FnOnce(&mut FonCollection, String),
) -> i32 {
    if collection.is_null() || key.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    let k = match cstr_to_str(key) {
        Ok(s) => s.to_owned(),
        Err(e) => {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
            return FON_ERROR_INVALID_ARGUMENT;
        }
    };
    let c = &mut *(collection as *mut FonCollection);
    f(c, k);
    FON_OK
}


#[no_mangle]
pub extern "C" fn fon_collection_add_int(
    collection: *mut c_void,
    key: *const c_char,
    value: i32,
    error: *mut FonError,
) -> i32 {
    unsafe { add_to_collection(collection, key, error, |c, k| c.add(k, FonValue::Int(value))) }
}


#[no_mangle]
pub extern "C" fn fon_collection_add_long(
    collection: *mut c_void,
    key: *const c_char,
    value: i64,
    error: *mut FonError,
) -> i32 {
    unsafe { add_to_collection(collection, key, error, |c, k| c.add(k, FonValue::Long(value))) }
}


#[no_mangle]
pub extern "C" fn fon_collection_add_float(
    collection: *mut c_void,
    key: *const c_char,
    value: f32,
    error: *mut FonError,
) -> i32 {
    unsafe { add_to_collection(collection, key, error, |c, k| c.add(k, FonValue::Float(value))) }
}


#[no_mangle]
pub extern "C" fn fon_collection_add_double(
    collection: *mut c_void,
    key: *const c_char,
    value: f64,
    error: *mut FonError,
) -> i32 {
    unsafe { add_to_collection(collection, key, error, |c, k| c.add(k, FonValue::Double(value))) }
}


#[no_mangle]
pub extern "C" fn fon_collection_add_bool(
    collection: *mut c_void,
    key: *const c_char,
    value: i32,
    error: *mut FonError,
) -> i32 {
    unsafe {
        add_to_collection(collection, key, error, |c, k| {
            c.add(k, FonValue::Bool(value != 0))
        })
    }
}


#[no_mangle]
pub extern "C" fn fon_collection_add_byte(
    collection: *mut c_void,
    key: *const c_char,
    value: u8,
    error: *mut FonError,
) -> i32 {
    unsafe { add_to_collection(collection, key, error, |c, k| c.add(k, FonValue::Byte(value))) }
}


#[no_mangle]
pub extern "C" fn fon_collection_add_short(
    collection: *mut c_void,
    key: *const c_char,
    value: i16,
    error: *mut FonError,
) -> i32 {
    unsafe { add_to_collection(collection, key, error, |c, k| c.add(k, FonValue::Short(value))) }
}


#[no_mangle]
pub extern "C" fn fon_collection_add_uint(
    collection: *mut c_void,
    key: *const c_char,
    value: u32,
    error: *mut FonError,
) -> i32 {
    unsafe { add_to_collection(collection, key, error, |c, k| c.add(k, FonValue::UInt(value))) }
}


#[no_mangle]
pub extern "C" fn fon_collection_add_ulong(
    collection: *mut c_void,
    key: *const c_char,
    value: u64,
    error: *mut FonError,
) -> i32 {
    unsafe { add_to_collection(collection, key, error, |c, k| c.add(k, FonValue::ULong(value))) }
}


/// Adds raw binary data under `key`. The bytes are copied and stored unpacked;
/// packing to Z85 (if ever needed) happens lazily at serialization time.
#[no_mangle]
pub extern "C" fn fon_collection_add_raw(
    collection: *mut c_void,
    key: *const c_char,
    data: *const u8,
    len: i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || len < 0 || (data.is_null() && len > 0) {
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
    let bytes = if len == 0 {
        Vec::new()
    } else {
        unsafe { slice::from_raw_parts(data, len as usize).to_vec() }
    };
    unsafe {
        let c = &mut *(collection as *mut FonCollection);
        c.add(k, FonValue::Raw(Box::new(RawData::from_bytes(bytes))));
    }
    FON_OK
}


#[no_mangle]
pub extern "C" fn fon_collection_add_string(
    collection: *mut c_void,
    key: *const c_char,
    value: *const c_char,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || value.is_null() {
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
    let v = match unsafe { cstr_to_str(value) } {
        Ok(s) => s.to_owned(),
        Err(e) => {
            set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
            return FON_ERROR_INVALID_ARGUMENT;
        }
    };
    unsafe {
        let c = &mut *(collection as *mut FonCollection);
        c.add(k, FonValue::String(v));
    }
    FON_OK
}


// ==================== GET ====================

#[no_mangle]
pub extern "C" fn fon_collection_get_int(
    collection: *mut c_void,
    key: *const c_char,
    value: *mut i32,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || value.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s,
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        match c.get(k) {
            Some(FonValue::Int(v)) => {
                *value = *v;
                FON_OK
            }
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                FON_ERROR_INVALID_ARGUMENT
            }
        }
    }
}


#[no_mangle]
pub extern "C" fn fon_collection_get_long(
    collection: *mut c_void,
    key: *const c_char,
    value: *mut i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || value.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s,
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        match c.get(k) {
            Some(FonValue::Long(v)) => {
                *value = *v;
                FON_OK
            }
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                FON_ERROR_INVALID_ARGUMENT
            }
        }
    }
}


#[no_mangle]
pub extern "C" fn fon_collection_get_float(
    collection: *mut c_void,
    key: *const c_char,
    value: *mut f32,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || value.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s,
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        match c.get(k) {
            Some(FonValue::Float(v)) => {
                *value = *v;
                FON_OK
            }
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                FON_ERROR_INVALID_ARGUMENT
            }
        }
    }
}


#[no_mangle]
pub extern "C" fn fon_collection_get_double(
    collection: *mut c_void,
    key: *const c_char,
    value: *mut f64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || value.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s,
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        match c.get(k) {
            Some(FonValue::Double(v)) => {
                *value = *v;
                FON_OK
            }
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                FON_ERROR_INVALID_ARGUMENT
            }
        }
    }
}


#[no_mangle]
pub extern "C" fn fon_collection_get_bool(
    collection: *mut c_void,
    key: *const c_char,
    value: *mut i32,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || value.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s,
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        match c.get(k) {
            Some(FonValue::Bool(v)) => {
                *value = if *v { 1 } else { 0 };
                FON_OK
            }
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                FON_ERROR_INVALID_ARGUMENT
            }
        }
    }
}


// Shared shape for scalar getters that don't already have a hand-written
// counterpart above (byte/short/uint/ulong): look up by key, type-check the
// variant, and write the value out through an out-pointer.
macro_rules! impl_scalar_get {
    ($fn_name:ident, $variant:ident, $elem:ty) => {
        #[no_mangle]
        pub extern "C" fn $fn_name(
            collection: *mut c_void,
            key: *const c_char,
            value: *mut $elem,
            error: *mut FonError,
        ) -> i32 {
            if collection.is_null() || key.is_null() || value.is_null() {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
                return FON_ERROR_INVALID_ARGUMENT;
            }
            unsafe {
                let c = &*(collection as *const FonCollection);
                let k = match cstr_to_str(key) {
                    Ok(s) => s,
                    Err(e) => {
                        set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                        return FON_ERROR_INVALID_ARGUMENT;
                    }
                };
                match c.get(k) {
                    Some(FonValue::$variant(v)) => {
                        *value = *v;
                        FON_OK
                    }
                    _ => {
                        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                        FON_ERROR_INVALID_ARGUMENT
                    }
                }
            }
        }
    };
}

impl_scalar_get!(fon_collection_get_byte, Byte, u8);
impl_scalar_get!(fon_collection_get_short, Short, i16);
impl_scalar_get!(fon_collection_get_uint, UInt, u32);
impl_scalar_get!(fon_collection_get_ulong, ULong, u64);


/// Reads raw binary data (two-call pattern, see <see cref="fon_collection_get_int_array"/>).
/// Transparently unpacks a Z85-encoded value into bytes without mutating the stored value.
#[no_mangle]
pub extern "C" fn fon_collection_get_raw(
    collection: *mut c_void,
    key: *const c_char,
    buffer: *mut u8,
    buffer_size: i64,
    actual_size: *mut i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || actual_size.is_null() {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s,
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        let raw = match c.get(k) {
            Some(FonValue::Raw(r)) => r,
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        let bytes: Vec<u8> = if raw.is_unpacked() {
            raw.data().to_vec()
        } else if raw.is_packed() {
            let mut tmp = RawData::from_encoded(raw.encoded().to_string());
            match tmp.unpack() {
                Ok(_) => tmp.data().to_vec(),
                Err(e) => {
                    set_error(error, FON_ERROR_PARSE_FAILED, &e.to_string());
                    return FON_ERROR_PARSE_FAILED;
                }
            }
        } else {
            Vec::new()
        };
        *actual_size = bytes.len() as i64;
        if !buffer.is_null() && buffer_size > 0 {
            let copy_count = (buffer_size as usize).min(bytes.len());
            let dst = slice::from_raw_parts_mut(buffer, copy_count);
            dst.copy_from_slice(&bytes[..copy_count]);
        }
        FON_OK
    }
}


#[no_mangle]
pub extern "C" fn fon_collection_get_string(
    collection: *mut c_void,
    key: *const c_char,
    buffer: *mut u8,
    buffer_size: i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || buffer.is_null() || buffer_size <= 0 {
        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Invalid argument");
        return FON_ERROR_INVALID_ARGUMENT;
    }
    unsafe {
        let c = &*(collection as *const FonCollection);
        let k = match cstr_to_str(key) {
            Ok(s) => s,
            Err(e) => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        let s = match c.get(k) {
            Some(FonValue::String(s)) => s,
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };

        let dst = slice::from_raw_parts_mut(buffer, buffer_size as usize);
        let bytes = s.as_bytes();
        let n = bytes.len().min(dst.len() - 1);
        dst[..n].copy_from_slice(&bytes[..n]);
        dst[n] = 0;
        FON_OK
    }
}
