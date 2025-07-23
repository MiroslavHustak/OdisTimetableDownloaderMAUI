use std::ffi::{CString, c_char};
use std::ptr;

/// Helper to get the length of a null-terminated UTF-16 string
fn wcslen(ptr: *const u16) -> usize {
    let mut len = 0;
    unsafe {
        while *ptr.add(len) != 0 {
            len += 1;
        }
    }
    len
}

/// C-compatible function to combine two UTF-16 strings and return a UTF-8 C string
#[no_mangle]
pub extern "C" fn combine_strings(s1_ptr: *const u16, s2_ptr: *const u16) -> *mut c_char {
    let s1 = unsafe {
        if s1_ptr.is_null() {
            return ptr::null_mut();
        }
        let s1_slice = std::slice::from_raw_parts(s1_ptr, wcslen(s1_ptr));
        match String::from_utf16(s1_slice) {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        }
    };

    let s2 = unsafe {
        if s2_ptr.is_null() {
            return ptr::null_mut();
        }
        let s2_slice = std::slice::from_raw_parts(s2_ptr, wcslen(s2_ptr));
        match String::from_utf16(s2_slice) {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        }
    };

    let result = format!("{}{}", s1, s2);
    match CString::new(result) {
        Ok(c_string) => c_string.into_raw(),
        Err(_) => ptr::null_mut(),
    }
}

/// Free memory allocated by combine_strings
#[no_mangle]
pub extern "C" fn free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}