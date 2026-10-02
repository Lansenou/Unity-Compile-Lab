namespace Ucl.Core.Testing;

/// <summary>
/// Classifies test cases by what actually happened (docs/test.md, "Classification"): the NUnit result state, and for a
/// failure the exception types in NUnit's failure message (<c>Type : message</c>, inner exceptions after <c>----&gt;</c>).
/// </summary>
public static class TestClassifier
{
    /// <summary>The attributes (full type names) that make a case unity-only, with the reason reported.</summary>
    public static IReadOnlyDictionary<string, string> UnityOnlyAttributes { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["UnityEngine.TestTools.UnityTestAttribute"] = "[UnityTest] runs in the Editor's player loop",
        ["UnityEngine.TestTools.UnityPlatformAttribute"] = "[UnityPlatform] depends on the Unity platform",
        ["UnityEngine.TestTools.RequiresPlayModeAttribute"] = "[RequiresPlayMode] runs in Play Mode",
    };

    /// <summary>The type whose use makes a case unity-only: log expectations need the Editor's log.</summary>
    public const string LogAssertType = "UnityEngine.TestTools.LogAssert";

    // Exception types whose presence in a failure means an engine call: CoreCLR refuses an InternalCall in a
    // non-system module (SecurityException "ECall methods must be packaged into a system module"); Mono reports a
    // missing internal call as MissingMethodException; native entry points fail to load; the engine's own error.
    private static readonly string[] EngineExceptions =
    [
        "System.Security.SecurityException",
        "System.MissingMethodException",
        "System.EntryPointNotFoundException",
        "System.DllNotFoundException",
        "UnityEngine.UnityException",
    ];

    /// <summary>
    /// The unity-only reason for a case, or null when it can run under .NET: a Play Mode assembly (not Editor-only),
    /// one of <see cref="UnityOnlyAttributes"/> on the method, its class or its assembly, or a call to <see cref="LogAssertType"/>.
    /// </summary>
    public static string? UnityOnlyReason(bool playModeAssembly, IEnumerable<string> attributeTypes, bool usesLogAssert)
    {
        ArgumentNullException.ThrowIfNull(attributeTypes);
        if (playModeAssembly)
        {
            return "Play Mode assembly (not Editor-only): the Unity Test Framework runs it in Play Mode";
        }

        foreach (var type in attributeTypes)
        {
            if (UnityOnlyAttributes.TryGetValue(type, out var reason))
            {
                return reason;
            }
        }

        return usesLogAssert ? "LogAssert needs the Editor's log" : null;
    }

    /// <summary>The category of a case NUnit ran, from its result state (status, label) and failure message.</summary>
    /// <param name="status">NUnit <c>TestStatus</c>: Passed, Failed, Skipped, Inconclusive, Warning.</param>
    /// <param name="label">NUnit result label (Ignored, Explicit, Error, Invalid, ...), or empty.</param>
    /// <param name="message">NUnit's message, or empty.</param>
    public static TestCategory FromResult(string status, string? label, string? message) => status switch
    {
        "Passed" or "Warning" => TestCategory.Passed,
        "Skipped" when label == "Ignored" => TestCategory.Ignored,
        "Skipped" or "Inconclusive" => TestCategory.Skipped,
        _ => IsEngineFailure(message ?? string.Empty) ? TestCategory.NeedsUnity : TestCategory.Failed,
    };

    /// <summary>
    /// True when a failure message names an engine-call exception (as <c>Type : message</c>), directly, as an inner
    /// exception (<c>----&gt; Type : ...</c>) or after a setup prefix (<c>OneTimeSetUp: Type : ...</c>).
    /// </summary>
    public static bool IsEngineFailure(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        foreach (var line in message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (EngineExceptions.Any(e => Named(line, e))
                || (Named(line, "System.TypeLoadException") && (line.Contains("UnityEngine", StringComparison.Ordinal) || line.Contains("UnityEditor", StringComparison.Ordinal))))
            {
                return true;
            }
        }

        return false;
    }

    // "Type : message" where Type starts the line or follows a separator (space, '>' of "---->", ':').
    private static bool Named(string line, string type)
    {
        var at = line.IndexOf(type + " : ", StringComparison.Ordinal);
        return at >= 0 && (at == 0 || line[at - 1] is ' ' or '>' or ':' or '\t');
    }

    /// <summary>The first line of a message, trimmed: the reason shown in reports.</summary>
    public static string FirstLine(string? message)
    {
        var text = (message ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        return newline < 0 ? text : text[..newline].TrimEnd();
    }
}
