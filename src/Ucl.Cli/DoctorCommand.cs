using System.Runtime.InteropServices;
using Ucl.Core.Model;
using Ucl.Core.Rules;
using Ucl.Discovery;
using Ucl.Reporting;

namespace Ucl.Cli;

/// <summary><c>ucl doctor</c>: report the environment (editors, variables, package cache, git) and whether a project can be checked. Writes nothing.</summary>
internal static class DoctorCommand
{
    private static readonly string[] Variables = [EditorLocator.EditorPathVariable, EditorLocator.EditorRootsVariable, DownloadCache.Variable];

    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        if (options.Format is not ("text" or "json"))
        {
            stderr.WriteLine($"error {ProblemIds.BadArguments}: doctor supports --format text or json, not '{options.Format}'");
            return ExitCodes.Configuration;
        }

        // No writable roots: doctor only looks.
        var fs = new PhysicalFileSystem([]);
        var locator = new EditorLocator(fs, env);
        var editors = locator.FindAll();
        var fixes = new List<string>();
        var projectPath = options.Project ?? options.Positionals.FirstOrDefault();

        // Without an argument, the current folder is checked only when it looks like a project, so doctor works anywhere.
        if (projectPath is null && fs.FileExists(Path.Combine(Path.GetFullPath("."), "ProjectSettings", "ProjectVersion.txt")))
        {
            projectPath = ".";
        }

        var project = projectPath is null ? null : InspectProject(Path.GetFullPath(projectPath), options, fs, env, locator, fixes);
        var cache = DownloadCache.Root(env);
        var report = new DoctorReport
        {
            UclVersion = App.Version,
            Runtime = RuntimeInformation.FrameworkDescription,
            Os = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            EditorRoots = locator.SearchRoots().Select(r => (r, fs.DirectoryExists(r))).ToList(),
            Editors = editors.Select(e => new DoctorEditor(
                e.Version.ToString(),
                e.Root,
                e.DataPath,
                fs.DirectoryExists(Path.Combine(e.DataPath, "Managed", "UnityEngine")),
                e.EngineModules.Count,
                e.EditorAssemblies.Count,
                e.NetStandardReferences.Count)).ToList(),
            Variables = Variables.Select(v => (v, env.GetVariable(v))).ToList(),
            PackageCache = cache,
            PackageCacheExists = fs.DirectoryExists(cache),
            GitVersion = ProbeGit(project?.Root ?? Path.GetFullPath(".")),
            Project = project,
            Fixes = fixes,
        };

        stdout.Write(options.Format == "json" ? report.Json() : report.Text());
        return fixes.Count > 0 ? ExitCodes.Configuration : ExitCodes.Clean;
    }

    private static DoctorProject InspectProject(string root, CliOptions options, IFileSystem fs, IEnvironment env, EditorLocator locator, List<string> fixes)
    {
        var context = new ProjectLoader(fs, env).Load(root);
        fixes.AddRange(context.Problems.Select(TextReport.FormatProblem));
        if (context.Inventory is not { } inventory)
        {
            return new DoctorProject(context.Root, null, null, [], []);
        }

        var version = options.UnityVersions.FirstOrDefault() ?? inventory.ProjectVersion;
        string? editor = null;
        if (!version.IsUnity6 && options.EditorPath is null)
        {
            fixes.Add($"error {ProblemIds.UnsupportedVersion}: the project is Unity {version}; ucl checks Unity 6 (6000.x) projects only.");
        }
        else if (locator.Locate(version, options.EditorPath) is { Ok: true } located)
        {
            editor = $"{located.Value!.Version} ({located.Value.Root})";
        }
        else
        {
            fixes.Add($"error {ProblemIds.EditorNotFound}: {locator.Locate(version, options.EditorPath).Error}");
        }

        var sources = inventory.Packages
            .GroupBy(p => p.Source, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, g.Count()))
            .ToList();
        var unresolved = context.Problems.Where(p => p.Id == ProblemIds.UnresolvedPackage).Select(UnresolvedName).ToList();
        return new DoctorProject(context.Root, version.ToString(), editor, sources, unresolved);
    }

    // The resolver reports a package as "Package <name@version> was not found. ..."; the short form keeps the
    // summary readable, and the full message is already in the list of fixes.
    private static string UnresolvedName(Problem p)
    {
        const string prefix = "Package ";
        var end = p.Message.IndexOf(" was not found", StringComparison.Ordinal);
        return p.Message.StartsWith(prefix, StringComparison.Ordinal) && end > prefix.Length ? p.Message[prefix.Length..end] : p.Message;
    }

    private static string? ProbeGit(string workingDirectory)
    {
        var folder = Directory.Exists(workingDirectory) ? workingDirectory : Path.GetFullPath(".");
        var result = new SystemProcessRunner().RunAsync("git", ["--version"], folder).GetAwaiter().GetResult();
        return result.Started && result.ExitCode == 0 ? result.Stdout.Trim() : null;
    }
}
