using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Ucl.Discovery.Tests;

/// <summary>Builds npm-style package tarballs and registry documents in memory.</summary>
public static class Tgz
{
    public static byte[] Build(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax))
        {
            foreach (var (name, content) in entries)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)) };
                tar.WriteEntry(entry);
            }
        }

        return stream.ToArray();
    }

    public static string PackageJson(string name, string version, string deps = "") =>
        $"{{ \"name\": \"{name}\", \"version\": \"{version}\", \"dependencies\": {{ {deps} }} }}";

    public static string Sha1(byte[] bytes) => Convert.ToHexStringLower(SHA1.HashData(bytes));

    /// <summary>An npm registry document with one version.</summary>
    public static byte[] Document(string name, string version, string tarballUrl, string? shasum) =>
        Encoding.UTF8.GetBytes(
            $"{{ \"name\": \"{name}\", \"versions\": {{ \"{version}\": {{ \"name\": \"{name}\", \"version\": \"{version}\", "
            + $"\"dist\": {{ \"tarball\": \"{tarballUrl}\"{(shasum is null ? string.Empty : $", \"shasum\": \"{shasum}\"")} }} }} }} }}");
}
