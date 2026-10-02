using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

/// <summary>Builds <see cref="ProjectInventory"/> values in memory, fluently. No disk access.</summary>
internal sealed class InventoryBuilder
{
    private readonly List<string> _scripts = [];
    private readonly List<TextFile> _asmdefs = [];
    private readonly List<TextFile> _asmrefs = [];
    private readonly List<TextFile> _plugins = [];
    private readonly List<TextFile> _rsps = [];
    private readonly List<string> _ruleSets = [];
    private readonly List<string> _configs = [];
    private readonly List<ResolvedPackage> _packages = [];
    private ProjectSettingsData _settings = ProjectSettingsData.Default;
    private readonly List<string> _testables = [];
    private readonly Dictionary<string, string> _pluginVersions = new(StringComparer.Ordinal);
    private UnityVersion _version = Cells.Version;

    public InventoryBuilder Scripts(params string[] paths)
    {
        _scripts.AddRange(paths);
        return this;
    }

    /// <summary>Adds an asmdef <c>{ "name": name, extra }</c> with a meta holding <paramref name="guid"/> when given.</summary>
    public InventoryBuilder Asmdef(string path, string name, string extraJson = "", string? guid = null)
    {
        var json = extraJson.Length == 0
            ? $"{{\n  \"name\": \"{name}\"\n}}"
            : $"{{\n  \"name\": \"{name}\",\n  {extraJson}\n}}";
        return AsmdefRaw(path, json, guid is null ? null : Metas.Asmdef(guid));
    }

    public InventoryBuilder AsmdefRaw(string path, string json, string? metaText = null)
    {
        _asmdefs.Add(new TextFile(path, json, metaText));
        return this;
    }

    public InventoryBuilder Asmref(string path, string reference) =>
        AsmrefRaw(path, $"{{\n  \"reference\": \"{reference}\"\n}}");

    public InventoryBuilder AsmrefRaw(string path, string json)
    {
        _asmrefs.Add(new TextFile(path, json, Metas.Asmdef("ffffffffffffffffffffffffffffffff")));
        return this;
    }

    /// <summary>Adds a DLL. <paramref name="metaText"/> null means the DLL has no <c>.meta</c>.</summary>
    public InventoryBuilder Plugin(string path, string? metaText = null)
    {
        _plugins.Add(new TextFile(path, string.Empty, metaText));
        return this;
    }

    public InventoryBuilder Rsp(string path, string text)
    {
        _rsps.Add(new TextFile(path, text));
        return this;
    }

    public InventoryBuilder RuleSet(string path)
    {
        _ruleSets.Add(path);
        return this;
    }

    public InventoryBuilder AnalyzerConfig(string path)
    {
        _configs.Add(path);
        return this;
    }

    public InventoryBuilder Package(string name, string version, string source = "cache")
    {
        var builtin = name.StartsWith("com.unity.modules.", StringComparison.Ordinal);
        _packages.Add(new ResolvedPackage(name, version, builtin ? null : $"Packages/{name}", builtin ? "builtin" : source));
        return this;
    }

    public InventoryBuilder Testables(params string[] names)
    {
        _testables.AddRange(names);
        return this;
    }

    public InventoryBuilder PluginVersion(string path, string version)
    {
        _pluginVersions[path] = version;
        return this;
    }

    public InventoryBuilder Settings(ProjectSettingsData settings)
    {
        _settings = settings;
        return this;
    }

    public InventoryBuilder Settings(string projectSettingsYaml) => Settings(ProjectSettingsParser.Parse(projectSettingsYaml));

    public InventoryBuilder Version(UnityVersion version)
    {
        _version = version;
        return this;
    }

    public ProjectInventory Build() => new()
    {
        ProjectVersion = _version,
        Scripts = [.. _scripts],
        Asmdefs = [.. _asmdefs],
        Asmrefs = [.. _asmrefs],
        Plugins = [.. _plugins],
        ResponseFiles = [.. _rsps],
        RuleSets = [.. _ruleSets],
        AnalyzerConfigs = [.. _configs],
        Settings = _settings,
        Packages = [.. _packages],
        Testables = [.. _testables],
        PluginVersions = _pluginVersions,
    };

    /// <summary>Same inventory with every list in reverse order (determinism checks).</summary>
    public ProjectInventory BuildReversed() => new()
    {
        ProjectVersion = _version,
        Scripts = Rev(_scripts),
        Asmdefs = Rev(_asmdefs),
        Asmrefs = Rev(_asmrefs),
        Plugins = Rev(_plugins),
        ResponseFiles = Rev(_rsps),
        RuleSets = Rev(_ruleSets),
        AnalyzerConfigs = Rev(_configs),
        Settings = _settings,
        Packages = Rev(_packages),
        Testables = Rev(_testables),
        PluginVersions = _pluginVersions,
    };

