using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary><see cref="IEnvironment"/> over the current process.</summary>
public sealed class SystemEnvironment : IEnvironment
{
    /// <inheritdoc/>
    public HostOs Os =>
        OperatingSystem.IsWindows() ? HostOs.Windows
        : OperatingSystem.IsMacOS() ? HostOs.MacOS
        : HostOs.Linux;

    /// <inheritdoc/>
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <inheritdoc/>
    public string? GetVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        // An exported-but-empty variable means "unset" to every caller here; treating it as a path would search "".
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
