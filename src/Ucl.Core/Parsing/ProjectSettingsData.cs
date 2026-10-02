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

    /// <summary>
    /// <c>editorAssembliesCompatibilityLevel</c>: 1 Default (the same as 2), 2 .NET Framework (<c>NET_Unity_4_8</c>),
    /// 3 .NET Standard. Applies to Editor-only assemblies (docs/defines.md, "API compatibility level").
    /// </summary>
    public int EditorAssembliesCompatibilityLevel { get; init; } = 1;

    /// <summary><c>activeInputHandler</c>: 0 legacy, 1 Input System, 2 both.</summary>
    public int ActiveInputHandler { get; init; }

    /// <summary><c>allowUnsafeCode</c> for predefined assemblies.</summary>
    public bool AllowUnsafeCode { get; init; }

    /// <summary><c>suppressCommonWarnings</c> (CS0169, CS0649); default on.</summary>
    public bool SuppressCommonWarnings { get; init; } = true;

    /// <summary>
    /// <c>playModeTestRunnerEnabled</c> ("Enable playmode tests for all assemblies"): every assembly, not only Editor-only
    /// ones, gets the test runner assemblies and <c>nunit.framework.dll</c> (docs/architecture.md, "Test runner references").
    /// </summary>
    public bool PlayModeTestRunnerEnabled { get; init; }

    /// <summary>True when the API compatibility level for <paramref name="group"/> is .NET Framework.</summary>
    public bool IsNetFramework(string group) =>
        (ApiCompatibilityPerGroup.TryGetValue(group, out var level) ? level : ApiCompatibilityLevel) == 3;

    /// <summary>True when Editor-only assemblies compile against .NET Framework (every level except 3, .NET Standard).</summary>
    public bool IsEditorNetFramework => EditorAssembliesCompatibilityLevel != 3;

    /// <summary>
    /// The API compatibility of one assembly: Editor-only assemblies follow <see cref="EditorAssembliesCompatibilityLevel"/>,
    /// every other assembly the level of the cell's build target group.
    /// </summary>
    public bool IsNetFrameworkFor(string group, bool editorOnly) => editorOnly ? IsEditorNetFramework : IsNetFramework(group);
}
