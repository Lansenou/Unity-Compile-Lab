using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Discovery.Tests;

public sealed class PackageFetcherTests : IDisposable
{
    private const string Registry = "https://reg.example.com";

    private readonly TempTree tree = ProjectLoaderTests.MakeProject(new TempTree());
    private readonly FakeHttpClient http = new();
    private readonly FakeEnvironment env;

    public PackageFetcherTests() => env = new FakeEnvironment(tree["home"]).With(DownloadCache.Variable, tree["cache"]);

    public void Dispose() => tree.Dispose();

    private void Manifest(string deps, bool scoped = true) =>
        tree.Write("Project/Packages/manifest.json", $"{{ \"dependencies\": {{ {deps} }}"
            + (scoped ? $", \"scopedRegistries\": [ {{ \"name\": \"Example\", \"url\": \"{Registry}/\", \"scopes\": [ \"com.example\" ] }} ]" : string.Empty)
            + " }");

    private PackageFetchReport Fetch() =>
        new PackageFetcher(new PhysicalFileSystem([tree["cache"]]), http, env).FetchAsync(tree["Project"]).GetAwaiter().GetResult();

    private static PackageFetchOutcome Outcome(PackageFetchReport r, string name) => Assert.Single(r.Outcomes, o => o.Name == name);

