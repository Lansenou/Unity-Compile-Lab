using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

public class MetaParserTests
{
    private const string AnyExceptWin64 = """
        fileFormatVersion: 2
        guid: 6C5E3B1A9F0D4E2B8A7C6D5E4F3A2B1C
        PluginImporter:
          externalObjects: {}
          serializedVersion: 2
          iconMap: {}
          executionOrder: {}
          defineConstraints:
          - USE_FOO
          - '!NO_FOO'
          isPreloaded: 0
          isOverridable: 0
          isExplicitlyReferenced: 1
          validateReferences: 0
          platformData:
          - first:
              : Any
            second:
              enabled: 0
              settings:
                Exclude Android: 0
                Exclude Editor: 0
                Exclude Linux64: 0
                Exclude OSXUniversal: 0
                Exclude WebGL: 0
                Exclude Win64: 1
                Exclude iOS: 0
          - first:
              Any:
            second:
              enabled: 1
              settings: {}
          - first:
              Editor: Editor
            second:
              enabled: 0
              settings:
                DefaultValueInitialized: true
          userData:
          assetBundleName:
          assetBundleVariant:
        """;

    [Fact]
    public void Any_platform_with_exclude()
    {
        var meta = MetaParser.Parse(AnyExceptWin64);
        Assert.Equal("6c5e3b1a9f0d4e2b8a7c6d5e4f3a2b1c", meta.Guid);
        Assert.Empty(meta.Labels);
        Assert.False(meta.IsRoslynAnalyzer);
        var p = meta.Plugin!;
        Assert.True(p.HasPlatformData);
        Assert.True(p.AnyPlatform);
        Assert.Equal(["Win64"], p.Excluded);
        Assert.Empty(p.Enabled);
        Assert.True(p.IsExplicitlyReferenced);
        Assert.False(p.ValidateReferences);
        Assert.Equal(["USE_FOO", "!NO_FOO"], p.DefineConstraints);
        Assert.False(p.IsCompatibleWith("Win64"));
        Assert.True(p.IsCompatibleWith("Linux64"));
        Assert.True(p.IsCompatibleWith("Editor"));
    }

    [Theory]
    [InlineData("Any:")]
    [InlineData(": Any")]
    [InlineData("'': Any")]
    public void Any_entry_settings_exclude_editor(string anyKey)
    {
        var text = $"PluginImporter:\n  platformData:\n  - first:\n      {anyKey}\n    second:\n      enabled: 1\n      settings:\n        Exclude Editor: 1\n"
            + (anyKey == "Any:" ? string.Empty : "  - first:\n      Any:\n    second:\n      enabled: 1\n      settings: {}\n")
            + "  - first:\n      Editor: Editor\n    second:\n      enabled: 0\n";
        var plugin = MetaParser.Parse(text).Plugin!;
        Assert.True(plugin.AnyPlatform);
        Assert.False(plugin.IsCompatibleWith("Editor"));
        Assert.True(plugin.IsCompatibleWith("Win64"));
    }

    [Fact]
    public void Explicit_per_platform_enable()
    {
        var text = "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nPluginImporter:\n  isExplicitlyReferenced: 0\n  platformData:\n"
            + "  - first:\n      : Any\n    second:\n      enabled: 0\n      settings:\n        Exclude Win64: 1\n"
            + "  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n"
            + "  - first:\n      Standalone: Win64\n    second:\n      enabled: 1\n      settings:\n        CPU: x86_64\n"
            + "  - first:\n      Android: Android\n    second:\n      enabled: 1\n      settings:\n        CPU: ARMv7\n"
            + "  - first:\n      Standalone: OSXUniversal\n    second:\n      enabled: 0\n      settings: {}\n"
            + "  - first:\n      Facebook: \n    second:\n      enabled: 1\n";
        var p = MetaParser.Parse(text).Plugin!;
        Assert.False(p.AnyPlatform);
        Assert.Equal(["Win64"], p.Excluded);
        Assert.Equal(["Android", "Facebook", "Win64"], p.Enabled.Order(StringComparer.Ordinal));
        Assert.True(p.IsCompatibleWith("Win64"));
        Assert.True(p.IsCompatibleWith("Android"));
        Assert.False(p.IsCompatibleWith("OSXUniversal"));
        Assert.False(p.IsCompatibleWith("Editor"));
        Assert.False(p.IsExplicitlyReferenced);
        Assert.True(p.ValidateReferences);
    }

    [Fact]
    public void No_platform_data_is_compatible_everywhere()
    {
        var p = MetaParser.Parse("fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nPluginImporter:\n  serializedVersion: 2\n  platformData: []\n").Plugin!;
        Assert.False(p.HasPlatformData);
        Assert.True(p.IsCompatibleWith("Win64"));
        Assert.True(p.IsCompatibleWith("Editor"));
        Assert.Empty(p.DefineConstraints);
        Assert.True(PluginSettings.Default.IsCompatibleWith("iOS"));
    }

    [Fact]
    public void Malformed_platform_entries_are_skipped()
    {
        var text = "PluginImporter:\n  platformData:\n  - first: {}\n    second:\n      enabled: 1\n  - other: 1\n  - first:\n      Standalone: Linux64\n";
        var p = MetaParser.Parse(text).Plugin!;
        Assert.True(p.HasPlatformData);
        Assert.Empty(p.Enabled);
        Assert.False(p.IsCompatibleWith("Linux64"));
    }

    [Fact]
    public void Roslyn_analyzer_label()
    {
        var text = "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nlabels:\n- RoslynAnalyzer\n- SourceGenerator\nPluginImporter:\n  platformData: []\n";
        var meta = MetaParser.Parse(text);
        Assert.Equal(["RoslynAnalyzer", "SourceGenerator"], meta.Labels);
        Assert.True(meta.IsRoslynAnalyzer);
        Assert.Equal("RoslynAnalyzer", MetaData.RoslynAnalyzerLabel);
    }

    [Fact]
    public void Roslyn_analyzer_label_is_case_sensitive()
    {
        Assert.False(MetaParser.Parse("labels:\n- roslynanalyzer\n").IsRoslynAnalyzer);
    }

    [Theory]
    [InlineData("guid: 1234")]
    [InlineData("guid: zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("fileFormatVersion: 2")]
    public void Invalid_or_missing_guid_is_null(string text)
    {
        Assert.Null(MetaParser.Parse(text).Guid);
    }

    [Fact]
    public void Non_plugin_importer_has_no_plugin_settings()
    {
        var meta = MetaParser.Parse(Metas.Asmdef("abcdefabcdefabcdefabcdefabcdefab"));
        Assert.Equal("abcdefabcdefabcdefabcdefabcdefab", meta.Guid);
        Assert.Null(meta.Plugin);
    }

    [Fact]
    public void Empty_text_never_fails()
    {
        var meta = MetaParser.Parse(string.Empty);
        Assert.Null(meta.Guid);
        Assert.Empty(meta.Labels);
        Assert.Null(meta.Plugin);
    }
}
