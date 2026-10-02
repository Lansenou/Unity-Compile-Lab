// verify: an independent, deliberately simple re-implementation of the define table (docs/defines.md) and the
// assembly graph rules (docs/architecture.md, docs/platforms.md). It shares no code with src/ and is written
// from the docs only. It is the one file in the repository allowed to exceed 400 lines (listed in
// docs/architecture.md, "Files over 400 lines"): keeping the whole second implementation in one readable file
// is the point. Simple and obviously correct beats fast.
//
//   verify <fixtures-dir> <ucl-command...>   compare every manifest cell with `ucl graph --format json`
//   verify --write-defines <fixtures-dir>     write verify's own full define sets into fixtures/manifest.json
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

var write = args.Length > 0 && args[0] == "--write-defines";
var rest = write ? args[1..] : args;
if (rest.Length < (write ? 1 : 2))
{
    Console.Error.WriteLine("usage: verify <fixtures-dir> <ucl-command...> | verify --write-defines <fixtures-dir>");
    return 2;
}

var fixturesDir = Path.GetFullPath(rest[0]);
var manifestPath = Path.Combine(fixturesDir, "manifest.json");
var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
var temp = Path.Combine(Path.GetTempPath(), "ucl-verify-" + Guid.NewGuid().ToString("N")[..8]);
// Planning reads a plugin's PE headers (managed or native), so fixtures get the real stub DLLs (scripts/check.sh builds
// them into artifacts/stubs before verify runs).
var stubDlls = Path.Combine(fixturesDir, "..", "artifacts", "stubs", "dlls");
var diffs = new List<string>();
var cells = 0;
try
{
    foreach (var fixture in manifest["fixtures"]!.AsArray())
    {
        var name = (string)fixture!["name"]!;
        var project = Path.Combine(temp, name, "project");
        CopyDir(Path.Combine(fixturesDir, name), project);
        foreach (var m in fixture["materialize"]?.AsArray() ?? new JsonArray())
        {
            var to = Path.Combine(project, (string)m!["to"]!);
            var from = Path.Combine(stubDlls, (string)m["dll"]! + ".dll");
            if (!File.Exists(from))
            {
                Console.Error.WriteLine($"verify: {from} is missing; build the stubs first (dotnet run --project tests/Ucl.StubBuilder)");
                return 2;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to, overwrite: true);
        }

        foreach (var cellNode in fixture["cells"]!.AsArray())
        {
            var cell = Cell.From(cellNode!);
            var mine = Planner.Plan(project, cell);
            cells++;
            if (write)
            {
                foreach (var a in cellNode!["assemblies"]?.AsArray() ?? new JsonArray())
                {
                    var an = (string)a!["name"]!;
                    if (mine.Assemblies.TryGetValue(an, out var asm))
                        a["defines"] = new JsonArray([.. asm.Defines.Order(StringComparer.Ordinal).Select(d => (JsonNode)d!)]);
                    else
                        Console.Error.WriteLine($"verify: {name} [{cell.Label}]: no assembly '{an}' in verify's plan; defines not written");
                }

                continue;
            }

            Compare(name, cell, mine, RunUcl(rest[1..], project, cell, Path.Combine(temp, name)), diffs);
        }
    }
}
finally
{
    try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
}

