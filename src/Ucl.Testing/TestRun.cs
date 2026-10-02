using Ucl.Core.Testing;

namespace Ucl.Testing;

/// <summary>The outcome of running test assemblies: every discovered case, each with exactly one category.</summary>
/// <param name="Cases">Cases sorted by assembly, then full name.</param>
/// <param name="Discovered">Cases NUnit discovered in the compiled assemblies (after <c>--filter</c>); always <c>Cases.Count</c>.</param>
/// <param name="Crashes">Every time the test host process died, in order.</param>
public sealed record TestRun(IReadOnlyList<TestCaseResult> Cases, int Discovered, IReadOnlyList<TestHostCrash> Crashes);
