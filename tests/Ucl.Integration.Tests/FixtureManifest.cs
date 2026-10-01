using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ucl.Integration.Tests;

/// <summary>fixtures/manifest.json (schema ucl-fixtures/1).</summary>
public sealed record FixtureManifest(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("editors")] IReadOnlyList<string> Editors,
    [property: JsonPropertyName("fixtures")] IReadOnlyList<FixtureEntry> Fixtures)
{
    /// <summary>Loads the committed manifest.</summary>
    public static FixtureManifest Load() =>
        JsonSerializer.Deserialize<FixtureManifest>(File.ReadAllText(Path.Combine(Repo.Fixtures, "manifest.json")))!;
}

/// <summary>One fixture.</summary>
public sealed record FixtureEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("requirements")] IReadOnlyList<string> Requirements,
    [property: JsonPropertyName("materialize")] IReadOnlyList<Materialize>? Materialize,
    [property: JsonPropertyName("cells")] IReadOnlyList<FixtureCell> Cells);

/// <summary>A DLL built from fixtures/_stubs placed into a copy of the fixture.</summary>
public sealed record Materialize(
    [property: JsonPropertyName("dll")] string Dll,
    [property: JsonPropertyName("to")] string To);

/// <summary>One expected cell.</summary>
public sealed record FixtureCell(
    [property: JsonPropertyName("unityVersion")] string UnityVersion,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("editorOs")] string? EditorOs,
    [property: JsonPropertyName("extraArgs")] IReadOnlyList<string>? ExtraArgs,
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("oracle")] string Oracle,
    [property: JsonPropertyName("realEditor")] bool RealEditor,
    [property: JsonPropertyName("assemblies")] IReadOnlyList<ExpectedAssembly>? Assemblies,
    [property: JsonPropertyName("excluded")] IReadOnlyList<string>? Excluded,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ExpectedDiagnostic>? Diagnostics);

/// <summary>An expected assembly.</summary>
public sealed record ExpectedAssembly(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("defines")] IReadOnlyList<string>? Defines,
    [property: JsonPropertyName("definesInclude")] IReadOnlyList<string>? DefinesInclude,
    [property: JsonPropertyName("definesExclude")] IReadOnlyList<string>? DefinesExclude);

/// <summary>An expected diagnostic.</summary>
public sealed record ExpectedDiagnostic(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("file")] string? File,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("column")] int Column)
{
    /// <inheritdoc/>
    public override string ToString() => $"{File}({Line},{Column}): {Severity} {Id}";
}
