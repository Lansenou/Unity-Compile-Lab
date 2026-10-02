namespace Ucl.Core.Bee;

/// <summary>One reference: a project assembly (<see cref="Assembly"/> set), or a DLL with its identity.</summary>
/// <param name="Assembly">Name of a project assembly compiled in the same run, or null for a DLL.</param>
/// <param name="FileName">DLL file name (for a project assembly, its name plus <c>.dll</c>).</param>
/// <param name="Version">Assembly version from the DLL's metadata, or empty when unknown.</param>
/// <param name="Location">Normalised location (see <see cref="CommandLine"/>); empty for a project assembly.</param>
public sealed record ReferenceEntry(string? Assembly, string FileName, string Version, string Location)
{
    /// <summary>A project assembly.</summary>
    public static ReferenceEntry ForAssembly(string name) => new(name, name + ".dll", string.Empty, string.Empty);

    /// <summary>Display form: <c>assembly Foo</c>, or <c>Bar.dll 1.2.0.0 (location)</c>.</summary>
    public string Display => Assembly is not null
        ? $"assembly {Assembly}"
        : $"{FileName}{(Version.Length > 0 ? " " + Version : string.Empty)} ({Location})";
}