if (write)
{
    var options = new JsonSerializerOptions { WriteIndented = true, IndentSize = 2, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    File.WriteAllText(manifestPath, manifest.ToJsonString(options) + "\n");
    Console.WriteLine($"verify: wrote defines for {cells} cells");
    return 0;
}

foreach (var d in diffs) Console.WriteLine(d);
Console.WriteLine(diffs.Count == 0 ? $"verify: {cells} cells agree" : $"verify: {diffs.Count} difference(s) in {cells} cells");
return diffs.Count == 0 ? 0 : 1;

static (int Exit, JsonNode? Json, string Stderr) RunUcl(string[] cmd, string project, Cell cell, string scratch)
{
    var psi = new ProcessStartInfo(cmd[0]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    string[] graph = ["graph", project, "--format", "json", "--unity-version", cell.UnityVersion, "--target", cell.Target, "--platform", cell.Platform,
        .. cell.EditorOs is null ? [] : new[] { "--editor-os", cell.EditorOs }, .. cell.ExtraArgs, "--cache-dir", Path.Combine(scratch, "cache")];
    foreach (var a in cmd[1..].Concat(graph)) psi.ArgumentList.Add(a);
    // A private home and an empty download cache: nothing outside the fixture can resolve a package.
    var home = Path.Combine(scratch, "home");
    Directory.CreateDirectory(home);
    psi.Environment["HOME"] = home;
    psi.Environment["USERPROFILE"] = home;
    psi.Environment["UCL_PACKAGE_CACHE"] = Path.Combine(scratch, "packages");
    using var p = Process.Start(psi)!;
    var stderrTask = p.StandardError.ReadToEndAsync();
    var stdout = p.StandardOutput.ReadToEnd();
    p.WaitForExit();
    JsonNode? json = null;
    try { json = JsonNode.Parse(stdout); } catch (JsonException) { }
    return (p.ExitCode, json, stderrTask.Result);
}

static void Compare(string fixture, Cell cell, Plan mine, (int Exit, JsonNode? Json, string Stderr) ucl, List<string> diffs)
{
    void Diff(string field, object expected, object actual) =>
        diffs.Add($"DIFF {fixture} [{cell.Label}] {field}: verify={expected} ucl={actual}");
    static string Show(IEnumerable<string> s) => "[" + string.Join(", ", s.Order(StringComparer.Ordinal)) + "]";
    static bool Same(IEnumerable<string> a, IEnumerable<string> b) => a.ToHashSet().SetEquals(b);

    if (ucl.Exit == 3 || mine.Problem is not null)
    {
        if (ucl.Exit != 3 || mine.Problem is null)
            Diff("problem", mine.Problem ?? "none", ucl.Exit == 3 ? "exit 3" : $"exit {ucl.Exit}, no problem");
        return;
    }

    var c = ucl.Json?["cells"]?.AsArray().FirstOrDefault();
    if (ucl.Exit is not (0 or 1) || c is null)
    {
        Diff("run", "a graph", $"exit {ucl.Exit}: {ucl.Stderr.Trim()}");
        return;
    }

    var theirs = c["assemblies"]!.AsArray().ToDictionary(a => (string)a!["name"]!, a => a!);
    if (!Same(mine.Assemblies.Keys, theirs.Keys)) Diff("assemblies", Show(mine.Assemblies.Keys), Show(theirs.Keys));
    var excluded = c["excluded"]!.AsArray().Select(e => (string)e!["name"]!).ToList();
    if (!Same(mine.Excluded, excluded)) Diff("excluded", Show(mine.Excluded), Show(excluded));
    foreach (var (n, a) in mine.Assemblies)
    {
        if (!theirs.TryGetValue(n, out var t)) continue;
        List<string> Strings(string key) => [.. t[key]!.AsArray().Select(x => (string)x!)];
        if (Strings("defines") is var td && !Same(a.Defines, td)) Diff(n + ".defines", "+" + Show(a.Defines.Except(td)), "+" + Show(td.Except(a.Defines)));
        if (!Same(a.References, Strings("references"))) Diff(n + ".references", Show(a.References), Show(Strings("references")));
        if (!Same(a.Precompiled, Strings("precompiledReferences"))) Diff(n + ".precompiledReferences", Show(a.Precompiled), Show(Strings("precompiledReferences")));
        if (!Same(a.Analyzers, Strings("analyzers"))) Diff(n + ".analyzers", Show(a.Analyzers), Show(Strings("analyzers")));
    }
}

static void CopyDir(string from, string to)
{
    Directory.CreateDirectory(to);
    foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
    foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
}

/// <summary>One manifest cell.</summary>
internal sealed record Cell(string UnityVersion, string Target, string Platform, string? EditorOs, string[] ExtraArgs)
{
    public bool Editor => Target == "editor";
    public bool Development => ExtraArgs.Contains("--development");
    public bool IncludeTests => ExtraArgs.Contains("--include-tests");
    public string? Backend => Array.IndexOf(ExtraArgs, "--backend") is var i and >= 0 ? ExtraArgs[i + 1] : null;
    public string Label => $"{UnityVersion} {Target} {Platform}{(ExtraArgs.Length > 0 ? " " + string.Join(' ', ExtraArgs) : "")}";

    public static Cell From(JsonNode n) => new(
        (string)n["unityVersion"]!, (string)n["target"]!, (string)n["platform"]!, (string?)n["editorOs"],
        [.. n["extraArgs"]?.AsArray().Select(a => (string)a!) ?? []]);
}

/// <summary>Verify's result for one cell.</summary>
internal sealed class Plan { public string? Problem; public Dictionary<string, Asm> Assemblies = []; public List<string> Excluded = []; }

/// <summary>A compiled assembly as verify sees it.</summary>
internal sealed class Asm { public HashSet<string> Defines = []; public List<string> References = [], Precompiled = [], Analyzers = []; }

/// <summary>An asmdef file.</summary>
internal sealed record AsmDef(string Name, string Path, string Dir, string[] Refs, string[] Include, string[] Exclude, string[] Constraints,
    (string Name, string Expr, string Define)[] VersionDefines, bool AutoReferenced, bool OverrideReferences, string[] PrecompiledRefs, bool LegacyTests = false,
    bool NoEngine = false);

/// <summary>A plugin DLL with a .meta.</summary>
internal sealed record Plugin(string Path, bool Analyzer, bool Explicit, string[] Constraints, Func<string, bool> EnabledFor);

internal static class Planner
{
    // --platform -> (asmdef platform, plugin .meta key, build target group, group number, D13-D20 suffixes); docs/platforms.md.
    private static readonly Dictionary<string, (string Asm, string Meta, string Group, string Num, string[] Suffixes)> Platforms = new()
    {
        ["StandaloneWindows64"] = ("WindowsStandalone64", "Win64", "Standalone", "1", ["STANDALONE", "STANDALONE_WIN"]),
        ["StandaloneOSX"] = ("macOSStandalone", "OSXUniversal", "Standalone", "1", ["STANDALONE", "STANDALONE_OSX"]),
        ["StandaloneLinux64"] = ("LinuxStandalone64", "Linux64", "Standalone", "1", ["STANDALONE", "STANDALONE_LINUX"]),
        ["iOS"] = ("iOS", "iOS", "iOS", "4", ["IOS"]),
        ["Android"] = ("Android", "Android", "Android", "7", ["ANDROID"]),
        ["WebGL"] = ("WebGL", "WebGL", "WebGL", "13", ["WEBGL"]),
    };

    private const string Firstpass = "Assembly-CSharp-firstpass", EditorFirstpass = "Assembly-CSharp-Editor-firstpass";
    private const string Main = "Assembly-CSharp", MainEditor = "Assembly-CSharp-Editor";

    public static Plan Plan(string root, Cell cell)
    {
        var plan = new Plan();
        try { Build(root, cell, plan); }
        catch (ProblemException e) { plan.Problem = e.Message; plan.Assemblies.Clear(); plan.Excluded.Clear(); }
        return plan;
    }

    private sealed class ProblemException(string message) : Exception(message);

    private static void Build(string root, Cell cell, Plan plan)
    {
        // Version: Unity 6 only. The cell's --unity-version is the version compiled (ProjectVersion.txt is its default).
        if (!cell.UnityVersion.StartsWith("6000.", StringComparison.Ordinal)) throw new ProblemException($"unsupported Unity version {cell.UnityVersion}");
        var vparts = cell.UnityVersion.Split('.');
        var minor = int.Parse(vparts[1]);
        var patch = int.Parse(Regex.Match(vparts[2], @"^\d+").Value);
        var unityVersion = $"6000.{minor}.{patch}";

        // Packages: name -> (version, folder or null for built-in modules).
        var packages = ResolvePackages(root, plan);

        // Files: project-relative path -> physical path, with the Asset Database's hidden-asset rules.
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        Scan(Path.Combine(root, "Assets"), "Assets", files);
        foreach (var (name, (_, folder)) in packages)
            if (folder is not null) Scan(folder, "Packages/" + name, files);

        var guids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rel, phys) in files.Where(f => f.Key.EndsWith(".meta", StringComparison.Ordinal)))
            if (Yaml.Scalar(ReadLines(phys), "guid") is { } g) guids[g] = rel[..^5];

        // asmdefs and asmrefs.
        var asmdefs = new List<AsmDef>();
        foreach (var (rel, phys) in files.Where(f => f.Key.EndsWith(".asmdef", StringComparison.Ordinal)))
        {
            var j = ParseJson(phys, rel);
            var a = new AsmDef((string?)j["name"] ?? throw new ProblemException($"{rel}: no name"), rel, Dir(rel), Strs(j, "references"),
                Strs(j, "includePlatforms"), Strs(j, "excludePlatforms"), Strs(j, "defineConstraints"),
                [.. (j["versionDefines"]?.AsArray() ?? []).Select(v => ((string?)v!["name"] ?? "", (string?)v["expression"] ?? "", (string?)v["define"] ?? ""))],
                (bool?)j["autoReferenced"] ?? true, (bool?)j["overrideReferences"] ?? false, Strs(j, "precompiledReferences"), NoEngine: (bool?)j["noEngineReferences"] ?? false);
            if (a.Include.Length > 0 && a.Exclude.Length > 0) throw new ProblemException($"{rel}: both includePlatforms and excludePlatforms");
            // A legacy test assembly (optionalUnityReferences: TestAssemblies) needs UNITY_INCLUDE_TESTS, is never
            // auto-referenced, and implicitly references the test runner assemblies and nunit.framework.dll.
            // A legacy test assembly (any optionalUnityReferences) also lists the test runners, overrides its references with
            // nunit.framework.dll, needs UNITY_INCLUDE_TESTS and is never auto-referenced (docs/architecture.md).
            if (Strs(j, "optionalUnityReferences").Length > 0)
                a = a with { Refs = [.. a.Refs, "UnityEngine.TestRunner", "UnityEditor.TestRunner"], Constraints = [.. a.Constraints, "UNITY_INCLUDE_TESTS"], AutoReferenced = false,
                    OverrideReferences = true, PrecompiledRefs = [.. a.PrecompiledRefs, "nunit.framework.dll"], LegacyTests = true };
            asmdefs.Add(a);
        }

        if (asmdefs.GroupBy(a => a.Name).FirstOrDefault(g => g.Count() > 1) is { } dup)
            throw new ProblemException($"duplicate assembly name {dup.Key}");
        var byName = asmdefs.ToDictionary(a => a.Name);
        var byPath = asmdefs.ToDictionary(a => a.Path);
        string? Resolve(string reference) => reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase)
            ? guids.TryGetValue(reference[5..], out var p) && byPath.TryGetValue(p, out var a) ? a.Name : null
            : byName.ContainsKey(reference) ? reference : null;

        // Folder -> owning assembly name (asmdef, or asmref target; an asmref to nothing owns its folder for nobody).
        var folderOwner = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var a in asmdefs) folderOwner[a.Dir] = a.Name;
        foreach (var (rel, phys) in files.Where(f => f.Key.EndsWith(".asmref", StringComparison.Ordinal)))
            folderOwner.TryAdd(Dir(rel), Resolve((string?)ParseJson(phys, rel)["reference"] ?? ""));

        // Ownership of scripts.
        var sources = new Dictionary<string, List<string>>();
        foreach (var rel in files.Keys.Where(f => f.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var owner = OwnerOf(rel, folderOwner, out var found);
            if (!found && rel.StartsWith("Assets/", StringComparison.Ordinal)) owner = PredefinedOf(rel);
            if (owner is null) continue; // package script outside any asmdef, or asmref to an unknown assembly
            (sources.TryGetValue(owner, out var l) ? l : sources[owner] = []).Add(rel);
        }

        // Project settings.
        var ps = ReadLines(Path.Combine(root, "ProjectSettings/ProjectSettings.asset"));
        var plat = Platforms[cell.Platform];
        List<string> ForGroup(string key) => Yaml.Map(ps, key) is var m && (m.GetValueOrDefault(plat.Group) ?? m.GetValueOrDefault(plat.Num)) is { } v ? v : [];

        var global = new HashSet<string>(StringComparer.Ordinal);
        // D01-D05 version symbols.
        global.UnionWith(["UNITY_6000", $"UNITY_6000_{minor}", $"UNITY_6000_{minor}_{patch}"]);
        for (var k = 0; k <= minor; k++) global.Add($"UNITY_6000_{k}_OR_NEWER");
        foreach (var (year, last) in new[] { ("5", 6), ("2017", 4), ("2018", 4), ("2019", 4), ("2020", 3), ("2021", 3), ("2022", 3), ("2023", 3) })
            for (var k = year == "5" ? 3 : 1; k <= last; k++) global.Add($"UNITY_{year}_{k}_OR_NEWER");
        // D10-D12 editor.
        if (cell.Editor)
        {
            var os = cell.EditorOs ?? (OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux");
            global.UnionWith(["UNITY_EDITOR", "UNITY_EDITOR_64", os switch { "windows" => "UNITY_EDITOR_WIN", "macos" => "UNITY_EDITOR_OSX", _ => "UNITY_EDITOR_LINUX" }]);
        }

        // D13-D21 platform (editor cells too: the active build target).
        foreach (var s in plat.Suffixes) global.UnionWith(["UNITY_" + s, "PLATFORM_" + s]);
        if (plat.Group is "Standalone" or "iOS") global.Add("UNITY_64");
        // D30-D39 runtime, profile, build.
        global.Add("CSHARP_7_3_OR_NEWER");
        var backend = cell.Backend ?? ForGroup("scriptingBackend").FirstOrDefault() switch { "0" => "mono", "1" => "il2cpp", _ => plat.Group == "Standalone" ? "mono" : "il2cpp" };
        global.Add(backend == "mono" || cell.Editor ? "ENABLE_MONO" : "ENABLE_IL2CPP"); // the Editor itself always runs Mono
        // D33/D34: the group's API level for every assembly except Editor-only ones, which use editorAssembliesCompatibilityLevel
        // (1 Default and 2 are .NET Framework, 3 .NET Standard). The profile symbols are added per assembly below.
        var api = ForGroup("apiCompatibilityLevelPerPlatform").FirstOrDefault() ?? Yaml.Scalar(ps, "apiCompatibilityLevel") ?? "6";
        var editorApi = (Yaml.Scalar(ps, "editorAssembliesCompatibilityLevel") ?? "1") == "3" ? "6" : "3";
        string[] Profile(bool editorOnly) => (editorOnly ? editorApi : api) == "3" ? ["NET_4_6", "NET_UNITY_4_8"] : ["NET_STANDARD_2_0", "NET_STANDARD_2_1", "NET_STANDARD", "NETSTANDARD2_1", "NETSTANDARD"];
        var input = Yaml.Scalar(ps, "activeInputHandler") ?? "0";
        if (input is "0" or "2") global.Add("ENABLE_LEGACY_INPUT_MANAGER");
        if (input is "1" or "2") global.Add("ENABLE_INPUT_SYSTEM");
        if (!cell.Editor && cell.Development) global.Add("DEVELOPMENT_BUILD");
        if (cell.Editor || cell.Development) global.UnionWith(["DEBUG", "TRACE", "UNITY_ASSERTIONS"]);
        // D50-D51 project symbols.
        foreach (var s in ForGroup("scriptingDefineSymbols")) global.UnionWith(s.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        foreach (var arg in ForGroup("additionalCompilerArguments")) global.UnionWith(RspDefines(arg));
        // D60.
        if ((packages.ContainsKey("com.unity.test-framework") && cell.Editor) || cell.IncludeTests) global.UnionWith(["UNITY_INCLUDE_TESTS", "UNITY_TESTS_FRAMEWORK"]);
        // E01, E04-E15: docs/defines.md "Built-in symbols".
        global.Add("CSHARP_7_OR_LATER");
        foreach (var (row, symbols) in BuiltIn.Rows)
        {
            var applies = row switch
            {
                "E04" => cell.Editor || cell.Development,
                // The stub editors have no platform engine build, so every player cell compiles against the editor's.
                "E16" => !cell.Editor && !cell.Development,
                "E05" => cell.Editor,
                "E07" => minor > 0 || patch >= 76,
                "E08" => minor >= 3,
                "E08a" => minor == 3 && patch < 19,
                "E17" => cell.Editor && (minor > 3 || (minor == 3 && patch >= 19)),
                "E10" => plat.Group == "Standalone",
                "E11" => cell.Platform == "StandaloneWindows64",
                "E12" => cell.Platform == "StandaloneLinux64",
                "E13" => cell.Platform == "StandaloneOSX",
                "E14" => cell.Platform == "WebGL",
                "E15" => cell.Platform == "Android",
                _ => true, // E06
            };
            if (applies) global.UnionWith(symbols);
        }

        // D52 response files: an asmdef's own csc.rsp replaces Assets/csc.rsp.
        HashSet<string> Rsp(string path) => files.TryGetValue(path, out var p) ? [.. File.ReadAllLines(p).SelectMany(RspDefines)] : [];
        var globalRsp = Rsp("Assets/csc.rsp");
        HashSet<string> DefinesOf(AsmDef? a, bool editorPredefined = false)
        {
            var d = new HashSet<string>(global, StringComparer.Ordinal);
            d.UnionWith(Profile(a is null ? editorPredefined : a.Include is ["Editor"]));
            if (a is null) { d.UnionWith(globalRsp); return d; }
            d.UnionWith(files.ContainsKey(a.Dir + "/csc.rsp") ? Rsp(a.Dir + "/csc.rsp") : globalRsp);
            foreach (var (res, expr, define) in a.VersionDefines) // D53
            {
                var version = res == "Unity" ? unityVersion : packages.TryGetValue(res, out var pk) ? pk.Version : null;
                if (version is not null && define != "" && Versions.InRange(version, expr)) d.Add(define);
            }

            return d;
        }

        static bool Holds(IEnumerable<string> constraints, HashSet<string> d) =>
            constraints.All(e => e.Trim() == "" || e.Split("||").Select(t => t.Trim()).Any(t => t.StartsWith('!') ? !d.Contains(t[1..].Trim()) : d.Contains(t)));

        // Testables: a package's test assemblies compile only when it is embedded (a folder in Packages/) or listed.
        var testables = Strs(ParseJson(Path.Combine(root, "Packages", "manifest.json"), "Packages/manifest.json"), "testables").ToHashSet();
        bool Testable(AsmDef a) => !a.Path.StartsWith("Packages/", StringComparison.Ordinal) || a.Path.Split('/')[1] is var pk
            && (testables.Contains(pk) || packages[pk].Folder?.StartsWith(Path.GetFullPath(Path.Combine(root, "Packages")), StringComparison.Ordinal) == true);

        // Cell membership of asmdefs.
        var platformName = cell.Editor ? "Editor" : plat.Asm;
        var compiled = new Dictionary<string, AsmDef>();
        var defines = new Dictionary<string, HashSet<string>>();
        foreach (var a in asmdefs)
        {
            defines[a.Name] = DefinesOf(a);
            var platformOk = a.Include.Length > 0 ? a.Include.Contains(platformName) : !a.Exclude.Contains(platformName);
            var test = a.Constraints.Contains("UNITY_INCLUDE_TESTS");
            var testsOk = cell.Editor || cell.IncludeTests || !(test || a.Constraints.Contains("UNITY_TESTS_FRAMEWORK"));
            if (platformOk && testsOk && Holds(a.Constraints, defines[a.Name]) && (!test || Testable(a))) compiled[a.Name] = a;
            else plan.Excluded.Add(a.Name);
            if (a.Include is ["Editor"]) defines[a.Name].Add("UNITY_EDITOR_ONLY_COMPILATION");
        }

        // Implicit references (docs/architecture.md): uGUI to every asmdef assembly but the UI, test runner, noEngineReferences and
        // Unity.*.CodeGen / Unity.*.Compiler ones; the test runners to Editor-only ones (all with playModeTestRunnerEnabled) that
        // list neither runner, except code-gen ones and the runners.
        var playMode = Yaml.Scalar(ps, "playModeTestRunnerEnabled") == "1";
        string[] runners = ["UnityEngine.TestRunner", "UnityEditor.TestRunner"];
        static bool CodeGen(string n) => n.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase)
            && (n.EndsWith(".CodeGen", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".Compiler", StringComparison.OrdinalIgnoreCase));
        IEnumerable<string> Implicit(string name, bool editorOnly, bool noEngine, IEnumerable<string> listed)
        {
            if (!noEngine && !CodeGen(name) && name is not ("UnityEngine.UI" or "UnityEditor.UI") && !runners.Contains(name))
                foreach (var ui in cell.Editor ? new[] { "UnityEngine.UI", "UnityEditor.UI" } : ["UnityEngine.UI"]) yield return ui;
            if ((playMode || editorOnly) && !CodeGen(name) && !runners.Contains(name) && !listed.Any(runners.Contains))
                foreach (var r in runners) yield return r;
        }

        // Cycles among compiled asmdefs (references to non-compiled targets are dropped first).
        List<string> Edges(AsmDef a)
        {
            var listed = a.Refs.Select(Resolve).OfType<string>().ToList();
            return [.. listed.Concat(Implicit(a.Name, a.Include is ["Editor"], a.NoEngine, listed)).Where(n => n != a.Name && compiled.ContainsKey(n)).Distinct()];
        }
        bool Reaches(string from, string to, HashSet<string> seen) =>
            Edges(compiled[from]).Any(n => n == to || (seen.Add(n) && Reaches(n, to, seen)));
        foreach (var n in compiled.Keys.Where(n => Reaches(n, n, [])).ToList()) { plan.Excluded.Add(n); compiled.Remove(n); }

        foreach (var a in compiled.Values) plan.Assemblies[a.Name] = new Asm { Defines = defines[a.Name], References = Edges(a) };

        // Predefined assemblies: only those with scripts; Editor ones never in a player.
        var phases = new[] { Firstpass, EditorFirstpass, Main, MainEditor };
        foreach (var p in phases.Where(sources.ContainsKey))
        {
            if (!cell.Editor && p.Contains("Editor", StringComparison.Ordinal)) { plan.Excluded.Add(p); continue; }
            var earlier = p switch { EditorFirstpass or Main => new[] { Firstpass }, MainEditor => [Firstpass, EditorFirstpass, Main], _ => [] };
            var editorPhase = p.Contains("Editor", StringComparison.Ordinal);
            var defs = DefinesOf(null, editorPhase);
            if (editorPhase) defs.Add("UNITY_EDITOR_ONLY_COMPILATION");
            plan.Assemblies[p] = new Asm
            {
                Defines = defs,
                // Runtime phases never see Editor-only asmdefs (includePlatforms exactly ["Editor"]).
                References = [.. compiled.Values.Where(a => a.AutoReferenced && (editorPhase || !(a.Include.Length == 1 && a.Include[0] == "Editor")))
                    .Select(a => a.Name).Concat(earlier.Where(plan.Assemblies.ContainsKey))
                    .Concat((playMode || editorPhase ? runners : []).Where(compiled.ContainsKey)).Distinct()],
            };
        }

        // Precompiled DLLs and analyzers.
        var plugins = new List<Plugin>();
        foreach (var rel in files.Keys.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && files.ContainsKey(f + ".meta")))
        {
            if (!HasCliHeader(files[rel])) continue; // a native plugin is never a reference
            var meta = ReadLines(files[rel + ".meta"]);
            plugins.Add(new Plugin(rel, Yaml.List(meta, "labels").Contains("RoslynAnalyzer"), Yaml.Scalar(meta, "isExplicitlyReferenced") == "1",
                [.. Yaml.List(meta, "defineConstraints")], PluginPlatforms(meta)));
        }

        var metaKey = cell.Editor ? "Editor" : plat.Meta;
        var pluginDefines = new HashSet<string>(global.Concat(Profile(false)), StringComparer.Ordinal); // the group's profile
        // One DLL per file name: the highest assembly version, then the first path.
        var usable = plugins.Where(p => !p.Analyzer && p.EnabledFor(metaKey) && Holds(p.Constraints, pluginDefines))
            .GroupBy(p => Path.GetFileName(p.Path))
            .Select(g => g.OrderByDescending(p => System.Reflection.AssemblyName.GetAssemblyName(files[p.Path]).Version).ThenBy(p => p.Path, StringComparer.Ordinal).First())
            .ToList();
        foreach (var (n, asm) in plan.Assemblies)
        {
            var a = compiled.GetValueOrDefault(n);
            asm.Precompiled = a is { OverrideReferences: true }
                ? [.. usable.Where(p => a.PrecompiledRefs.Contains(Path.GetFileName(p.Path))).Select(p => p.Path)]
                : [.. usable.Where(p => !p.Explicit).Select(p => p.Path)];
            var editorOnly = a is null ? n.Contains("Editor", StringComparison.Ordinal) : a.Include is ["Editor"];
            if ((playMode || editorOnly) && !(a is { OverrideReferences: true } && a.PrecompiledRefs.Contains("nunit.framework.dll")))
                asm.Precompiled = [.. asm.Precompiled.Union(usable.Where(p => Path.GetFileName(p.Path) == "nunit.framework.dll").Select(p => p.Path))];
        }

        // Analyzers: one under an asmdef or asmref folder serves that assembly and everything that reaches it through references;
        // any other serves every assembly.
        bool ReachesAsm(string from, string to, HashSet<string> seen) =>
            plan.Assemblies[from].References.Any(r => r == to || (seen.Add(r) && plan.Assemblies.ContainsKey(r) && ReachesAsm(r, to, seen)));
        foreach (var analyzer in plugins.Where(p => p.Analyzer))
        {
            var scope = OwnerOf(analyzer.Path, folderOwner, out var owned);
            foreach (var (n, asm) in plan.Assemblies)
                if (!owned || scope is null || n == scope || ReachesAsm(n, scope, [])) asm.Analyzers.Add(analyzer.Path);
        }
    }

    /// <summary>PE/COFF: the CLI header is data directory 14 of the optional header; a native DLL leaves it empty.</summary>
    private static bool HasCliHeader(string path)
    {
        var b = File.ReadAllBytes(path);
        if (b.Length < 0x40 || b[0] != 'M' || b[1] != 'Z') return false;
        var pe = BitConverter.ToInt32(b, 0x3C);
        if (pe + 24 + 2 > b.Length || b[pe] != 'P' || b[pe + 1] != 'E') return false;
        var opt = pe + 24;
        var dirs = opt + (BitConverter.ToUInt16(b, opt) == 0x20B ? 112 : 96);
        var cli = dirs + 14 * 8;
        return cli + 8 <= b.Length && BitConverter.ToInt32(b, cli + 4) > 0;
    }

    /// <summary>The nearest ancestor folder (within Assets/ or the package root) that has an owner.</summary>
    private static string? OwnerOf(string rel, Dictionary<string, string?> folderOwner, out bool found)
    {
        var stop = rel.StartsWith("Packages/", StringComparison.Ordinal) ? 2 : 1;
        var segs = rel.Split('/');
        for (var n = segs.Length - 1; n >= stop; n--)
            if (folderOwner.TryGetValue(string.Join('/', segs[..n]), out var owner)) { found = true; return owner; }
        found = false;
        return null;
    }

    private static string PredefinedOf(string rel)
    {
        var segs = rel.Split('/');
        var firstpass = segs.Length > 2 && segs[1] is "Plugins" or "Standard Assets" or "Pro Standard Assets";
        var editor = segs[..^1].Contains("Editor");
        return firstpass ? (editor ? EditorFirstpass : Firstpass) : (editor ? MainEditor : Main);
    }

    /// <summary>docs/platforms.md "Plugin import settings".</summary>
    private static Func<string, bool> PluginPlatforms(string[] meta)
    {
        var enabled = new Dictionary<string, bool>();
        var excluded = new HashSet<string>();
        string? current = null;
        for (var i = 0; i < meta.Length; i++)
        {
            var t = meta[i].Trim();
            if (t == "- first:" && i + 1 < meta.Length)
            {
                var kv = meta[i + 1].Trim();
                var colon = kv.IndexOf(':');
                var (left, right) = (kv[..colon].Trim(), kv[(colon + 1)..].Trim());
                current = left == "" ? ":Any" : left == "Any" ? "Any" : right;
            }
            else if (current is not null && t.StartsWith("enabled:", StringComparison.Ordinal)) enabled[current] = t[8..].Trim() == "1";
            else if (current == ":Any" && Regex.Match(t, @"^Exclude (\S+): 1$") is { Success: true } m) excluded.Add(m.Groups[1].Value);
        }

        if (enabled.Count == 0) return _ => true;
        if (enabled.GetValueOrDefault("Any")) return key => !excluded.Contains(key);
        return key => enabled.GetValueOrDefault(key);
    }

    private static Dictionary<string, (string Version, string? Folder)> ResolvePackages(string root, Plan plan)
    {
        var packagesDir = Path.Combine(root, "Packages");
        var wanted = new Dictionary<string, string>();
        foreach (var (k, v) in ParseJson(Path.Combine(packagesDir, "manifest.json"), "Packages/manifest.json")["dependencies"]?.AsObject() ?? new JsonObject())
            wanted[k] = (string?)v ?? "";
        var lockPath = Path.Combine(packagesDir, "packages-lock.json");
        if (File.Exists(lockPath))
            foreach (var (k, v) in ParseJson(lockPath, "Packages/packages-lock.json")["dependencies"]?.AsObject() ?? new JsonObject())
                if ((string?)v?["version"] is { } lv) wanted[k] = lv; // the lock file decides the version

        string? PackageName(string dir) => File.Exists(Path.Combine(dir, "package.json"))
            ? (string?)ParseJson(Path.Combine(dir, "package.json"), dir)["name"] : null;
        // Like Unity, every folder under Packages/ with a package.json is an embedded package, listed in manifest.json or not.
        foreach (var d in Directory.GetDirectories(packagesDir)) if (PackageName(d) is { } embedded) wanted.TryAdd(embedded, "");
        var result = new Dictionary<string, (string, string?)>();
        foreach (var (name, version) in wanted)
        {
            string? folder = null;
            if (!name.StartsWith("com.unity.modules.", StringComparison.Ordinal))
            {
                var cacheDir = Path.Combine(root, "Library", "PackageCache");
                var cached = Directory.Exists(cacheDir) ? Directory.GetDirectories(cacheDir, name + "@*").Order(StringComparer.Ordinal).ToList() : [];
                folder = Directory.GetDirectories(packagesDir).Order(StringComparer.Ordinal).FirstOrDefault(d => PackageName(d) == name)
                    ?? (version.StartsWith("file:", StringComparison.Ordinal) && Directory.Exists(Path.Combine(packagesDir, version[5..])) ? Path.GetFullPath(Path.Combine(packagesDir, version[5..])) : null)
                    ?? cached.FirstOrDefault(d => d.EndsWith("@" + version, StringComparison.Ordinal)) ?? cached.FirstOrDefault()
                    ?? "";
                if (folder == "") { plan.Problem ??= $"package {name} not found"; continue; } // reported, the plan goes on without it
            }

            var actual = folder is not null && File.Exists(Path.Combine(folder, "package.json"))
                ? (string?)ParseJson(Path.Combine(folder, "package.json"), name)["version"] ?? version : version;
            result[name] = (actual, folder);
        }

        return result;
    }

    private static void Scan(string dir, string rel, SortedDictionary<string, string> files)
    {
        if (!Directory.Exists(dir)) return;
        static bool Hidden(string n) => n.StartsWith('.') || n.EndsWith('~') || n == "cvs" || n.EndsWith(".tmp", StringComparison.Ordinal);
        foreach (var f in Directory.GetFiles(dir).Where(f => !Hidden(Path.GetFileName(f)))) files[rel + "/" + Path.GetFileName(f)] = f;
        foreach (var d in Directory.GetDirectories(dir).Where(d => !Hidden(Path.GetFileName(d)))) Scan(d, rel + "/" + Path.GetFileName(d), files);
    }

    /// <summary>`-define:`/`-d:` entries (either `-` or `/` spelling, `;` or `,` separated) of a response-file line.</summary>
    private static IEnumerable<string> RspDefines(string line) =>
        line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Regex.Match(t.Trim('"'), @"^[-/](?:define|d):(.*)$"))
            .Where(m => m.Success)
            .SelectMany(m => m.Groups[1].Value.Split(';', ',').Select(s => s.Trim()).Where(s => s != ""));

    private static JsonNode ParseJson(string path, string rel)
    {
        if (!File.Exists(path)) throw new ProblemException($"{rel}: missing");
        try { return JsonNode.Parse(File.ReadAllText(path)) ?? throw new ProblemException($"{rel}: empty JSON"); }
        catch (JsonException e) { throw new ProblemException($"{rel}: bad JSON: {e.Message}"); }
    }

    private static string[] Strs(JsonNode j, string key) => [.. j[key]?.AsArray().Select(x => (string?)x ?? "") ?? []];
    private static string Dir(string rel) => rel[..rel.LastIndexOf('/')];
    private static string[] ReadLines(string path) => File.Exists(path) ? File.ReadAllLines(path) : [];
}

