use std::fmt;


pub enum FonNativeError {
    Parse(String),
    Write(String),
    InvalidArgument(String),
}


impl fmt::Display for FonNativeError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            FonNativeError::Parse(m) => write!(f, "{}", m),
            FonNativeError::Write(m) => write!(f, "{}", m),
            FonNativeError::InvalidArgument(m) => write!(f, "{}", m),
        }
    }
}
