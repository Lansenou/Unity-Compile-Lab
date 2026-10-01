namespace Ucl.Integration.Tests;

/// <summary>A temporary folder deleted on dispose.</summary>
public sealed class TempDir : IDisposable
{
    /// <summary>Creates the folder.</summary>
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ucl-test-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path);
    }

    /// <summary>The folder.</summary>
    public string Path { get; }

    /// <summary>Copies a folder tree into <paramref name="destination"/>.</summary>
    public static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = System.IO.Path.Combine(destination, System.IO.Path.GetRelativePath(source, file));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a locked file on Windows must not fail the test.
        }
    }
}
