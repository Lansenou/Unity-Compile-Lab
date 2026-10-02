using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Core.Graph;

/// <summary>Everything the graph builder needs from a project, already read from disk. Paths are logical and sorted.</summary>
public sealed record ProjectInventory
{
    /// <summary>Editor version from <c>ProjectVersion.txt</c>.</summary>
    public required UnityVersion ProjectVersion { get; init; }

    /// <summary>C# scripts.</summary>
    public IReadOnlyList<string> Scripts { get; init; } = [];

    /// <summary>asmdef files with their meta.</summary>
    public IReadOnlyList<TextFile> Asmdefs { get; init; } = [];

    /// <summary>asmref files with their meta.</summary>
    public IReadOnlyList<TextFile> Asmrefs { get; init; } = [];

    /// <summary>Managed DLLs (text empty) with their meta.</summary>
    public IReadOnlyList<TextFile> Plugins { get; init; } = [];

    /// <summary>Unmanaged (native) DLLs: plugins Unity loads at run time and never passes to the compiler.</summary>
    public IReadOnlyList<string> NativePlugins { get; init; } = [];

    /// <summary><c>csc.rsp</c> files.</summary>
    public IReadOnlyList<TextFile> ResponseFiles { get; init; } = [];

    /// <summary><c>.ruleset</c> files (paths only).</summary>
    public IReadOnlyList<string> RuleSets { get; init; } = [];

    /// <summary><c>.editorconfig</c> and <c>.globalconfig</c> files (paths only).</summary>
    public IReadOnlyList<string> AnalyzerConfigs { get; init; } = [];

    /// <summary>Player settings.</summary>
    public ProjectSettingsData Settings { get; init; } = ProjectSettingsData.Default;

    /// <summary>Resolved packages, sorted by name.</summary>
    public IReadOnlyList<ResolvedPackage> Packages { get; init; } = [];

    /// <summary>Packages listed in <c>Packages/manifest.json</c> <c>testables</c>.</summary>
    public IReadOnlyList<string> Testables { get; init; } = [];

    /// <summary>
    /// Assembly versions (<c>6.0.0.0</c>, empty when unreadable) of the managed DLLs that share their file name with another
    /// one, keyed by path; Unity keeps the highest (docs/architecture.md, "Precompiled DLLs").
    /// </summary>
    public IReadOnlyDictionary<string, string> PluginVersions { get; init; } = new Dictionary<string, string>();
}
