using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ucl.Discovery;

/// <summary>Content fingerprints and integrity manifests for an immutable player cache entry.</summary>
public static class PlayerHostCache
{
    private const string Manifest = "host-manifest.json";

    /// <summary>Hashes relative names and bytes, in ordinal order; missing trees are distinguished.</summary>
    public static string TreeDigest(string root)
    {
        if (!Directory.Exists(root)) return "missing";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/') + "\0"));
            using var stream = File.OpenRead(path);
            hash.AppendData(SHA256.HashData(stream));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
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
