using System.Text.RegularExpressions;

namespace Ucl.Core.Testing;

/// <summary>
/// The Unity Test Framework <c>-testFilter</c> value that excludes every test class whose cases all passed under
/// <c>ucl test</c> (docs/test.md, "Skipping what passed"): <c>!&lt;regex&gt;;!&lt;regex&gt;...</c>, one anchored
/// regex per class over the test full name.
/// </summary>
public static class UnityTestFilter
{
    /// <summary>Classes (assembly, class) whose every case passed: no failed, skipped, ignored, needs-unity or unity-only case.</summary>
    public static IReadOnlyList<string> FullyPassingClasses(IEnumerable<TestCaseResult> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        return cases
            .GroupBy(c => c.ClassName, StringComparer.Ordinal)
            .Where(g => g.All(c => c.Category == TestCategory.Passed))
            .Select(g => g.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The filter text, or empty when no class qualifies.</summary>
    public static string Build(IEnumerable<TestCaseResult> cases) =>
        string.Join(';', FullyPassingClasses(cases).Select(c => "!^" + Regex.Escape(c) + "\\."));
}
