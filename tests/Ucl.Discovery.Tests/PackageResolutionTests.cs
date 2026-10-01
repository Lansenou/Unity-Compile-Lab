using Ucl.Core.Graph;
using Ucl.Core.Model;

namespace Ucl.Discovery.Tests;

public sealed class PackageResolutionTests : IDisposable
{
    private readonly TempTree tree = ProjectLoaderTests.MakeProject(new TempTree());

    public void Dispose() => tree.Dispose();

    private static string PackageJson(string name, string version, string deps = "") =>
        $"{{ \"name\": \"{name}\", \"version\": \"{version}\", \"dependencies\": {{ {deps} }} }}";

    private TempTree Manifest(string deps, string extra = "") =>
        tree.Write("Project/Packages/manifest.json", $"{{ \"dependencies\": {{ {deps} }} {extra} }}");

    private ProjectContext Load(FakeEnvironment? env = null) => ProjectLoaderTests.Load(tree, env: env);

    private static ResolvedPackage Package(ProjectContext ctx, string name) => Assert.Single(ctx.Inventory!.Packages, p => p.Name == name);

    [Fact]
    public void EmbeddedPackageUsesPackageNameNotFolderName()
    {
        Manifest("\"com.foo.embedded\": \"1.0.0\"")
            .Write("Project/Packages/FooFolder/package.json", PackageJson("com.foo.embedded", "1.2.3"))
            .Write("Project/Packages/FooFolder/Runtime/A.cs");
        var ctx = Load();
        Assert.Empty(ctx.Problems);
        Assert.Equal(new ResolvedPackage("com.foo.embedded", "1.2.3", "Packages/com.foo.embedded", "embedded"), Package(ctx, "com.foo.embedded"));
        Assert.Equal(["Packages/com.foo.embedded/Runtime/A.cs"], ctx.Inventory!.Scripts);
        Assert.Equal(tree["Project/Packages/FooFolder"], ctx.PackageRoots["Packages/com.foo.embedded"]);
        Assert.Equal(tree["Project/Packages/FooFolder/Runtime/A.cs"], ctx.ToPhysical("Packages/com.foo.embedded/Runtime/A.cs"));
        Assert.Equal("Packages/com.foo.embedded/Runtime/A.cs", ctx.ToLogical(tree["Project/Packages/FooFolder/Runtime/A.cs"]));
    }

    [Fact]
    public void EmbeddedPackageNotInManifestIsIncluded()
    {
        Manifest(string.Empty).Write("Project/Packages/com.extra/package.json", PackageJson("com.extra", "0.1.0"));
        Assert.Equal("embedded", Package(Load(), "com.extra").Source);
    }

    [Fact]
    public void EmbeddedPackageWithBadJsonIsUcl3008()
    {
        Manifest(string.Empty).Write("Project/Packages/broken/package.json", "{");
        var problem = Assert.Single(Load().Problems);
        Assert.Equal(ProblemIds.BadProjectFile, problem.Id);
        Assert.Equal("Packages/broken/package.json", problem.File);
    }

    [Fact]
    public void LocalPackagesResolveRelativeAbsoluteAndUriForms()
    {
        var absolute = tree["Elsewhere/Abs Pkg"].Replace('\\', '/');
        var uri = "file://" + (absolute.StartsWith('/') ? string.Empty : "/") + tree["Elsewhere/Uri Pkg"].Replace('\\', '/').Replace(" ", "%20", StringComparison.Ordinal);
        Manifest($"\"com.local.rel\": \"file:../../Shared/Rel\", \"com.local.abs\": \"file:{absolute}\", \"com.local.uri\": \"{uri}\"")
            .Write("Shared/Rel/package.json", PackageJson("com.local.rel", "2.0.0"))
            .Write("Shared/Rel/Editor/E.cs")
            .Write("Elsewhere/Abs Pkg/package.json", PackageJson("com.local.abs", "3.0.0"))
            .Write("Elsewhere/Abs Pkg/X.cs")
            .Write("Elsewhere/Uri Pkg/package.json", PackageJson("com.local.uri", "4.0.0"));
        var ctx = Load();
        Assert.Empty(ctx.Problems);
        Assert.Equal(new ResolvedPackage("com.local.rel", "2.0.0", "Packages/com.local.rel", "local"), Package(ctx, "com.local.rel"));
        Assert.Equal("local", Package(ctx, "com.local.abs").Source);
        Assert.Equal("local", Package(ctx, "com.local.uri").Source);
        Assert.Equal(tree["Elsewhere/Abs Pkg"], ctx.PackageRoots["Packages/com.local.abs"]);
        Assert.Equal(tree["Elsewhere/Uri Pkg"], ctx.PackageRoots["Packages/com.local.uri"]);
        Assert.Contains("Packages/com.local.rel/Editor/E.cs", ctx.Inventory!.Scripts);
        Assert.Contains("Packages/com.local.abs/X.cs", ctx.Inventory.Scripts);
    }

