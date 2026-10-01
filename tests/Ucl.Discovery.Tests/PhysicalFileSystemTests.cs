namespace Ucl.Discovery.Tests;

public sealed class PhysicalFileSystemTests : IDisposable
{
    private readonly TempTree tree = new();

    public void Dispose() => tree.Dispose();

    [Fact]
    public void WritesInsideWritableRootCreatingFolders()
    {
        var fs = new PhysicalFileSystem(tree["Project/Library/ucl"]);
        var target = tree["Project/Library/ucl/cache/Ünï code/a.bin"];
        fs.WriteAllBytes(target, [1, 2, 3]);
        Assert.Equal([1, 2, 3], fs.ReadAllBytes(target));
        Assert.Equal(3, fs.GetStamp(target).Length);
        fs.DeleteFile(target);
        Assert.False(fs.FileExists(target));
        fs.DeleteFile(target);
    }

    [Theory]
    [InlineData("Project/Assets/A.cs")]
    [InlineData("Project/Library/ucl2/x")]
    [InlineData("Project/Library/ucl/../x")]
    [InlineData("Project/Library/ucl")]
    public void RefusesWritesAndDeletesOutsideWritableRoots(string relative)
    {
        tree.Write("Project/Assets/A.cs", "keep");
        var fs = new PhysicalFileSystem(tree["Project/Library/ucl"] + Path.DirectorySeparatorChar);
        var path = tree[relative];
        Assert.False(fs.IsWritable(path));
        Assert.Throws<InvalidOperationException>(() => fs.WriteAllBytes(path, [0]));
        Assert.Throws<InvalidOperationException>(() => fs.DeleteFile(path));
        Assert.Equal("keep", File.ReadAllText(tree["Project/Assets/A.cs"]));
    }

    [Fact]
    public void NoWritableRootsRefusesEverything()
    {
        var fs = new PhysicalFileSystem();
        Assert.Throws<InvalidOperationException>(() => fs.WriteAllBytes(tree["x"], [0]));
        Assert.False(File.Exists(tree["x"]));
    }

    [Fact]
    public void ListingsAreOrdinalAndMissingFoldersAreEmpty()
    {
        tree.Write("d/b.txt").Write("d/B2.txt").Write("d/a.txt").Dir("d/sub").Dir("d/Sub2");
        var fs = new PhysicalFileSystem();
        Assert.Equal(["B2.txt", "a.txt", "b.txt"], fs.ListFiles(tree["d"]));
        Assert.Equal(["Sub2", "sub"], fs.ListDirectories(tree["d"]));
        Assert.Empty(fs.ListFiles(tree["missing"]));
        Assert.Empty(fs.ListDirectories(tree["missing"]));
    }

    [Fact]
    public void ReadAllTextStripsUtf8Bom()
    {
        File.WriteAllBytes(tree["bom.txt"], [0xEF, 0xBB, 0xBF, 0xC3, 0xBC]);
        Assert.Equal("ü", new PhysicalFileSystem().ReadAllText(tree["bom.txt"]));
    }
}
