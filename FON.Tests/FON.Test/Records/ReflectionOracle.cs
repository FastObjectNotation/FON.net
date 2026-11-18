using System.Collections;
using System.Reflection;
using System.Text;
using FON.Core;
using FON.Types;

namespace FON.Test.Records;


/// <summary>
/// The reflection mapper that the generated code replaces, ported unchanged in behavior from
/// Decerium's FonCodec. The tests check that generated code writes the same bytes and reads the
/// same text.
/// </summary>
public sealed class ReflectionOracle<T> {
    private static readonly PropertyInfo[] readableProperties = GetReadableProperties();
    private static readonly ConstructorInfo chosenConstructor = ChooseConstructor();


    public byte[] Encode(T value) {
        try {
            using FonCollection collection = ToCollection(value!);
            string text = Fon.SerializeToString(collection);
            return Encoding.UTF8.GetBytes(text);
        } catch (Exception ex) when (ex is not OracleException) {
            throw new OracleException("FON encode failed.", ex);
        }
    }


    public T Decode(byte[] bytes) {
        try {
            using FonCollection collection = Fon.DeserializeCollection(bytes);
            return FromCollection(collection);
        } catch (Exception ex) when (ex is not OracleException) {
            throw new OracleException("FON decode failed.", ex);
        }
    }


    private static FonCollection ToCollection(T value) {
        FonCollection collection = new FonCollection();
        foreach (PropertyInfo prop in readableProperties) {
            object? propValue = prop.GetValue(value);
            if (propValue is null) {
                continue;
            }
            collection.Add(prop.Name, EncodeValue(prop.PropertyType, propValue));
        }
        return collection;
    }


