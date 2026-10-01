using System.Text.Json;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>R12: <c>ucl explain</c> names the owning assembly, the rule that chose it, and its defines.</summary>
public sealed class ExplainTests
{
    /// <summary>An Editor-folder script: Assembly-CSharp-Editor in the editor, not compiled in a player.</summary>
    [Fact]
    public void Editor_folder_script()
    {
        using var temp = new TempDir();
        var file = Path.Combine(Repo.Fixtures, "editor-folder-depth", "Assets", "Tools", "Level", "Editor", "LevelInfoEditor.cs");
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "explain", file, "--target", "editor", "--target", "player", "--format", "json");
        Assert.Equal(0, exit);
        var cells = JsonDocument.Parse(stdout).RootElement.GetProperty("cells");
        Assert.Equal("Assembly-CSharp-Editor", cells[0].GetProperty("assembly").GetString());
        Assert.True(cells[0].GetProperty("compiled").GetBoolean());
        Assert.Equal("D10", cells[0].GetProperty("defines").GetProperty("UNITY_EDITOR").GetString());
        Assert.False(cells[1].GetProperty("compiled").GetBoolean());
        Assert.Contains("Editor", cells[1].GetProperty("why").GetString(), StringComparison.Ordinal);
    }

    /// <summary>An asmdef script names its asmdef in text output.</summary>
    [Fact]
    public void Asmdef_script_text()
    {
        using var temp = new TempDir();
        var file = Path.Combine(Repo.Fixtures, "asmdef-ref-by-name", "Assets", "Game", "Core", "Health.cs");
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "explain", file);
        Assert.Equal(0, exit);
        Assert.Contains("assembly: Game.Core", stdout, StringComparison.Ordinal);
        Assert.Contains("Game.Core.asmdef", stdout, StringComparison.Ordinal);
    }

    /// <summary>Usage errors and files outside a project are exit 3.</summary>
    [Fact]
    public void Bad_usage()
    {
        using var temp = new TempDir();
        Assert.Equal(3, Cli.Run(new TestEnvironment(temp.Path), "explain").Exit);
        var stray = Path.Combine(temp.Path, "x.cs");
        File.WriteAllText(stray, "class X {}");
        Assert.Equal(3, Cli.Run(new TestEnvironment(temp.Path), "explain", stray).Exit);
    }
}