    [Fact]
    public void FetchesFromScopedRegistryStripsPackageFolderAndResolvesAfterwards()
    {
        Manifest("\"com.example.a\": \"1.2.3\"");
        http.Publish(Registry, "com.example.a", "1.2.3", Tgz.Build(
            ("package/package.json", Tgz.PackageJson("com.example.a", "1.2.3")),
            ("package/Runtime/A.cs", "class A {}")));

        var report = Fetch();

        Assert.Empty(report.Problems);
        Assert.Equal(new PackageFetchOutcome("com.example.a", "1.2.3", PackageFetchStatus.Fetched, Registry), Outcome(report, "com.example.a"));
        Assert.Equal("class A {}", File.ReadAllText(tree["cache/com.example.a@1.2.3/Runtime/A.cs"]));
        var ctx = ProjectLoaderTests.Load(tree, env: env);
        Assert.Empty(ctx.Problems);
        Assert.Equal("download", Assert.Single(ctx.Inventory!.Packages).Source);

        // A second run finds everything locally and does not touch the network.
        http.Requests.Clear();
        Assert.Equal(PackageFetchStatus.Cached, Outcome(Fetch(), "com.example.a").Status);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public void WithoutLockFileDependenciesAreFetchedTransitivelyAndBuiltinsSkipped()
    {
        Manifest("\"com.example.a\": \"1.0.0\"");
        http.Publish(Registry, "com.example.a", "1.0.0", Tgz.Build(
            ("package/package.json", Tgz.PackageJson("com.example.a", "1.0.0", "\"com.example.b\": \"2.0.0\", \"com.unity.modules.physics\": \"1.0.0\""))));
        http.Publish(Registry, "com.example.b", "2.0.0", Tgz.Build(("package/package.json", Tgz.PackageJson("com.example.b", "2.0.0"))));

        var report = Fetch();

        Assert.Empty(report.Problems);
        Assert.Equal(["com.example.a", "com.example.b"], report.Outcomes.Select(o => o.Name));
        Assert.All(report.Outcomes, o => Assert.Equal(PackageFetchStatus.Fetched, o.Status));
        Assert.DoesNotContain(http.Requests, r => r.Contains("com.unity.modules", StringComparison.Ordinal));
    }

    [Fact]
    public void LockFileUrlIsTheRegistryWhenNoScopeMatches()
    {
        Manifest("\"com.other.c\": \"0.1.0\"", scoped: false);
        tree.Write("Project/Packages/packages-lock.json",
            "{ \"dependencies\": { \"com.other.c\": { \"version\": \"0.1.0\", \"source\": \"registry\", \"dependencies\": {}, \"url\": \"https://lock.example.com\" } } }");
        http.Publish("https://lock.example.com", "com.other.c", "0.1.0", Tgz.Build(("package/package.json", Tgz.PackageJson("com.other.c", "0.1.0"))));

        var report = Fetch();

        Assert.Empty(report.Problems);
        Assert.Equal("https://lock.example.com", Outcome(report, "com.other.c").Detail);
    }

    [Fact]
    public void RegistryChoiceFollowsScopesThenLockThenUnity()
    {
        ScopedRegistry[] scoped = [new("Broad", "https://broad/", ["com.example"]), new("Narrow", "https://narrow", ["com.example.special"])];
        Assert.Equal("https://narrow", PackageFetcher.RegistryFor("com.example.special.x", "https://lock", scoped));
        Assert.Equal("https://broad", PackageFetcher.RegistryFor("com.example.x", "https://lock", scoped));
        Assert.Equal("https://lock", PackageFetcher.RegistryFor("com.examples", "https://lock", scoped));
        Assert.Equal(PackageFetcher.DefaultRegistry, PackageFetcher.RegistryFor("com.unity.x", string.Empty, scoped));
    }

    [Fact]
    public void ShaMismatchFailsAndWritesNothing()
    {
        Manifest("\"com.example.a\": \"1.0.0\"");
        http.Publish(Registry, "com.example.a", "1.0.0", Tgz.Build(("package/package.json", Tgz.PackageJson("com.example.a", "1.0.0"))), shasum: new string('0', 40));

        var report = Fetch();

        var outcome = Outcome(report, "com.example.a");
        Assert.Equal(PackageFetchStatus.Failed, outcome.Status);
        Assert.Contains("SHA-1", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal(ProblemIds.UnresolvedPackage, Assert.Single(report.Problems).Id);
        Assert.False(Directory.Exists(tree["cache/com.example.a@1.0.0"]));
    }

    [Theory]
    [InlineData("package/../../evil.txt")]
    [InlineData("/etc/evil.txt")]
    [InlineData("package/..\\evil.txt")]
    public void TarballEntriesEscapingThePackageFolderAreRejected(string entry)
    {
        Manifest("\"com.example.a\": \"1.0.0\"");
        http.Publish(Registry, "com.example.a", "1.0.0", Tgz.Build(("package/package.json", Tgz.PackageJson("com.example.a", "1.0.0")), (entry, "x")));

        var outcome = Outcome(Fetch(), "com.example.a");

        Assert.Equal(PackageFetchStatus.Failed, outcome.Status);
        Assert.Contains("escapes", outcome.Detail, StringComparison.Ordinal);
        Assert.False(File.Exists(tree["evil.txt"]));
        Assert.False(Directory.Exists(tree["cache/com.example.a@1.0.0"]));
    }

    [Fact]
    public void GitPackagesAreNotFetchable()
    {
        Manifest("\"com.example.git\": \"https://github.com/x/y.git#v1\"");

        var report = Fetch();

        var outcome = Outcome(report, "com.example.git");
        Assert.Equal(PackageFetchStatus.Failed, outcome.Status);
        Assert.Contains("not fetchable", outcome.Detail, StringComparison.Ordinal);
        Assert.Empty(http.Requests);
        Assert.Equal(ProblemIds.UnresolvedPackage, Assert.Single(report.Problems).Id);
    }

    [Fact]
    public void NetworkErrorsAndMissingVersionsAreFailures()
    {
        Manifest("\"com.example.gone\": \"1.0.0\", \"com.example.old\": \"9.9.9\"");
        http.Publish(Registry, "com.example.old", "1.0.0", Tgz.Build(("package/package.json", Tgz.PackageJson("com.example.old", "1.0.0"))));

        var report = Fetch();

        Assert.Contains("404", Outcome(report, "com.example.gone").Detail, StringComparison.Ordinal);
        Assert.Contains("no version 9.9.9", Outcome(report, "com.example.old").Detail, StringComparison.Ordinal);
        Assert.Equal(2, report.Problems.Count);
    }

    [Fact]
    public void LocallyResolvedPackagesAreReportedAsCached()
    {
        Manifest("\"com.example.emb\": \"1.0.0\", \"com.unity.modules.ui\": \"1.0.0\"");
        tree.Write("Project/Packages/Emb/package.json", Tgz.PackageJson("com.example.emb", "1.0.0"));

        var report = Fetch();

        Assert.Empty(report.Problems);
        Assert.Equal(new PackageFetchOutcome("com.example.emb", "1.0.0", PackageFetchStatus.Cached, "embedded"), Assert.Single(report.Outcomes));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public void InterruptedFetchLeftoversAreReplaced()
    {
        Manifest("\"com.example.a\": \"1.0.0\"");
        tree.Write("cache/com.example.a@1.0.0/Stale.cs", "stale");
        http.Publish(Registry, "com.example.a", "1.0.0", Tgz.Build(("package/package.json", Tgz.PackageJson("com.example.a", "1.0.0"))));

        Assert.Empty(Fetch().Problems);
        Assert.False(File.Exists(tree["cache/com.example.a@1.0.0/Stale.cs"]));
    }
}