    private static T FromCollection(FonCollection collection) {
        if (chosenConstructor.GetParameters().Length > 0) {
            ParameterInfo[] parameters = chosenConstructor.GetParameters();
            object?[] args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) {
                args[i] = DecodeValue(
                    collection,
                    parameters[i].Name!,
                    parameters[i].ParameterType,
                    allowMissing: IsNullableParameter(parameters[i])
                );
            }
            return (T)chosenConstructor.Invoke(args);
        }
        T instance = (T)chosenConstructor.Invoke(Array.Empty<object?>());
        foreach (PropertyInfo prop in readableProperties) {
            if (!prop.CanWrite) {
                continue;
            }
            object? val = DecodeValue(
                collection,
                prop.Name,
                prop.PropertyType,
                allowMissing: IsNullableProperty(prop)
            );
            prop.SetValue(instance, val);
        }
        return instance;
    }


    private static object EncodeValue(Type type, object value) {
        if (Fon.SupportTypes.ContainsKey(type)) {
            return value;
        }
        Type? elementType = GetCollectionElementType(type);
        if (elementType is not null) {
            if (Fon.SupportTypes.ContainsKey(elementType)) {
                return ToTypedList(elementType, (IEnumerable)value);
            }
            IEnumerable source = (IEnumerable)value;
            List<FonCollection> fonList = new List<FonCollection>();
            foreach (object? item in source) {
                fonList.Add(RecordToFonCollection(elementType, item!));
            }
            return fonList;
        }
        return RecordToFonCollection(type, value);
    }


    private static object? DecodeValue(FonCollection collection, string key, Type type, bool allowMissing) {
        if (Fon.SupportTypes.ContainsKey(type)) {
            return DecodeScalar(collection, key, type, allowMissing);
        }
        Type? elementType = GetCollectionElementType(type);
        if (elementType is not null) {
            return DecodeCollectionValue(collection, key, type, elementType, allowMissing);
        }
        FonCollection? child = collection.TryGet<FonCollection>(key);
        if (child is null) {
            return MissingReference(type, allowMissing, key);
        }
        return CollectionToRecord(type, child);
    }


    private static object? DecodeScalar(FonCollection collection, string key, Type type, bool allowMissing) {
        object? value = collection.TryGet(key);
        if (value is not null) {
            return value;
        }
        return type.IsValueType ? MissingScalar(type, allowMissing, key) : MissingReference(type, allowMissing, key);
    }


    private static object DecodeCollectionValue(
        FonCollection collection,
        string key,
        Type targetType,
        Type elementType,
        bool allowMissing
    ) {
        object? raw = collection.TryGet(key);
        if (raw is null) {
            return MissingList(targetType, elementType, allowMissing, key);
        }
        if (Fon.SupportTypes.ContainsKey(elementType)) {
            return BuildCollection(targetType, elementType, ((IEnumerable)raw).Cast<object>().ToArray());
        }
        List<FonCollection> children = (List<FonCollection>)raw;
        object[] decoded = new object[children.Count];
        for (int i = 0; i < children.Count; i++) {
            decoded[i] = CollectionToRecord(elementType, children[i]);
        }
        return BuildCollection(targetType, elementType, decoded);
    }


    private static object? MissingScalar(Type type, bool allowMissing, string key) {
        if (allowMissing) {
            return Activator.CreateInstance(type);
        }
        throw new OracleException($"FON decode: required field '{key}' is missing.");
    }


    private static object? MissingReference(Type type, bool allowMissing, string key) {
        if (allowMissing) {
            return null;
        }
        throw new OracleException($"FON decode: required field '{key}' is missing.");
    }


    private static object MissingList(Type targetType, Type elementType, bool allowMissing, string key) {
        if (allowMissing) {
            return BuildCollection(targetType, elementType, Array.Empty<object>());
        }
        throw new OracleException($"FON decode: required list field '{key}' is missing.");
    }


    private static FonCollection RecordToFonCollection(Type type, object value) {
        IEnumerable<PropertyInfo> props = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
        FonCollection collection = new FonCollection();
        foreach (PropertyInfo? prop in props) {
            object? propValue = prop.GetValue(value);
            if (propValue is null) {
                continue;
            }
            collection.Add(prop.Name, EncodeValue(prop.PropertyType, propValue));
        }
        return collection;
    }


    private static object CollectionToRecord(Type type, FonCollection collection) {
        ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        HashSet<string> propertyNames = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        ConstructorInfo? bestCtor = ctors
            .Where(c => c.GetParameters().All(p => p.Name is not null && propertyNames.Contains(p.Name)))
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
        if (bestCtor is not null && bestCtor.GetParameters().Length > 0) {
            ParameterInfo[] ctorParams = bestCtor.GetParameters();
            object?[] args = new object?[ctorParams.Length];
            for (int i = 0; i < ctorParams.Length; i++) {
                args[i] = DecodeValue(
                    collection,
                    ctorParams[i].Name!,
                    ctorParams[i].ParameterType,
                    allowMissing: IsNullableParameter(ctorParams[i])
                );
            }
            return bestCtor.Invoke(args);
        }
        ConstructorInfo? parameterlessCtor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (parameterlessCtor is null) {
            throw new OracleException($"Type {type.FullName} has no suitable constructor for FON decode.");
        }
        object instance = parameterlessCtor.Invoke(Array.Empty<object?>());
        IEnumerable<PropertyInfo> writableProps = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);
        foreach (PropertyInfo? prop in writableProps) {
            prop.SetValue(instance, DecodeValue(
                collection,
                prop.Name,
                prop.PropertyType,
                allowMissing: IsNullableProperty(prop)
            ));
        }
        return instance;
    }


    private static object ToTypedList(Type elementType, IEnumerable source) {
        Type listType = typeof(List<>).MakeGenericType(elementType);
        IList list = (IList)Activator.CreateInstance(listType)!;
        foreach (object? item in source) {
            list.Add(item);
        }
        return list;
    }


    private static Type? GetCollectionElementType(Type type) {
        if (type.IsArray) {
            return type.GetElementType();
        }
        if (!type.IsGenericType) {
            return null;
        }
        Type? ienum = type.GetInterfaces()
            .Concat(new[] { type })
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return ienum?.GetGenericArguments()[0];
    }


    private static object BuildCollection(Type targetType, Type elementType, object[] elements) {
        if (targetType.IsArray) {
            Array arr = Array.CreateInstance(elementType, elements.Length);
            for (int i = 0; i < elements.Length; i++) {
                arr.SetValue(elements[i], i);
            }
            return arr;
        }
        Type listType = typeof(List<>).MakeGenericType(elementType);
        IList list = (IList)Activator.CreateInstance(listType)!;
        foreach (object element in elements) {
            list.Add(element);
        }
        return list;
    }


    private static PropertyInfo[] GetReadableProperties() {
        return typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();
    }


    private static ConstructorInfo ChooseConstructor() {
        ConstructorInfo[] ctors = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (ctors.Length == 0) {
            throw new OracleException($"Type {typeof(T).FullName} has no public instance constructor.");
        }
        HashSet<string> propertyNames = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        ConstructorInfo? bestMatch = ctors
            .Where(c => c.GetParameters().All(p => p.Name is not null && propertyNames.Contains(p.Name)))
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
        if (bestMatch is not null && bestMatch.GetParameters().Length > 0) {
            return bestMatch;
        }
        ConstructorInfo? parameterless = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (parameterless is not null) {
            return parameterless;
        }
        return ctors.OrderByDescending(c => c.GetParameters().Length).First();
    }


    private static bool IsNullableParameter(ParameterInfo parameter) {
        if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null) {
            return true;
        }
        return new NullabilityInfoContext().Create(parameter).ReadState == NullabilityState.Nullable;
    }


    private static bool IsNullableProperty(PropertyInfo property) {
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null) {
            return true;
        }
        return new NullabilityInfoContext().Create(property).WriteState == NullabilityState.Nullable;
    }
}



public sealed class OracleException : Exception {
    public OracleException(string message, Exception? inner = null) : base(message, inner) {
    }
}
