using Ucl.Core.Model;

namespace Ucl.Core.Parsing;

/// <summary>The Player Settings fields that affect compilation, keyed by build target group name.</summary>
public sealed record ProjectSettingsData
{
    /// <summary>Settings of a project whose <c>ProjectSettings.asset</c> is missing or empty (Unity defaults).</summary>
    public static ProjectSettingsData Default { get; } = new();

    /// <summary><c>scriptingDefineSymbols</c> per group, already split.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ScriptingDefineSymbols { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary><c>scriptingBackend</c> per group.</summary>
    public IReadOnlyDictionary<string, ScriptingBackend> ScriptingBackend { get; init; } = new Dictionary<string, ScriptingBackend>();

    /// <summary><c>additionalCompilerArguments</c> per group.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> AdditionalCompilerArguments { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary><c>apiCompatibilityLevelPerPlatform</c> per group.</summary>
    public IReadOnlyDictionary<string, int> ApiCompatibilityPerGroup { get; init; } = new Dictionary<string, int>();

    /// <summary><c>apiCompatibilityLevel</c>: 6 is .NET Standard 2.1 (default), 3 is .NET Framework.</summary>
    public int ApiCompatibilityLevel { get; init; } = 6;

    /// <summary><c>activeInputHandler</c>: 0 legacy, 1 Input System, 2 both.</summary>
    public int ActiveInputHandler { get; init; }

    /// <summary><c>allowUnsafeCode</c> for predefined assemblies.</summary>
    public bool AllowUnsafeCode { get; init; }

    /// <summary><c>suppressCommonWarnings</c> (CS0169, CS0649); default on.</summary>
    public bool SuppressCommonWarnings { get; init; } = true;

    /// <summary>True when the API compatibility level for <paramref name="group"/> is .NET Framework.</summary>
    public bool IsNetFramework(string group) =>
        (ApiCompatibilityPerGroup.TryGetValue(group, out var level) ? level : ApiCompatibilityLevel) == 3;
}
