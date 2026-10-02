namespace Ucl.Core.Bee;

/// <summary>Direction of a difference, seen from the Editor's command line.</summary>
public enum BeeChange
{
    /// <summary>The Editor has it, ucl does not.</summary>
    Missing,

    /// <summary>ucl has it, the Editor does not.</summary>
    Extra,

    /// <summary>Both have it with different values (a reference's version or location, the language version).</summary>
    Changed,
}