/// <summary>The few YAML shapes Unity writes for the keys verify needs; line based.</summary>
internal static class Yaml
{
    private static int Indent(string l) => l.Length - l.TrimStart().Length;
    private static int Find(string[] lines, string key) => Array.FindIndex(lines, l => l.TrimStart().StartsWith(key + ":", StringComparison.Ordinal));

    /// <summary>`key: value` (first occurrence).</summary>
    public static string? Scalar(string[] lines, string key) => Find(lines, key) is var i and >= 0 ? lines[i].Split(':', 2)[1].Trim() : null;

    /// <summary>`key: []`, `key: [a, b]`, or `key:` followed by `- item` lines.</summary>
    public static List<string> List(string[] lines, string key)
    {
        var i = Find(lines, key);
        if (i < 0) return [];
        var inline = lines[i].Split(':', 2)[1].Trim();
        if (inline.StartsWith('[')) return [.. inline.Trim('[', ']').Split(',').Select(s => s.Trim()).Where(s => s != "")];
        var result = new List<string>();
        for (var j = i + 1; j < lines.Length && Indent(lines[j]) >= Indent(lines[i]) && lines[j].TrimStart().StartsWith("- ", StringComparison.Ordinal); j++)
            result.Add(lines[j].TrimStart()[2..].Trim());
        return result;
    }

