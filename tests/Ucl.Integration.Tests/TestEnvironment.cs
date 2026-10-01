using Ucl.Core.Model;
using Ucl.Discovery;

namespace Ucl.Integration.Tests;

/// <summary>An environment that sees only the stub editors: a temporary home (no real Unity Hub installs) and UCL_EDITOR_ROOTS.</summary>
public sealed class TestEnvironment : IEnvironment
{
    private readonly Dictionary<string, string> _vars;

    /// <summary>Creates the environment.</summary>
    public TestEnvironment(string home, IDictionary<string, string>? extra = null)
    {
        HomeDirectory = home;
        _vars = new Dictionary<string, string>(StringComparer.Ordinal) { ["UCL_EDITOR_ROOTS"] = Repo.StubEditors };
        foreach (var (k, v) in extra ?? new Dictionary<string, string>())
        {
            _vars[k] = v;
        }
    }

    /// <inheritdoc/>
    public HostOs Os => OperatingSystem.IsWindows() ? HostOs.Windows : OperatingSystem.IsMacOS() ? HostOs.MacOS : HostOs.Linux;

    /// <inheritdoc/>
    public string HomeDirectory { get; }

    /// <inheritdoc/>
    public string? GetVariable(string name) => _vars.GetValueOrDefault(name);
}
