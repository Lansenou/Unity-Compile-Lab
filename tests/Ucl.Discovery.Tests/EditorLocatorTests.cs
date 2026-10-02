using Ucl.Core.Model;

namespace Ucl.Discovery.Tests;

public sealed class EditorLocatorTests : IDisposable
{
    private static readonly UnityVersion V30 = new(6000, 0, 30, "f1");
    private static readonly UnityVersion V31 = new(6000, 0, 31, "f1");

    private readonly TempTree tree = new();

    public void Dispose() => tree.Dispose();

    /// <summary>Writes a fake install whose data folder is <paramref name="data"/>; files are empty, only names matter.</summary>
    private void FakeData(string data)
    {
        foreach (var dll in new[] { "UnityEngine.dll", "UnityEngine.CoreModule.dll", "UnityEngine.PhysicsModule.dll", "UnityEditor.dll", "UnityEditor.CoreModule.dll", "Unity.Other.dll", "UnityEngine.xml" })
        {
            tree.Write($"{data}/Managed/UnityEngine/{dll}");
        }

        tree.Write($"{data}/NetStandard/ref/2.1.0/netstandard.dll")
            .Write($"{data}/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll")
            .Write($"{data}/UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll")
            .Write($"{data}/UnityReferenceAssemblies/unity-4.8-api/Facades/System.Runtime.dll");
    }

    private void WindowsLinuxInstall(string root, bool exe = false)
    {
        FakeData($"{root}/Editor/Data");
        tree.Write($"{root}/Editor/{(exe ? "Unity.exe" : "Unity")}");
    }

    private void MacInstall(string root)
    {
        FakeData($"{root}/Unity.app/Contents");
        tree.Write($"{root}/Unity.app/Contents/MacOS/Unity");
    }

    private EditorLocator Locator(FakeEnvironment env) => new(new PhysicalFileSystem(), env);

    private FakeEnvironment Env(HostOs os = HostOs.Linux) => new(tree["home"], os);

    [Fact]
    public void LinuxHubDefaultInstallListsReferenceAssemblies()
    {
        WindowsLinuxInstall("home/Unity/Hub/Editor/6000.0.30f1");
        var result = Locator(Env()).Locate(V30, null);
        Assert.True(result.Ok, result.Error);
        var install = result.Value!;
        var data = tree["home/Unity/Hub/Editor/6000.0.30f1/Editor/Data"];
        var managed = Path.Combine(data, "Managed", "UnityEngine");
        Assert.Equal(V30, install.Version);
        Assert.Equal(tree["home/Unity/Hub/Editor/6000.0.30f1"], install.Root);
        Assert.Equal(data, install.DataPath);
        Assert.Equal([Path.Combine(managed, "UnityEngine.CoreModule.dll"), Path.Combine(managed, "UnityEngine.PhysicsModule.dll")], install.EngineModules);
        Assert.Equal([Path.Combine(managed, "UnityEditor.CoreModule.dll"), Path.Combine(managed, "UnityEditor.dll")], install.EditorAssemblies);
        Assert.Equal(
            [Path.Combine(data, "NetStandard", "compat", "2.1.0", "shims", "netfx", "mscorlib.dll"), Path.Combine(data, "NetStandard", "ref", "2.1.0", "netstandard.dll")],
            install.NetStandardReferences);
        Assert.Equal(
            [Path.Combine(data, "UnityReferenceAssemblies", "unity-4.8-api", "Facades", "System.Runtime.dll"), Path.Combine(data, "UnityReferenceAssemblies", "unity-4.8-api", "mscorlib.dll")],
            install.NetFrameworkReferences);
    }

    [Fact]
    public void A05_A06_profile_reference_sets_follow_Unity_folders()
    {
        WindowsLinuxInstall("home/Unity/Hub/Editor/6000.0.30f1");
        var data = "home/Unity/Hub/Editor/6000.0.30f1/Editor/Data";
        tree.Write($"{data}/UnityReferenceAssemblies/unity-4.8-api/System.Core.dll")
            .Write($"{data}/UnityReferenceAssemblies/unity-4.8-api/System.Web.dll")
            .Write($"{data}/UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll")
            .Write($"{data}/NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll")
            .Write($"{data}/NetStandard/Extensions/2.0.0/System.Memory.dll")
            .Write($"{data}/NetStandard/EditorExtensions/Editor.Ext.dll");
        var install = Locator(Env()).Locate(V30, null).Value!;
        string P(string relative) => tree[$"{data}/{relative}"];

        // A05: the listed core libraries (System.Web is not one) and every facade.
        Assert.Equal(
            [P("UnityReferenceAssemblies/unity-4.8-api/Facades/System.Runtime.dll"), P("UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll"),
             P("UnityReferenceAssemblies/unity-4.8-api/System.Core.dll"), P("UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll")],
            install.NetFrameworkReferences);

        // A06: netstandard.dll, both shim folders and the extensions; EditorExtensions only for Editor-only assemblies.
        Assert.Equal(
            [P("NetStandard/Extensions/2.0.0/System.Memory.dll"), P("NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"),
             P("NetStandard/compat/2.1.0/shims/netstandard/System.Runtime.dll"), P("NetStandard/ref/2.1.0/netstandard.dll")],
            install.NetStandardReferences);
        Assert.Equal([P("NetStandard/EditorExtensions/Editor.Ext.dll")], install.NetStandardEditorExtensions);
    }

