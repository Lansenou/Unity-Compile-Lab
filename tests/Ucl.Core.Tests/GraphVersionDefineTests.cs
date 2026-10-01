using Ucl.Core.Model;

namespace Ucl.Core.Tests;

/// <summary>D53: asmdef versionDefines.</summary>
public class GraphVersionDefineTests
{
    private static InventoryBuilder With(string name, string expression, string define = "HAS_IT") => new InventoryBuilder()
        .Asmdef("Assets/Mod/Mod.asmdef", "Mod", $"\"versionDefines\": [{{ \"name\": \"{name}\", \"expression\": \"{expression}\", \"define\": \"{define}\" }}]");

    [Fact]
    public void D53_package_version_in_range_defines_symbol()
    {
        var g = With("com.unity.inputsystem", "[1.4,2.0)").Package("com.unity.inputsystem", "1.11.2").Editor();
        var mod = g.Find("Mod")!;
        Assert.Equal("Assets/Mod/Mod.asmdef versionDefines", mod.Defines.Reasons["HAS_IT"]);
        Assert.False(g.BaseDefines.Contains("HAS_IT"));
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void D53_package_version_out_of_range_is_a_miss()
    {
        Assert.False(With("com.unity.inputsystem", "[1.4,2.0)").Package("com.unity.inputsystem", "2.0.0").Editor().Find("Mod")!.Defines.Contains("HAS_IT"));
        Assert.False(With("com.unity.inputsystem", "1.4").Package("com.unity.inputsystem", "1.4.0-pre.1").Editor().Find("Mod")!.Defines.Contains("HAS_IT"));
    }

    [Fact]
    public void D53_absent_package_is_a_miss()
    {
        var g = With("com.unity.inputsystem", "1.0").Editor();
        Assert.False(g.Find("Mod")!.Defines.Contains("HAS_IT"));
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void D53_empty_expression_matches_any_present_version()
    {
        Assert.True(With("com.foo", string.Empty).Package("com.foo", "0.0.1").Editor().Find("Mod")!.Defines.Contains("HAS_IT"));
        Assert.False(With("com.foo", string.Empty).Editor().Find("Mod")!.Defines.Contains("HAS_IT"));
    }

    [Fact]
    public void D53_non_semver_package_version_is_a_miss()
    {
        var g = With("com.git", string.Empty).Package("com.git", "https://github.com/x/y.git#abc").Editor();
        Assert.False(g.Find("Mod")!.Defines.Contains("HAS_IT"));
    }

    [Theory]
    [InlineData("6000.0", true)]
    [InlineData("[6000.0,6000.1)", true)]
    [InlineData("[6000.0.30]", true)]
    [InlineData("(6000.0.30,7000)", false)]
    [InlineData("6000.1", false)]
    public void D53_Unity_resource_uses_editor_version_without_suffix(string expression, bool expected)
    {
        var g = With("Unity", expression).Editor();
        Assert.Equal(expected, g.Find("Mod")!.Defines.Contains("HAS_IT"));
    }

    [Fact]
    public void D53_Unity_resource_follows_cell_version()
    {
        var cell = Cells.Editor() with { UnityVersion = new UnityVersion(6000, 2, 1, "f1") };
        Assert.True(With("Unity", "6000.1").Graph(cell).Find("Mod")!.Defines.Contains("HAS_IT"));
    }

    [Theory]
    [InlineData("com.foo", "[bad", "HAS_IT")]
    [InlineData("com.foo", "1.0", "1INVALID")]
    [InlineData("com.foo", "1.0", "")]
    [InlineData("", "1.0", "HAS_IT")]
    public void D53_invalid_entry_is_UCL1021_and_ignored(string name, string expression, string define)
    {
        var g = With(name, expression, define).Package("com.foo", "1.0.0").Editor();
        var d = Assert.Single(g.Diagnostics);
        Assert.Equal(ProblemIds.BadVersionDefine, d.Id);
        Assert.Equal("UCL1021", d.Id);
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.Equal("Mod", d.Assembly);
        Assert.Equal("Assets/Mod/Mod.asmdef", d.File);
        Assert.NotNull(g.Find("Mod"));
        Assert.False(g.Find("Mod")!.Defines.Contains("HAS_IT"));
    }

    [Fact]
    public void D53_multiple_entries_are_independent()
    {
        var g = new InventoryBuilder()
            .Package("com.a", "2.0.0")
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod",
                "\"versionDefines\": [" +
                "{ \"name\": \"com.a\", \"expression\": \"1.0\", \"define\": \"A_1\" }," +
                "{ \"name\": \"com.a\", \"expression\": \"(,2.0)\", \"define\": \"A_OLD\" }," +
                "{ \"name\": \"com.b\", \"expression\": \"\", \"define\": \"B_ANY\" }," +
                "{ \"name\": \"Unity\", \"expression\": \"6000.0\", \"define\": \"U6\" }]")
            .Editor();
        var defines = g.Find("Mod")!.Defines;
        Assert.True(defines.Contains("A_1"));
        Assert.False(defines.Contains("A_OLD"));
        Assert.False(defines.Contains("B_ANY"));
        Assert.True(defines.Contains("U6"));
    }

    [Fact]
    public void D53_version_define_does_not_override_existing_reason()
    {
        var g = With("Unity", "6000.0", "UNITY_EDITOR").Editor();
        Assert.Equal("D10", g.Find("Mod")!.Defines.Reasons["UNITY_EDITOR"]);
    }
}
