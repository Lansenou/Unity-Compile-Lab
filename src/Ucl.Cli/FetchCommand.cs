using Ucl.Core.Rules;
using Ucl.Discovery;

namespace Ucl.Cli;

/// <summary>Placeholder; implemented in phase 3.</summary>
internal static class FetchCommand
{
    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        stderr.WriteLine("not implemented yet");
        return ExitCodes.Configuration;
    }
}
