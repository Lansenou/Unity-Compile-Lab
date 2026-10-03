using System.Text.RegularExpressions;
using Ucl.Compilation;
using Ucl.Core.Model;
using Ucl.Core.Rules;
using Ucl.Core.Testing;
using Ucl.Discovery;
using Ucl.Reporting;
using Ucl.Testing;

namespace Ucl.Cli;

/// <summary>
/// <c>ucl test</c>: compiles the test assemblies of the editor cell the way the Editor does (full images), runs their
/// NUnit cases under .NET in a child test host and classifies every case (docs/test.md). Test assemblies are the assemblies compiled
/// against <c>nunit.framework.dll</c>.
/// </summary>
internal static class TestCommand
{
    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        if (options.Host && options.CacheDir is null)
            options = options with { CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ucl", "test-compile") };
        Regex? filter = null;
        if (options.Filter is { } pattern)
        {
            try
            {
                filter = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException e)
            {
                stderr.WriteLine($"error {ProblemIds.BadArguments}: --filter is not a valid regular expression: {e.Message}");
                return ExitCodes.Configuration;
            }
        }

        // EditMode tests run in the Editor: one editor cell, on the active build target (--platform, default StandaloneWindows64).
        var session = Session.Open(options with { Targets = [TargetKind.Editor], Platforms = options.Platforms.Take(1).ToList() }, env, needEditor: true);
        var report = new TestRunReport { ToolVersion = App.Version, Problems = session.Problems, Timings = options.Timings };
        if (session.Problems.Count == 0 && session.Cells.FirstOrDefault() is (var cell, { } editor))
        {
            try
            {
                report = RunCell(session, cell, editor, options, filter, report, stderr);
            }
            catch (Exception e) when (options.Host && e is ArgumentException or IOException or OperationCanceledException)
            {
                report = report with { Problems = [new Problem(ProblemIds.BadArguments, "Player host: " + e.Message)] };
            }
        }

        var problem = OutputSink.Write(options.Output, TestReport.Render(report, options.Format), session.ProjectRoot, stdout);
        if (problem is null && options.EmitUnityFilter is { } filterFile && report.Problems.Count == 0)
        {
            problem = OutputSink.Write(filterFile, UnityTestFilter.Build(report.Cases) + "\n", session.ProjectRoot, stdout);
        }

        if (problem is not null)
        {
            stderr.WriteLine(TextReport.FormatProblem(problem));
            return ExitCodes.Configuration;
        }

        if (options.Host && options.Output is not null)
            stdout.Write(TestReport.Text(report));

        return report.ExitCode;
    }

    private static TestRunReport RunCell(Session session, CompileCell cell, EditorInstall editor, CliOptions options, Regex? filter, TestRunReport report, TextWriter progress)
    {
        if (options.Host)
        {
            ProjectPlayerCache.Validate(session, editor);
            if (cell.Platform != BuildPlatform.StandaloneWindows64)
                throw new ArgumentException("--host requires --platform StandaloneWindows64.");
            var relative = Path.GetRelativePath(session.ProjectRoot, session.CacheDir);
            if (relative == "." || !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
                throw new ArgumentException("--host requires --cache-dir outside the input project.");
        }
        var graph = session.Graph(cell);
        report = report with { Cell = cell, Problems = graph.Problems };
        if (graph.Problems.Count > 0)
        {
            return report;
        }

        var testPlans = graph.Assemblies
            .Where(p => p.PrecompiledReferences.Any(r => Core.Rules.ProjectPaths.FileName(r).Equals(TestFramework, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var settings = new CompileSettings
        {
            Analyzers = options.Analyzers,
            MaxParallelism = options.Jobs ?? Environment.ProcessorCount,
            CacheDirectory = options.NoCache ? null : session.CacheDir,
            ToolVersion = App.Version,
            FullImages = true,
        };
        var (compile, images) = new CompilationRunner(session.Fs).RunWithImages(
            graph, session.Project, editor, settings, testPlans.Select(p => p.Name).ToHashSet(StringComparer.Ordinal));
        report = report with { Compile = compile, Assemblies = [.. testPlans.Select(p => p.Name).Order(StringComparer.Ordinal)] };
        if (compile.ExitCode != ExitCodes.Clean || testPlans.Count == 0)
        {
            return report;
        }

        // Plugin and editor DLLs resolve by simple name (the file name); project assemblies from their images.
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in editor.EngineModules.Concat(editor.EditorAssemblies)
            .Concat(graph.Assemblies.SelectMany(p => p.PrecompiledReferences).Select(session.Project.ToPhysical)))
        {
            files.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        var run = TestHost.Run(
            [.. testPlans.Select(p => new TestAssemblyImage(p.Name, PlayMode: !p.IsEditorOnly))],
            images,
            files,
            options.Filter,
            arguments => TestHostLauncher.Launch(arguments, session.ProjectRoot),
            options.EditorCases is null ? null : File.ReadAllLines(options.EditorCases).Where(n => n.Length > 0).ToHashSet(StringComparer.Ordinal));
        report = report with { Cases = run.Cases, HostCrashes = run.Crashes };
        if (!options.Host) return report;
        try
        {
            return ProjectPlayerTest.Run(session, graph, editor, options, report, progress);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or OperationCanceledException)
        {
            return report with
            {
                Cases = report.Cases.Select(c => c.Category == TestCategory.NeedsUnity
                    ? c with { Route = "host", Category = TestCategory.Failed, Reason = "Player infrastructure failure: " + e.Message }
                    : c with { Route = c.Category == TestCategory.UnityOnly ? "needs-editor" : "dotnet" }).ToList(),
                HostCrashes = [.. report.HostCrashes, new TestHostCrash(null, null, e.Message)],
            };
        }
    }

    private const string TestFramework = "nunit.framework.dll";
}
