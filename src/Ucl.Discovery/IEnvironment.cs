using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary>Port to the process environment.</summary>
public interface IEnvironment
{
    /// <summary>An environment variable, or null.</summary>
    string? GetVariable(string name);

    /// <summary>The host operating system.</summary>
    HostOs Os { get; }

    /// <summary>The user's home folder.</summary>
    string HomeDirectory { get; }
}
