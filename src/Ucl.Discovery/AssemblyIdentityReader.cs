using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Ucl.Discovery;

/// <summary>Reads a DLL's assembly version from its metadata, memoised per path.</summary>
/// <param name="fs">Disk access.</param>
public sealed class AssemblyIdentityReader(IFileSystem fs)
{
    private readonly Dictionary<string, string> _versions = new(StringComparer.Ordinal);

    /// <summary>The assembly version (<c>4.0.1.2</c>), or empty when the file is missing or is not a .NET assembly.</summary>
    public string Version(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_versions.TryGetValue(path, out var known))
        {
            return known;
        }

        var version = string.Empty;
        if (fs.FileExists(path))
        {
            try
            {
                using var reader = new PEReader(new MemoryStream(fs.ReadAllBytes(path)));
                if (reader.HasMetadata)
                {
                    version = reader.GetMetadataReader().GetAssemblyDefinition().Version.ToString();
                }
            }
            catch (Exception e) when (e is BadImageFormatException or InvalidOperationException)
            {
                // Not a PE image, or a module without an assembly manifest: no identity.
            }
        }

        return _versions[path] = version;
    }
}
