using Ucl.Core.Model;

namespace Ucl.Discovery.Tests;

/// <summary>An environment with explicit variables, OS and home folder; never reads the real process environment.</summary>
public sealed class FakeEnvironment(string home, HostOs os = HostOs.Linux) : IEnvironment
{
    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);

    public HostOs Os { get; } = os;

    public string HomeDirectory { get; } = home;

    public string? GetVariable(string name) => Variables.GetValueOrDefault(name);

    public FakeEnvironment With(string name, string value)
    {
        Variables[name] = value;
        return this;
    }
}
