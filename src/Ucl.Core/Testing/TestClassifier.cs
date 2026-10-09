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

    /// <summary>
    /// True when <c>[UnityPlatform]</c> is among <paramref name="attributeTypes"/> (method, class and assembly): the case
    /// names the Editor's platform, independent of which unity-only reason <see cref="UnityOnlyReason"/> reports.
    /// </summary>
    public static bool NamesPlatform(IEnumerable<string> attributeTypes) =>
        attributeTypes.Contains("UnityEngine.TestTools.UnityPlatformAttribute", StringComparer.Ordinal);

    /// <summary>The type whose use needs Unity: log expectations require a Unity log scope.</summary>
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
    /// one of <see cref="UnityOnlyAttributes"/> on the method, its class or its assembly.
    /// </summary>
    public static string? UnityOnlyReason(bool playModeAssembly, IEnumerable<string> attributeTypes)
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

        return null;
    }

    /// <summary>
    /// The needs-unity reason for a case that constructs <paramref name="typeFullName"/>, an engine type with a
    /// finalizer: outside Unity its finalizer runs on a native object that was never created and throws on the GC
    /// finalizer thread, which ends the process, so such a case is never run.
    /// </summary>
    public static string FinalizerReason(string typeFullName) =>
        $"constructs {typeFullName}, an engine type whose finalizer ends the process outside Unity";

    /// <summary>True for an assembly whose types are the engine's (<c>UnityEngine*</c>, <c>UnityEditor*</c>).</summary>
    public static bool IsEngineAssembly(string assemblyName) =>
        assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal) || assemblyName.StartsWith("UnityEditor", StringComparison.Ordinal);

    /// <summary>
    /// A case interrupted by a host crash is an error, with the host's error output retained as its reason.
    /// </summary>
    public static (TestCategory Category, string Reason) FromHostCrash(string crashText)
    {
        ArgumentNullException.ThrowIfNull(crashText);
        return (TestCategory.Failed, "test host crashed during this case: " + FirstLine(crashText));
    }

    /// <summary>The category of a case NUnit ran, from its result state (status, label) and failure message.</summary>
    /// <param name="status">NUnit <c>TestStatus</c>: Passed, Failed, Skipped, Inconclusive, Warning.</param>
    /// <param name="label">NUnit result label (Ignored, Explicit, Error, Invalid, ...), or empty.</param>
    /// <param name="message">NUnit's message, or empty.</param>
    /// <param name="stackTrace">Runtime frames, needed to identify a missing Unity log scope.</param>
    public static TestCategory FromResult(string status, string? label, string? message, string? stackTrace = null) => status switch
    {
        "Passed" or "Warning" => TestCategory.Passed,
        "Skipped" when label == "Ignored" => TestCategory.Ignored,
        "Skipped" or "Inconclusive" => TestCategory.Skipped,
        _ => (IsEngineFailure(message ?? string.Empty) || IsMissingLogScope(message, stackTrace) || IsReadonlyStaticReflectionFailure(message)) ? TestCategory.NeedsUnity : TestCategory.Failed,
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

    /// <summary>CoreCLR's explicit restriction on reflection writes after a readonly static field's type is initialized.</summary>
    public static bool IsReadonlyStaticReflectionFailure(string? message) =>
        (message ?? string.Empty).Split('\n').Any(line => Named(line, "System.FieldAccessException")
            && line.Contains("Cannot set initonly static field", StringComparison.Ordinal)
            && line.Contains("is initialized", StringComparison.Ordinal));

    /// <summary>The public test framework's missing-scope exception, with a framework stack frame; not a general assertion.</summary>
    public static bool IsMissingLogScope(string? message, string? stackTrace) =>
        (message ?? string.Empty).Split('\n').Any(line => Named(line, "System.InvalidOperationException")
            && line.Contains("No log scope is available", StringComparison.Ordinal))
        && EngineMember(stackTrace) is { } member
        && (member.StartsWith("UnityEngine.TestTools.Logging.LogScope.", StringComparison.Ordinal)
            || member.StartsWith("UnityEngine.TestTools.LogAssert.", StringComparison.Ordinal));

    /// <summary>First UnityEngine/UnityEditor type and member in a runtime stack trace; never infer one from the test name.</summary>
    public static string? EngineMember(string? stackTrace)
    {
        foreach (var line in (stackTrace ?? string.Empty).Split('\n'))
        {
            var frame = line.Trim();
            if (!frame.StartsWith("at ", StringComparison.Ordinal)) continue;
            frame = frame[3..];
            if (!frame.StartsWith("UnityEngine.", StringComparison.Ordinal) && !frame.StartsWith("UnityEditor.", StringComparison.Ordinal)) continue;
            var arguments = frame.IndexOf('(');
            if (arguments > 0) return frame[..arguments].TrimEnd();
        }
        return null;
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
