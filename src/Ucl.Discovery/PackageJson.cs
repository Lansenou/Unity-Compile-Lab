using System.Text.Json;
using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary>The fields of a package's <c>package.json</c> that resolution needs.</summary>
/// <param name="Name">Package name, or empty when the file has none.</param>
/// <param name="Version">Package version, or empty.</param>
/// <param name="Dependencies">Dependency name to requested version, ordinal order.</param>
internal sealed record PackageJson(string Name, string Version, IReadOnlyList<KeyValuePair<string, string>> Dependencies)
{
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>Reads <c>&lt;folder&gt;/package.json</c>; null when there is none, a failure when it is not valid JSON.</summary>
    public static Result<PackageJson>? Read(IFileSystem fs, string folder)
    {
        var path = Path.Combine(folder, "package.json");
        if (!fs.FileExists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(fs.ReadAllText(path), Options);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<PackageJson>.Failure("package.json is not a JSON object");
            }

            var deps = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("dependencies", out var d) && d.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in d.EnumerateObject())
                {
                    deps[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : string.Empty;
                }
            }

            return Result<PackageJson>.Success(new PackageJson(Str(root, "name"), Str(root, "version"), deps.ToList()));
        }
        catch (JsonException e)
        {
            return Result<PackageJson>.Failure($"invalid JSON: {e.Message}");
        }
    }

    /// <summary>Reads a package.json and returns it only when it is valid; used where a bad file just means "not this folder".</summary>
    public static PackageJson? ReadValid(IFileSystem fs, string folder) => Read(fs, folder) is { Ok: true } r ? r.Value : null;

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : string.Empty;
}
