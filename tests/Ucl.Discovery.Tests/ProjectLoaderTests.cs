using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Discovery.Tests;

public sealed class ProjectLoaderTests : IDisposable
{
    private readonly TempTree tree = new();

    public void Dispose() => tree.Dispose();

    internal static TempTree MakeProject(TempTree t, string project = "Project", string version = "6000.0.30f1")
    {
        t.Dir($"{project}/Assets").Dir($"{project}/Packages");
        t.Write($"{project}/ProjectSettings/ProjectVersion.txt", $"m_EditorVersion: {version}\nm_EditorVersionWithRevision: {version} (abc)\n");
        return t;
    }

    internal static ProjectContext Load(TempTree t, string project = "Project", FakeEnvironment? env = null) =>
        new ProjectLoader(new PhysicalFileSystem(), env ?? new FakeEnvironment(t["home"])).Load(t[project]);

    [Fact]
    public void Managed_DLLs_that_share_a_file_name_carry_their_assembly_versions()
    {
        MakeProject(tree)
            .Write("Project/Assets/A/Same.dll")
            .Write("Project/Assets/B/Same.dll")
            .Write("Project/Assets/C/Unique.dll");
        var managed = File.ReadAllBytes(typeof(ProjectLoader).Assembly.Location);
        foreach (var path in new[] { "Project/Assets/A/Same.dll", "Project/Assets/B/Same.dll", "Project/Assets/C/Unique.dll" })
        {
            File.WriteAllBytes(tree[path], managed);
        }

        var inv = Load(tree).Inventory!;
        var version = typeof(ProjectLoader).Assembly.GetName().Version!.ToString();
        Assert.Equal(["Assets/A/Same.dll", "Assets/B/Same.dll"], inv.PluginVersions.Keys);
        Assert.All(inv.PluginVersions.Values, v => Assert.Equal(version, v));
    }

    [Fact]
    public void FolderWithoutProjectLayoutIsUcl3001()
    {
        tree.Dir("Project/Assets");
        var ctx = Load(tree);
        var problem = Assert.Single(ctx.Problems);
        Assert.Equal(ProblemIds.NotAProject, problem.Id);
        Assert.Contains("Packages/", problem.Message);
        Assert.Contains("ProjectSettings/", problem.Message);
        Assert.Null(ctx.Inventory);
    }

    [Fact]
    public void MissingProjectVersionIsUcl3002()
    {
        tree.Dir("Project/Assets").Dir("Project/Packages").Dir("Project/ProjectSettings");
        var ctx = Load(tree);
        Assert.Equal(ProblemIds.UnsupportedVersion, Assert.Single(ctx.Problems).Id);
        Assert.Null(ctx.Inventory);
    }

    [Fact]
    public void UnparsableProjectVersionIsUcl3002()
    {
        MakeProject(tree).Write("Project/ProjectSettings/ProjectVersion.txt", "m_EditorVersion: banana\n");
        var problem = Assert.Single(Load(tree).Problems);
        Assert.Equal(ProblemIds.UnsupportedVersion, problem.Id);
        Assert.Equal("ProjectSettings/ProjectVersion.txt", problem.File);
    }

    [Fact]
    public void NonUnity6VersionIsNotAProblemHere()
    {
        MakeProject(tree, version: "2022.3.10f1");
        var ctx = Load(tree);
        Assert.Empty(ctx.Problems);
        Assert.Equal(new UnityVersion(2022, 3, 10, "f1"), ctx.Inventory!.ProjectVersion);
    }

    [Fact]
    public void MissingManifestAndSettingsAreEmptyDefaults()
    {
        MakeProject(tree);
        var ctx = Load(tree);
        Assert.Empty(ctx.Problems);
        Assert.Empty(ctx.Inventory!.Packages);
        Assert.Same(ProjectSettingsData.Default, ctx.Inventory.Settings);
    }

    [Fact]
    public void ProjectSettingsAreParsed()
    {
        MakeProject(tree).Write("Project/ProjectSettings/ProjectSettings.asset", "PlayerSettings:\n  allowUnsafeCode: 1\n");
        Assert.True(Load(tree).Inventory!.Settings.AllowUnsafeCode);
    }

    [Fact]
    public void InvalidManifestIsUcl3008()
    {
        MakeProject(tree).Write("Project/Packages/manifest.json", "{ \"dependencies\": ");
        var problem = Assert.Single(Load(tree).Problems);
        Assert.Equal(ProblemIds.BadProjectFile, problem.Id);
        Assert.Equal("Packages/manifest.json", problem.File);
    }

    [Fact]
    public void InvalidLockFileIsUcl3008()
    {
        MakeProject(tree)
            .Write("Project/Packages/manifest.json", "{\"dependencies\":{}}")
            .Write("Project/Packages/packages-lock.json", "not json");
        var problem = Assert.Single(Load(tree).Problems);
        Assert.Equal(ProblemIds.BadProjectFile, problem.Id);
        Assert.Equal("Packages/packages-lock.json", problem.File);
    }

