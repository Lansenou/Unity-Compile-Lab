using Ucl.Core.Model;
using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

/// <summary>Scripting backend resolution (docs/defines.md, "Scripting backend").</summary>
public class BackendTests
{
    [Fact]
    public void D31_D32_backend_resolution_order()
    {
        var monoEverywhere = new ProjectSettingsData
        {
            ScriptingBackend = new Dictionary<string, ScriptingBackend> { ["iOS"] = ScriptingBackend.Mono, ["WebGL"] = ScriptingBackend.Mono, ["Standalone"] = ScriptingBackend.IL2CPP },
        };
        Assert.Equal(ScriptingBackend.IL2CPP, DefineTable.BackendOf(Cells.Player(BuildPlatform.iOS), monoEverywhere));
        Assert.Equal(ScriptingBackend.IL2CPP, DefineTable.BackendOf(Cells.Player(BuildPlatform.WebGL), monoEverywhere));
        Assert.Equal(ScriptingBackend.IL2CPP, DefineTable.BackendOf(Cells.Player(BuildPlatform.StandaloneOSX), monoEverywhere));
        Assert.Equal(ScriptingBackend.Mono, DefineTable.BackendOf(Cells.Player(BuildPlatform.StandaloneOSX, backend: ScriptingBackend.Mono), monoEverywhere));
        Assert.Equal(ScriptingBackend.IL2CPP, DefineTable.BackendOf(Cells.Player(BuildPlatform.Android), ProjectSettingsData.Default));
        Assert.Equal(ScriptingBackend.Mono, DefineTable.BackendOf(Cells.Editor(BuildPlatform.StandaloneWindows64), ProjectSettingsData.Default));
    }
}
