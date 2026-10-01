using Ucl.Core.Model;

namespace Ucl.Core.Tests;

public class PlatformInfoTests
{
    [Theory]
    [InlineData(BuildPlatform.StandaloneWindows64, "WindowsStandalone64", "Win64", "Standalone", 1, ScriptingBackend.Mono, true)]
    [InlineData(BuildPlatform.StandaloneOSX, "macOSStandalone", "OSXUniversal", "Standalone", 1, ScriptingBackend.Mono, true)]
    [InlineData(BuildPlatform.StandaloneLinux64, "LinuxStandalone64", "Linux64", "Standalone", 1, ScriptingBackend.Mono, true)]
    [InlineData(BuildPlatform.iOS, "iOS", "iOS", "iOS", 4, ScriptingBackend.IL2CPP, true)]
    [InlineData(BuildPlatform.Android, "Android", "Android", "Android", 7, ScriptingBackend.IL2CPP, false)]
    [InlineData(BuildPlatform.WebGL, "WebGL", "WebGL", "WebGL", 13, ScriptingBackend.IL2CPP, false)]
    public void Name_table_matches_docs_platforms(BuildPlatform p, string asmdef, string key, string group, int number, ScriptingBackend backend, bool is64)
    {
        var info = PlatformInfo.Of(p);
        Assert.Equal(new PlatformInfo(p, asmdef, key, group, number, backend, is64), info);
        Assert.Equal(p, info.Platform);
        Assert.Equal(asmdef, info.AsmdefName);
        Assert.Equal(key, info.PluginKey);
        Assert.Equal(group, info.TargetGroup);
        Assert.Equal(number, info.TargetGroupNumber);
        Assert.Equal(backend, info.DefaultBackend);
        Assert.Equal(is64, info.Is64Bit);
    }

    [Fact]
    public void All_is_in_enum_order_and_complete()
    {
        Assert.Equal(Enum.GetValues<BuildPlatform>(), PlatformInfo.All.Select(p => p.Platform));
        Assert.Equal("Editor", PlatformInfo.EditorAsmdefName);
        Assert.Equal("Editor", PlatformInfo.EditorPluginKey);
    }

    [Theory]
    [InlineData("StandaloneWindows64", BuildPlatform.StandaloneWindows64)]
    [InlineData("standaloneosx", BuildPlatform.StandaloneOSX)]
    [InlineData("ANDROID", BuildPlatform.Android)]
    [InlineData("webgl", BuildPlatform.WebGL)]
    [InlineData("ios", BuildPlatform.iOS)]
    public void Parse_is_case_insensitive(string text, BuildPlatform expected)
    {
        var r = PlatformInfo.Parse(text);
        Assert.True(r.Ok);
        Assert.Equal(expected, r.Value);
    }

    [Fact]
    public void Parse_unknown_lists_supported()
    {
        var r = PlatformInfo.Parse("PS5");
        Assert.False(r.Ok);
        Assert.Contains("unknown platform 'PS5'", r.Error);
        Assert.Contains("StandaloneWindows64, StandaloneOSX, StandaloneLinux64, iOS, Android, WebGL", r.Error);
    }

    [Fact]
    public void CompileCell_label_and_IsEditor()
    {
        var editor = Cells.Editor();
        Assert.True(editor.IsEditor);
        Assert.Equal("6000.0.30f1 editor StandaloneWindows64", editor.Label);
        var dev = Cells.Player(BuildPlatform.Android, development: true);
        Assert.False(dev.IsEditor);
        Assert.Equal("6000.0.30f1 player Android development", dev.Label);
        Assert.Equal("6000.0.30f1 player WebGL", Cells.Player(BuildPlatform.WebGL).Label);
    }
}