    [Fact]
    public void HiddenEntriesAreSkipped()
    {
        MakeProject(tree)
            .Write("Project/Assets/Kept.cs")
            .Write("Project/Assets/Samples~/Sample.cs")
            .Write("Project/Assets/.git/Git.cs")
            .Write("Project/Assets/cvs/Cvs.cs")
            .Write("Project/Assets/x.tmp/Tmp.cs")
            .Write("Project/Assets/.Hidden.cs")
            .Write("Project/Assets/Backup.cs~")
            .Write("Project/Assets/Plugin.dll.tmp");
        var inv = Load(tree).Inventory!;
        Assert.Equal(["Assets/Kept.cs"], inv.Scripts);
        Assert.Empty(inv.Plugins);
    }

    [Fact]
    public void CollectsEveryFileKindWithMetas()
    {
        MakeProject(tree)
            .Write("Project/.editorconfig", "root = true")
            .Write("Project/team.globalconfig")
            .Write("Project/Assets/csc.rsp", "-nowarn:0168")
            .Write("Project/Assets/Upper.CS")
            .Write("Project/Assets/Game/Game.asmdef", "{\"name\":\"Game\"}")
            .Write("Project/Assets/Game/Game.asmdef.meta", "guid: 123")
            .Write("Project/Assets/Game/.editorconfig")
            .Write("Project/Assets/Game/Ext/Ext.asmref", "{\"reference\":\"Game\"}")
            .Write("Project/Assets/Plugins/Lib.dll", "MZ")
            .Write("Project/Assets/Plugins/Lib.dll.meta", "PluginImporter:")
            .Write("Project/Assets/Plugins/NoMeta.DLL")
            .Write("Project/Assets/Rules.ruleset")
            .Write("Project/Assets/x.globalconfig")
            .Write("Project/Assets/Plugins/x86_64/native.dll", "MZ");
        var managed = File.ReadAllBytes(typeof(ProjectLoader).Assembly.Location);
        File.WriteAllBytes(tree["Project/Assets/Plugins/Lib.dll"], managed);
        File.WriteAllBytes(tree["Project/Assets/Plugins/NoMeta.DLL"], managed);
        var inv = Load(tree).Inventory!;

        Assert.Equal(["Assets/Upper.CS"], inv.Scripts);
        var asmdef = Assert.Single(inv.Asmdefs);
        Assert.Equal(("Assets/Game/Game.asmdef", "{\"name\":\"Game\"}", "guid: 123"), (asmdef.Path, asmdef.Text, asmdef.MetaText));
        var asmref = Assert.Single(inv.Asmrefs);
        Assert.Equal("Assets/Game/Ext/Ext.asmref", asmref.Path);
        Assert.Null(asmref.MetaText);
        Assert.Equal(["Assets/Plugins/Lib.dll", "Assets/Plugins/NoMeta.DLL"], inv.Plugins.Select(p => p.Path));
        Assert.All(inv.Plugins, p => Assert.Equal(string.Empty, p.Text));
        Assert.Equal(["Assets/Plugins/x86_64/native.dll"], inv.NativePlugins);
        Assert.Equal("PluginImporter:", inv.Plugins[0].MetaText);
        Assert.Equal(("Assets/csc.rsp", "-nowarn:0168"), (inv.ResponseFiles[0].Path, inv.ResponseFiles[0].Text));
        Assert.Equal(["Assets/Rules.ruleset"], inv.RuleSets);
        Assert.Equal([".editorconfig", "Assets/Game/.editorconfig", "Assets/x.globalconfig", "team.globalconfig"], inv.AnalyzerConfigs);
    }

    [Fact]
    public void PathsWithSpacesAndNonAsciiStayLogical()
    {
        MakeProject(tree).Write("Project/Assets/My Scripts/Ünïcode/A.cs").Write("Project/Assets/My Scripts/Ünïcode/B.cs");
        var ctx = Load(tree);
        Assert.Equal(["Assets/My Scripts/Ünïcode/A.cs", "Assets/My Scripts/Ünïcode/B.cs"], ctx.Inventory!.Scripts);
        Assert.True(File.Exists(ctx.ToPhysical(ctx.Inventory.Scripts[0])));
    }

    [Fact]
    public void ListsAreSortedOrdinal()
    {
        MakeProject(tree).Write("Project/Assets/b.cs").Write("Project/Assets/C.cs").Write("Project/Assets/a/Z.cs").Write("Project/Assets/A.cs");
        Assert.Equal(["Assets/A.cs", "Assets/C.cs", "Assets/a/Z.cs", "Assets/b.cs"], Load(tree).Inventory!.Scripts);
    }

    [Fact]
    public void Utf8BomInAsmdefIsStripped()
    {
        MakeProject(tree);
        File.WriteAllBytes(tree["Project/Assets/G.asmdef"], [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}']);
        Assert.Equal("{}", Assert.Single(Load(tree).Inventory!.Asmdefs).Text);
    }
}