    /// <summary>`key:` followed by `group: value` lines or `group:` with `- item` lines. `key: {}` is empty.</summary>
    public static Dictionary<string, List<string>> Map(string[] lines, string key)
    {
        var result = new Dictionary<string, List<string>>();
        var i = Find(lines, key);
        if (i < 0) return result;
        List<string>? current = null;
        for (var j = i + 1; j < lines.Length && (lines[j].Trim() == "" || Indent(lines[j]) > Indent(lines[i])); j++)
        {
            var t = lines[j].Trim();
            if (t == "") continue;
            if (t.StartsWith("- ", StringComparison.Ordinal)) { current?.Add(t[2..].Trim()); continue; }
            var kv = t.Split(':', 2);
            result[kv[0].Trim()] = current = [];
            if (kv.Length > 1 && kv[1].Trim() is { Length: > 0 } v) current.Add(v);
        }

        return result;
    }
}

/// <summary>Unity's versionDefines range syntax (docs/defines.md D53).</summary>
internal static class Versions
{
    public static bool InRange(string version, string expr)
    {
        expr = expr.Trim();
        if (expr == "") return true;
        if (expr[0] is not ('[' or '(')) return Compare(version, expr) >= 0;
        var inner = expr[1..^1];
        if (!inner.Contains(',')) return Compare(version, inner) == 0;
        var parts = inner.Split(',', 2);
        var (lo, hi) = (parts[0].Trim(), parts[1].Trim());
        var loOk = lo == "" || (expr[0] == '[' ? Compare(version, lo) >= 0 : Compare(version, lo) > 0);
        var hiOk = hi == "" || (expr[^1] == ']' ? Compare(version, hi) <= 0 : Compare(version, hi) < 0);
        return loOk && hiOk;
    }