    [Fact]
    public void Editor_references_beyond_the_modules_follow_Unity_folders()
    {
        WindowsLinuxInstall("home/Unity/Hub/Editor/6000.0.30f1");
        var data = "home/Unity/Hub/Editor/6000.0.30f1/Editor/Data";
        tree.Write($"{data}/Managed/UnityEditor.Graphs.dll")
            .Write($"{data}/Managed/Unity.CompilationPipeline.Common.dll")
            .Write($"{data}/PlaybackEngines/WebGLSupport/Managed/UnityEngine.WebGLModule.dll")
            .Write($"{data}/PlaybackEngines/WebGLSupport/Managed/Other.dll")
            .Write($"{data}/PlaybackEngines/WebGLSupport/UnityEditor.WebGL.Extensions.dll")
            .Write($"{data}/PlaybackEngines/WebGLSupport/UnityEditor.WebGL.Other.dll")
            .Write($"{data}/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll")
            .Write($"{data}/PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll")
            .Write($"{data}/PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll")
            .Write($"{data}/Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll")
            .Write($"{data}/Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll");
        var install = Locator(Env()).Locate(V30, null).Value!;
        string P(string relative) => tree[$"{data}/{relative}"];

        Assert.Equal(P("Managed/UnityEngine/UnityEngine.dll"), install.EngineFacade);
        Assert.Equal([P("PlaybackEngines/WebGLSupport/Managed/UnityEngine.WebGLModule.dll")], install.PlatformModules[BuildPlatform.WebGL]);
        Assert.Empty(install.PlatformModules[BuildPlatform.iOS]);
        Assert.Equal(
            [P("Managed/UnityEditor.Graphs.dll"), P("PlaybackEngines/AndroidPlayer/Unity.Android.Gradle.dll"), P("PlaybackEngines/AndroidPlayer/Unity.Android.Types.dll"),
             P("PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll"), P("PlaybackEngines/WebGLSupport/UnityEditor.WebGL.Extensions.dll")],
            install.EditorExtensions);
        Assert.Equal(P("Managed/Unity.CompilationPipeline.Common.dll"), install.CompilationPipeline);
        Assert.Equal(
            [P("Tools/BuildPipeline/Unity.SourceGenerators/Unity.Properties.SourceGenerator.dll"), P("Tools/BuildPipeline/Unity.SourceGenerators/Unity.SourceGenerators.dll")],
            install.SourceGenerators);
        Assert.Equal(tree[data], install.PlaybackEnginesParent);
    }

    [Fact]
    public void Mac_platform_support_lives_beside_Unity_app_and_old_generators_under_Tools()
    {
        MacInstall("Applications/Unity/Hub/Editor/6000.0.30f1");
        var root = "Applications/Unity/Hub/Editor/6000.0.30f1";
        tree.Write($"{root}/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll")
            .Write($"{root}/Unity.app/Contents/Tools/Unity.SourceGenerators/Unity.SourceGenerators.dll");
        var install = Locator(Env(HostOs.MacOS)).Locate(V30, tree[$"{root}/Unity.app"]).Value!;
        Assert.Equal([tree[$"{root}/PlaybackEngines/MacStandaloneSupport/UnityEditor.OSXStandalone.Extensions.dll"]], install.EditorExtensions);
        Assert.Equal(tree[root], install.PlaybackEnginesParent);
        Assert.Equal([tree[$"{root}/Unity.app/Contents/Tools/Unity.SourceGenerators/Unity.SourceGenerators.dll"]], install.SourceGenerators);
        Assert.Null(install.CompilationPipeline);
    }

    [Fact]
    public void WindowsHubDefaultUnderProgramFiles()
    {
        WindowsLinuxInstall("Program Files/Unity/Hub/Editor/6000.0.30f1", exe: true);
        var install = Locator(Env(HostOs.Windows).With("ProgramFiles", tree["Program Files"])).Locate(V30, null);
        Assert.True(install.Ok, install.Error);
        Assert.Equal(tree["Program Files/Unity/Hub/Editor/6000.0.30f1/Editor/Data"], install.Value!.DataPath);
    }

