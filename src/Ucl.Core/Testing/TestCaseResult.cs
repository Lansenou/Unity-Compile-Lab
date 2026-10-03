namespace Ucl.Core.Testing;

/// <summary>One test case and what happened to it.</summary>
/// <param name="Assembly">Test assembly name.</param>
/// <param name="ClassName">Full name of the fixture class.</param>
/// <param name="FullName">NUnit full name (<c>Namespace.Class.Method(args)</c>).</param>
/// <param name="Category">The outcome.</param>
/// <param name="Reason">Why (the complete NUnit failure message, the ignore reason, the unity-only rule), or empty.</param>
/// <param name="DurationMs">Wall-clock milliseconds; reported only with <c>--timings</c>.</param>
public sealed record TestCaseResult(string Assembly, string ClassName, string FullName, TestCategory Category, string Reason, long DurationMs = 0)
{
    /// <summary>First engine exception frame, or the engine constructor found by finalizer prescan; null when unavailable.</summary>
    public string? EngineMember { get; init; }
}