    /// <summary>Numeric components (missing ones are 0), then a pre-release suffix orders below the release.</summary>
    public static int Compare(string a, string b)
    {
        var (ac, ap) = Split(a);
        var (bc, bp) = Split(b);
        for (var i = 0; i < Math.Max(ac.Length, bc.Length); i++)
        {
            var c = (i < ac.Length ? ac[i] : 0).CompareTo(i < bc.Length ? bc[i] : 0);
            if (c != 0) return c;
        }

        if (ap is null || bp is null) return (ap is null ? 1 : 0) - (bp is null ? 1 : 0);
        var (ax, bx) = (ap.Split('.'), bp.Split('.'));
        for (var i = 0; i < Math.Min(ax.Length, bx.Length); i++)
        {
            var c = long.TryParse(ax[i], out var x) && long.TryParse(bx[i], out var y) ? x.CompareTo(y) : string.CompareOrdinal(ax[i], bx[i]);
            if (c != 0) return Math.Sign(c);
        }

        return ax.Length.CompareTo(bx.Length);
    }

    private static (long[] Core, string? Pre) Split(string v)
    {
        var dash = v.IndexOf('-');
        var core = dash < 0 ? v : v[..dash];
        return ([.. core.Split('.').Select(p => long.TryParse(Regex.Match(p, @"^\d*").Value, out var n) ? n : 0)], dash < 0 ? null : v[(dash + 1)..]);
    }
}

