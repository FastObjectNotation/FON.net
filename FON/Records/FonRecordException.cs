namespace FON.Records;


/// <summary>
/// Thrown when a collection cannot be read as a FON record: a required key is missing, or a key
/// holds a value that does not fit the member.
/// </summary>
public sealed class FonRecordException : Exception {
    public FonRecordException(string message) : base(message) {
    }


    public FonRecordException(string message, Exception? innerException) : base(message, innerException) {
    }
}
