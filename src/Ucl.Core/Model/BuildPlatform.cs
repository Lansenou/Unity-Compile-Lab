namespace Ucl.Core.Model;

/// <summary>A build platform <c>ucl</c> can compile for. Names match Unity's <c>BuildTarget</c> names.</summary>
public enum BuildPlatform
{
    /// <summary>Windows standalone, 64-bit.</summary>
    StandaloneWindows64,

    /// <summary>macOS standalone.</summary>
    StandaloneOSX,

    /// <summary>Linux standalone, 64-bit.</summary>
    StandaloneLinux64,

    /// <summary>iOS.</summary>
    iOS,

    /// <summary>Android.</summary>
    Android,

    /// <summary>Web (WebGL).</summary>
    WebGL,
}
