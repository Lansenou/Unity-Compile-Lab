namespace Ucl.Core.Bee;

/// <summary>One difference between the Editor's command line and ucl's for one assembly.</summary>
/// <param name="Category">What differs.</param>
/// <param name="Change">Missing in ucl, extra in ucl, or changed.</param>
/// <param name="Value">The item (a path, a symbol, a reference's display form).</param>
/// <param name="Expected">The Editor's value for a change, else null.</param>
/// <param name="Actual">ucl's value for a change, else null.</param>
public sealed record BeeDifference(BeeCategory Category, BeeChange Change, string Value, string? Expected = null, string? Actual = null);
