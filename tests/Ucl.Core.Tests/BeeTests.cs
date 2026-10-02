using Ucl.Core.Bee;
using Ucl.Core.Model;

namespace Ucl.Core.Tests;

/// <summary>The Bee oracle's pure parts: response-file parsing, dag classification, the diff.</summary>
public class BeeTests
{
    private const string Rsp = """
        "Assets/Game/Player.cs"
        "Packages/com.foo/Runtime/A B.cs"
        /home/me/Local Package/Runtime/C.cs
        -r:"C:/Program Files/Unity/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"
        /reference:Assets/Plugins/Lib.dll
        -define:UNITY_EDITOR
        -d:A;B
        /define:C,D
        /nowarn:0169
        -nowarn:0649,CS1701
        -analyzer:"Assets/Analyzers/Gen.dll"
        /a:Assets/Analyzers/Other.dll
        /additionalfile:"Library/Bee/artifacts/1900b0aE.dag/Game.UnityAdditionalFile.txt"
        -langversion:9.0
        -target:library
        -out:"Library/Bee/artifacts/1900b0aE.dag/Game.Core.dll"
        -refout:"Library/Bee/artifacts/1900b0aE.dag/Game.Core.ref.dll"
        /deterministic
        /optimize+
        -unsafe
        /debug:portable
        /nologo
        /RuntimeMetadataVersion:v4.0.30319
        /utf8output
        /preferreduilang:en-US

        """;

    [Fact]
    public void Parses_every_argument_kind()
    {
        var r = BeeResponseFile.Parse(Rsp.Replace("\n", "\r\n", StringComparison.Ordinal));
        Assert.Equal(["Assets/Game/Player.cs", "Packages/com.foo/Runtime/A B.cs", "/home/me/Local Package/Runtime/C.cs"], r.Sources);
        Assert.Equal(["C:/Program Files/Unity/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll", "Assets/Plugins/Lib.dll"], r.References);
        Assert.Equal(["UNITY_EDITOR", "A", "B", "C", "D"], r.Defines);
        Assert.Equal(["0169", "0649", "CS1701"], r.NoWarn);
        Assert.Equal(["Assets/Analyzers/Gen.dll", "Assets/Analyzers/Other.dll"], r.Analyzers);
        Assert.Equal(["Library/Bee/artifacts/1900b0aE.dag/Game.UnityAdditionalFile.txt"], r.AdditionalFiles);
        Assert.Equal("9.0", r.LangVersion);
        Assert.True(r.Unsafe);
        Assert.Equal("Game.Core", r.AssemblyName("whatever.rsp"));
        Assert.False(BeeResponseFile.Parse("-unsafe+\n-unsafe-\n").Unsafe);
        Assert.True(BeeResponseFile.Parse("/unsafe+\n").Unsafe);
    }

    [Theory]
    [InlineData("Game.rsp", "Game")]
    [InlineData("Game.dll.rsp", "Game")]
    [InlineData("Assembly-CSharp.rsp", "Assembly-CSharp")]
    public void Assembly_name_falls_back_to_the_file_name(string file, string name)
    {
        Assert.Equal(name, BeeResponseFile.Parse("\"Assets/A.cs\"\n").AssemblyName(file));
        Assert.Equal("Win", BeeResponseFile.Parse("-out:\"Library\\Bee\\artifacts\\x.dag\\Win.dll\"\n").AssemblyName(file));
    }

    [Theory]
    [InlineData("169", "CS0169")]
    [InlineData("0169", "CS0169")]
    [InlineData("cs0169", "CS0169")]
    [InlineData(" CS1701 ", "CS1701")]
    [InlineData("nullable", "nullable")]
    public void Warning_ids_are_normalised(string id, string expected) => Assert.Equal(expected, CommandLine.WarningId(id));

    private static readonly UnityVersion Editor = new(6000, 3, 19, "f1");

    [Fact]
    public void Dag_cell_comes_from_its_defines()
    {
        var editor = BeeDag.CellOf(["UNITY_EDITOR", "UNITY_EDITOR_WIN", "UNITY_STANDALONE_WIN", "ENABLE_MONO", "UNITY_6000_3_19"], Editor).Value!;
        Assert.Equal(new CompileCell(Editor, TargetKind.Editor, BuildPlatform.StandaloneWindows64, ScriptingBackend.Mono, false, HostOs.Windows, PlatformEngine: true), editor);

        var player = BeeDag.CellOf(["UNITY_ANDROID", "ENABLE_IL2CPP", "DEVELOPMENT_BUILD", "UNITY_6000_3_19"], Editor).Value!;
        Assert.Equal(TargetKind.Player, player.Target);
        Assert.Equal(BuildPlatform.Android, player.Platform);
        Assert.Equal(ScriptingBackend.IL2CPP, player.Backend);
        Assert.True(player.Development);

        Assert.Equal(HostOs.MacOS, BeeDag.CellOf(["UNITY_EDITOR", "UNITY_EDITOR_OSX", "UNITY_STANDALONE_OSX"], Editor).Value!.EditorOs);
        Assert.Equal(HostOs.Linux, BeeDag.CellOf(["UNITY_EDITOR", "UNITY_EDITOR_LINUX", "UNITY_STANDALONE_LINUX"], Editor).Value!.EditorOs);
        Assert.Equal(BuildPlatform.iOS, BeeDag.CellOf(["UNITY_IOS"], Editor).Value!.Platform);
        Assert.Equal(BuildPlatform.WebGL, BeeDag.CellOf(["UNITY_WEBGL"], Editor).Value!.Platform);
        Assert.Null(BeeDag.CellOf(["UNITY_WEBGL"], Editor).Value!.Backend);
        Assert.True(BeeDag.CellOf(["UNITY_WEBGL", "UNITY_INCLUDE_TESTS"], Editor).Value!.IncludeTests);
    }