/// <summary>docs/defines.md "Built-in symbols", rows E04-E15 (E01 and E02 are applied inline).</summary>
internal static class BuiltIn
{
    public static readonly (string Row, string[] Symbols)[] Rows =
    [
        ("E04", ["ENABLE_PROFILER", "ENABLE_UNITY_COLLECTIONS_CHECKS"]),
        ("E16", ["ENABLE_UNITY_COLLECTIONS_CHECKS"]),
        ("E05", ["EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED", "ENABLE_ACCELERATOR_CLIENT_DEBUGGING", "ENABLE_BURST_AOT", "ENABLE_CLOUD_LICENSE", "ENABLE_EDITOR_GAME_SERVICES",
            "ENABLE_EDITOR_HUB_LICENSE", "ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API", "ENABLE_MARSHALLING_TESTS", "UNITY_TEAM_LICENSE"]),
        ("E06", ["ENABLE_AUDIO", "ENABLE_CLOTH", "ENABLE_CLOUD_SERVICES", "ENABLE_CLOUD_SERVICES_ADS", "ENABLE_CLOUD_SERVICES_ANALYTICS", "ENABLE_CLOUD_SERVICES_BUILD",
            "ENABLE_CLOUD_SERVICES_CRASH_REPORTING", "ENABLE_CLOUD_SERVICES_PURCHASING", "ENABLE_CLOUD_SERVICES_USE_WEBREQUEST", "ENABLE_CRUNCH_TEXTURE_COMPRESSION",
            "ENABLE_CUSTOM_RENDER_TEXTURE", "ENABLE_DIRECTOR", "ENABLE_DIRECTOR_AUDIO", "ENABLE_DIRECTOR_TEXTURE", "ENABLE_LOCALIZATION", "ENABLE_MANAGED_ANIMATION_JOBS",
            "ENABLE_MANAGED_AUDIO_JOBS", "ENABLE_MANAGED_JOBS", "ENABLE_MANAGED_TRANSFORM_JOBS", "ENABLE_MANAGED_UNITYTLS", "ENABLE_MULTIPLE_DISPLAYS",
            "ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK", "ENABLE_PHYSICS", "ENABLE_SPRITES", "ENABLE_TERRAIN", "ENABLE_TEXTURE_STREAMING", "ENABLE_TILEMAP", "ENABLE_TIMELINE",
            "ENABLE_UNITYEVENTS", "ENABLE_UNITYWEBREQUEST", "ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT", "ENABLE_VIDEO", "ENABLE_VR", "ENABLE_WEBCAM", "ENABLE_WEBSOCKET_CLIENT",
            "ENABLE_WWW", "TEXTCORE_1_0_OR_NEWER", "TEXTCORE_FONT_ENGINE_1_5_OR_NEWER", "TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER"]),
        ("E07", ["ENABLE_UNITY_CLOUD_IDENTIFIERS", "ENABLE_UNITY_CONSENT"]),
        ("E08", ["TEXTCORE_FONT_ENGINE_1_6_OR_NEWER"]),
        ("E08a", ["ENABLE_AUDIO_SCRIPTABLE_PIPELINE"]),
        ("E17", ["ENABLE_PROFILER_ASSISTANT_INTEGRATION"]),
        ("E10", ["ENABLE_CACHING", "ENABLE_CLUSTERINPUT", "ENABLE_CLUSTER_SYNC", "ENABLE_LZMA", "ENABLE_MICROPHONE", "ENABLE_MOVIES", "ENABLE_NETWORK", "ENABLE_RUNTIME_GI",
            "ENABLE_SCRIPTING_GC_WBARRIERS", "ENABLE_VIRTUALTEXTURING", "INCLUDE_DYNAMIC_GI", "PLATFORM_ARCH_64", "PLATFORM_SUPPORTS_MONO", "RENDER_SOFTWARE_CURSOR"]),
        ("E11", ["ENABLE_ACCESSIBILITY_SCREEN_READER", "ENABLE_AMD", "ENABLE_AR", "ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS", "ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING",
            "ENABLE_EVENT_QUEUE", "ENABLE_NVIDIA", "ENABLE_OUT_OF_PROCESS_CRASH_HANDLER", "GFXDEVICE_WAITFOREVENT_MESSAGEPUMP", "PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS",
            "PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION", "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP", "PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER"]),
        ("E12", ["ENABLE_MODULAR_UNITYENGINE_ASSEMBLIES", "ENABLE_SPATIALTRACKING", "PLATFORM_SUPPORTS_DISPLAYINFO_API", "PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS",
            "PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER", "UNITY_STANDALONE_LINUX_API"]),
        ("E13", ["ENABLE_AR", "ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING", "ENABLE_GAMECENTER", "ENABLE_SPATIALTRACKING", "PLATFORM_HAS_CUSTOM_MUTEX",
            "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP"]),
        ("E14", ["ENABLE_ENGINE_CODE_STRIPPING", "ENABLE_ONSCREEN_KEYBOARD", "ENABLE_SPATIALTRACKING", "RENDER_SOFTWARE_CURSOR", "UNITY_DISABLE_WEB_VERIFICATION",
            "UNITY_GFX_USE_PLATFORM_VSYNC", "UNITY_WEBGL_API"]),
        ("E15", ["ENABLE_ACCESSIBILITY", "ENABLE_ANDROID_ADVERTISING_IDS", "ENABLE_ANDROID_APP_SET_ID", "ENABLE_AR", "ENABLE_CACHING", "ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS",
            "ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING", "ENABLE_EGL", "ENABLE_ENGINE_CODE_STRIPPING", "ENABLE_ETC_COMPRESSION", "ENABLE_EVENT_QUEUE",
            "ENABLE_FIREBASE_IDENTIFIERS", "ENABLE_INSIGHTS_PLATFORM_SPECIFIC_RESOURCES", "ENABLE_LZMA", "ENABLE_MICROPHONE", "ENABLE_NETWORK", "ENABLE_ONSCREEN_KEYBOARD",
            "ENABLE_RUNTIME_GI", "ENABLE_SCRIPTING_GC_WBARRIERS", "ENABLE_SPATIALTRACKING", "ENABLE_UNITYADS_RUNTIME", "INCLUDE_DYNAMIC_GI", "PLATFORM_EXTENDS_VULKAN_DEVICE",
            "PLATFORM_EXTENDS_VULKAN_PIPELINE_CACHE", "PLATFORM_HAS_ADDITIONAL_API_CHECKS", "PLATFORM_HAS_BUGGY_MSAA_RESOLVE", "PLATFORM_HAS_MULTIPLE_SWAPCHAINS",
            "PLATFORM_IMPLEMENTS_INSIGHTS_ANR", "PLATFORM_REQUIRES_TETHERED_VULKAN_COMMAND_POOL", "PLATFORM_SUPPORTS_INSIGHTS_DEVICE_INFO", "PLATFORM_SUPPORTS_MONO",
            "PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS", "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP", "UNITY_ANDROID_API", "UNITY_ANDROID_SUPPORTS_SHADOWFILES",
            "UNITY_CAN_SHOW_SPLASH_SCREEN", "UNITY_HAS_GOOGLEVR", "UNITY_HAS_TANGO", "UNITY_UNITYADS_API"]),
    ];
}
