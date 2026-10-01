using Ucl.Cli;

namespace Ucl.Integration.Tests;

/// <summary>Runs the CLI in process.</summary>
public static class Cli
{
    /// <summary>Runs <c>ucl</c> with the given arguments and environment.</summary>
    public static (int Exit, string Stdout, string Stderr) Run(TestEnvironment env, params string[] args)
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        var exit = App.Run(args, stdout, stderr, env);
        return (exit, stdout.ToString(), stderr.ToString());
    }
}