    [Fact]
    public void PackageCacheFolderWithHashMapsToPackageName()
    {
        Manifest("\"com.unity.inputsystem\": \"1.11.2\"")
            .Write("Project/Library/PackageCache/com.unity.inputsystem@7fe2b9d4e3/package.json", PackageJson("com.unity.inputsystem", "1.11.2"))
            .Write("Project/Library/PackageCache/com.unity.inputsystem@7fe2b9d4e3/InputSystem/Input.cs")
            .Write("Project/Library/PackageCache/com.unity.inputsystem@7fe2b9d4e3/Samples~/S.cs");
        var ctx = Load();
        Assert.Empty(ctx.Problems);
        Assert.Equal(new ResolvedPackage("com.unity.inputsystem", "1.11.2", "Packages/com.unity.inputsystem", "cache"), Package(ctx, "com.unity.inputsystem"));
        Assert.Equal(["Packages/com.unity.inputsystem/InputSystem/Input.cs"], ctx.Inventory!.Scripts);
    }

    [Fact]
    public void PackageCachePrefersLockVersionThenOrdinalLast()
    {
        Manifest("\"com.a\": \"1.0.0\", \"com.b\": \"1.0.0\"")
            .Write("Project/Packages/packages-lock.json",
                "{ \"dependencies\": { \"com.a\": { \"version\": \"1.0.0\", \"source\": \"registry\" }, \"com.b\": { \"version\": \"9.9.9\", \"source\": \"registry\" } } }")
            .Write("Project/Library/PackageCache/com.a@zzz/package.json", PackageJson("com.a", "2.0.0"))
            .Write("Project/Library/PackageCache/com.a@aaa/package.json", PackageJson("com.a", "1.0.0"))
            .Write("Project/Library/PackageCache/com.ab@ccc/package.json", PackageJson("com.ab", "1.0.0"))
            .Write("Project/Library/PackageCache/com.b@111/package.json", PackageJson("com.b", "1.0.0"))
            .Write("Project/Library/PackageCache/com.b@222/package.json", PackageJson("com.b", "1.0.1"));
        var ctx = Load();
        Assert.Empty(ctx.Problems);
        Assert.Equal(tree["Project/Library/PackageCache/com.a@aaa"], ctx.PackageRoots["Packages/com.a"]);
        Assert.Equal(tree["Project/Library/PackageCache/com.b@222"], ctx.PackageRoots["Packages/com.b"]);
        Assert.Equal("1.0.1", Package(ctx, "com.b").Version);
    }

    [Fact]
    public void ScopedRegistryPackageIsFoundInPackageCache()
    {
        Manifest("\"com.company.tools\": \"4.2.0\"",
                ", \"scopedRegistries\": [ { \"name\": \"Company\", \"url\": \"https://npm.company.example\", \"scopes\": [ \"com.company\" ] } ]")
            .Write("Project/Library/PackageCache/com.company.tools@4.2.0/package.json", PackageJson("com.company.tools", "4.2.0"))
            .Write("Project/Library/PackageCache/com.company.tools@4.2.0/Runtime/T.cs");
        var ctx = Load();
        Assert.Empty(ctx.Problems);
        Assert.Equal("cache", Package(ctx, "com.company.tools").Source);
        Assert.Equal(["Packages/com.company.tools/Runtime/T.cs"], ctx.Inventory!.Scripts);
    }

    [Fact]
    public void DownloadCacheFromVariableAndFromHome()
    {
        Manifest("\"com.dl.one\": \"1.0.0\", \"com.dl.two\": \"2.0.0\"")
            .Write("custom cache/com.dl.one@1.0.0/package.json", PackageJson("com.dl.one", "1.0.0"))
            .Write("custom cache/com.dl.one@1.0.0/One.cs")
            .Write("home/.cache/ucl/packages/com.dl.two@2.0.0/package.json", PackageJson("com.dl.two", "2.0.0"));

        var withVariable = Load(new FakeEnvironment(tree["home"]).With("UCL_PACKAGE_CACHE", tree["custom cache"]));
        Assert.Equal("download", Package(withVariable, "com.dl.one").Source);
        Assert.Contains("Packages/com.dl.one/One.cs", withVariable.Inventory!.Scripts);
        Assert.Contains("com.dl.two@2.0.0", Assert.Single(withVariable.Problems).Message);

        var withHome = Load();
        Assert.Equal(new ResolvedPackage("com.dl.two", "2.0.0", "Packages/com.dl.two", "download"), Package(withHome, "com.dl.two"));
        Assert.DoesNotContain(withHome.Inventory!.Packages, p => p.Name == "com.dl.one");
    }

