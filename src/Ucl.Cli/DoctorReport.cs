using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Ucl.Cli;

/// <summary>What <c>ucl doctor</c> found, and its text and JSON (schema <c>ucl-doctor/1</c>) renderings.</summary>
internal sealed record DoctorReport
{
    public const string Schema = "ucl-doctor/1";

    public required string UclVersion { get; init; }

    public required string Runtime { get; init; }

    public required string Os { get; init; }

    public IReadOnlyList<(string Path, bool Exists)> EditorRoots { get; init; } = [];

    public IReadOnlyList<DoctorEditor> Editors { get; init; } = [];

    public IReadOnlyList<(string Name, string? Value)> Variables { get; init; } = [];

    public required string PackageCache { get; init; }

    public bool PackageCacheExists { get; init; }

    public string? GitVersion { get; init; }

    public DoctorProject? Project { get; init; }

    /// <summary>What must change before the project can be checked; empty means ready.</summary>
    public IReadOnlyList<string> Fixes { get; init; } = [];

    public string Text()
    {
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append('\n');
        Line($"ucl {UclVersion}");
        Line($"runtime: {Runtime}");
        Line($"os: {Os}");
        Line("editor search roots:");
        foreach (var (path, exists) in EditorRoots)
        {
            Line($"  {path}{(exists ? string.Empty : " (missing)")}");
        }

        Line(Editors.Count == 0 ? "editors: none found" : "editors:");
        foreach (var e in Editors)
        {
            Line($"  {e.Version}  {e.Root}  managed: {Yes(e.HasManaged)} ({e.EngineModules} engine, {e.EditorAssemblies} editor DLLs), netstandard: {Yes(e.NetStandardReferences > 0)} ({e.NetStandardReferences})");
        }

        Line("environment:");
        foreach (var (name, value) in Variables)
        {
            Line($"  {name}={value ?? "(not set)"}");
        }

        Line($"package cache: {PackageCache}{(PackageCacheExists ? string.Empty : " (does not exist yet)")}");
        Line($"git: {GitVersion ?? "not found on PATH (only needed by --changed)"}");
        if (Project is { } p)
        {
            Line($"project: {p.Root}");
            Line($"  version: {p.Version ?? "unknown"}");
            Line($"  editor: {p.Editor ?? "not installed"}");
            Line($"  packages: {(p.PackageSources.Count == 0 ? "none" : string.Join(", ", p.PackageSources.Select(s => $"{s.Count} {s.Source}")))}");
            foreach (var u in p.Unresolved)
            {
                Line($"  unresolved: {u}");
            }
        }

        if (Fixes.Count == 0)
        {
            Line(Project is null ? "ok" : "ok: the project can be checked");
        }
        else
        {
            Line("to fix:");
            foreach (var f in Fixes)
            {
                Line($"  - {f}");
            }
        }

        return sb.ToString();
    }

    public string Json()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("schema", Schema);
            w.WriteString("uclVersion", UclVersion);
            w.WriteString("runtime", Runtime);
            w.WriteString("os", Os);
            w.WriteStartArray("editorRoots");
            foreach (var (path, exists) in EditorRoots)
            {
                w.WriteStartObject();
                w.WriteString("path", path);
                w.WriteBoolean("exists", exists);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("editors");
            foreach (var e in Editors)
            {
                w.WriteStartObject();
                w.WriteString("version", e.Version);
                w.WriteString("root", e.Root);
                w.WriteString("dataPath", e.DataPath);
                w.WriteBoolean("hasManaged", e.HasManaged);
                w.WriteNumber("engineModules", e.EngineModules);
                w.WriteNumber("editorAssemblies", e.EditorAssemblies);
                w.WriteNumber("netStandardReferences", e.NetStandardReferences);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartObject("environment");
            foreach (var (name, value) in Variables)
            {
                if (value is null)
                {
                    w.WriteNull(name);
                }
                else
                {
                    w.WriteString(name, value);
                }
            }

            w.WriteEndObject();
            w.WriteStartObject("packageCache");
            w.WriteString("path", PackageCache);
            w.WriteBoolean("exists", PackageCacheExists);
            w.WriteEndObject();
            w.WriteStartObject("git");
            w.WriteBoolean("found", GitVersion is not null);
            w.WriteString("version", GitVersion);
            w.WriteEndObject();
            if (Project is { } p)
            {
                w.WriteStartObject("project");
                w.WriteString("root", p.Root);
                w.WriteString("version", p.Version);
                w.WriteString("editor", p.Editor);
                w.WriteStartObject("packages");
                foreach (var (source, count) in p.PackageSources)
                {
                    w.WriteNumber(source, count);
                }

                w.WriteEndObject();
                w.WriteStartArray("unresolved");
                foreach (var u in p.Unresolved)
                {
                    w.WriteStringValue(u);
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }
            else
            {
                w.WriteNull("project");
            }

            w.WriteBoolean("ok", Fixes.Count == 0);
            w.WriteStartArray("fixes");
            foreach (var f in Fixes)
            {
                w.WriteStringValue(f);
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static string Yes(bool b) => b ? "yes" : "no";
}
