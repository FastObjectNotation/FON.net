//! Array value add/get: fixed-width numeric arrays (via a shared macro),
//! plus the two bespoke element types — bool[] (byte-buffer marshalled)
//! and string[] (variable-length, count + indexed item fetch).

use std::ffi::{c_char, c_void};
use std::slice;

use fon::types::{FonCollection, FonValue};

use crate::error::{cstr_to_str, set_error, FonError, FON_ERROR_INVALID_ARGUMENT, FON_OK};


#[no_mangle]
pub extern "C" fn fon_collection_add_int_array(
    collection: *mut c_void,
    key: *const c_char,
    values: *const i32,
    count: i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || values.is_null() || count < 0 {
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
    let vec = unsafe { slice::from_raw_parts(values, count as usize).to_vec() };
    unsafe {
        let c = &mut *(collection as *mut FonCollection);
        c.add(k, FonValue::IntArray(vec));
    }
    FON_OK
}


#[no_mangle]
pub extern "C" fn fon_collection_add_float_array(
    collection: *mut c_void,
    key: *const c_char,
    values: *const f32,
    count: i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || values.is_null() || count < 0 {
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
    let vec = unsafe { slice::from_raw_parts(values, count as usize).to_vec() };
    unsafe {
        let c = &mut *(collection as *mut FonCollection);
        c.add(k, FonValue::FloatArray(vec));
    }
    FON_OK
}


#[no_mangle]
pub extern "C" fn fon_collection_get_int_array(
    collection: *mut c_void,
    key: *const c_char,
    buffer: *mut i32,
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
        let arr = match c.get(k) {
            Some(FonValue::IntArray(v)) => v,
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        *actual_size = arr.len() as i64;
        if !buffer.is_null() && buffer_size > 0 {
            let copy_count = (buffer_size as usize).min(arr.len());
            let dst = slice::from_raw_parts_mut(buffer, copy_count);
            dst.copy_from_slice(&arr[..copy_count]);
        }
        FON_OK
    }
}


#[no_mangle]
pub extern "C" fn fon_collection_get_float_array(
    collection: *mut c_void,
    key: *const c_char,
    buffer: *mut f32,
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
        let arr = match c.get(k) {
            Some(FonValue::FloatArray(v)) => v,
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        *actual_size = arr.len() as i64;
        if !buffer.is_null() && buffer_size > 0 {
            let copy_count = (buffer_size as usize).min(arr.len());
            let dst = slice::from_raw_parts_mut(buffer, copy_count);
            dst.copy_from_slice(&arr[..copy_count]);
        }
        FON_OK
    }
}


// Shared shape for the fixed-width numeric array types: add copies the input
// slice into a new FonValue::<Variant>, get copies out via the standard
// two-call (query-size-then-fill) pattern used throughout this crate.
macro_rules! impl_array_ops {
    ($add_fn:ident, $get_fn:ident, $variant:ident, $elem:ty) => {
        #[no_mangle]
        pub extern "C" fn $add_fn(
            collection: *mut c_void,
            key: *const c_char,
            values: *const $elem,
            count: i64,
            error: *mut FonError,
        ) -> i32 {
            if collection.is_null() || key.is_null() || count < 0 || (values.is_null() && count > 0) {
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
            let vec = if count == 0 {
                Vec::new()
            } else {
                unsafe { slice::from_raw_parts(values, count as usize).to_vec() }
            };
            unsafe {
                let c = &mut *(collection as *mut FonCollection);
                c.add(k, FonValue::$variant(vec));
            }
            FON_OK
        }


        #[no_mangle]
        pub extern "C" fn $get_fn(
            collection: *mut c_void,
            key: *const c_char,
            buffer: *mut $elem,
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
                let arr = match c.get(k) {
                    Some(FonValue::$variant(v)) => v,
                    _ => {
                        set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                        return FON_ERROR_INVALID_ARGUMENT;
                    }
                };
                *actual_size = arr.len() as i64;
                if !buffer.is_null() && buffer_size > 0 {
                    let copy_count = (buffer_size as usize).min(arr.len());
                    let dst = slice::from_raw_parts_mut(buffer, copy_count);
                    dst.copy_from_slice(&arr[..copy_count]);
                }
                FON_OK
            }
        }
    };
}

impl_array_ops!(fon_collection_add_byte_array, fon_collection_get_byte_array, ByteArray, u8);
impl_array_ops!(fon_collection_add_short_array, fon_collection_get_short_array, ShortArray, i16);
impl_array_ops!(fon_collection_add_uint_array, fon_collection_get_uint_array, UIntArray, u32);
impl_array_ops!(fon_collection_add_long_array, fon_collection_get_long_array, LongArray, i64);
impl_array_ops!(fon_collection_add_ulong_array, fon_collection_get_ulong_array, ULongArray, u64);
impl_array_ops!(fon_collection_add_double_array, fon_collection_get_double_array, DoubleArray, f64);


// bool[] is marshalled as a u8 buffer (0/1) since Vec<bool> is not FFI-safe.
#[no_mangle]
pub extern "C" fn fon_collection_add_bool_array(
    collection: *mut c_void,
    key: *const c_char,
    values: *const u8,
    count: i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || count < 0 || (values.is_null() && count > 0) {
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
    let vec: Vec<bool> = if count == 0 {
        Vec::new()
    } else {
        unsafe {
            slice::from_raw_parts(values, count as usize)
                .iter()
                .map(|&b| b != 0)
                .collect()
        }
    };
    unsafe {
        let c = &mut *(collection as *mut FonCollection);
        c.add(k, FonValue::BoolArray(vec));
    }
    FON_OK
}


#[no_mangle]
pub extern "C" fn fon_collection_get_bool_array(
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
        let arr = match c.get(k) {
            Some(FonValue::BoolArray(v)) => v,
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        *actual_size = arr.len() as i64;
        if !buffer.is_null() && buffer_size > 0 {
            let copy_count = (buffer_size as usize).min(arr.len());
            let dst = slice::from_raw_parts_mut(buffer, copy_count);
            for (i, v) in arr.iter().take(copy_count).enumerate() {
                dst[i] = if *v { 1 } else { 0 };
            }
        }
        FON_OK
    }
}


// string[] elements vary in length, so add takes an array of C strings, and
// get is split into a count query plus an indexed per-item fetch (each item
// uses the same nul-terminated-buffer convention as fon_collection_get_string).
#[no_mangle]
pub extern "C" fn fon_collection_add_string_array(
    collection: *mut c_void,
    key: *const c_char,
    values: *const *const c_char,
    count: i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || count < 0 || (values.is_null() && count > 0) {
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
    let mut vec: Vec<String> = Vec::with_capacity(count.max(0) as usize);
    unsafe {
        let ptrs = if count == 0 { &[][..] } else { slice::from_raw_parts(values, count as usize) };
        for &p in ptrs {
            match cstr_to_str(p) {
                Ok(s) => vec.push(s.to_owned()),
                Err(e) => {
                    set_error(error, FON_ERROR_INVALID_ARGUMENT, &e.to_string());
                    return FON_ERROR_INVALID_ARGUMENT;
                }
            }
        }
        let c = &mut *(collection as *mut FonCollection);
        c.add(k, FonValue::StringArray(vec));
    }
    FON_OK
}


#[no_mangle]
pub extern "C" fn fon_collection_get_string_array_count(
    collection: *mut c_void,
    key: *const c_char,
    count: *mut i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || count.is_null() {
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
            Some(FonValue::StringArray(v)) => {
                *count = v.len() as i64;
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
pub extern "C" fn fon_collection_get_string_array_item(
    collection: *mut c_void,
    key: *const c_char,
    index: i64,
    buffer: *mut u8,
    buffer_size: i64,
    error: *mut FonError,
) -> i32 {
    if collection.is_null() || key.is_null() || buffer.is_null() || buffer_size <= 0 || index < 0 {
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
        let item = match c.get(k) {
            Some(FonValue::StringArray(v)) => match v.get(index as usize) {
                Some(s) => s,
                None => {
                    set_error(error, FON_ERROR_INVALID_ARGUMENT, "Index out of range");
                    return FON_ERROR_INVALID_ARGUMENT;
                }
            },
            _ => {
                set_error(error, FON_ERROR_INVALID_ARGUMENT, "Key not found or wrong type");
                return FON_ERROR_INVALID_ARGUMENT;
            }
        };
        let bytes = item.as_bytes();
        let dst = slice::from_raw_parts_mut(buffer, buffer_size as usize);
        let n = bytes.len().min(dst.len() - 1);
        dst[..n].copy_from_slice(&bytes[..n]);
        dst[n] = 0;
        FON_OK
    }
}
