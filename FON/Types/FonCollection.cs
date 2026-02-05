using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FON.Types;


public class FonCollection : IEnumerable<KeyValuePair<string, object>>, IDisposable {
    private ConcurrentDictionary<string, object> Collection = new();

    public object this[string key] {
        get => Collection[key];
        set => Collection[key] = value;
    }

    public void Dispose() {
        foreach (var disposable in Collection.Values.OfType<IDisposable>()) {
            disposable.Dispose();
        }
        Collection.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Collection.GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(string key, object value) {
        if (!Collection.TryAdd(key, value)) {
            throw new InvalidOperationException($"Object with key {key} already exists in the collection");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(string key, object value) => Collection.TryAdd(key, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(string key) => Collection.Remove(key, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object Get(string key) => Collection[key];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>(string key) => (T)Collection[key];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? TryGet(string key) => Collection.TryGetValue(key, out object? value) ? value : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? TryGet<T>(string key) where T : class => Collection.TryGetValue(key, out object? value) && value is T ? (T)value : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? TryGetNullable<T>(string key) where T : struct => Collection.TryGetValue(key, out object? value) && value is T ? (T)value : null;




    public static FonCollection Serialize<T>(T obj) {
        var properties = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var collection = new FonCollection();

        foreach (var property in properties) {
            var value = property.GetValue(obj);
            if (value != null) {
                collection.Add(property.Name, value);
            }
        }

        return collection;
    }



    public T Deserialize<T>() where T : new() {
        T result = new T();
        object boxedResult = result;

        var properties = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var property in properties) {
            if (Collection.TryGetValue(property.Name, out var value) && value.GetType().IsAssignableFrom(property.PropertyType)) {
                property.SetValue(boxedResult, value);
            }
        }

        return (T)boxedResult;
    }

}
