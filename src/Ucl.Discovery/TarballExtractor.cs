using System.Formats.Tar;
using System.IO.Compression;
using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary>Reads an npm package tarball (<c>.tgz</c>) into memory, validating every entry path before anything is written.</summary>
internal static class TarballExtractor
{
    /// <summary>
    /// The regular files of the tarball with the leading folder stripped (npm tarballs wrap everything in
    /// <c>package/</c>; npm strips whatever the first folder is called, and so does this). Paths use <c>/</c>.
    /// </summary>
    public static Result<IReadOnlyList<(string Path, byte[] Bytes)>> Extract(byte[] tgz)
    {
        var files = new List<(string, byte[])>();
        try
        {
            using var gzip = new GZipStream(new MemoryStream(tgz, writable: false), CompressionMode.Decompress);
            using var tar = new TarReader(gzip);
            while (tar.GetNextEntry() is { } entry)
            {
                // Links, devices and pax headers are never written: links are the classic way out of the target folder.
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile))
                {
                    continue;
                }

                var relative = Strip(entry.Name);
                if (relative is null)
                {
                    return Result<IReadOnlyList<(string, byte[])>>.Failure($"tarball entry '{entry.Name}' escapes the package folder");
                }

                using var data = new MemoryStream();
                entry.DataStream?.CopyTo(data);
                files.Add((relative, data.ToArray()));
            }
        }
        catch (Exception e) when (e is InvalidDataException or FormatException or EndOfStreamException)
        {
            return Result<IReadOnlyList<(string, byte[])>>.Failure($"not a valid .tgz: {e.Message}");
        }

        return Result<IReadOnlyList<(string, byte[])>>.Success(files);
    }

    /// <summary>The entry path without its first folder, or null when it is absolute, has <c>..</c>, or is otherwise unsafe.</summary>
    public static string? Strip(string name)
    {
        if (name.StartsWith('/') || name.Contains('\\', StringComparison.Ordinal) || name.Contains(':', StringComparison.Ordinal) || name.Contains('\0', StringComparison.Ordinal))
        {
            return null;
        }

        var segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(s => s != ".").ToList();
        if (segments.Count < 2 || segments.Any(s => s == ".."))
        {
            // A file directly at the tarball root has no wrapping folder to strip, so it keeps its name.
            return segments.Count == 1 && segments[0] != ".." ? segments[0] : null;
        }

        return string.Join('/', segments.Skip(1));
    }
}
