using FON.Records;
using FON.Types;

namespace FON.Core;


public partial class Fon {
    /// <summary>
    /// Writes a record into a new owned collection; the caller disposes it. The record type is
    /// marked with <see cref="FonRecordAttribute"/>.
    /// </summary>
    public static FonCollection ToCollection<T>(T value) where T : IFonRecord<T> {
        return T.ToFon(value);
    }


    /// <summary>
    /// Reads a record from a collection. The result does not depend on the collection, which may
    /// be disposed afterwards.
    /// </summary>
    /// <exception cref="FonRecordException">A required key is missing or holds a value of another type.</exception>
    public static T FromCollection<T>(FonCollection collection) where T : IFonRecord<T> {
        return T.FromFon(collection);
    }


    /// <summary>Writes a record as one line of FON text, without a line break.</summary>
    public static string SerializeRecord<T>(T value) where T : IFonRecord<T> {
        using FonCollection collection = T.ToFon(value);
        return SerializeToString(collection);
    }


    /// <summary>Reads a record from one line of FON text.</summary>
    /// <exception cref="FonRecordException">A required key is missing or holds a value of another type.</exception>
    public static T DeserializeRecord<T>(string text) where T : IFonRecord<T> {
        using FonCollection collection = DeserializeCollection(text);
        return T.FromFon(collection);
    }


    /// <summary>Reads a record from one line of FON text in UTF-8.</summary>
    /// <exception cref="FonRecordException">A required key is missing or holds a value of another type.</exception>
    public static T DeserializeRecord<T>(ReadOnlySpan<byte> utf8) where T : IFonRecord<T> {
        using FonCollection collection = DeserializeCollection(utf8);
        return T.FromFon(collection);
    }
}
