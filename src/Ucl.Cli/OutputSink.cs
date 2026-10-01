using Ucl.Core.Model;

namespace Ucl.Cli;

/// <summary>Writes a report to stdout or to <c>--output</c>, refusing the folders ucl must never write to.</summary>
internal static class OutputSink
{
    public static Problem? Write(string? output, string text, string projectRoot, TextWriter stdout)
    {
        if (output is null)
        {
            stdout.Write(text);
            return null;
        }

        var full = Path.GetFullPath(output);
        foreach (var protectedDir in new[] { "Assets", "Packages", "ProjectSettings" })
        {
            var rel = Path.GetRelativePath(Path.Combine(projectRoot, protectedDir), full);
            if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
            {
                return new Problem(ProblemIds.BadArguments, $"--output must not be inside {protectedDir}/ (ucl never writes there)");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return null;
    }
}