    public AssemblyGraph Graph(CompileCell cell) => AssemblyGraphBuilder.Build(Build(), cell);

    public AssemblyGraph Editor(BuildPlatform platform = BuildPlatform.StandaloneWindows64) => Graph(Cells.Editor(platform));

    public AssemblyGraph Player(BuildPlatform platform = BuildPlatform.StandaloneWindows64, bool development = false) =>
        Graph(Cells.Player(platform, development));

    private static List<T> Rev<T>(List<T> list)
    {
        var copy = new List<T>(list);
        copy.Reverse();
        return copy;
    }
}

/// <summary>Compile cells used by the tests.</summary>
internal static class Cells
{
    public static readonly UnityVersion Version = new(6000, 0, 30, "f1");

    public static CompileCell Editor(BuildPlatform platform = BuildPlatform.StandaloneWindows64, HostOs os = HostOs.Windows, ScriptingBackend? backend = null) =>
        new(Version, TargetKind.Editor, platform, backend, false, os);

    public static CompileCell Player(BuildPlatform platform = BuildPlatform.StandaloneWindows64, bool development = false, ScriptingBackend? backend = null, bool includeTests = false) =>
        new(Version, TargetKind.Player, platform, backend, development, HostOs.Windows, includeTests);
}

/// <summary>Realistic Unity 6 <c>.meta</c> texts.</summary>
internal static class Metas
{
    public static string Asmdef(string guid) =>
        $"fileFormatVersion: 2\nguid: {guid}\nAssemblyDefinitionImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

    /// <summary>A PluginImporter meta. <paramref name="platformData"/> is the text of the <c>platformData:</c> list (entries at two spaces).</summary>
    public static string Plugin(
        string platformData,
        bool explicitlyReferenced = false,
        string[]? defineConstraints = null,
        string[]? labels = null,
        string guid = "0123456789abcdef0123456789abcdef")
    {
        var labelText = labels is null ? string.Empty : "labels:\n" + string.Concat(labels.Select(l => $"- {l}\n"));
        var constraints = defineConstraints is null || defineConstraints.Length == 0
            ? "  defineConstraints: []\n"
            : "  defineConstraints:\n" + string.Concat(defineConstraints.Select(c => $"  - {c}\n"));
        var data = platformData.Length == 0 ? "  platformData: []\n" : "  platformData:\n" + platformData;
        return $"fileFormatVersion: 2\nguid: {guid}\n{labelText}PluginImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  iconMap: {{}}\n  executionOrder: {{}}\n{constraints}  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: {(explicitlyReferenced ? 1 : 0)}\n  validateReferences: 1\n{data}  userData: \n  assetBundleName: \n  assetBundleVariant: \n";
    }

    /// <summary>"Any Platform" ticked, with <c>Exclude &lt;key&gt;: 1</c> for each excluded key.</summary>
    public static string AnyPlatformData(params string[] excludedKeys)
    {
        var all = new[] { "Android", "Editor", "Linux64", "OSXUniversal", "WebGL", "Win64", "iOS" };
        var settings = string.Concat(all.Select(k => $"        Exclude {k}: {(excludedKeys.Contains(k) ? 1 : 0)}\n"));
        return "  - first:\n      : Any\n    second:\n      enabled: 0\n      settings:\n" + settings
             + "  - first:\n      Any: \n    second:\n      enabled: 1\n      settings: {}\n";
    }

    /// <summary>"Any Platform" off; each (category, key) entry is enabled.</summary>
    public static string ExplicitPlatformData(params (string Category, string Key)[] enabled)
    {
        var text = "  - first:\n      : Any\n    second:\n      enabled: 0\n      settings:\n        Exclude Win64: 0\n"
                 + "  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n"
                 + "  - first:\n      Standalone: Linux64\n    second:\n      enabled: 0\n      settings:\n        CPU: None\n";
        foreach (var (category, key) in enabled)
        {
            text += $"  - first:\n      {category}: {key}\n    second:\n      enabled: 1\n      settings:\n        CPU: AnyCPU\n";
        }

        return text;
    }

    public static string Analyzer(string guid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") =>
        Plugin(ExplicitPlatformData(), labels: ["RoslynAnalyzer"], guid: guid);
}
