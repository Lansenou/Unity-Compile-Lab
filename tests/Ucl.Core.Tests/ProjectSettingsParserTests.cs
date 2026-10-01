using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

public class ProjectSettingsParserTests
{
    /// <summary>An excerpt of a Unity 6 <c>ProjectSettings.asset</c> with the fields ucl reads.</summary>
    internal const string Unity6Asset = """
        %YAML 1.1
        %TAG !u! tag:unity3d.com,2011:
        --- !u!129 &1
        PlayerSettings:
          m_ObjectHideFlags: 0
          serializedVersion: 28
          productGUID: 1b2c3d4e5f60718293a4b5c6d7e8f901
          AndroidProfiler: 0
          defaultScreenOrientation: 4
          targetDevice: 2
          companyName: DefaultCompany
          productName: My Game
          defaultCursor: {fileID: 0}
          cursorHotspot: {x: 0, y: 0}
          m_SplashScreenBackgroundColor: {r: 0.13725491, g: 0.12156863, b: 0.1254902, a: 1}
          m_ShowUnitySplashScreen: 1
          m_SplashScreenLogos: []
          m_VirtualRealitySplashScreen: {fileID: 0}
          bundleVersion: 0.1
          preloadedAssets: []
          cloudProjectId:
          applicationIdentifier:
            Android: com.DefaultCompany.MyGame
            Standalone: com.DefaultCompany.MyGame
          buildNumber:
            Standalone: 0
            iPhone: 0
          AndroidMinSdkVersion: 23
          scriptingDefineSymbols:
            Android: ANDROID_ONLY;SHARED
            Standalone: MY_GAME;STEAM;MY_GAME
            WebGL:
            4: IOS_LEGACY
          additionalCompilerArguments:
            Standalone:
            - -nowarn:1234
            - -define:FROM_ARGS
            Android:
            - -warnaserror
          platformArchitecture: {}
          scriptingBackend:
            Android: 1
            Standalone: 0
            13: 1
          il2cppCompilerConfiguration: {}
          managedStrippingLevel:
            EmbeddedLinux: 1
          incrementalIl2cppBuild: {}
          suppressCommonWarnings: 0
          allowUnsafeCode: 1
          useDeterministicCompilation: 1
          additionalIl2CppArgs:
          scriptingRuntimeVersion: 1
          gcIncremental: 1
          gcWBarrierValidation: 0
          apiCompatibilityLevelPerPlatform: {}
          editorAssembliesCompatibilityLevel: 1
          m_RenderingPath: 1
          m_MobileRenderingPath: 1
          metroPackageName: MyGame
          vrSettings:
            enable360StereoCapture: 0
          m_BuildTargetGroupLightmapEncodingQuality:
          - serializedVersion: 2
            m_BuildTarget: Android
            m_EncodingQuality: 1
          activeInputHandler: 2
          windowsGamepadBackendHint: 0
          cloudProjectId:
          framebufferDepthMemorylessMode: 0
          qualitySettingsNames: []
          projectName:
          organizationId:
          cloudEnabled: 0
          legacyClampBlendShapeWeights: 0
          hmiLoadingImage: {fileID: 0}
          platformRequiresReadableAssets: 0
          virtualTexturingSupportEnabled: 0
          insecureHttpOption: 0
        """;

    [Fact]
    public void D50_reads_named_and_numeric_scriptingDefineSymbols_keys()
    {
        var s = ProjectSettingsParser.Parse(Unity6Asset);
        Assert.Equal(["MY_GAME", "STEAM"], s.ScriptingDefineSymbols["Standalone"]);
        Assert.Equal(["ANDROID_ONLY", "SHARED"], s.ScriptingDefineSymbols["Android"]);
        Assert.Equal(["IOS_LEGACY"], s.ScriptingDefineSymbols["iOS"]);
        Assert.Empty(s.ScriptingDefineSymbols["WebGL"]);
    }

    [Fact]
    public void Reads_unindented_additionalCompilerArguments_lists()
    {
        var s = ProjectSettingsParser.Parse(Unity6Asset);
        Assert.Equal(["-nowarn:1234", "-define:FROM_ARGS"], s.AdditionalCompilerArguments["Standalone"]);
        Assert.Equal(["-warnaserror"], s.AdditionalCompilerArguments["Android"]);
    }

    [Fact]
    public void Reads_scriptingBackend_with_numeric_keys()
    {
        var s = ProjectSettingsParser.Parse(Unity6Asset);
        Assert.Equal(ScriptingBackend.Mono, s.ScriptingBackend["Standalone"]);
        Assert.Equal(ScriptingBackend.IL2CPP, s.ScriptingBackend["Android"]);
        Assert.Equal(ScriptingBackend.IL2CPP, s.ScriptingBackend["WebGL"]);
    }

    [Fact]
    public void Reads_flags()
    {
        var s = ProjectSettingsParser.Parse(Unity6Asset);
        Assert.Equal(2, s.ActiveInputHandler);
        Assert.True(s.AllowUnsafeCode);
        Assert.False(s.SuppressCommonWarnings);
        Assert.Empty(s.ApiCompatibilityPerGroup);
        Assert.Equal(6, s.ApiCompatibilityLevel);
        Assert.False(s.IsNetFramework("Standalone"));
    }

