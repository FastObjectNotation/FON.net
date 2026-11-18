using FON.Types;

namespace FON.Records;


/// <summary>
/// A type that maps itself to and from a <see cref="FonCollection"/> without reflection.
/// Implemented by the FON source generator on types marked with <see cref="FonRecordAttribute"/>.
/// </summary>
/// <remarks>
/// Generic code reaches the mapping through the constraint, for example
/// <c>static byte[] Encode&lt;T&gt;(T value) where T : IFonRecord&lt;T&gt;</c> calling
/// <c>T.ToFon(value)</c>. <see cref="FON.Core.Fon.SerializeRecord{T}(T)"/> and
/// <see cref="FON.Core.Fon.DeserializeRecord{T}(string)"/> wrap the text round trip.
/// </remarks>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface IFonRecord<TSelf> where TSelf : IFonRecord<TSelf> {
    /// <summary>
    /// Writes the value into a new owned collection. The caller disposes it, or transfers it by
    /// putting it into another collection or a dump.
    /// </summary>
    static abstract FonCollection ToFon(TSelf value);


    /// <summary>
    /// Reads a value from the collection. Every value is copied out, so the result stays valid
    /// after the collection, or the root it is a view of, is disposed.
    /// </summary>
    /// <exception cref="FonRecordException">A required key is missing or holds a value of another type.</exception>
    static abstract TSelf FromFon(FonCollection collection);
}
