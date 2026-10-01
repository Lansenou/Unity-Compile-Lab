using System.Security.Cryptography;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>R13: a run changes nothing in the project except <c>Library/ucl</c>.</summary>
public sealed class ReadOnlyTests
{
    /// <summary>Hashes the whole tree of every fixture copy before and after a run with the default cache folder.</summary>
    [Fact]
    public void Check_writes_nothing_outside_Library_ucl()
    {
        foreach (var entry in FixtureManifest.Load().Fixtures)
        {
            using var temp = new TempDir();
            var project = FixtureRunner.Prepare(entry, temp.Path);
            var before = Snapshot(project);
            var home = Path.Combine(temp.Path, "home");
            Directory.CreateDirectory(home);
            var cell = entry.Cells[0];
            var args = FixtureRunner.Args(project, cell, "unused");
            var i = args.IndexOf("--cache-dir");
            args.RemoveRange(i, 2);
            Cli.Run(new TestEnvironment(home), [.. args]);
            var after = Snapshot(project);
            Assert.Equal(before, after);
        }
    }

    /// <summary>The committed fixture tree itself is untouched by graph runs (which never write).</summary>
    [Fact]
    public void Graph_on_committed_fixtures_changes_nothing()
    {
        var before = Snapshot(Repo.Fixtures);
        using var temp = new TempDir();
        foreach (var entry in FixtureManifest.Load().Fixtures.Where(f => (f.Materialize ?? []).Count == 0))
        {
            Cli.Run(new TestEnvironment(temp.Path), "graph", Path.Combine(Repo.Fixtures, entry.Name), "--format", "json", "--cache-dir", Path.Combine(temp.Path, "cache"));
        }

        Assert.Equal(before, Snapshot(Repo.Fixtures));
    }

    private static SortedDictionary<string, string> Snapshot(string root)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (rel.StartsWith("Library/ucl/", StringComparison.Ordinal) || rel.Contains("/Library/ucl/", StringComparison.Ordinal))
            {
                continue;
            }

            result[rel] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        }

        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, dir).Replace('\\', '/');
            if (!rel.EndsWith("Library/ucl", StringComparison.Ordinal) && !rel.Contains("Library/ucl/", StringComparison.Ordinal) && rel != "Library")
            {
                result[rel + "/"] = "dir";
            }
        }

        return result;
    }
}