    [Fact]
    public void Api_compatibility_global_and_per_group()
    {
        var s = ProjectSettingsParser.Parse("PlayerSettings:\n  apiCompatibilityLevel: 3\n  apiCompatibilityLevelPerPlatform:\n    Android: 6\n    Standalone: x\n");
        Assert.Equal(3, s.ApiCompatibilityLevel);
        Assert.True(s.IsNetFramework("iOS"));
        Assert.False(s.IsNetFramework("Android"));
        Assert.False(s.IsNetFramework("Standalone"));
        var t = ProjectSettingsParser.Parse("PlayerSettings:\n  apiCompatibilityLevelPerPlatform:\n    Standalone: 3\n");
        Assert.True(t.IsNetFramework("Standalone"));
        Assert.False(t.IsNetFramework("Android"));
    }

    [Fact]
    public void Reads_api_levels_after_wrapped_values()
    {
        // The shape of a real Unity 6 asset: wrapped flow mappings and long values before the API keys.
        const string Asset = """
            %YAML 1.1
            %TAG !u! tag:unity3d.com,2011:
            --- !u!129 &1
            PlayerSettings:
              iOSLaunchScreenPortrait: {fileID: 2800000, guid: 0a1b2c3d4e5f60718293a4b5c6d7e8f9,
                type: 3}
              metroApplicationDescription: A long description that wraps onto a
                continuation line
              m_BuildTargetPlatformIcons:
              - m_BuildTarget: Android
                m_Icons:
                - m_Textures: []
                  m_Width: 432
              apiCompatibilityLevelPerPlatform:
                Standalone: 3
              editorAssembliesCompatibilityLevel: 2
              apiCompatibilityLevel: 6
              activeInputHandler: 1
            """;
        var s = ProjectSettingsParser.Parse(Asset);
        Assert.Equal(3, s.ApiCompatibilityPerGroup["Standalone"]);
        Assert.Equal(2, s.EditorAssembliesCompatibilityLevel);
        Assert.Equal(6, s.ApiCompatibilityLevel);
        Assert.Equal(1, s.ActiveInputHandler);
        Assert.True(s.IsNetFramework("Standalone"));
        Assert.False(s.IsNetFramework("Android"));
        Assert.True(s.IsEditorNetFramework);
        Assert.Equal(1, ProjectSettingsData.Default.EditorAssembliesCompatibilityLevel);
    }

    [Fact]
    public void Defaults_when_empty()
    {
        var s = ProjectSettingsParser.Parse(string.Empty);
        Assert.Empty(s.ScriptingDefineSymbols);
        Assert.Empty(s.ScriptingBackend);
        Assert.Empty(s.AdditionalCompilerArguments);
        Assert.Equal(0, s.ActiveInputHandler);
        Assert.False(s.AllowUnsafeCode);
        Assert.True(s.SuppressCommonWarnings);
        Assert.Equal(ProjectSettingsData.Default, ProjectSettingsData.Default);
        Assert.True(ProjectSettingsData.Default.SuppressCommonWarnings);
    }

    [Fact]
    public void Without_PlayerSettings_header_reads_the_root()
    {
        var s = ProjectSettingsParser.Parse("activeInputHandler: 1\nscriptingBackend:\n  Standalone: 1\n");
        Assert.Equal(1, s.ActiveInputHandler);
        Assert.Equal(ScriptingBackend.IL2CPP, s.ScriptingBackend["Standalone"]);
    }

    [Fact]
    public void Non_numeric_ints_fall_back()
    {
        var s = ProjectSettingsParser.Parse("PlayerSettings:\n  activeInputHandler: two\n  apiCompatibilityLevel: \n");
        Assert.Equal(0, s.ActiveInputHandler);
        Assert.Equal(6, s.ApiCompatibilityLevel);
    }

    [Fact]
    public void Additional_arguments_empty_list_and_scalar()
    {
        var s = ProjectSettingsParser.Parse("PlayerSettings:\n  additionalCompilerArguments:\n    Standalone: []\n    Android: -nowarn:1\n");
        Assert.Empty(s.AdditionalCompilerArguments["Standalone"]);
        Assert.Empty(s.AdditionalCompilerArguments["Android"]);
    }

    [Theory]
    [InlineData("A;B;C", new[] { "A", "B", "C" })]
    [InlineData(" A ; B ;; A ", new[] { "A", "B" })]
    [InlineData("A,B", new[] { "A", "B" })]
    [InlineData("", new string[0])]
    public void SplitDefines(string text, string[] expected) => Assert.Equal(expected, ProjectSettingsParser.SplitDefines(text));

    [Fact]
    public void ProjectVersion_reads_m_EditorVersion()
    {
        var r = ProjectSettingsParser.ParseProjectVersion("m_EditorVersion: 6000.0.30f1\nm_EditorVersionWithRevision: 6000.0.30f1 (abc)\n");
        Assert.True(r.Ok);
        Assert.Equal(new UnityVersion(6000, 0, 30, "f1"), r.Value);
    }

    [Fact]
    public void ProjectVersion_with_crlf()
    {
        var r = ProjectSettingsParser.ParseProjectVersion("m_EditorVersion: 6000.1.2f1\r\nm_EditorVersionWithRevision: 6000.1.2f1 (deadbeef)\r\n");
        Assert.Equal(new UnityVersion(6000, 1, 2, "f1"), r.Value);
    }

    [Fact]
    public void ProjectVersion_missing_or_bad()
    {
        var missing = ProjectSettingsParser.ParseProjectVersion("m_EditorVersionWithRevision: 6000.0.30f1 (abc)\n");
        Assert.False(missing.Ok);
        Assert.Contains("no m_EditorVersion", missing.Error);
        Assert.False(ProjectSettingsParser.ParseProjectVersion("m_EditorVersion: banana\n").Ok);
    }
}
