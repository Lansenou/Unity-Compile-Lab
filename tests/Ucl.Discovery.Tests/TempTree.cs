using System.Text;

namespace Ucl.Discovery.Tests;

/// <summary>A temporary folder that tests fill with files and that is deleted afterwards.</summary>
public sealed class TempTree : IDisposable
{
    public TempTree()
    {
        // Space and non-ASCII in the root itself, so every test also covers such paths.
        Root = Path.Combine(Path.GetTempPath(), "ucl discovery ü " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string this[string relative] => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public TempTree Write(string relative, string content = "")
    {
        var path = this[relative];
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return this;
    }

    public TempTree Dir(string relative)
    {
        Directory.CreateDirectory(this[relative]);
        return this;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leaked temp folder must not fail the test that created it.
        }
    }
}
