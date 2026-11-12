using System.Text;

namespace FoxTail.Core;

public record TailBatch(bool Reset, IReadOnlyList<string> Lines, string Partial, long Position, long Length, bool MoreAvailable);

/// <summary>Incremental UTF-8 reader. Each poll does bounded I/O; start reads only the last 2 MiB.</summary>
public sealed class TailReader(string path)
{
    public const int InitialBytes = 2 * 1024 * 1024;
    public const int MaxReadBytes = 256 * 1024;
    public const int MaxLineChars = 64 * 1024;
    private long position;
    private bool initialized;
    private byte[] signature = [];
    private DateTime creation;
    private Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder partial = new();
    private bool skipFirst;
    private bool truncatedLine;

    public TailBatch Poll()
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = file.Length;
        var stamp = File.GetCreationTimeUtc(path);
        var prefix = new byte[(int)Math.Min(64, length)];
        file.ReadExactly(prefix);
        var reset = !initialized || length < position || stamp != creation ||
            !prefix.AsSpan(0, Math.Min(prefix.Length, signature.Length)).SequenceEqual(signature.AsSpan(0, Math.Min(prefix.Length, signature.Length)));
        if (reset)
        {
            position = initialized ? 0 : Math.Max(0, length - InitialBytes);
            skipFirst = position > 0;
            decoder = Encoding.UTF8.GetDecoder();
            partial.Clear();
            truncatedLine = false;
            initialized = true;
        }
        signature = prefix;
        creation = stamp;
        file.Position = position;
        var buffer = new byte[(int)Math.Min(MaxReadBytes, Math.Max(0, length - position))];
        var count = file.Read(buffer);
        position += count;
        var chars = new char[Encoding.UTF8.GetMaxCharCount(count)];
        var decoded = decoder.GetChars(buffer, 0, count, chars, 0, false);
        var lines = new List<string>();
        for (var i = 0; i < decoded; i++)
        {
            var ch = chars[i];
            if (ch == '\n')
            {
                if (!skipFirst) lines.Add(CurrentLine());
                partial.Clear();
                truncatedLine = false;
                skipFirst = false;
            }
            else if (!skipFirst)
            {
                if (partial.Length < MaxLineChars) partial.Append(ch);
                else truncatedLine = true;
            }
        }
        return new TailBatch(reset, lines, skipFirst ? "" : CurrentLine(), position, length, position < length);
    }

    private string CurrentLine() => partial.ToString().TrimEnd('\r') + (truncatedLine ? " … [line truncated]" : "");
}
