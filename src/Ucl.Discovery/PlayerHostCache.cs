using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ucl.Discovery;

/// <summary>Content fingerprints and integrity manifests for an immutable player cache entry.</summary>
public static class PlayerHostCache
{
    private const string Manifest = "host-manifest.json";

    /// <summary>Hashes relative names and bytes, in ordinal order; missing trees are distinguished.</summary>
    public static string TreeDigest(string root) => TreeDigests([root])[root];

    /// <summary>Hashes overlapping trees once per file in this call, using bounded IO parallelism.</summary>
    public static IReadOnlyDictionary<string, string> TreeDigests(IReadOnlyList<string> roots)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var trees = roots.Distinct(comparer).ToDictionary(r => r, r => Directory.Exists(r)
            ? Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray() : null, comparer);
        var files = trees.Values.Where(p => p is not null).SelectMany(p => p!)
            .GroupBy(Path.GetFullPath, comparer).Select(g => g.First()).ToArray();
        var digests = new ConcurrentDictionary<string, byte[]>(comparer);
        try
        {
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 8 }, path =>
            {
                using var stream = File.OpenRead(path);
                digests[Path.GetFullPath(path)] = SHA256.HashData(stream);
            });
        }
        catch (AggregateException e) when (e.InnerExceptions.All(x => x is IOException or UnauthorizedAccessException))
        {
            throw new IOException("Input fingerprint failed: " + e.Message, e);
        }
        var results = new Dictionary<string, string>(comparer);
        foreach (var (root, paths) in trees)
        {
            if (paths is null) { results[root] = "missing"; continue; }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var path in paths)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/') + "\0"));
                hash.AppendData(digests[Path.GetFullPath(path)]);
            }
            results[root] = Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        return results;
    }

    /// <summary>Writes a manifest after a successful build, including every player file.</summary>
    public static void Seal(string playerDirectory, string inputKey)
    {
        File.WriteAllText(Path.Combine(playerDirectory, Manifest), JsonSerializer.Serialize(new Entry(inputKey, Files(playerDirectory))));
    }

    /// <summary>Checks input identity and the complete player output set before reuse.</summary>
    public static bool Valid(string playerDirectory, string inputKey)
    {
        try
        {
            var entry = JsonSerializer.Deserialize<Entry>(File.ReadAllText(Path.Combine(playerDirectory, Manifest)));
            if (entry?.Key != inputKey || entry.Files is null || entry.Files.Count == 0) return false;
            var actual = Files(playerDirectory);
            return actual.Count == entry.Files.Count && entry.Files.All(p => actual.GetValueOrDefault(p.Key) == p.Value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static Dictionary<string, string> Files(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(p => Path.GetRelativePath(root, p) != Manifest)
        .Order(StringComparer.Ordinal).ToDictionary(p => Path.GetRelativePath(root, p).Replace('\\', '/'), p =>
        {
            using var stream = File.OpenRead(p);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }, StringComparer.Ordinal);

    private sealed record Entry(string Key, Dictionary<string, string>? Files);
}
