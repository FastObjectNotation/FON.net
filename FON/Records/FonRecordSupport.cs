using System.Collections;
using System.ComponentModel;
using FON.Types;

namespace FON.Records;


/// <summary>
/// Reads and writes the members of FON records. Called by the code that the FON source generator
/// emits; not meant to be called directly.
/// </summary>
/// <remarks>
/// A missing key throws <see cref="FonRecordException"/> unless the member is nullable. A nullable
/// list member that is missing reads as an empty list. Numbers widen the way reflection widens
/// them: a stored int reads into a long, float or double member, never the other way.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FonRecordSupport {
    // ==================== WRITE ====================

    public static void WriteEnum<T>(FonCollection collection, string key, T value) where T : struct, Enum {
        collection.Add(key, value.ToString());
    }


    public static void WriteCollection(FonCollection collection, string key, FonCollection value) {
        AddOwned(collection, key, value.Clone());
    }


    public static void WriteRecord<T>(FonCollection collection, string key, T value) where T : IFonRecord<T> {
        AddOwned(collection, key, T.ToFon(value));
    }


    /// <summary>Writes numbers, bools or strings as an array of that type.</summary>
    public static void WriteList<T>(FonCollection collection, string key, IEnumerable<T> items) {
        collection.Add(key, new List<T>(items));
    }


    public static void WriteEnumList<T>(FonCollection collection, string key, IEnumerable<T> items) where T : struct, Enum {
        List<string> names = new List<string>();
        foreach (T item in items) {
            names.Add(item.ToString());
        }
        collection.Add(key, names);
    }


    public static void WriteCollectionList(FonCollection collection, string key, IEnumerable<FonCollection> items) {
        List<FonCollection> clones = new List<FonCollection>();
        try {
            foreach (FonCollection item in items) {
                clones.Add(item.Clone());
            }
            collection.Add(key, clones);
        } catch {
            DisposeAll(clones);
            throw;
        }
    }


    public static void WriteRecordList<T>(FonCollection collection, string key, IEnumerable<T> items) where T : IFonRecord<T> {
        List<FonCollection> children = new List<FonCollection>();
        try {
            foreach (T item in items) {
                children.Add(T.ToFon(item));
            }
            collection.Add(key, children);
        } catch {
            DisposeAll(children);
            throw;
        }
    }



    // ==================== READ ONE VALUE ====================

    /// <summary>Reads a number or a bool. A missing key throws.</summary>
    public static T ReadValue<T>(FonCollection collection, string key) where T : struct {
        object stored = collection.TryGet(key) ?? throw MissingField(key);
        return ConvertScalar<T>(stored, key);
    }


    /// <summary>Reads a number or a bool. A missing key reads as null.</summary>
    public static T? ReadOptionalValue<T>(FonCollection collection, string key) where T : struct {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return null;
        }
        return ConvertScalar<T>(stored, key);
    }


    public static string ReadString(FonCollection collection, string key) {
        object stored = collection.TryGet(key) ?? throw MissingField(key);
        return ConvertReference<string>(stored, key, "a string");
    }


    public static string? ReadOptionalString(FonCollection collection, string key) {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return null;
        }
        return ConvertReference<string>(stored, key, "a string");
    }


    public static RawData ReadRawData(FonCollection collection, string key) {
        object stored = collection.TryGet(key) ?? throw MissingField(key);
        return ConvertReference<RawData>(stored, key, "a raw value");
    }


    public static RawData? ReadOptionalRawData(FonCollection collection, string key) {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return null;
        }
        return ConvertReference<RawData>(stored, key, "a raw value");
    }


    /// <summary>Reads a nested collection as an owned copy.</summary>
    public static FonCollection ReadCollection(FonCollection collection, string key) {
        object stored = collection.TryGet(key) ?? throw MissingField(key);
        return ConvertReference<FonCollection>(stored, key, "an object").Clone();
    }


    /// <summary>Reads a nested collection as an owned copy. A missing key reads as null.</summary>
    public static FonCollection? ReadOptionalCollection(FonCollection collection, string key) {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return null;
        }
        return ConvertReference<FonCollection>(stored, key, "an object").Clone();
    }


    /// <summary>Reads an enum from its name, as <see cref="WriteEnum{T}"/> writes it.</summary>
    public static T ReadEnum<T>(FonCollection collection, string key) where T : struct, Enum {
        object stored = collection.TryGet(key) ?? throw MissingField(key);
        return ParseEnum<T>(stored, key);
    }


    public static T? ReadOptionalEnum<T>(FonCollection collection, string key) where T : struct, Enum {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return null;
        }
        return ParseEnum<T>(stored, key);
    }


    /// <summary>
    /// Reads a nested record. A key that is missing or does not hold an object throws.
    /// </summary>
    public static T ReadRecord<T>(FonCollection collection, string key) where T : IFonRecord<T> {
        if (collection.TryGet(key) is not FonCollection child) {
            throw MissingField(key);
        }
        return T.FromFon(child);
    }


    /// <summary>
    /// Reads a nested record of a reference type. A key that is missing or does not hold an
    /// object reads as null.
    /// </summary>
    public static T? ReadOptionalRecord<T>(FonCollection collection, string key) where T : class, IFonRecord<T> {
        if (collection.TryGet(key) is not FonCollection child) {
            return null;
        }
        return T.FromFon(child);
    }


    /// <summary>
    /// Reads a nested record of a value type. A key that is missing or does not hold an object
    /// reads as null.
    /// </summary>
    public static T? ReadOptionalRecordValue<T>(FonCollection collection, string key) where T : struct, IFonRecord<T> {
        if (collection.TryGet(key) is not FonCollection child) {
            return null;
        }
        return T.FromFon(child);
    }



    // ==================== READ LISTS ====================

    /// <summary>
    /// Reads numbers, bools or strings into a list. Every element must have exactly the type
    /// <typeparamref name="T"/>. A missing key throws, or reads as an empty list when
    /// <paramref name="optional"/>.
    /// </summary>
    public static List<T> ReadList<T>(FonCollection collection, string key, bool optional) {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return MissingList<T>(key, optional);
        }
        if (stored is List<T> list) {
            return list;
        }
        IEnumerable sequence = AsSequence(stored, key, typeof(T));
        List<T> result = new List<T>();
        foreach (object? element in sequence) {
            if (element is not T item) {
                throw WrongValue(key, stored, ListName(typeof(T)));
            }
            result.Add(item);
        }
        return result;
    }


    /// <summary>
    /// Reads numbers, bools or strings into an array. Numbers widen as in <see cref="ReadValue{T}"/>.
    /// </summary>
    public static T[] ReadArray<T>(FonCollection collection, string key, bool optional) {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return MissingList<T>(key, optional).ToArray();
        }
        IEnumerable sequence = AsSequence(stored, key, typeof(T));
        List<T> result = new List<T>();
        foreach (object? element in sequence) {
            if (element is null || !TryConvertScalar(element, out T item)) {
                throw WrongValue(key, stored, ListName(typeof(T)));
            }
            result.Add(item);
        }
        return result.ToArray();
    }


    public static List<T> ReadEnumList<T>(FonCollection collection, string key, bool optional) where T : struct, Enum {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return MissingList<T>(key, optional);
        }
        if (stored is not List<string> names) {
            throw WrongValue(key, stored, "a list of enum names");
        }
        List<T> result = new List<T>(names.Count);
        foreach (string name in names) {
            result.Add(ParseEnum<T>(name, key));
        }
        return result;
    }


    public static T[] ReadEnumArray<T>(FonCollection collection, string key, bool optional) where T : struct, Enum {
        return ReadEnumList<T>(collection, key, optional).ToArray();
    }


    /// <summary>Reads nested collections as owned copies.</summary>
    public static List<FonCollection> ReadCollectionList(FonCollection collection, string key, bool optional) {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return MissingList<FonCollection>(key, optional);
        }
        IEnumerable sequence = AsSequence(stored, key, typeof(FonCollection));
        List<FonCollection> result = new List<FonCollection>();
        try {
            foreach (object? element in sequence) {
                if (element is not FonCollection child) {
                    throw WrongValue(key, stored, ListName(typeof(FonCollection)));
                }
                result.Add(child.Clone());
            }
        } catch {
            DisposeAll(result);
            throw;
        }
        return result;
    }


    public static FonCollection[] ReadCollectionArray(FonCollection collection, string key, bool optional) {
        return ReadCollectionList(collection, key, optional).ToArray();
    }


    public static List<T> ReadRecordList<T>(FonCollection collection, string key, bool optional) where T : IFonRecord<T> {
        object? stored = collection.TryGet(key);
        if (stored is null) {
            return MissingList<T>(key, optional);
        }
        if (stored is not List<FonCollection> children) {
            throw WrongValue(key, stored, "a list of objects");
        }
        List<T> result = new List<T>(children.Count);
        foreach (FonCollection child in children) {
            result.Add(T.FromFon(child));
        }
        return result;
    }


    public static T[] ReadRecordArray<T>(FonCollection collection, string key, bool optional) where T : IFonRecord<T> {
        return ReadRecordList<T>(collection, key, optional).ToArray();
    }



    // ==================== CONVERSION ====================

    private static T ConvertScalar<T>(object stored, string key) where T : struct {
        if (TryConvertScalar(stored, out T value)) {
            return value;
        }
        throw WrongValue(key, stored, ScalarName(typeof(T)));
    }


    private static T ConvertReference<T>(object stored, string key, string expected) where T : class {
        if (stored is T value) {
            return value;
        }
        throw WrongValue(key, stored, expected);
    }


    /// <summary>
    /// Converts a stored scalar to <typeparamref name="T"/>: the same type, or a widening
    /// conversion between numbers (the conversions that reflection applies to arguments).
    /// </summary>
    private static bool TryConvertScalar<T>(object stored, out T value) {
        if (stored is T exact) {
            value = exact;
            return true;
        }
        if (Widen(stored, typeof(T)) is T widened) {
            value = widened;
            return true;
        }
        value = default!;
        return false;
    }


    private static object? Widen(object stored, Type target) {
        if (target == typeof(short)) {
            if (stored is byte b) {
                return (short)b;
            }
            return null;
        }
        if (target == typeof(int)) {
            switch (stored) {
                case byte b:
                    return (int)b;
                case short s:
                    return (int)s;
                default:
                    return null;
            }
        }
        if (target == typeof(uint)) {
            if (stored is byte b) {
                return (uint)b;
            }
            return null;
        }
        if (target == typeof(long)) {
            switch (stored) {
                case byte b:
                    return (long)b;
                case short s:
                    return (long)s;
                case int i:
                    return (long)i;
                case uint u:
                    return (long)u;
                default:
                    return null;
            }
        }
        if (target == typeof(ulong)) {
            switch (stored) {
                case byte b:
                    return (ulong)b;
                case uint u:
                    return (ulong)u;
                default:
                    return null;
            }
        }
        if (target == typeof(float)) {
            switch (stored) {
                case byte b:
                    return (float)b;
                case short s:
                    return (float)s;
                case int i:
                    return (float)i;
                case uint u:
                    return (float)u;
                case long l:
                    return (float)l;
                case ulong g:
                    return (float)g;
                default:
                    return null;
            }
        }
        if (target == typeof(double)) {
            switch (stored) {
                case byte b:
                    return (double)b;
                case short s:
                    return (double)s;
                case int i:
                    return (double)i;
                case uint u:
                    return (double)u;
                case long l:
                    return (double)l;
                case ulong g:
                    return (double)g;
                case float f:
                    return (double)f;
                default:
                    return null;
            }
        }
        return null;
    }


    private static T ParseEnum<T>(object stored, string key) where T : struct, Enum {
        if (stored is not string name) {
            throw WrongValue(key, stored, "an enum name");
        }
        if (!Enum.TryParse(name, ignoreCase: false, out T value)) {
            throw new FonRecordException($"FON decode: field '{key}' holds '{name}', which is not a name of the enum.");
        }
        return value;
    }


    /// <summary>
    /// The elements of a stored value read as a list. Any enumerable value is accepted, as
    /// reflection accepts it; its elements are checked one by one.
    /// </summary>
    private static IEnumerable AsSequence(object stored, string key, Type element) {
        if (stored is IEnumerable sequence) {
            return sequence;
        }
        throw WrongValue(key, stored, ListName(element));
    }



    // ==================== ERRORS ====================

    private static List<T> MissingList<T>(string key, bool optional) {
        if (optional) {
            return new List<T>();
        }
        throw new FonRecordException($"FON decode: required list field '{key}' is missing.");
    }


    private static FonRecordException MissingField(string key) {
        return new FonRecordException($"FON decode: required field '{key}' is missing.");
    }


    private static FonRecordException WrongValue(string key, object stored, string expected) {
        return new FonRecordException($"FON decode: field '{key}' holds {Describe(stored)}, which cannot be read as {expected}.");
    }


    /// <summary>Names what a stored value is, for error messages.</summary>
    private static string Describe(object stored) {
        if (stored is IList list && stored is not FonCollection) {
            return list.Count == 0 ? "an empty list" : "a list of " + Noun(list[0]!.GetType()) + "s";
        }
        string noun = Noun(stored.GetType());
        return Article(noun) + " " + noun;
    }


    private static string ScalarName(Type type) {
        string noun = Noun(type);
        return Article(noun) + " " + noun;
    }


    private static string ListName(Type element) {
        return "a list of " + Noun(element) + "s";
    }


    private static string Noun(Type type) {
        if (type == typeof(byte)) {
            return "byte";
        }
        if (type == typeof(short)) {
            return "short";
        }
        if (type == typeof(int)) {
            return "int";
        }
        if (type == typeof(uint)) {
            return "uint";
        }
        if (type == typeof(long)) {
            return "long";
        }
        if (type == typeof(ulong)) {
            return "ulong";
        }
        if (type == typeof(float)) {
            return "float";
        }
        if (type == typeof(double)) {
            return "double";
        }
        if (type == typeof(bool)) {
            return "bool";
        }
        if (type == typeof(string)) {
            return "string";
        }
        if (type == typeof(RawData)) {
            return "raw value";
        }
        if (type == typeof(FonCollection)) {
            return "object";
        }
        return "value";
    }


    private static string Article(string noun) {
        return noun == "int" || noun == "uint" || noun == "object" ? "an" : "a";
    }


    private static void AddOwned(FonCollection collection, string key, FonCollection child) {
        try {
            collection.Add(key, child);
        } catch {
            child.Dispose();
            throw;
        }
    }


    private static void DisposeAll(List<FonCollection> children) {
        foreach (FonCollection child in children) {
            child.Dispose();
        }
    }
}
