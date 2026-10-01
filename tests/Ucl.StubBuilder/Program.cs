namespace Ucl.StubBuilder;

/// <summary>Entry point: <c>Ucl.StubBuilder [outDir]</c>, default <c>&lt;repo&gt;/artifacts/stubs</c>.</summary>
public static class Program
{
    /// <summary>Builds the stubs; returns the process exit code.</summary>
    public static int Main(string[] args)
    {
        if (args.Length > 1 || args.Any(a => a is "-h" or "--help"))
        {
            Console.Error.WriteLine("usage: Ucl.StubBuilder [outDir]");
            return 2;
        }

        var repo = FindRepoRoot(Directory.GetCurrentDirectory());
        if (repo is null)
        {
            Console.Error.WriteLine("Ucl.StubBuilder: no folder containing Ucl.slnx above the current directory");
            return 2;
        }

        var outDir = args.Length == 1 ? Path.GetFullPath(args[0]) : Path.Combine(repo, "artifacts", "stubs");
        Console.WriteLine(StubBuilder.Build(repo, outDir));
        return 0;
    }

    /// <summary>Walks up from <paramref name="start"/> to the folder that holds <c>Ucl.slnx</c>.</summary>
    public static string? FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(start)); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Ucl.slnx")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
