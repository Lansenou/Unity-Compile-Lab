using Ucl.Core.Graph;
using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

public class PackageManifestParserTests
{
    private const string Manifest = """
        {
          "scopedRegistries": [
            {
              "name": "OpenUPM",
              "url": "https://package.openupm.com",
              "scopes": [ "com.cysharp", "jp.hadashikick" ]
            },
            "not an object"
          ],
          "dependencies": {
            "com.unity.test-framework": "1.4.5",
            "com.unity.modules.physics": "1.0.0",
            "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "com.local.thing": "file:../LocalPackages/thing",
            "com.weird": 5,
          },
          // comments are tolerated
          "testables": [ "com.local.thing", 3 ]
        }
        """;

    private const string Lock = """
        {
          "dependencies": {
            "com.unity.test-framework": {
              "version": "1.4.5",
              "depth": 0,
              "source": "registry",
              "dependencies": {
                "com.unity.modules.jsonserialize": "1.0.0",
                "com.unity.ext.nunit": "2.0.3"
              },
              "url": "https://packages.unity.com"
            },
            "com.unity.ext.nunit": {
              "version": "2.0.3",
              "depth": 1,
              "source": "registry",
              "dependencies": {},
              "url": "https://packages.unity.com"
            },
            "com.unity.modules.physics": {
              "version": "1.0.0",
              "depth": 0,
              "source": "builtin",
              "dependencies": {}
            },
            "com.cysharp.unitask": {
              "version": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
              "depth": 0,
              "source": "git",
              "dependencies": {},
              "hash": "abc"
            },
            "broken": 1
          }
        }
        """;

    [Fact]
    public void Parses_manifest_with_lock()
    {
        var r = PackageManifestParser.Parse(Manifest, Lock);
        Assert.True(r.Ok, r.Error);
        var m = r.Value!;
        Assert.Equal(
            ["com.cysharp.unitask", "com.local.thing", "com.unity.ext.nunit", "com.unity.modules.physics", "com.unity.test-framework", "com.weird"],
            m.Packages.Select(p => p.Name));

        var tf = m.Packages.Single(p => p.Name == "com.unity.test-framework");
        Assert.Equal("1.4.5", tf.Requested);
        Assert.Equal("1.4.5", tf.Version);
        Assert.Equal("registry", tf.Source);
        Assert.Equal("https://packages.unity.com", tf.Url);
        Assert.Equal(["com.unity.ext.nunit", "com.unity.modules.jsonserialize"], tf.Dependencies);
        Assert.False(tf.IsBuiltInModule);

        var nunit = m.Packages.Single(p => p.Name == "com.unity.ext.nunit");
        Assert.Equal(string.Empty, nunit.Requested);
        Assert.Equal("2.0.3", nunit.Version);

        var physics = m.Packages.Single(p => p.Name == "com.unity.modules.physics");
        Assert.True(physics.IsBuiltInModule);
        Assert.Equal("builtin", physics.Source);
        Assert.Equal(string.Empty, physics.Url);

        var local = m.Packages.Single(p => p.Name == "com.local.thing");
        Assert.Equal("file:../LocalPackages/thing", local.Version);
        Assert.Equal(string.Empty, local.Source);
        Assert.Empty(local.Dependencies);

        Assert.Equal(string.Empty, m.Packages.Single(p => p.Name == "com.weird").Requested);
        Assert.Equal("git", m.Packages.Single(p => p.Name == "com.cysharp.unitask").Source);

        var reg = Assert.Single(m.ScopedRegistries);
        Assert.Equal("OpenUPM", reg.Name);
        Assert.Equal("https://package.openupm.com", reg.Url);
        Assert.Equal(["com.cysharp", "jp.hadashikick"], reg.Scopes);
        Assert.Equal(["com.local.thing"], m.Testables);
    }

    [Fact]
    public void Parses_manifest_without_lock()
    {
        var m = PackageManifestParser.Parse("{ \"dependencies\": { \"com.b\": \"1.0.0\", \"com.a\": \"2.0.0\" } }", null).Value!;
        Assert.Equal(["com.a", "com.b"], m.Packages.Select(p => p.Name));
        Assert.Equal("2.0.0", m.Packages[0].Version);
        Assert.Empty(m.ScopedRegistries);
        Assert.Empty(m.Testables);
    }

    [Fact]
    public void Tolerates_missing_or_wrongly_typed_sections()
    {
        var m = PackageManifestParser.Parse("{ \"dependencies\": [], \"scopedRegistries\": {}, \"testables\": \"x\" }", "{ \"dependencies\": [] }").Value!;
        Assert.Empty(m.Packages);
        Assert.Empty(m.ScopedRegistries);
        Assert.Empty(m.Testables);
        var e = PackageManifestParser.Parse("{}", "{}").Value!;
        Assert.Empty(e.Packages);
    }

    [Fact]
    public void Lock_entry_without_dependencies_object()
    {
        var m = PackageManifestParser.Parse("{}", "{ \"dependencies\": { \"com.a\": { \"version\": \"1.0.0\", \"dependencies\": [] } } }").Value!;
        var a = Assert.Single(m.Packages);
        Assert.Empty(a.Dependencies);
        Assert.Equal(string.Empty, a.Requested);
    }

    [Theory]
    [InlineData("{ not json", null)]
    [InlineData("{}", "{ not json")]
    public void Malformed_json_is_failure(string manifest, string? lockJson)
    {
        var r = PackageManifestParser.Parse(manifest, lockJson);
        Assert.False(r.Ok);
        Assert.StartsWith("invalid JSON", r.Error);
    }

    [Theory]
    [InlineData("com.cysharp", true)]
    [InlineData("com.cysharp.unitask", true)]
    [InlineData("com.cysharpx", false)]
    [InlineData("com", false)]
    [InlineData("jp.hadashikick.vcontainer", true)]
    public void ScopedRegistry_serves_whole_prefixes(string package, bool expected)
    {
        var reg = new ScopedRegistry("r", "u", ["com.cysharp", "jp.hadashikick"]);
        Assert.Equal(expected, reg.Serves(package));
    }

    [Fact]
    public void ResolvedPackage_builtin_module()
    {
        Assert.True(new ResolvedPackage("com.unity.modules.ui", "1.0.0", null, "builtin").IsBuiltInModule);
        Assert.False(new ResolvedPackage("com.unity.ugui", "2.0.0", "Packages/com.unity.ugui", "cache").IsBuiltInModule);
    }
}
