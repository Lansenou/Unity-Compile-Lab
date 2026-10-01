using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

public class ProjectPathsTests
{
    [Theory]
    [InlineData("Assets/A/B.cs", "Assets/A")]
    [InlineData("Assets", "")]
    [InlineData("Packages/com.foo/Runtime/X.cs", "Packages/com.foo/Runtime")]
    public void Folder(string path, string expected) => Assert.Equal(expected, ProjectPaths.Folder(path));

    [Theory]
    [InlineData("Assets/A/B.cs", "B.cs")]
    [InlineData("B.cs", "B.cs")]
    public void FileName(string path, string expected) => Assert.Equal(expected, ProjectPaths.FileName(path));

    [Fact]
    public void SelfAndAncestors_walks_to_the_top()
    {
        Assert.Equal(["a/b/c", "a/b", "a"], ProjectPaths.SelfAndAncestors("a/b/c"));
        Assert.Empty(ProjectPaths.SelfAndAncestors(string.Empty));
    }

    [Theory]
    [InlineData("Assets/A", "Assets/A", true)]
    [InlineData("Assets/A/B.cs", "Assets/A", true)]
    [InlineData("Assets/AB/C.cs", "Assets/A", false)]
    [InlineData("Assets/a/B.cs", "Assets/A", false)]
    public void IsUnder(string path, string folder, bool expected) => Assert.Equal(expected, ProjectPaths.IsUnder(path, folder));

    [Theory]
    [InlineData(".git", true)]
    [InlineData(".hidden.cs", true)]
    [InlineData("Samples~", true)]
    [InlineData("cvs", true)]
    [InlineData("CVS", true)]
    [InlineData("file.tmp", true)]
    [InlineData("FILE.TMP", true)]
    [InlineData("Scripts", false)]
    [InlineData("a.cs", false)]
    [InlineData("cvsfile", false)]
    public void IsHiddenName(string name, bool expected) => Assert.Equal(expected, ProjectPaths.IsHiddenName(name));
}
