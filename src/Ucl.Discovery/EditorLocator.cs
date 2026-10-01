using System.Text.Json;
using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary>
/// Finds installed Unity Editors: an explicit path, <c>UNITY_EDITOR_PATH</c>, the roots in <c>UCL_EDITOR_ROOTS</c>,
/// and Unity Hub's default and secondary install folders. Lists each install's managed reference DLLs.
/// </summary>
/// <param name="fs">Disk access.</param>
/// <param name="env">Environment variables, host OS and home folder.</param>
public sealed class EditorLocator(IFileSystem fs, IEnvironment env)
{
    /// <summary>Environment variable naming one editor install (same forms as <c>--editor</c>).</summary>
    public const string EditorPathVariable = "UNITY_EDITOR_PATH";

    /// <summary>Environment variable holding folders of <c>&lt;version&gt;/</c> installs, separated by <see cref="Path.PathSeparator"/>.</summary>
    public const string EditorRootsVariable = "UCL_EDITOR_ROOTS";

    /// <summary>Every valid install found outside an explicit path, sorted by version then root, without duplicates.</summary>
    public IReadOnlyList<EditorInstall> FindAll()
    {
        var installs = new List<EditorInstall>();
        if (env.GetVariable(EditorPathVariable) is { Length: > 0 } single && TryInstall(single, null) is { } fromVariable)
        {
            installs.Add(fromVariable);
        }

        foreach (var root in SearchRoots())
        {
            foreach (var dir in fs.ListDirectories(root))
            {
                if (UnityVersion.Parse(dir) is { Ok: true } v && TryInstall(Path.Combine(root, dir), v.Value) is { } install)
                {
                    installs.Add(install);
                }
            }
        }

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return installs
            .DistinctBy(i => i.Root, comparer)
            .OrderBy(i => i.Version)
            .ThenBy(i => i.Root, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Picks the install for <paramref name="version"/>. An explicit path is used as given (its version comes from a
    /// version-named folder in the path, else is assumed to be <paramref name="version"/>), so the caller can compare
    /// <see cref="EditorInstall.Version"/> to decide. Otherwise the installs from <see cref="FindAll"/> must match exactly.
    /// </summary>
    /// <param name="version">Wanted editor version. A version without a suffix (<c>6000.0.30</c>) matches any suffix.</param>
    /// <param name="explicitEditorPath">The <c>--editor</c> value: install root, <c>Editor</c> folder, executable, or <c>Unity.app</c>.</param>
    /// <returns>The install, or a failure that lists installed versions and how to point <c>ucl</c> at an editor.</returns>
    public Result<EditorInstall> Locate(UnityVersion version, string? explicitEditorPath)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (explicitEditorPath is not null)
        {
            return TryInstall(explicitEditorPath, version) is { } install
                ? Result<EditorInstall>.Success(install)
                : Result<EditorInstall>.Failure(
                    $"'{explicitEditorPath}' is not a Unity editor install: expected Editor/Data/Managed/UnityEngine (Windows, Linux) "
                    + "or Unity.app/Contents/Managed/UnityEngine (macOS) under it.");
        }

        var all = FindAll();
        var match = all.FirstOrDefault(i => Matches(i.Version, version));
        if (match is not null)
        {
            return Result<EditorInstall>.Success(match);
        }

        var installed = all.Count == 0
            ? "none found"
            : string.Join(", ", all.Select(i => $"{i.Version} ({i.Root})"));
        var searched = string.Join(", ", SearchRoots());
        return Result<EditorInstall>.Failure(
            $"Unity {version} is not installed. Installed editors: {installed}. Searched: {EditorPathVariable}, {EditorRootsVariable}"
            + (searched.Length > 0 ? $", {searched}" : string.Empty)
            + $". Install {version} with Unity Hub, or pass --editor <install>, or set {EditorPathVariable} to the install folder, "
            + $"or set {EditorRootsVariable} to a folder holding <version>/ installs (separate several with '{Path.PathSeparator}').");
    }

    /// <summary>Folders that hold <c>&lt;version&gt;/</c> installs: <c>UCL_EDITOR_ROOTS</c>, then Unity Hub's default and secondary folders.</summary>
    public IReadOnlyList<string> SearchRoots()
    {
        var roots = new List<string>();
        if (env.GetVariable(EditorRootsVariable) is { Length: > 0 } list)
        {
            roots.AddRange(list.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        roots.Add(env.Os switch
        {
            HostOs.Windows => Path.Combine(env.GetVariable("ProgramFiles") ?? @"C:\Program Files", "Unity", "Hub", "Editor"),
            HostOs.MacOS => "/Applications/Unity/Hub/Editor",
            _ => Path.Combine(env.HomeDirectory, "Unity", "Hub", "Editor"),
        });
        if (ReadSecondaryInstallPath() is { } secondary)
        {
            roots.Add(secondary);
        }

        return roots.Distinct(StringComparer.Ordinal).ToList();
    }

    private string? ReadSecondaryInstallPath()
    {
        var hubConfig = env.Os switch
        {
            HostOs.Windows => Path.Combine(env.GetVariable("APPDATA") ?? Path.Combine(env.HomeDirectory, "AppData", "Roaming"), "UnityHub"),
            HostOs.MacOS => Path.Combine(env.HomeDirectory, "Library", "Application Support", "UnityHub"),
            _ => Path.Combine(env.GetVariable("XDG_CONFIG_HOME") ?? Path.Combine(env.HomeDirectory, ".config"), "UnityHub"),
        };
        var file = Path.Combine(hubConfig, "secondaryInstallPath.json");
        if (!fs.FileExists(file))
        {
            return null;
        }

        // Hub writes a bare JSON string; an empty string means "not set". A corrupt file is Hub's problem, not a run failure.
        try
        {
            using var doc = JsonDocument.Parse(fs.ReadAllText(file));
            return doc.RootElement.ValueKind == JsonValueKind.String && doc.RootElement.GetString() is { Length: > 0 } path ? path : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Matches(UnityVersion installed, UnityVersion wanted) =>
        wanted.Suffix.Length == 0
            ? installed.Major == wanted.Major && installed.Minor == wanted.Minor && installed.Patch == wanted.Patch
            : installed == wanted;

    private EditorInstall? TryInstall(string path, UnityVersion? assumed)
    {
        var layout = FindLayout(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
        if (layout is not var (root, data))
        {
            return null;
        }

        var version = VersionFromPath(root) ?? assumed;
        if (version is null)
        {
            return null;
        }

        var managed = Path.Combine(data, "Managed", "UnityEngine");
        var engineFiles = Dlls(managed);
        return new EditorInstall
        {
            Version = version,
            Root = root,
            DataPath = data,
            EngineModules = engineFiles
                .Where(f => Path.GetFileName(f).StartsWith("UnityEngine.", StringComparison.Ordinal)
                    && !Path.GetFileName(f).Equals("UnityEngine.dll", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            EditorAssemblies = engineFiles.Where(f => Path.GetFileName(f).StartsWith("UnityEditor", StringComparison.Ordinal)).ToList(),
            NetStandardReferences = Sorted(
                Dlls(Path.Combine(data, "NetStandard", "ref", "2.1.0")),
                Dlls(Path.Combine(data, "NetStandard", "compat", "2.1.0", "shims", "netfx"))),
            NetFrameworkReferences = Sorted(
                Dlls(Path.Combine(data, "UnityReferenceAssemblies", "unity-4.8-api")),
                Dlls(Path.Combine(data, "UnityReferenceAssemblies", "unity-4.8-api", "Facades"))),
        };
    }

    private (string Root, string Data)? FindLayout(string path)
    {
        // Normalise the executable forms (Editor/Unity.exe, Editor/Unity, Unity.app/Contents/MacOS/Unity) to a folder.
        var p = fs.FileExists(path) ? Path.GetDirectoryName(path) ?? path : path;
        if (NameIs(p, "MacOS"))
        {
            p = Path.GetDirectoryName(p) ?? p;
        }

        if (NameIs(p, "Contents"))
        {
            p = Path.GetDirectoryName(p) ?? p;
        }

        if (NameIs(p, "Editor") && HasManaged(Path.Combine(p, "Data")))
        {
            return (Path.GetDirectoryName(p) ?? p, Path.Combine(p, "Data"));
        }

        if (p.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && HasManaged(Path.Combine(p, "Contents")))
        {
            return (p, Path.Combine(p, "Contents"));
        }

        if (HasManaged(Path.Combine(p, "Editor", "Data")))
        {
            return (p, Path.Combine(p, "Editor", "Data"));
        }

        var app = Path.Combine(p, "Unity.app");
        return HasManaged(Path.Combine(app, "Contents")) ? (app, Path.Combine(app, "Contents")) : null;
    }

    private bool HasManaged(string data) => fs.DirectoryExists(Path.Combine(data, "Managed", "UnityEngine"));

    private static bool NameIs(string path, string name) => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase);

    // Hub installs live in a folder named after the version (<v>/Editor, <v>/Unity.app). Only the root and its parent
    // are checked, so an unrelated ancestor such as "/opt/tools/2.1" is never mistaken for the editor version.
    private static UnityVersion? VersionFromPath(string root)
    {
        foreach (var p in new[] { root, Path.GetDirectoryName(root) })
        {
            if (!string.IsNullOrEmpty(p) && UnityVersion.Parse(Path.GetFileName(p)) is { Ok: true } v)
            {
                return v.Value;
            }
        }

        return null;
    }

    private List<string> Dlls(string folder) =>
        fs.ListFiles(folder)
            .Where(n => n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(n => Path.Combine(folder, n))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static List<string> Sorted(List<string> a, List<string> b) => a.Concat(b).Order(StringComparer.Ordinal).ToList();
}
