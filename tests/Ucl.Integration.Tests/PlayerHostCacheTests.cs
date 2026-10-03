using System.Security.Cryptography;
using System.Text;
using Ucl.Discovery;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>Player caches bind inputs and every output byte, independently of timestamps.</summary>
public sealed class PlayerHostCacheTests
{
    [Fact]
    public void Changed_or_extra_player_files_refuse_reuse()
    {
        using var temp = new TempDir();
        var player = Path.Combine(temp.Path, "player");
        Directory.CreateDirectory(player);
        File.WriteAllText(Path.Combine(player, "Host.exe"), "original");
        PlayerHostCache.Seal(player, "input-key");
        Assert.True(PlayerHostCache.Valid(player, "input-key"));
        Assert.False(PlayerHostCache.Valid(player, "other-key"));
        File.WriteAllText(Path.Combine(player, "Host.exe"), "tampered");
        Assert.False(PlayerHostCache.Valid(player, "input-key"));
        File.WriteAllText(Path.Combine(player, "Host.exe"), "original");
        File.WriteAllText(Path.Combine(player, "extra.dll"), "extra");
        Assert.False(PlayerHostCache.Valid(player, "input-key"));
    }

    [Fact]
    public void Tree_digest_includes_relative_names_and_content()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "a"), "original");
        var first = PlayerHostCache.TreeDigest(temp.Path);
        File.WriteAllText(Path.Combine(temp.Path, "a"), "changed");
        Assert.NotEqual(first, PlayerHostCache.TreeDigest(temp.Path));
        File.WriteAllText(Path.Combine(temp.Path, "a"), "original");
        File.Move(Path.Combine(temp.Path, "a"), Path.Combine(temp.Path, "b"));
        Assert.NotEqual(first, PlayerHostCache.TreeDigest(temp.Path));
    }

    [Fact]
    public void Incomplete_manifest_refuses_reuse()
    {
        using var temp = new TempDir();
        Assert.False(PlayerHostCache.Valid(temp.Path, "key"));
        File.WriteAllText(Path.Combine(temp.Path, "host-manifest.json"), "{}");
        Assert.False(PlayerHostCache.Valid(temp.Path, "key"));
    }
    [Fact]
    public void Overlapping_tree_batch_preserves_serial_fingerprints_and_detects_content_changes()
    {
        using var temp = new TempDir();
        var child = Path.Combine(temp.Path, "package");
        Directory.CreateDirectory(child);
        for (var i = 0; i < 32; i++) File.WriteAllText(Path.Combine(child, $"file-{i}.txt"), i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(temp.Path, "root.txt"), "root");
        var missing = Path.Combine(temp.Path, "missing");
        var batch = PlayerHostCache.TreeDigests([temp.Path, child, missing]);
        Assert.Equal(Serial(temp.Path), batch[temp.Path]);
        Assert.Equal(Serial(child), batch[child]);
        Assert.Equal("missing", batch[missing]);
        File.WriteAllText(Path.Combine(child, "file-1.txt"), "changed");
        var changed = PlayerHostCache.TreeDigests([temp.Path, child]);
        Assert.NotEqual(batch[temp.Path], changed[temp.Path]);
        Assert.NotEqual(batch[child], changed[child]);

        static string Serial(string root)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/') + "\0"));
                hash.AppendData(SHA256.HashData(File.ReadAllBytes(path)));
            }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
    }

}
