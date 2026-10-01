using Ucl.Core.Rules;
using Ucl.Discovery;
using Ucl.Reporting;

namespace Ucl.Cli;

/// <summary><c>ucl fetch</c>: download the registry packages a project cannot resolve locally into the download cache.</summary>
internal static class FetchCommand
{
    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        var root = Path.GetFullPath(options.Project ?? options.Positionals.FirstOrDefault() ?? ".");

        // The download cache is outside the project, so it is the only place this command may write.
        var fs = new PhysicalFileSystem([DownloadCache.Root(env)]);
        using var http = new SystemHttpClient();
        var report = new PackageFetcher(fs, http, env).FetchAsync(root).GetAwaiter().GetResult();
        foreach (var o in report.Outcomes)
        {
            stdout.WriteLine(o.Status switch
            {
                PackageFetchStatus.Fetched => $"fetched {o.Name}@{o.Version} from {o.Detail}",
                PackageFetchStatus.Cached => $"cached {o.Name}@{o.Version} ({o.Detail})",
                _ => $"failed {o.Name}@{o.Version}: {o.Detail}",
            });
        }

        foreach (var p in report.Problems)
        {
            stderr.WriteLine(TextReport.FormatProblem(p));
        }

        return report.Problems.Count > 0 ? ExitCodes.Configuration : ExitCodes.Clean;
    }
}
