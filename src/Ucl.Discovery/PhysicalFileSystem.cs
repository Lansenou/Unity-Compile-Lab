namespace Ucl.Discovery;

/// <summary>
/// <see cref="IFileSystem"/> over the real disk. Reads are unrestricted; writes and deletes are allowed only under
/// the writable roots given at construction, which is how <c>ucl</c> keeps its read-only promise for project files.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly string[] writableRoots;

    /// <summary>Creates a file system that may write only inside <paramref name="writableRoots"/>.</summary>
    /// <param name="writableRoots">Folders (absolute, or relative to the current directory) that writes may target.</param>
    public PhysicalFileSystem(params IEnumerable<string> writableRoots)
    {
        ArgumentNullException.ThrowIfNull(writableRoots);
        this.writableRoots = writableRoots.Select(r => Path.TrimEndingDirectorySeparator(Path.GetFullPath(r))).ToArray();
    }

    /// <summary>The normalised writable roots.</summary>
    public IReadOnlyList<string> WritableRoots => writableRoots;

    // Windows file names are case-insensitive, so "c:\x" and "C:\X" must be the same root there and only there.
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public string ReadAllText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var start = bytes.AsSpan().StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;
        return System.Text.Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
    }

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    /// <inheritdoc/>
    public byte[] ReadPrefix(string path, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[(int)Math.Min(maxBytes, stream.Length)];
        stream.ReadExactly(buffer);
        return buffer;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListFiles(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory).Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal).ToList()
            : [];

    /// <inheritdoc/>
    public IReadOnlyList<string> ListDirectories(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateDirectories(directory).Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal).ToList()
            : [];

    /// <inheritdoc/>
    public (long Length, long LastWriteUtcTicks) GetStamp(string path)
    {
        var info = new FileInfo(path);
        return (info.Length, info.LastWriteTimeUtc.Ticks);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The path is outside every writable root.</exception>
    public void WriteAllBytes(string path, byte[] bytes)
    {
        var full = EnsureWritable(path);
        var folder = Path.GetDirectoryName(full);
        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(full, bytes);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The path is outside every writable root.</exception>
    public void DeleteFile(string path)
    {
        var full = EnsureWritable(path);
        if (File.Exists(full))
        {
            File.Delete(full);
        }
    }

    /// <summary>True when <paramref name="path"/> is inside one of the writable roots.</summary>
    public bool IsWritable(string path)
    {
        // GetFullPath collapses "..", so "root/../Assets/x" cannot slip past the prefix check.
        var full = Path.GetFullPath(path);
        foreach (var root in writableRoots)
        {
            if (full.Length > root.Length
                && full.StartsWith(root, PathComparison)
                && (full[root.Length] == Path.DirectorySeparatorChar || full[root.Length] == Path.AltDirectorySeparatorChar
                    || Path.EndsInDirectorySeparator(root)))
            {
                return true;
            }
        }

        return false;
    }

    private string EnsureWritable(string path)
    {
        if (!IsWritable(path))
        {
            throw new InvalidOperationException(
                $"Refusing to write '{path}': ucl writes only under {string.Join(", ", writableRoots.Select(r => $"'{r}'"))}.");
        }

        return Path.GetFullPath(path);
    }
}
