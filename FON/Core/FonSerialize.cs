using FON.Types;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace FON.Core;


public partial class Fon {
    /// <summary>
    /// Threshold for switching between serialization methods.
    /// Below threshold - Pipeline, above - Chunked.
    /// </summary>
    public static int ParallelMethodThreshold { get; set; } = 2000;




    /// <summary>
    /// Parallel serialization to file.
    /// Each FonCollection is serialized in parallel, then written sequentially.
    /// </summary>
    public static async Task SerializeToFileAsync(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism = null) {
        var parallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;
        var orderedItems = dump.FonObjects.OrderBy(x => x.Key).ToArray();
        var serializedLines = new string[orderedItems.Length];

        // Parallel serialization to strings
        Parallel.For(0, orderedItems.Length, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i => {
            serializedLines[i] = SerializeToString(orderedItems[i].Value);
        });

        // Sequential write to file
        await using var fileStream = new FileStream(
            fileInfo.FullName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        await using var writer = new StreamWriter(fileStream, Encoding.UTF8, bufferSize: 64 * 1024);

        foreach (var line in serializedLines) {
            await writer.WriteLineAsync(line);
        }
    }




    /// <summary>
    /// Parallel serialization using pipeline (producer-consumer).
    /// Serialization and writing happen simultaneously.
    /// </summary>
    public static async Task SerializeToFilePipelineAsync(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism = null) {
        var parallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;
        var orderedItems = dump.FonObjects.OrderBy(x => x.Key).ToArray();

        // Use ConcurrentDictionary to store results while preserving order
        var results = new ConcurrentDictionary<int, string>();
        var nextToWrite = 0;
        var completedCount = 0;
        var totalCount = orderedItems.Length;

        await using var fileStream = new FileStream(
            fileInfo.FullName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var writer = new StreamWriter(fileStream, Encoding.UTF8, bufferSize: 64 * 1024);

        // Semaphore for signaling new ready results
        using var newResultAvailable = new SemaphoreSlim(0);

        // Start parallel serialization
        var serializationTask = Task.Run(() => {
            Parallel.For(0, orderedItems.Length, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i => {
                var serialized = SerializeToString(orderedItems[i].Value);
                results[i] = serialized;
                Interlocked.Increment(ref completedCount);
                newResultAvailable.Release();
            });
        });

        // Write results as they become ready (in correct order)
        while (nextToWrite < totalCount) {
            await newResultAvailable.WaitAsync();

            // Write all consecutive ready results
            while (results.TryRemove(nextToWrite, out var line)) {
                await writer.WriteLineAsync(line);
                nextToWrite++;
            }
        }

        await serializationTask;
    }




    /// <summary>
    /// Optimized parallel serialization with chunks.
    /// Serializes data in portions to reduce memory pressure on large files.
    /// </summary>
    public static async Task SerializeToFileChunkedAsync(FonDump dump, FileInfo fileInfo, int chunkSize = 1000, int? maxDegreeOfParallelism = null) {
        var parallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;
        var orderedItems = dump.FonObjects.OrderBy(x => x.Key).ToArray();

        await using var fileStream = new FileStream(
            fileInfo.FullName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 256 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        await using var writer = new StreamWriter(fileStream, Encoding.UTF8, bufferSize: 256 * 1024);

        // Process data in chunks
        for (int chunkStart = 0; chunkStart < orderedItems.Length; chunkStart += chunkSize) {
            var chunkEnd = Math.Min(chunkStart + chunkSize, orderedItems.Length);
            var currentChunkSize = chunkEnd - chunkStart;
            var serializedChunk = new string[currentChunkSize];

            // Parallel chunk serialization
            Parallel.For(0, currentChunkSize, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i => {
                serializedChunk[i] = SerializeToString(orderedItems[chunkStart + i].Value);
            });

            // Sequential chunk write
            foreach (var line in serializedChunk) {
                await writer.WriteLineAsync(line);
            }
        }
    }




    /// <summary>
    /// Automatic selection of best serialization method based on data size.
    /// Optimized based on benchmarks:
    /// - Pipeline: better for very small data (less synchronization overhead)
    /// - Chunked: better for medium and large data (less memory pressure)
    /// </summary>
    public static Task SerializeToFileAutoAsync(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism = null) {
        var count = dump.FonObjects.Count;
        var parallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;

        if (count < ParallelMethodThreshold) {
            // For small data - Pipeline with less overhead
            return SerializeToFilePipelineAsync(dump, fileInfo, parallelism);
        } else {
            // For medium and large - Chunked
            // Optimal chunk size: large enough for efficient parallelization,
            // but not too large to avoid memory pressure
            // Formula: ~50-100 chunks, minimum 500 records per chunk
            var targetChunks = Math.Max(parallelism * 4, 50);
            var chunkSize = Math.Max(500, Math.Min(2000, count / targetChunks));
            return SerializeToFileChunkedAsync(dump, fileInfo, chunkSize, parallelism);
        }
    }



    /// <summary>
    /// Synchronous version for compatibility.
    /// </summary>
    public static void SerializeToFileAuto(FonDump dump, FileInfo fileInfo, int? maxDegreeOfParallelism = null) {
        SerializeToFileAutoAsync(dump, fileInfo, maxDegreeOfParallelism).GetAwaiter().GetResult();
    }




    /// <summary>
    /// Synchronous FonCollection serialization to string using StringBuilder.
    /// Optimized for speed.
    /// </summary>
    public static string SerializeToString(FonCollection fonCollection) {
        // Preliminary size estimation (approximately 100 bytes per element + data)
        var sb = new StringBuilder(fonCollection.Count() * 200);

        bool isFirst = true;
        foreach (var kvp in fonCollection) {
            if (isFirst) {
                isFirst = false;
            } else {
                sb.Append(',');
            }

            SerializeKeyValue(sb, kvp.Key, kvp.Value);
        }

        return sb.ToString();
    }




    private static void SerializeKeyValue(StringBuilder sb, string key, object value) {
        sb.Append(key);
        sb.Append('=');
        SerializeObject(sb, value);
    }




    private static void SerializeObject(StringBuilder sb, object value) {
        if (value is IList list) {
            var arrayArgs = value.GetType().GenericTypeArguments;
            if (arrayArgs.Length == 0) {
                throw new InvalidOperationException($"Attempt to serialize list with undetermined item types. Type: {value.GetType().FullName}");
            }

            if (GetTypeShort(arrayArgs[0]) is not char shortType) {
                throw new InvalidOperationException($"Unsupported list item type for serialization. Item type: {arrayArgs[0].FullName}");
            }

            sb.Append(shortType);
            sb.Append(':');
            SerializeArray(sb, shortType, list);
        } else {
            if (GetTypeShort(value.GetType()) is not char shortType) {
                throw new InvalidOperationException($"Unsupported type for serialization. Type: {value.GetType().FullName}");
            }

            sb.Append(shortType);
            sb.Append(':');
            SerializeBaseObject(sb, shortType, value);
        }
    }




    private static void SerializeBaseObject(StringBuilder sb, char shortType, object value) {
        // Use Span for number formatting without allocations
        Span<char> buffer = stackalloc char[32];

        switch (shortType) {
            case 'e':
                if (((byte)value).TryFormat(buffer, out int bytesWritten)) {
                    sb.Append(buffer[..bytesWritten]);
                } else {
                    sb.Append(value.ToString());
                }
            break;

            case 't':
                if (((short)value).TryFormat(buffer, out int shortWritten)) {
                    sb.Append(buffer[..shortWritten]);
                } else {
                    sb.Append(value.ToString());
                }
            break;

            case 'i':
                if (((int)value).TryFormat(buffer, out int intWritten)) {
                    sb.Append(buffer[..intWritten]);
                } else {
                    sb.Append(value.ToString());
                }
            break;

            case 'u':
                if (((uint)value).TryFormat(buffer, out int uintWritten)) {
                    sb.Append(buffer[..uintWritten]);
                } else {
                    sb.Append(value.ToString());
                }
            break;

            case 'l':
                if (((long)value).TryFormat(buffer, out int longWritten)) {
                    sb.Append(buffer[..longWritten]);
                } else {
                    sb.Append(value.ToString());
                }
            break;

            case 'g':
                if (((ulong)value).TryFormat(buffer, out int ulongWritten)) {
                    sb.Append(buffer[..ulongWritten]);
                } else {
                    sb.Append(value.ToString());
                }
            break;

            case 'f':
                if (((float)value).TryFormat(buffer, out int floatWritten, default, CultureInfo.InvariantCulture)) {
                    sb.Append(buffer[..floatWritten]);
                } else {
                    sb.Append(((float)value).ToString(CultureInfo.InvariantCulture));
                }
            break;

            case 'd':
                if (((double)value).TryFormat(buffer, out int doubleWritten, default, CultureInfo.InvariantCulture)) {
                    sb.Append(buffer[..doubleWritten]);
                } else {
                    sb.Append(((double)value).ToString(CultureInfo.InvariantCulture));
                }
            break;

            case 'b':
                sb.Append((bool)value ? '1' : '0');
            break;

            case 's':
                SerializeString(sb, (string)value);
            break;

            case 'r':
                SerializeRaw(sb, (RawData)value);
            break;

            default:
                throw new Exception($"Unsupported type: {shortType}");
        }
    }




    private static void SerializeString(StringBuilder sb, string str) {
        sb.Append('"');

        foreach (char c in str) {
            switch (c) {
                case '\\':
                case '"':
                    sb.Append('\\');
                    sb.Append(c);
                break;

                case '\n':
                    sb.Append("\\n");
                break;

                case '\r':
                    sb.Append("\\r");
                break;

                case '\t':
                    sb.Append("\\t");
                break;

                case '\b':
                    sb.Append("\\b");
                break;

                case '\f':
                    sb.Append("\\f");
                break;

                default:
                    if (c < ' ') {
                        sb.Append("\\u");
                        sb.Append(((uint)c).ToString("X4"));
                    } else {
                        sb.Append(c);
                    }
                break;
            }
        }

        sb.Append('"');
    }




    private static void SerializeRaw(StringBuilder sb, RawData raw) {
        sb.Append('"');
        sb.Append(raw.Pack().encoded);
        sb.Append('"');
    }




    private static void SerializeArray(StringBuilder sb, char shortType, IList array) {
        sb.Append('[');

        bool isFirst = true;
        foreach (var item in array) {
            if (isFirst) {
                isFirst = false;
            } else {
                sb.Append(',');
            }

            SerializeBaseObject(sb, shortType, item);
        }

        sb.Append(']');
    }
}