    [Fact]
    public void MissingPackageIsUcl3006NamingPlacesSearched()
    {
        Manifest("\"com.missing\": \"1.2.3\"");
        var ctx = Load();
        var problem = Assert.Single(ctx.Problems);
        Assert.Equal(ProblemIds.UnresolvedPackage, problem.Id);
        Assert.Contains("com.missing@1.2.3", problem.Message);
        Assert.Contains("embedded", problem.Message);
        Assert.Contains("file:", problem.Message);
        Assert.Contains("PackageCache", problem.Message);
        Assert.Contains("~/.cache/ucl/packages", problem.Message);
        Assert.DoesNotContain(tree["home"], problem.Message);
        Assert.Contains("ucl fetch", problem.Message);
        Assert.Empty(ctx.Inventory!.Packages);
    }

    [Fact]
    public void BuiltInModulesNeedNoFolder()
    {
        Manifest("\"com.unity.modules.physics\": \"1.0.0\", \"com.unity.modules.ui\": \"1.0.0\"")
            .Write("Project/Packages/packages-lock.json",
                "{ \"dependencies\": { \"com.unity.modules.physics\": { \"version\": \"1.0.0\", \"source\": \"builtin\", \"dependencies\": {} }, "
                + "\"com.unity.modules.ui\": { \"version\": \"1.0.1\", \"source\": \"builtin\" } } }");
        var ctx = Load();
        Assert.Empty(ctx.Problems);
        Assert.Equal(
            [new ResolvedPackage("com.unity.modules.physics", "1.0.0", null, "builtin"), new ResolvedPackage("com.unity.modules.ui", "1.0.1", null, "builtin")],
            ctx.Inventory!.Packages);
        Assert.All(ctx.Inventory.Packages, p => Assert.True(p.IsBuiltInModule));
        Assert.Empty(ctx.PackageRoots);
    }

    [Fact]
    public void WithoutLockFileDependenciesAreFollowedTransitively()
    {
        Manifest("\"com.top\": \"1.0.0\"")
            .Write("Project/Library/PackageCache/com.top@h1/package.json",
                PackageJson("com.top", "1.0.0", "\"com.mid\": \"2.0.0\", \"com.unity.modules.audio\": \"1.0.0\""))
            .Write("Project/Library/PackageCache/com.mid@h2/package.json", PackageJson("com.mid", "2.0.0", "\"com.leaf\": \"3.0.0\""))
            .Write("home/.cache/ucl/packages/com.leaf@3.0.0/package.json", PackageJson("com.leaf", "3.0.0"));
        var ctx = Load();
        Assert.Empty(ctx.Problems);
        Assert.Equal(
            [("com.leaf", "download"), ("com.mid", "cache"), ("com.top", "cache"), ("com.unity.modules.audio", "builtin")],
            ctx.Inventory!.Packages.Select(p => (p.Name, p.Source)));
    }

    [Fact]
    public void WithoutLockFileMissingTransitiveDependencyIsUcl3006()
    {
        Manifest("\"com.top\": \"1.0.0\"")
            .Write("Project/Library/PackageCache/com.top@h1/package.json", PackageJson("com.top", "1.0.0", "\"com.gone\": \"5.0.0\""));
        var problem = Assert.Single(Load().Problems);
        Assert.Equal(ProblemIds.UnresolvedPackage, problem.Id);
        Assert.Contains("com.gone@5.0.0", problem.Message);
    }

    [Fact]
    public void WithLockFileOnlyLockedPackagesAreResolved()
    {
        Manifest("\"com.top\": \"1.0.0\"")
            .Write("Project/Packages/packages-lock.json",
                "{ \"dependencies\": { \"com.top\": { \"version\": \"1.0.0\", \"source\": \"registry\", \"dependencies\": { \"com.mid\": \"2.0.0\" } }, "
                + "\"com.mid\": { \"version\": \"2.0.0\", \"source\": \"registry\" } } }")
            .Write("Project/Library/PackageCache/com.top@h1/package.json",
                PackageJson("com.top", "1.0.0", "\"com.mid\": \"2.0.0\", \"com.not.locked\": \"1.0.0\""))
            .Write("Project/Library/PackageCache/com.mid@h2/package.json", PackageJson("com.mid", "2.0.0"));
        var ctx = Load();
        Assert.Empty(ctx.Problems);
        Assert.Equal(["com.mid", "com.top"], ctx.Inventory!.Packages.Select(p => p.Name));
    }

    [Fact]
    public void RootLevelLibraryIsNeverScannedAsProjectFiles()
    {
        Manifest("\"com.self\": \"file:..\"")
            .Write("Project/package.json", PackageJson("com.self", "1.0.0"))
            .Write("Project/Library/Junk.cs")
            .Write("Project/Temp/Junk.cs")
            .Write("Project/obj/Junk.cs")
            .Write("Project/Assets/A.cs");
        var scripts = Load().Inventory!.Scripts;
        Assert.DoesNotContain(scripts, s => s.Contains("Junk", StringComparison.Ordinal));
        Assert.Contains("Packages/com.self/Assets/A.cs", scripts);
    }
}
