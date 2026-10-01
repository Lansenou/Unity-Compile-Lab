using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

public class SpecialFoldersTests
{
    [Fact]
    public void Predefined_names_in_compile_order()
    {
        Assert.Equal(
            ["Assembly-CSharp-firstpass", "Assembly-CSharp", "Assembly-CSharp-Editor-firstpass", "Assembly-CSharp-Editor"],
            SpecialFolders.Predefined);
    }

    [Theory]
    [InlineData("Assets/Scripts/Player.cs", "Assembly-CSharp")]
    [InlineData("Assets/Player.cs", "Assembly-CSharp")]
    [InlineData("Assets/Plugins/Lib.cs", "Assembly-CSharp-firstpass")]
    [InlineData("Assets/Plugins/Deep/Lib.cs", "Assembly-CSharp-firstpass")]
    [InlineData("Assets/Standard Assets/Water.cs", "Assembly-CSharp-firstpass")]
    [InlineData("Assets/Pro Standard Assets/Image.cs", "Assembly-CSharp-firstpass")]
    [InlineData("Assets/plugins/Lib.cs", "Assembly-CSharp-firstpass")]
    [InlineData("Assets/Editor/Tool.cs", "Assembly-CSharp-Editor")]
    [InlineData("Assets/Game/Tools/Editor/Tool.cs", "Assembly-CSharp-Editor")]
    [InlineData("Assets/Game/editor/Tool.cs", "Assembly-CSharp-Editor")]
    [InlineData("Assets/Game/EDITOR/Deeper/Tool.cs", "Assembly-CSharp-Editor")]
    [InlineData("Assets/Plugins/Editor/Tool.cs", "Assembly-CSharp-Editor-firstpass")]
    [InlineData("Assets/Standard Assets/Sub/Editor/Tool.cs", "Assembly-CSharp-Editor-firstpass")]
    [InlineData("Assets/Scripts/Plugins/Lib.cs", "Assembly-CSharp")]
    [InlineData("Assets/EditorTools/Tool.cs", "Assembly-CSharp")]
    [InlineData("Assets/Scripts/Editor.cs", "Assembly-CSharp")]
    public void PredefinedAssemblyFor(string path, string expected) =>
        Assert.Equal(expected, SpecialFolders.PredefinedAssemblyFor(path));

    [Fact]
    public void IsEditorPredefined()
    {
        Assert.True(SpecialFolders.IsEditorPredefined(SpecialFolders.Editor));
        Assert.True(SpecialFolders.IsEditorPredefined(SpecialFolders.EditorFirstPass));
        Assert.False(SpecialFolders.IsEditorPredefined(SpecialFolders.Main));
        Assert.False(SpecialFolders.IsEditorPredefined(SpecialFolders.FirstPass));
        Assert.False(SpecialFolders.IsEditorPredefined("Game.Editor"));
    }

    [Fact]
    public void IsInEditorFolder_ignores_the_top_folder_and_file_name()
    {
        Assert.False(SpecialFolders.IsInEditorFolder("Editor/X.cs"));
        Assert.False(SpecialFolders.IsInEditorFolder("Assets/Editor"));
        Assert.True(SpecialFolders.IsInEditorFolder("Assets/Editor/X.cs"));
    }
}
