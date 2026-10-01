namespace Ucl.Integration.Tests;

/// <summary>Locations in the repository and the stub editors, built once per test run.</summary>
public static class Repo
{
    /// <summary>The repository root (the folder holding Ucl.slnx).</summary>
    public static string Root { get; } = FindRoot();

    private static readonly Lazy<string> StubsLazy = new(() => Ucl.StubBuilder.StubBuilder.Build(Root!, Path.Combine(Root!, "artifacts", "stubs")));

    /// <summary>The fixtures folder.</summary>
    public static string Fixtures => Path.Combine(Root, "fixtures");

    /// <summary>Stub output folder (<c>editors/</c>, <c>dlls/</c>), built on first use.</summary>
    public static string Stubs => StubsLazy.Value;

    /// <summary>Folder that holds the stub editors, Unity Hub layout.</summary>
    public static string StubEditors => Path.Combine(Stubs, "editors");

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Ucl.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Ucl.slnx not found above the test output folder");
    }
}
