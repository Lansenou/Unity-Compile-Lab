namespace Ucl.Core.Testing;

/// <summary>What happened to one test case under <c>ucl test</c> (docs/test.md, "Classification").</summary>
public enum TestCategory
{
    /// <summary>Ran and passed.</summary>
    Passed,

    /// <summary>Ran and failed for a reason that is not an engine call: a real failure.</summary>
    Failed,

    /// <summary>Not run or not concluded: explicit, inconclusive, skipped by NUnit.</summary>
    Skipped,

    /// <summary><c>[Ignore]</c> (NUnit reports it as skipped with the label Ignored).</summary>
    Ignored,

    /// <summary>Ran and failed because it called into the native engine, which cannot run outside the Editor.</summary>
    NeedsUnity,

    /// <summary>Never run: it needs the Editor's player loop, log capture, a platform or Play Mode.</summary>
    UnityOnly,
}
