using System.Text.Json;
using Ucl.Core.Model;

namespace Ucl.Core.Parsing;

/// <summary>Parses <c>Packages/manifest.json</c> and <c>Packages/packages-lock.json</c>.</summary>
public static class PackageManifestParser
{
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>Parses the manifest and the optional lock file.</summary>
    public static Result<PackageManifest> Parse(string manifestJson, string? lockJson)
    {
        try
        {
            using var manifest = JsonDocument.Parse(manifestJson, Options);
            var root = manifest.RootElement;
            var direct = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in deps.EnumerateObject())
                {
                    direct[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : string.Empty;
                }
            }

            var registries = new List<ScopedRegistry>();
            if (root.TryGetProperty("scopedRegistries", out var regs) && regs.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in regs.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.Object))
                {
                    registries.Add(new ScopedRegistry(Str(r, "name"), Str(r, "url"), StrList(r, "scopes")));
                }
            }

            var testables = root.TryGetProperty("testables", out _) ? StrList(root, "testables") : [];
            var locked = new SortedDictionary<string, PackageDeclaration>(StringComparer.Ordinal);
            if (lockJson is not null)
            {
                using var lockDoc = JsonDocument.Parse(lockJson, Options);
                if (lockDoc.RootElement.TryGetProperty("dependencies", out var lockDeps) && lockDeps.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in lockDeps.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
                    {
                        var v = p.Value;
                        var dependencies = v.TryGetProperty("dependencies", out var d) && d.ValueKind == JsonValueKind.Object
                            ? d.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal).ToList()
                            : [];
                        locked[p.Name] = new PackageDeclaration(
                            p.Name, direct.GetValueOrDefault(p.Name, string.Empty), Str(v, "version"), Str(v, "source"), Str(v, "url"), dependencies);
                    }
                }
            }

            foreach (var (name, requested) in direct)
            {
                if (!locked.ContainsKey(name))
                {
                    locked[name] = new PackageDeclaration(name, requested, requested, string.Empty, string.Empty, []);
                }
            }

            return Result<PackageManifest>.Success(new PackageManifest(locked.Values.ToList(), registries, testables));
        }
        catch (JsonException e)
        {
            return Result<PackageManifest>.Failure($"invalid JSON: {e.Message}");
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : string.Empty;

    private static List<string> StrList(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];
}