    [Fact]
    public void MacLayoutThroughEditorRoots()
    {
        MacInstall("Editors/6000.0.31f1");
        var install = Locator(Env(HostOs.MacOS).With("UCL_EDITOR_ROOTS", tree["Editors"])).Locate(V31, null);
        Assert.True(install.Ok, install.Error);
        Assert.Equal(tree["Editors/6000.0.31f1/Unity.app"], install.Value!.Root);
        Assert.Equal(tree["Editors/6000.0.31f1/Unity.app/Contents"], install.Value.DataPath);
        Assert.Equal(2, install.Value.EngineModules.Count);
    }

    [Fact]
    public void EditorRootsListIsSplitAndFindAllIsSorted()
    {
        WindowsLinuxInstall("A/6000.0.31f1");
        WindowsLinuxInstall("B/6000.0.30f1");
        WindowsLinuxInstall("B/not-a-version");
        tree.Dir("B/6000.0.29f1/Editor/Data");
        var env = Env().With("UCL_EDITOR_ROOTS", tree["A"] + Path.PathSeparator + tree["B"] + Path.PathSeparator);
        var all = Locator(env).FindAll();
        Assert.Equal([V30, V31], all.Select(i => i.Version));
    }

    [Fact]
    public void SecondaryInstallPathFromHubConfig()
    {
        WindowsLinuxInstall("Secondary Root/6000.0.30f1");
        tree.Write("home/.config/UnityHub/secondaryInstallPath.json", System.Text.Json.JsonSerializer.Serialize(tree["Secondary Root"]));
        Assert.True(Locator(Env()).Locate(V30, null).Ok);
    }

    [Fact]
    public void UnityEditorPathVariable()
    {
        WindowsLinuxInstall("custom/6000.0.30f1");
        var result = Locator(Env().With("UNITY_EDITOR_PATH", tree["custom/6000.0.30f1/Editor/Unity"])).Locate(V30, null);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(tree["custom/6000.0.30f1"], result.Value!.Root);
    }

    [Theory]
    [InlineData("6000.0.31f1")]
    [InlineData("6000.0.31f1/Editor")]
    [InlineData("6000.0.31f1/Editor/Unity.exe")]
    public void ExplicitPathFormsForWindowsLayout(string relative)
    {
        WindowsLinuxInstall("x/6000.0.31f1", exe: true);
        var result = Locator(Env()).Locate(V30, tree["x/" + relative]);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(V31, result.Value!.Version);
        Assert.Equal(tree["x/6000.0.31f1"], result.Value.Root);
    }

    [Theory]
    [InlineData("6000.0.31f1")]
    [InlineData("6000.0.31f1/Unity.app")]
    [InlineData("6000.0.31f1/Unity.app/Contents/MacOS/Unity")]
    public void ExplicitPathFormsForMacLayout(string relative)
    {
        MacInstall("x/6000.0.31f1");
        var result = Locator(Env(HostOs.MacOS)).Locate(V31, tree["x/" + relative]);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(tree["x/6000.0.31f1/Unity.app"], result.Value!.Root);
        Assert.Equal(V31, result.Value.Version);
    }

    [Fact]
    public void ExplicitPathWithoutVersionFolderAssumesRequestedVersion()
    {
        WindowsLinuxInstall("My Unity/editor install");
        var result = Locator(Env()).Locate(V31, tree["My Unity/editor install"]);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(V31, result.Value!.Version);
    }

    [Fact]
    public void ExplicitPathThatIsNotAnInstallFails()
    {
        tree.Dir("empty");
        var result = Locator(Env()).Locate(V30, tree["empty"]);
        Assert.False(result.Ok);
        Assert.Contains("Managed/UnityEngine", result.Error);
    }

    [Fact]
    public void VersionMismatchFailureListsInstalledVersionsAndVariables()
    {
        WindowsLinuxInstall("home/Unity/Hub/Editor/6000.0.31f1");
        var result = Locator(Env()).Locate(V30, null);
        Assert.False(result.Ok);
        Assert.Contains("6000.0.30f1 is not installed", result.Error);
        Assert.Contains("6000.0.31f1", result.Error);
        Assert.Contains("UNITY_EDITOR_PATH", result.Error);
        Assert.Contains("UCL_EDITOR_ROOTS", result.Error);
        Assert.Contains("--editor", result.Error);
    }

    [Fact]
    public void NoInstallsSaysNoneFound()
    {
        var result = Locator(Env()).Locate(V30, null);
        Assert.False(result.Ok);
        Assert.Contains("none found", result.Error);
    }

    [Fact]
    public void VersionWithoutSuffixMatchesAnySuffix()
    {
        WindowsLinuxInstall("home/Unity/Hub/Editor/6000.0.30f1");
        var result = Locator(Env()).Locate(new UnityVersion(6000, 0, 30, string.Empty), null);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(V30, result.Value!.Version);
    }
}
