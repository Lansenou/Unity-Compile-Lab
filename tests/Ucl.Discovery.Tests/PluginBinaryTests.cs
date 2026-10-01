using System.Reflection.PortableExecutable;

namespace Ucl.Discovery.Tests;

public sealed class PluginBinaryTests : IDisposable
{
    private readonly TempTree tree = new();

    public void Dispose() => tree.Dispose();

    private static byte[] Managed() => File.ReadAllBytes(typeof(PluginBinary).Assembly.Location);

    // The same image with the CLI header directory (data directory 14) cleared: what a native DLL looks like.
    private static byte[] WithoutCliHeader(byte[] image)
    {
        var copy = (byte[])image.Clone();
        using var stream = new MemoryStream(copy);
        var headers = new PEHeaders(stream);
        var directories = headers.PEHeaderStartOffset + (headers.PEHeader!.Magic == PEMagic.PE32Plus ? 112 : 96);
        Array.Clear(copy, directories + (14 * 8), 8);
        return copy;
    }

    [Fact]
    public void A_dotnet_assembly_is_managed()
    {
        Assert.True(PluginBinary.IsManaged(Managed()));
    }

    [Fact]
    public void A_PE_image_without_a_CLI_header_is_native()
    {
        Assert.False(PluginBinary.IsManaged(WithoutCliHeader(Managed())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(64)]
    public void Not_a_PE_image_is_not_managed(int length)
    {
        var bytes = new byte[length];
        if (length >= 2)
        {
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
        }

        Assert.False(PluginBinary.IsManaged(bytes));
    }

    [Fact]
    public void ReadPrefix_reads_at_most_the_requested_bytes()
    {
        var fs = new PhysicalFileSystem([]);
        tree.Write("a.bin", "0123456789");
        Assert.Equal("0123"u8.ToArray(), fs.ReadPrefix(tree["a.bin"], 4));
        Assert.Equal(10, fs.ReadPrefix(tree["a.bin"], PluginBinary.HeaderBytes).Length);
    }

    [Fact]
    public void Scanner_separates_native_plugins_from_managed_ones()
    {
        tree.Write("P/ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.0.30f1\n");
        tree.Write("P/Packages/manifest.json", "{ \"dependencies\": {} }");
        tree.Write("P/Assets/A.cs", "class A {}");
        Directory.CreateDirectory(tree["P/Assets/Plugins/x86_64"]);
        File.WriteAllBytes(tree["P/Assets/Plugins/Managed.dll"], Managed());
        File.WriteAllBytes(tree["P/Assets/Plugins/x86_64/native.dll"], WithoutCliHeader(Managed()));
        var project = new ProjectLoader(new PhysicalFileSystem([]), new FakeEnvironment(tree["home"])).Load(tree["P"]);
        Assert.Equal(["Assets/Plugins/Managed.dll"], project.Inventory!.Plugins.Select(p => p.Path));
        Assert.Equal(["Assets/Plugins/x86_64/native.dll"], project.Inventory.NativePlugins);
    }
}
