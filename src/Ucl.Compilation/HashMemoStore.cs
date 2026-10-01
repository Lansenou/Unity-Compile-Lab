using System.Text;
using System.Text.Json;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>Persists <see cref="ContentHasher"/>'s (path, size, mtime) -> hash memo, so a warm run hashes only changed files.</summary>
internal sealed class HashMemoStore
{
    private readonly IFileSystem _fs;
    private readonly string _path;

    public HashMemoStore(IFileSystem fs, string cacheDirectory)
    {
        _fs = fs;
        _path = Path.Combine(cacheDirectory, "files.json");
    }

    public void LoadInto(ContentHasher hasher)
    {
        if (!_fs.FileExists(_path))
        {
            return;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(_fs.ReadAllText(_path)) ?? [];
            foreach (var (path, e) in entries)
            {
                hasher.Remember(path, e.Length, e.Ticks, e.Hash);
            }
        }
        catch (JsonException)
        {
            // Unreadable memo: start cold.
        }
    }

    public void Save(ContentHasher hasher)
    {
        var entries = hasher.Snapshot()
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(e => e.Key, e => new Entry(e.Value.Length, e.Value.Ticks, e.Value.Hash), StringComparer.Ordinal);
        _fs.WriteAllBytes(_path, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries)));
    }

    private sealed record Entry(long Length, long Ticks, string Hash);
}