    [Fact]
    public void Dag_version_follows_its_defines_when_they_name_another_patch()
    {
        Assert.Equal(new UnityVersion(6000, 3, 2, string.Empty), BeeDag.CellOf(["UNITY_STANDALONE_WIN", "UNITY_6000_3_2", "UNITY_6000_3"], Editor).Value!.UnityVersion);
        Assert.Equal(Editor, BeeDag.CellOf(["UNITY_STANDALONE_WIN", "UNITY_6000_3_19"], Editor).Value!.UnityVersion);
    }

    [Fact]
    public void Dag_without_a_supported_platform_is_a_failure()
    {
        var r = BeeDag.CellOf(["UNITY_EDITOR", "UNITY_PS5"], Editor);
        Assert.False(r.Ok);
        Assert.Contains("platform", r.Error, StringComparison.Ordinal);
    }

    private static ReferenceEntry Dll(string file, string version, string location) => new(null, file, version, location);

    [Fact]
    public void Equal_command_lines_have_no_differences()
    {
        var c = new CommandLine
        {
            Sources = ["Assets/A.cs"],
            References = [ReferenceEntry.ForAssembly("Core"), Dll("Lib.dll", "1.0.0.0", "Assets/Plugins/Lib.dll")],
            Defines = ["X"],
            NoWarn = ["0169"],
            Analyzers = ["Assets/Gen.dll"],
            AdditionalFiles = ["Assets/a.txt"],
            LangVersion = "9.0",
        };
        Assert.Empty(BeeDiff.Compare(c, c with { NoWarn = ["CS0169"], Analyzers = ["assets/gen.dll"] }));
    }

    [Fact]
    public void Every_category_is_reported_in_a_stable_order()
    {
        var bee = new CommandLine
        {
            Sources = ["Assets/A.cs", "Assets/B.cs"],
            References = [ReferenceEntry.ForAssembly("Core"), Dll("netstandard.dll", "2.1.0.0", "editor:NetStandard/ref/2.1.0/netstandard.dll")],
            Defines = ["X", "Y"],
            NoWarn = ["0282"],
            Analyzers = ["editor:Tools/Unity.SourceGenerators/Unity.SourceGenerators.dll"],
            AdditionalFiles = ["Assets/a.txt"],
            LangVersion = "9.0",
        };
        var ucl = new CommandLine
        {
            Sources = ["Assets/A.cs", "Assets/C.cs"],
            References = [ReferenceEntry.ForAssembly("Other"), Dll("mscorlib.dll", "4.0.0.0", "editor:UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll")],
            Defines = ["X"],
            LangVersion = "10.0",
            Unsafe = true,
        };
        var lines = BeeDiff.Compare(bee, ucl).Select(d => $"{d.Category} {d.Change} {d.Value} {d.Expected} {d.Actual}".TrimEnd()).ToList();
        Assert.Equal(
            [
                "Sources Missing Assets/B.cs",
                "Sources Extra Assets/C.cs",
                "References Missing assembly Core",
                "References Missing netstandard.dll 2.1.0.0 (editor:NetStandard/ref/2.1.0/netstandard.dll)",
                "References Extra assembly Other",
                "References Extra mscorlib.dll 4.0.0.0 (editor:UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll)",
                "Defines Missing Y",
                "Options Changed langversion 9.0 10.0",
                "Options Changed unsafe off on",
                "NoWarn Missing CS0282",
                "Analyzers Missing editor:Tools/Unity.SourceGenerators/Unity.SourceGenerators.dll",
                "AdditionalFiles Missing Assets/a.txt",
            ],
            lines);
    }

    [Fact]
    public void References_match_by_identity_then_location()
    {
        var bee = new CommandLine
        {
            References =
            [
                Dll("System.Memory.dll", "4.0.1.2", "Assets/Plugins/System.Memory.dll"),
                Dll("Lib.dll", "1.0.0.0", "Assets/Plugins/Lib.dll"),
                Dll("Same.dll", string.Empty, "Assets/Same.dll"),
                Dll("Dup.dll", "1.0.0.0", "Assets/A/Dup.dll"),
                Dll("Dup.dll", "1.0.0.0", "Assets/B/Dup.dll"),
            ],
        };
        var ucl = new CommandLine
        {
            References =
            [
                Dll("System.Memory.dll", "4.0.1.2", "Packages/com.x/System.Memory.dll"),
                Dll("Lib.dll", "2.0.0.0", "Assets/Plugins/Lib.dll"),
                Dll("Same.dll", "3.0.0.0", "Assets/Same.dll"),
                Dll("Dup.dll", "1.0.0.0", "Assets/B/Dup.dll"),
                Dll("Dup.dll", "1.0.0.0", "Assets/A/Dup.dll"),
            ],
        };
        var d = BeeDiff.Compare(bee, ucl);
        Assert.Equal(2, d.Count);
        Assert.Contains(new BeeDifference(BeeCategory.References, BeeChange.Changed, "Lib.dll", "1.0.0.0 (Assets/Plugins/Lib.dll)", "2.0.0.0 (Assets/Plugins/Lib.dll)"), d);
        Assert.Contains(new BeeDifference(BeeCategory.References, BeeChange.Changed, "System.Memory.dll", "Assets/Plugins/System.Memory.dll", "Packages/com.x/System.Memory.dll"), d);
        Assert.Equal("Lib.dll 1.0.0.0 (Assets/Plugins/Lib.dll)", bee.References[1].Display);
        Assert.Equal("Same.dll (Assets/Same.dll)", bee.References[2].Display);
    }
}
