namespace Ucl.Core.Model;

/// <summary>The operating system the Editor runs on, which selects <c>UNITY_EDITOR_WIN</c>, <c>_OSX</c> or <c>_LINUX</c>.</summary>
public enum HostOs
{
    /// <summary>Windows.</summary>
    Windows,

    /// <summary>macOS.</summary>
    MacOS,

    /// <summary>Linux.</summary>
    Linux,
}
