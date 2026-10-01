using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Rules;
using Ucl.Discovery;

namespace Ucl.Cli;

/// <summary>
/// <c>--changed &lt;git-ref&gt;</c>: the files changed since a ref (committed, staged, unstaged and untracked), and
/// the assemblies they touch plus everything that depends on them.
/// </summary>
internal static class ChangedFiles
{
    /// <summary>Logical paths changed since <paramref name="gitRef"/>, or a problem when git cannot tell.</summary>
    public static Result<IReadOnlyList<string>> Since(string gitRef, ProjectContext project, IProcessRunner git)
    {
        var top = Git(git, project.Root, "rev-parse", "--show-toplevel");
        if (!top.Ok)
        {
            return Result<IReadOnlyList<string>>.Failure(top.Error!);
        }

        var root = top.Value!.Trim();
        var diff = Git(git, root, "diff", "--name-only", "-z", gitRef, "--");
        var untracked = Git(git, root, "ls-files", "--others", "--exclude-standard", "-z");
        if (!diff.Ok || !untracked.Ok)
        {
            return Result<IReadOnlyList<string>>.Failure(diff.Error ?? untracked.Error!);
        }

        var logical = (diff.Value + untracked.Value)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => project.ToLogical(Path.Combine(root, p)))
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
        return Result<IReadOnlyList<string>>.Success(logical);
    }

    /// <summary>
    /// The assemblies to report: those whose inputs include a changed file, and their dependents. Null means
    /// "everything", for changes that can reshape the graph (settings, manifests, definitions, global options)
    /// or a changed script no planned assembly owns (a deleted or moved file).
    /// </summary>
    public static IReadOnlySet<string>? Affected(AssemblyGraph graph, IReadOnlyList<string> changed)
    {
        var seeds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in changed)
        {
            if (ReshapesGraph(path))
            {
                return null;
            }

            var owners = graph.Assemblies.Where(a => Inputs(a).Contains(path)).Select(a => a.Name).ToList();
            if (owners.Count == 0 && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !graph.ScriptOwners.ContainsKey(path))
            {
                return null;
            }

            seeds.UnionWith(owners);
        }

        var affected = new HashSet<string>(seeds, StringComparer.Ordinal);
        foreach (var plan in graph.Assemblies)
        {
            // Topological order: dependencies come first, so one pass closes over dependents.
            if (plan.References.Any(affected.Contains))
            {
                affected.Add(plan.Name);
            }
        }

        return affected;
    }

    private static bool ReshapesGraph(string path)
    {
        var name = ProjectPaths.FileName(path);
        return path.StartsWith("ProjectSettings/", StringComparison.Ordinal)
            || path is "Packages/manifest.json" or "Packages/packages-lock.json" or "Assets/csc.rsp"
            || name == "package.json"
            || name.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".globalconfig", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string?> Inputs(AssemblyPlan a) =>
        a.Sources.Concat(a.PrecompiledReferences).Concat(a.Analyzers).Concat(a.AdditionalFiles).Concat(a.AnalyzerConfigs)
            .Append(a.DefinitionPath).Append(a.ResponseFile).Append(a.RuleSet);

    private static Result<string> Git(IProcessRunner git, string dir, params string[] args)
    {
        var r = git.RunAsync("git", args, dir).GetAwaiter().GetResult();
        if (!r.Started)
        {
            return Result<string>.Failure("--changed needs git on PATH");
        }

        return r.ExitCode == 0
            ? Result<string>.Success(r.Stdout)
            : Result<string>.Failure($"--changed: git {string.Join(' ', args)} failed: {r.Stderr.Trim()}");
    }
}
