using Ucl.Core.Testing;

namespace Ucl.Testing;

/// <summary>What <see cref="NUnitHost"/> reports while it runs, in order.</summary>
public interface ITestEvents
{
    /// <summary>A case NUnit discovered (its category is not meaningful yet).</summary>
    void Discovered(TestCaseResult testCase);

    /// <summary>A selected case is about to run.</summary>
    void Started(string assembly, string fullName);

    /// <summary>A case's outcome: classified before the run, or its result.</summary>
    void Finished(TestCaseResult testCase);
}
