using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>SHA-256 of file contents, memoised per run by path, size and modification time.</summary>
public sealed class ContentHasher
{
    private readonly IFileSystem _fs;
    private readonly ConcurrentDictionary<string, (long Length, long Ticks, string Hash)> _memo = new(StringComparer.Ordinal);

    /// <summary>Creates a hasher reading through <paramref name="fs"/>.</summary>
    public ContentHasher(IFileSystem fs)
    {
        _fs = fs;
    }

    /// <summary>Lowercase hex SHA-256 of the file.</summary>
    public string HashFile(string path)
    {
        var stamp = _fs.GetStamp(path);
        if (_memo.TryGetValue(path, out var known) && known.Length == stamp.Length && known.Ticks == stamp.LastWriteUtcTicks)
        {
            return known.Hash;
        }

        var hash = HashBytes(_fs.ReadAllBytes(path));
        _memo[path] = (stamp.Length, stamp.LastWriteUtcTicks, hash);
        return hash;
    }

    /// <summary>Lowercase hex SHA-256 of bytes.</summary>
    public static string HashBytes(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Seeds the memo, for example from a persisted cache.</summary>
    public void Remember(string path, long length, long ticks, string hash) => _memo[path] = (length, ticks, hash);

    /// <summary>Snapshot of the memo for persisting.</summary>
    public IReadOnlyDictionary<string, (long Length, long Ticks, string Hash)> Snapshot() => new Dictionary<string, (long, long, string)>(_memo, StringComparer.Ordinal);
}
