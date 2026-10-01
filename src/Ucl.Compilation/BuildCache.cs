using System.Text;
using System.Text.Json;
using Ucl.Core.Model;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>
/// The incremental cache: one entry per inputs hash, holding the diagnostics and the metadata-only image
/// dependents compile against. Entries are immutable (the key covers every input), so a hit is always valid.
/// </summary>
internal sealed class BuildCache
{
    private const int FormatVersion = 1;
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
        if (!_fs.FileExists(json))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(_fs.ReadAllText(json));
            if (stored is null)
            {
                return null;
            }

            byte[]? image = null;
            if (!stored.Failed)
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

            return new Entry(stored.Failed, stored.Diagnostics.Select(d => d.ToDiagnostic()).ToList(), image, stored.ImageHash);
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

        var stored = new Stored(failed, imageHash, diagnostics.Select(StoredDiagnostic.From).ToList());
        _fs.WriteAllBytes(EntryPath(key, ".json"), Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stored)));
    }

    private string EntryPath(string key, string extension) => Path.Combine(_dir, key[..2], key + extension);

    internal sealed record Entry(bool Failed, IReadOnlyList<Diagnostic> Diagnostics, byte[]? Image, string ImageHash);

    private sealed record Stored(bool Failed, string ImageHash, List<StoredDiagnostic> Diagnostics);

    private sealed record StoredDiagnostic(string Id, Severity Severity, DiagnosticOrigin Origin, string? Assembly, string? File, int Line, int Column, string Message, bool WarningAsError)
    {
        public static StoredDiagnostic From(Diagnostic d) => new(d.Id, d.Severity, d.Origin, d.Assembly, d.File, d.Line, d.Column, d.Message, d.WarningAsError);

        public Diagnostic ToDiagnostic() => new(Id, Severity, Origin, Assembly, File, Line, Column, Message, WarningAsError);
    }
}
