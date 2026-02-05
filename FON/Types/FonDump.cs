using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FON.Types;


public class FonDump : IEnumerable<KeyValuePair<ulong, FonCollection>>, IDisposable {
    public ConcurrentDictionary<ulong, FonCollection> FonObjects { get; private set; }

    public FonDump() {
        FonObjects = new(Environment.ProcessorCount, 1024);
    }

    public FonDump(int capacity) {
        FonObjects = new(Environment.ProcessorCount, capacity);
    }

    public FonCollection this[ulong index] {
        get => FonObjects[index];
        set => FonObjects[index] = value;
    }

    public int Count => FonObjects.Count;

    public void Dispose() {
        Parallel.ForEach(FonObjects.Values, disposable => disposable.Dispose());
        FonObjects.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<KeyValuePair<ulong, FonCollection>> GetEnumerator() => FonObjects.GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ulong id, FonCollection value) {
        if (!FonObjects.TryAdd(id, value)) {
            throw new InvalidOperationException($"FonCollection with id {id} already exists in the FON dump");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(ulong id, FonCollection value) => FonObjects.TryAdd(id, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(ulong id) => FonObjects.Remove(id, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FonCollection Get(ulong id) => FonObjects[id];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FonCollection? TryGet(ulong id) => FonObjects.TryGetValue(id, out FonCollection? value) ? value : null;
}
