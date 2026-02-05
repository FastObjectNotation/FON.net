using System.Runtime.CompilerServices;

namespace FON.Types;


public readonly ref struct FonObject {
    public readonly string Key;
    public readonly object Value;

    // For super fast verification
    private static readonly SortedSet<char> KeyNameWhiteList = [.. "qwertyuioplkjhgfdsazxcvbnmQWERTYUIOPLKJHGFDSAZXCVBNM1234567890-_"];



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FonObject(string key, object value) {
        if (!CheckKeyName(key)) {
            throw new Exception($"Wrong FonObject key name: {key}");
        }
        this.Key = key;
        this.Value = value;
    }




    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FonObject(KeyValuePair<string, object> pair) {
        if (!CheckKeyName(pair.Key)) {
            throw new Exception($"Wrong FonObject key name: {pair.Key}");
        }
        Key = pair.Key;
        Value = pair.Value;
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckKeyName(string key) {
        foreach (var symbol in key) {
            if (!KeyNameWhiteList.Contains(symbol)) {
                return false;
            }
        }
        return true;
    }
}
