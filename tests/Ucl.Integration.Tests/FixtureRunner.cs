namespace Ucl.Integration.Tests;

/// <summary>Prepares a fixture (copy plus materialised DLLs) and runs a manifest cell against it.</summary>
public static class FixtureRunner
{
    /// <summary>Copies the fixture into <paramref name="temp"/>/project and places its materialised DLLs.</summary>
    public static string Prepare(FixtureEntry entry, string temp)
    {
        var project = Path.Combine(temp, "project");
        TempDir.Copy(Path.Combine(Repo.Fixtures, entry.Name), project);
        foreach (var m in entry.Materialize ?? [])
        {
            var target = Path.Combine(project, m.To);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(Repo.Stubs, "dlls", m.Dll + ".dll"), target, overwrite: true);
        }

        return project;
    }

    /// <summary>The CLI arguments of a cell.</summary>
    public static List<string> Args(string project, FixtureCell cell, string cacheDir)
    {
        var args = new List<string>
        {
            "check", project, "--format", "json", "--cache-dir", cacheDir,
            "--unity-version", cell.UnityVersion, "--target", cell.Target, "--platform", cell.Platform,
        };
        // Pin fixture runs to the stub install: a real Hub install of the same version must not win discovery.
        var stub = Path.Combine(Repo.StubEditors, cell.UnityVersion);
        if (RealEditor.Path is null && Directory.Exists(stub))
            args.AddRange(["--editor", stub]);

        if (cell.EditorOs is not null)
        {
            args.AddRange(["--editor-os", cell.EditorOs]);
        }

        args.AddRange(cell.ExtraArgs ?? []);
        return args;
    }

    /// <summary>Runs one cell with a private home and cache.</summary>
    public static (int Exit, string Stdout, string Stderr) Check(string project, FixtureCell cell, string temp)
    {
        var home = Path.Combine(temp, "home");
        Directory.CreateDirectory(home);
        return Cli.Run(new TestEnvironment(home), [.. Args(project, cell, Path.Combine(temp, "cache"))]);
    }
}
