namespace Ucl.Core.Testing;

/// <summary>The test host process ended before its run was complete (docs/test.md, "Test host").</summary>
/// <param name="After">Full name of the last case the host reported before it died, or null.</param>
/// <param name="During">Full name of the case that was running when it died, or null (it died between cases).</param>
/// <param name="Text">What the host wrote to its error stream: the runtime's unhandled exception and stack.</param>
public sealed record TestHostCrash(string? After, string? During, string Text);
