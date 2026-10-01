using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

public class BuiltInModulesTests
{
    [Theory]
    [InlineData("UnityEngine.PhysicsModule.dll", "physics")]
    [InlineData("UnityEngine.Physics2DModule.dll", "physics2d")]
    [InlineData("UnityEngine.CoreModule.dll", "core")]
    [InlineData("UnityEngine.UnityWebRequestWWWModule.dll", "unitywebrequestwww")]
    [InlineData("UnityEngine.dll", null)]
    [InlineData("UnityEngine.Module.dll", null)]
    [InlineData("UnityEditor.CoreModule.dll", null)]
    [InlineData("UnityEngine.PhysicsModule.xml", null)]
    [InlineData("Newtonsoft.Json.dll", null)]
    public void ModuleKey(string fileName, string? expected) => Assert.Equal(expected, BuiltInModules.ModuleKey(fileName));

    [Fact]
    public void Known_packages_include_common_modules_and_are_lowercase()
    {
        Assert.Contains("physics", BuiltInModules.KnownPackages);
        Assert.Contains("ui", BuiltInModules.KnownPackages);
        Assert.DoesNotContain("core", BuiltInModules.KnownPackages);
        Assert.All(BuiltInModules.KnownPackages, k => Assert.Equal(k.ToLowerInvariant(), k));
        Assert.Equal(BuiltInModules.KnownPackages.Count, BuiltInModules.KnownPackages.Distinct().Count());
    }

    [Fact]
    public void Module_with_package_is_referenced_only_when_enabled()
    {
        string[] packages = ["physics", "audio"];
        Assert.True(BuiltInModules.IsReferenced("UnityEngine.PhysicsModule.dll", ["physics"], packages));
        Assert.False(BuiltInModules.IsReferenced("UnityEngine.AudioModule.dll", ["physics"], packages));
    }

    [Fact]
    public void Module_without_package_and_non_module_dlls_are_always_referenced()
    {
        Assert.True(BuiltInModules.IsReferenced("UnityEngine.CoreModule.dll", [], BuiltInModules.KnownPackages.ToList()));
        Assert.True(BuiltInModules.IsReferenced("UnityEngine.SharedInternalsModule.dll", [], BuiltInModules.KnownPackages.ToList()));
        Assert.True(BuiltInModules.IsReferenced("UnityEngine.dll", [], ["physics"]));
        Assert.True(BuiltInModules.IsReferenced("UnityEditor.dll", [], ["physics"]));
    }
}
