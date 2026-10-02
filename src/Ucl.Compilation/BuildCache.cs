using System.Text;
using System.Text.Json;
using Ucl.Core.Model;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>
/// The incremental cache: one entry per inputs hash, holding the diagnostics and the metadata-only image
/// dependents compile against. Entries are immutable (the key covers every input), so a hit is always valid.
/// The diagnostics are a separate file, loaded apart from the header and image: dependents need only the image,
/// so reading a large diagnostics list is never on the dependency chain of a warm run.
/// </summary>
internal sealed class BuildCache
{
    private const int FormatVersion = 3;
    private readonly IFileSystem _fs;
    private readonly string _dir;

    public BuildCache(IFileSystem fs, string cacheDirectory)
    {
        _fs = fs;
        _dir = Path.Combine(cacheDirectory, "cache", $"v{FormatVersion}");
    }

    public Entry? TryLoad(string key)
    {
        var json = EntryPath(key, ".json");
        var diagnostics = EntryPath(key, ".diagnostics.json");
        if (!_fs.FileExists(json) || !_fs.FileExists(diagnostics))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(_fs.ReadAllBytes(json));
            if (stored is null || stored.ImageHash is null)
            {
                return null;
            }

            byte[]? image = null;
            if (!stored.Failed || stored.ImageHash.Length > 0)
            {
                var dll = EntryPath(key, ".dll");
                if (!_fs.FileExists(dll))
                {
                    return null;
                }

                image = _fs.ReadAllBytes(dll);
                if (ContentHasher.HashBytes(image) != stored.ImageHash)
                {
                    return null;
                }
            }

            return new Entry(stored.Failed, () => LoadDiagnostics(diagnostics), image, stored.ImageHash);
        }
        catch (JsonException)
        {
            // A torn or foreign file is a miss, never an error: the entry is simply rebuilt.
            return null;
        }
    }

    public void Store(string key, bool failed, IReadOnlyList<Diagnostic> diagnostics, byte[]? image, string imageHash)
    {
        if (image is not null)
        {
            _fs.WriteAllBytes(EntryPath(key, ".dll"), image);
        }

        // The header last: an entry is complete once its header exists.
        _fs.WriteAllBytes(EntryPath(key, ".diagnostics.json"), JsonSerializer.SerializeToUtf8Bytes(diagnostics.Select(StoredDiagnostic.From).ToList()));
        _fs.WriteAllBytes(EntryPath(key, ".json"), JsonSerializer.SerializeToUtf8Bytes(new Stored(failed, imageHash)));
    }

    // Written by Store before the header, never changed after: a hit's diagnostics are there. A torn file (a
    // concurrent writer, a full disk) is reported as an internal error rather than silently dropping diagnostics.
    private IReadOnlyList<Diagnostic> LoadDiagnostics(string path) =>
        (JsonSerializer.Deserialize<List<StoredDiagnostic>>(_fs.ReadAllBytes(path)) ?? []).Select(d => d.ToDiagnostic()).ToList();

    private string EntryPath(string key, string extension) => Path.Combine(_dir, key[..2], key + extension);

    internal sealed record Entry(bool Failed, Func<IReadOnlyList<Diagnostic>> LoadDiagnostics, byte[]? Image, string ImageHash);

    private sealed record Stored(bool Failed, string ImageHash);

    private sealed record StoredDiagnostic(string Id, Severity Severity, DiagnosticOrigin Origin, string? Assembly, string? File, int Line, int Column, string Message, bool WarningAsError)
    {
        public static StoredDiagnostic From(Diagnostic d) => new(d.Id, d.Severity, d.Origin, d.Assembly, d.File, d.Line, d.Column, d.Message, d.WarningAsError);

        public Diagnostic ToDiagnostic() => new(Id, Severity, Origin, Assembly, File, Line, Column, Message, WarningAsError);
    }
}
