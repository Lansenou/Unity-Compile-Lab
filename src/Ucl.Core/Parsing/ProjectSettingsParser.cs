using System.Globalization;
using Ucl.Core.Model;

namespace Ucl.Core.Parsing;

/// <summary>Parses <c>ProjectSettings/ProjectSettings.asset</c> and <c>ProjectVersion.txt</c>.</summary>
public static class ProjectSettingsParser
{
    // Legacy numeric BuildTargetGroup keys; Unity 6 writes names, older projects numbers.
    private static readonly Dictionary<string, string> GroupNumbers = new(StringComparer.Ordinal)
    {
        ["1"] = "Standalone",
        ["4"] = "iOS",
        ["7"] = "Android",
        ["13"] = "WebGL",
    };

    /// <summary>Parses the Player Settings asset.</summary>
    public static ProjectSettingsData Parse(string text)
    {
        var root = SimpleYaml.Parse(text);
        var player = root["PlayerSettings"] ?? root;
        return new ProjectSettingsData
        {
            ScriptingDefineSymbols = GroupMap(player["scriptingDefineSymbols"], n => (IReadOnlyList<string>)SplitDefines(n.Scalar ?? string.Empty)),
            ScriptingBackend = GroupMap(player["scriptingBackend"], n => n.Scalar == "1" ? Model.ScriptingBackend.IL2CPP : Model.ScriptingBackend.Mono),
            AdditionalCompilerArguments = GroupMap(player["additionalCompilerArguments"], n =>
                (IReadOnlyList<string>)(n.Items?.Select(i => i.Scalar ?? string.Empty).Where(s => s.Length > 0).ToList() ?? [])),
            ApiCompatibilityPerGroup = GroupMap(player["apiCompatibilityLevelPerPlatform"], n => Int(n.Scalar, 6)),
            ApiCompatibilityLevel = Int(player.Get("apiCompatibilityLevel"), 6),
            EditorAssembliesCompatibilityLevel = Int(player.Get("editorAssembliesCompatibilityLevel"), 1),
            ActiveInputHandler = Int(player.Get("activeInputHandler"), 0),
            AllowUnsafeCode = player.Get("allowUnsafeCode") == "1",
            SuppressCommonWarnings = player.Get("suppressCommonWarnings") != "0",
        };
    }

    /// <summary>Reads <c>m_EditorVersion</c> from <c>ProjectVersion.txt</c>.</summary>
    public static Result<UnityVersion> ParseProjectVersion(string text)
    {
        var version = SimpleYaml.Parse(text).Get("m_EditorVersion");
        return version is null
            ? Result<UnityVersion>.Failure("ProjectVersion.txt has no m_EditorVersion")
            : UnityVersion.Parse(version);
    }

    /// <summary>Splits a <c>;</c>-separated define list, dropping empties and duplicates, keeping order.</summary>
    public static List<string> SplitDefines(string text) =>
        text.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();

    private static Dictionary<string, T> GroupMap<T>(YamlNode? node, Func<YamlNode, T> convert)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var (key, value) in node?.Map ?? [])
        {
            result[GroupNumbers.GetValueOrDefault(key, key)] = convert(value);
        }

        return result;
    }

    private static int Int(string? s, int fallback) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
