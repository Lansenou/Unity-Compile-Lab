using System.Reflection;
using Ucl.Core.Model;
using Ucl.Core.Rules;
using Ucl.Discovery;

namespace Ucl.Cli;

/// <summary>Entry point shared by <c>Program</c> and the in-process integration tests.</summary>
public static class App
{
    /// <summary>The tool version (assembly informational version without build metadata).</summary>
    public static string Version { get; } =
        (typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>Runs one command and returns its exit code. Never throws: bugs become exit 4.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        try
        {
            var parsed = ArgParser.Parse(args);
            if (!parsed.Ok)
            {
                stderr.WriteLine($"error {ProblemIds.BadArguments}: {parsed.Error}");
                return ExitCodes.Configuration;
            }

            var options = parsed.Value!;
            switch (options.Command)
            {
                case "help":
                    stdout.Write(HelpText.Usage);
                    return ExitCodes.Clean;
                case "version":
                    stdout.WriteLine($"ucl {Version}");
                    return ExitCodes.Clean;
                case "check":
                    return CheckCommand.Run(options, stdout, stderr, env);
                case "graph":
                    return GraphCommand.Run(options, stdout, stderr, env);
                default:
                    stderr.WriteLine($"error {ProblemIds.BadArguments}: command '{options.Command}' is not available in ucl {Version} yet");
                    return ExitCodes.Configuration;
            }
        }
        catch (Exception e)
        {
            stderr.WriteLine($"internal error: {e}");
            return ExitCodes.Internal;
        }
    }

    /// <summary>Combines exit codes by severity: internal, configuration, errors, warnings-as-errors, clean.</summary>
    public static int Worst(IEnumerable<int> codes) =>
        codes.DefaultIfEmpty(ExitCodes.Clean).MaxBy(Rank);

    private static int Rank(int code) => code switch
    {
        ExitCodes.Internal => 4,
        ExitCodes.Configuration => 3,
        ExitCodes.Errors => 2,
        ExitCodes.WarningsAsErrors => 1,
        _ => 0,
    };
}
