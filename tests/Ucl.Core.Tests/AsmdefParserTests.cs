using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

public class AsmdefParserTests
{
    private const string Full = """
        {
            "name": " Game.Runtime ",
            "rootNamespace": "Game",
            "references": [
                "Game.Core",
                "GUID:0123456789abcdef0123456789abcdef",
                "",
                42
            ],
            "includePlatforms": [],
            "excludePlatforms": ["Android", "WebGL"],
            "allowUnsafeCode": true,
            "overrideReferences": true,
            "precompiledReferences": ["Newtonsoft.Json.dll"],
            "autoReferenced": false,
            "defineConstraints": ["UNITY_2021_1_OR_NEWER", "A || B", "!C"],
            "versionDefines": [
                { "name": "com.unity.inputsystem", "expression": "1.4.0", "define": "HAS_INPUT_SYSTEM" },
                { "name": "Unity", "expression": "[6000.0,6000.1)" },
                "not an object"
            ],
            "noEngineReferences": true,
            "optionalUnityReferences": ["TestAssemblies"],
        }
        """;

    [Fact]
    public void Parses_every_field()
    {
        var r = AsmdefParser.ParseAsmdef(Full);
        Assert.True(r.Ok, r.Error);
        var a = r.Value!;
        Assert.Equal("Game.Runtime", a.Name);
        Assert.Equal(["Game.Core", "GUID:0123456789abcdef0123456789abcdef"], a.References);
        Assert.Empty(a.IncludePlatforms);
        Assert.Equal(["Android", "WebGL"], a.ExcludePlatforms);
        Assert.True(a.AllowUnsafeCode);
        Assert.True(a.OverrideReferences);
        // optionalUnityReferences: the legacy test assembly gets nunit.framework.dll and UNITY_INCLUDE_TESTS on load.
        Assert.Equal(["Newtonsoft.Json.dll", "nunit.framework.dll"], a.PrecompiledReferences);
        Assert.False(a.AutoReferenced);
        Assert.Equal(["UNITY_2021_1_OR_NEWER", "A || B", "!C", "UNITY_INCLUDE_TESTS"], a.DefineConstraints);
        Assert.Equal(
            [new VersionDefine("com.unity.inputsystem", "1.4.0", "HAS_INPUT_SYSTEM"), new VersionDefine("Unity", "[6000.0,6000.1)", "")],
            a.VersionDefines);
        Assert.True(a.NoEngineReferences);
        Assert.Equal(["TestAssemblies"], a.OptionalUnityReferences);
    }

    [Fact]
    public void Defaults_for_minimal_asmdef()
    {
        var a = AsmdefParser.ParseAsmdef("﻿{ \"name\": \"Min\" }").Value!;
        Assert.Equal("Min", a.Name);
        Assert.Empty(a.References);
        Assert.Empty(a.IncludePlatforms);
        Assert.Empty(a.ExcludePlatforms);
        Assert.False(a.AllowUnsafeCode);
        Assert.False(a.OverrideReferences);
        Assert.Empty(a.PrecompiledReferences);
        Assert.True(a.AutoReferenced);
        Assert.Empty(a.DefineConstraints);
        Assert.Empty(a.VersionDefines);
        Assert.False(a.NoEngineReferences);
        Assert.Empty(a.OptionalUnityReferences);
    }

    [Fact]
    public void Wrong_types_fall_back_to_defaults()
    {
        var a = AsmdefParser.ParseAsmdef("{ \"name\": \"X\", \"autoReferenced\": \"no\", \"allowUnsafeCode\": 1, \"references\": \"Game\", \"versionDefines\": {} }").Value!;
        Assert.True(a.AutoReferenced);
        Assert.False(a.AllowUnsafeCode);
        Assert.Empty(a.References);
        Assert.Empty(a.VersionDefines);
    }

    [Fact]
    public void Comments_are_tolerated()
    {
        var a = AsmdefParser.ParseAsmdef("// header\n{ /* inline */ \"name\": \"C\" }");
        Assert.True(a.Ok);
    }

    [Theory]
    [InlineData("{ \"name\": ", "invalid JSON")]
    [InlineData("[]", "not a JSON object")]
    [InlineData("{ }", "no 'name'")]
    [InlineData("{ \"name\": \"  \" }", "no 'name'")]
    [InlineData("{ \"name\": 3 }", "no 'name'")]
    [InlineData("{ \"name\": \"X\", \"includePlatforms\": [\"Editor\"], \"excludePlatforms\": [\"Android\"] }", "both 'includePlatforms' and 'excludePlatforms'")]
    public void Malformed_asmdefs(string json, string error)
    {
        var r = AsmdefParser.ParseAsmdef(json);
        Assert.False(r.Ok);
        Assert.Contains(error, r.Error);
    }

    [Theory]
    [InlineData("{ \"reference\": \"Game.Runtime\" }", "Game.Runtime")]
    [InlineData("﻿{ \"reference\": \" GUID:0123456789abcdef0123456789abcdef \" }", "GUID:0123456789abcdef0123456789abcdef")]
    public void Asmref_reference(string json, string expected)
    {
        var r = AsmdefParser.ParseAsmref(json);
        Assert.True(r.Ok);
        Assert.Equal(expected, r.Value);
    }

    [Theory]
    [InlineData("{ }", "no 'reference'")]
    [InlineData("[]", "no 'reference'")]
    [InlineData("{ \"reference\": \"\" }", "no 'reference'")]
    [InlineData("{ broken", "invalid JSON")]
    public void Malformed_asmrefs(string json, string error)
    {
        var r = AsmdefParser.ParseAsmref(json);
        Assert.False(r.Ok);
        Assert.Contains(error, r.Error);
    }

    [Fact]
    public void Empty_text_is_invalid()
    {
        Assert.False(AsmdefParser.ParseAsmdef(string.Empty).Ok);
        Assert.False(AsmdefParser.ParseAsmref(string.Empty).Ok);
    }
}
