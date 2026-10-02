namespace Ucl.Core.Bee;

/// <summary>What kind of input differs.</summary>
public enum BeeCategory
{
    /// <summary>The assembly exists on one side only.</summary>
    Assembly,

    /// <summary>Source files.</summary>
    Sources,

    /// <summary>References.</summary>
    References,

    /// <summary>Preprocessor symbols.</summary>
    Defines,

    /// <summary>Language version or unsafe code.</summary>
    Options,

    /// <summary>Suppressed warnings.</summary>
    NoWarn,

    /// <summary>Analyzers and source generators.</summary>
    Analyzers,

    /// <summary>Analyzer additional files.</summary>
    AdditionalFiles,
}
