using FON.Types;
using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace FON.Core;

public partial class Fon {
    /// <summary>
    /// Optimized parallel deserialization.
    /// Reads file once, parses in parallel.
    /// </summary>
    public static async Task<FonDump> DeserializeFromFileAsync(FileInfo file, int? maxDegreeOfParallelism = null) {
        var parallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;

        // Read all lines in one pass
        var lines = await ReadAllLinesOptimizedAsync(file);
        var fonDump = new FonDump(lines.Length);

        // Parallel parsing of all lines
        Parallel.For(0, lines.Length, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i => {
            if (!string.IsNullOrEmpty(lines[i])) {
                var collection = DeserializeLineOptimized(lines[i].AsSpan());
                fonDump.TryAdd((ulong)i, collection);
            }
        });

        return fonDump;
    }




    /// <summary>
    /// Optimized deserialization with chunked reading.
    /// Better for very large files - less memory pressure.
    /// </summary>
    public static async Task<FonDump> DeserializeFromFileChunkedAsync(FileInfo file, int chunkSize = 10000, int? maxDegreeOfParallelism = null) {
        var parallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;
        var fonDump = new FonDump();

        await using var fileStream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 256 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 256 * 1024);

        var lineBuffer = new List<string>(chunkSize);
        ulong globalIndex = 0;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null) {
            lineBuffer.Add(line);

            if (lineBuffer.Count >= chunkSize) {
                await ProcessChunkAsync(fonDump, lineBuffer, globalIndex, parallelism);
                globalIndex += (ulong)lineBuffer.Count;
                lineBuffer.Clear();
            }
        }

        if (lineBuffer.Count > 0) {
            await ProcessChunkAsync(fonDump, lineBuffer, globalIndex, parallelism);
        }

        return fonDump;
    }




    /// <summary>
    /// Automatic selection of best deserialization method.
    /// </summary>
    public static async Task<FonDump> DeserializeFromFileAutoAsync(FileInfo file, int? maxDegreeOfParallelism = null) {
        var fileSize = file.Length;

        // For files < 500MB - load everything into memory and parse in parallel
        // For files >= 500MB - use chunked approach
        if (fileSize < 500 * 1024 * 1024) {
            return await DeserializeFromFileAsync(file, maxDegreeOfParallelism);
        } else {
            return await DeserializeFromFileChunkedAsync(file, 10000, maxDegreeOfParallelism);
        }
    }




    private static async Task ProcessChunkAsync(FonDump fonDump, List<string> lines, ulong startIndex, int parallelism) {
        var results = new FonCollection?[lines.Count];

        Parallel.For(0, lines.Count, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i => {
            if (!string.IsNullOrEmpty(lines[i])) {
                results[i] = DeserializeLineOptimized(lines[i].AsSpan());
            }
        });

        for (int i = 0; i < results.Length; i++) {
            if (results[i] != null) {
                fonDump.TryAdd(startIndex + (ulong)i, results[i]!);
            }
        }
    }




    private static async Task<string[]> ReadAllLinesOptimizedAsync(FileInfo file) {
        // Optimized reading of all lines
        await using var fileStream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 256 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 256 * 1024);

        // Estimate line count: average line ~50KB for our data
        var estimatedLines = (int)Math.Max(100, file.Length / 50000);
        var lines = new List<string>(estimatedLines);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null) {
            lines.Add(line);
        }

        return lines.ToArray();
    }




    /// <summary>
    /// Optimized line parsing using Span and SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static FonCollection DeserializeLineOptimized(ReadOnlySpan<char> chars) {
        var fonList = new FonCollection();
        int position = 0;

        while (position < chars.Length) {
            var remaining = chars.Slice(position);

            // Search for '=' using SIMD-optimized IndexOf
            var eqIndex = remaining.IndexOf('=');
            if (eqIndex < 0) {
                break;
            }

            var key = remaining.Slice(0, eqIndex).ToString();
            position += eqIndex + 1;
            remaining = chars.Slice(position);

            // Data type (1 char + ':')
            if (remaining.Length < 2 || remaining[1] != ':') {
                throw new FormatException($"Invalid format at position {position}");
            }

            var typeChar = remaining[0];
            var type = GetType(typeChar);
            if (type == null) {
                throw new FormatException($"Unknown type '{typeChar}' at position {position}");
            }

            position += 2;
            remaining = chars.Slice(position);

            // Parse value
            object data;
            int consumed;

            if (remaining.Length > 0 && remaining[0] == '[') {
                (data, consumed) = DeserializeArrayOptimized(remaining, type, typeChar);
            } else {
                (data, consumed) = DeserializeValueOptimized(remaining, type, typeChar);
            }

            fonList.Add(key, data);
            position += consumed;

            // Skip separator ','
            if (position < chars.Length && chars[position] == ',') {
                position++;
            }
        }

        return fonList;
    }




    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (object data, int consumed) DeserializeValueOptimized(ReadOnlySpan<char> chars, Type type, char typeChar) {
        if (typeChar == 's') {
            return DeserializeStringOptimized(chars);
        }

        if (typeChar == 'r') {
            return DeserializeRawOptimized(chars);
        }

        // Find end of value
        var endIndex = FindValueEnd(chars);
        var valueSpan = chars.Slice(0, endIndex);
        var consumed = endIndex;

        // Skip ',' or ']' after value
        if (consumed < chars.Length && (chars[consumed] == ',' || chars[consumed] == ']')) {
            if (chars[consumed] == ',') {
                consumed++;
            }
        }

        object value = typeChar switch {
            'e' => byte.Parse(valueSpan),
            't' => short.Parse(valueSpan),
            'i' => int.Parse(valueSpan),
            'u' => uint.Parse(valueSpan),
            'l' => long.Parse(valueSpan),
            'g' => ulong.Parse(valueSpan),
            'f' => float.Parse(valueSpan, CultureInfo.InvariantCulture),
            'd' => double.Parse(valueSpan, CultureInfo.InvariantCulture),
            'b' => valueSpan[0] != '0',
            _ => throw new NotSupportedException($"Type '{typeChar}' is not supported")
        };

        return (value, consumed);
    }




    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindValueEnd(ReadOnlySpan<char> chars) {
        for (int i = 0; i < chars.Length; i++) {
            var c = chars[i];
            if (c == ',' || c == ']' || c == '\r' || c == '\n') {
                return i;
            }
        }
        return chars.Length;
    }




    private static (IList data, int consumed) DeserializeArrayOptimized(ReadOnlySpan<char> chars, Type elementType, char typeChar) {
        if (chars[0] != '[') {
            throw new FormatException("Array must start with '['");
        }

        // Find closing bracket
        var closeIndex = FindClosingBracket(chars);
        var arrayContent = chars.Slice(1, closeIndex - 1);

        // Create typed list
        var list = CreateTypedList(elementType, typeChar);

        if (arrayContent.Length == 0) {
            var consumed = closeIndex + 1;
            if (consumed < chars.Length && chars[consumed] == ',') {
                consumed++;
            }

            return (list, consumed);
        }

        // Parse elements
        int position = 0;
        while (position < arrayContent.Length) {
            var remaining = arrayContent.Slice(position);
            var (value, valueConsumed) = DeserializeValueOptimized(remaining, elementType, typeChar);
            list.Add(value);
            position += valueConsumed;
        }

        var totalConsumed = closeIndex + 1;
        if (totalConsumed < chars.Length && chars[totalConsumed] == ',') {
            totalConsumed++;
        }

        return (list, totalConsumed);
    }




    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindClosingBracket(ReadOnlySpan<char> chars) {
        int depth = 0;
        bool inString = false;

        for (int i = 0; i < chars.Length; i++) {
            var c = chars[i];

            if (c == '"' && (i == 0 || chars[i - 1] != '\\')) {
                inString = !inString;
            } else if (!inString) {
                if (c == '[') {
                    depth++;
                } else if (c == ']') {
                    depth--;
                    if (depth == 0) {
                        return i;
                    }
                }
            }
        }

        throw new FormatException("Closing bracket not found");
    }




    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IList CreateTypedList(Type elementType, char typeChar) {
        // Avoid Activator.CreateInstance - use direct creation
        return typeChar switch {
            'e' => new List<byte>(),
            't' => new List<short>(),
            'i' => new List<int>(),
            'u' => new List<uint>(),
            'l' => new List<long>(),
            'g' => new List<ulong>(),
            'f' => new List<float>(),
            'd' => new List<double>(),
            'b' => new List<bool>(),
            's' => new List<string>(),
            'r' => new List<RawData>(),
            _ => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!
        };
    }




    private static (string data, int consumed) DeserializeStringOptimized(ReadOnlySpan<char> chars) {
        if (chars[0] != '"') {
            throw new FormatException("String must start with '\"'");
        }

        // Find closing quote
        int endQuote = 1;
        while (endQuote < chars.Length) {
            if (chars[endQuote] == '"' && chars[endQuote - 1] != '\\') {
                break;
            }
            endQuote++;
        }

        var stringContent = chars.Slice(1, endQuote - 1);

        // Check if escape sequence processing is needed
        var backslashIndex = stringContent.IndexOf('\\');
        string result;

        if (backslashIndex < 0) {
            // No escape sequences - just create string
            result = stringContent.ToString();
        } else {
            // Has escapes - process
            result = UnescapeString(stringContent);
        }

        var consumed = endQuote + 1;
        if (consumed < chars.Length && chars[consumed] == ',')
            consumed++;

        return (result, consumed);
    }




    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static string UnescapeString(ReadOnlySpan<char> chars) {
        // Use stackalloc for small strings
        var maxLength = chars.Length;
        char[]? rentedArray = null;

        Span<char> buffer = maxLength <= 1024
            ? stackalloc char[maxLength]
            : (rentedArray = ArrayPool<char>.Shared.Rent(maxLength));

        try {
            int writePos = 0;

            for (int i = 0; i < chars.Length; i++) {
                if (chars[i] == '\\' && i + 1 < chars.Length) {
                    i++;
                    buffer[writePos++] = chars[i] switch {
                        '"' => '"',
                        '\\' => '\\',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        'b' => '\b',
                        'f' => '\f',
                        '/' => '/',
                        _ => chars[i]
                    };
                } else {
                    buffer[writePos++] = chars[i];
                }
            }

            return new string(buffer.Slice(0, writePos));
        } finally {
            if (rentedArray != null) {
                ArrayPool<char>.Shared.Return(rentedArray);
            }
        }
    }




    private static (RawData data, int consumed) DeserializeRawOptimized(ReadOnlySpan<char> chars) {
        if (chars[0] != '"') {
            throw new FormatException("RawData must start with '\"'");
        }

        // Find closing quote
        int endQuote = 1;
        while (endQuote < chars.Length && chars[endQuote] != '"') {
            endQuote++;
        }

        var base64Content = chars.Slice(1, endQuote - 1);
        var rawData = new RawData(base64Content);

        if (DeserializeRawUnpack) {
            rawData.Unpack();
        }

        var consumed = endQuote + 1;
        if (consumed < chars.Length && chars[consumed] == ',') {
            consumed++;
        }

        return (rawData, consumed);
    }
}
