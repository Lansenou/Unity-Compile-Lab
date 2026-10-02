using System.Text.Json;
using Ucl.Core.Model;

namespace Ucl.Core.Parsing;

/// <summary>Parses <c>.asmdef</c> and <c>.asmref</c> JSON as Unity does: unknown fields are ignored, comments and trailing commas tolerated.</summary>
public static class AsmdefParser
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Parses an asmdef.</summary>
    public static Result<AsmdefData> ParseAsmdef(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(StripBom(json), Options);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<AsmdefData>.Failure("asmdef is not a JSON object");
            }

            var name = Str(root, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result<AsmdefData>.Failure("asmdef has no 'name'");
            }

            var data = new AsmdefData
            {
                Name = name.Trim(),
                References = StrList(root, "references"),
                IncludePlatforms = StrList(root, "includePlatforms"),
                ExcludePlatforms = StrList(root, "excludePlatforms"),
                AllowUnsafeCode = Bool(root, "allowUnsafeCode", false),
                OverrideReferences = Bool(root, "overrideReferences", false),
                PrecompiledReferences = StrList(root, "precompiledReferences"),
                AutoReferenced = Bool(root, "autoReferenced", true),
                DefineConstraints = StrList(root, "defineConstraints"),
                VersionDefines = VersionDefines(root),
                NoEngineReferences = Bool(root, "noEngineReferences", false),
                OptionalUnityReferences = StrList(root, "optionalUnityReferences"),
            };
            // A legacy test assembly (optionalUnityReferences) is rewritten on load, as Unity does (UnityCsReference
            // CustomScriptAssemblyWithLegacyData.UpdateLegacyData); its test runner references are added by the graph builder.
            if (data.OptionalUnityReferences.Count > 0)
            {
                data = data with
                {
                    AutoReferenced = false,
                    OverrideReferences = true,
                    PrecompiledReferences = [.. data.PrecompiledReferences, "nunit.framework.dll"],
                    DefineConstraints = [.. data.DefineConstraints, "UNITY_INCLUDE_TESTS"],
                };
            }

            if (data.IncludePlatforms.Count > 0 && data.ExcludePlatforms.Count > 0)
            {
                return Result<AsmdefData>.Failure("asmdef sets both 'includePlatforms' and 'excludePlatforms'");
            }

            return Result<AsmdefData>.Success(data);
        }
        catch (JsonException e)
        {
            return Result<AsmdefData>.Failure($"invalid JSON: {e.Message}");
        }
    }

    /// <summary>Parses an asmref and returns its <c>reference</c> (a name or <c>GUID:</c>).</summary>
    public static Result<string> ParseAsmref(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(StripBom(json), Options);
            var reference = doc.RootElement.ValueKind == JsonValueKind.Object ? Str(doc.RootElement, "reference") : null;
            return string.IsNullOrWhiteSpace(reference)
                ? Result<string>.Failure("asmref has no 'reference'")
                : Result<string>.Success(reference.Trim());
        }
        catch (JsonException e)
        {
            return Result<string>.Failure($"invalid JSON: {e.Message}");
        }
    }

    private static string StripBom(string s) => s.Length > 0 && s[0] == '﻿' ? s[1..] : s;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name, bool fallback) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        } : fallback;

    private static IReadOnlyList<string> StrList(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return v.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private static IReadOnlyList<VersionDefine> VersionDefines(JsonElement e)
    {
        if (!e.TryGetProperty("versionDefines", out var v) || v.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return v.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.Object)
            .Select(x => new VersionDefine(Str(x, "name") ?? string.Empty, Str(x, "expression") ?? string.Empty, Str(x, "define") ?? string.Empty))
            .ToList();
    }
}
