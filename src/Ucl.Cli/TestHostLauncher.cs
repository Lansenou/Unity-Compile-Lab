using Ucl.Discovery;
using Ucl.Testing;

namespace Ucl.Cli;

/// <summary>
/// Starts the test host: this same program with the hidden <see cref="TestHost.Command"/>. A published <c>ucl</c>
/// runs itself; <c>ucl.dll</c> (a .NET tool, the in-process integration tests) runs under the <c>dotnet</c> host.
/// </summary>
internal static class TestHostLauncher
{
    public static (int Exit, string Error) Launch(IReadOnlyList<string> arguments)
    {
        var (program, prefix) = Program();
        var result = new SystemProcessRunner()
            .RunAsync(program, [.. prefix, TestHost.Command, .. arguments], Path.GetTempPath())
            .GetAwaiter().GetResult();
        return result.Started ? (result.ExitCode, result.Stderr) : (-1, $"the test host did not start ({program}): {result.Stderr}");
    }

    private static (string Program, string[] Prefix) Program()
    {
        var process = Environment.ProcessPath;
        if (process is not null && Path.GetFileNameWithoutExtension(process).Equals("ucl", StringComparison.OrdinalIgnoreCase))
        {
            return (process, []);
        }

        var self = Path.Combine(AppContext.BaseDirectory, "ucl.dll");
        if (!File.Exists(self))
        {
            return (process ?? "ucl", []);
        }

        var dotnet = process is not null && Path.GetFileNameWithoutExtension(process).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? process
            : Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";
        return (dotnet, [self]);
    }
}
