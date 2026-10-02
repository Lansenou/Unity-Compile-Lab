# Real-project findings G1 to G5

On 2026-10-02 the maintainer ran `ucl` 0.4.0 on a real Unity 6000.3.19f1 project (100 assemblies, NuGet DLLs in
`Assets/Plugins/`, one native plugin) that compiles with 0 errors in the Editor. `ucl check` reported 1581 false
errors. Each root cause below has a fixture that reproduced the failure on the stub editor before the fix
("red run", `ucl` built from `067e54c`, the `v0.4.0` code) and passes after it. Paths in the red output are
shortened: `<project>` is the temporary fixture copy, `<stubs>` is `artifacts/stubs/editors`.

## G1. The API compatibility level was ignored

**Root cause.** Two faults. (1) `SimpleYaml` ended a mapping at the first line indented deeper than a line
that already had a value. The Editor wraps long values onto such continuation lines
(`iOSLaunchScreenPortrait: {fileID: ..., guid: ...,\n    type: 3}`), so everything after the first wrapped
value, `apiCompatibilityLevelPerPlatform` included, was lost and `apiCompatibilityLevel: 6` (.NET Standard)
won in every cell. (2) The level was per cell; Unity's is per assembly: Editor-only assemblies follow
`editorAssembliesCompatibilityLevel` (docs/defines.md, rows A01-A06).

**Fix.** Continuation lines are joined to their value; the level, the D33/D34 defines and the reference set
are chosen per assembly; the .NET Framework reference set is Unity's 17 core libraries plus every facade, and
the .NET Standard set gained `shims/netstandard` and `Extensions/2.0.0`.

**Fixture** `api-compat-netfx` (and `realistic-netfx-nuget`). Red run:

```
== 6000.3.2f1 editor StandaloneWindows64
Assets/Scripts/ScoreBuffer.cs(12,9): error CS0433: The type 'Span<T>' exists in both 'System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=null' and 'netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Assets/Scripts/ScoreBuffer.cs(12,33): error CS0121: The call is ambiguous between the following methods or properties: 'System.MemoryExtensions.AsSpan<T>(T[]) [<stubs>/6000.3.2f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll]' and 'System.MemoryExtensions.AsSpan<T>(T[]) [<project>/Assets/Plugins/System.Memory.dll]'
Assets/Scripts/ScoreBuffer.cs(13,26): error CS0433: The type 'ArrayPool<T>' exists in both 'System.Buffers, Version=4.0.3.0, Culture=neutral, PublicKeyToken=null' and 'netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Assets/Scripts/ScoreBuffer.cs(14,9): error CS0433: The type 'ArrayPool<T>' exists in both 'System.Buffers, Version=4.0.3.0, Culture=neutral, PublicKeyToken=null' and 'netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Assets/Scripts/ScoreBuffer.cs(15,39): error CS0121: The call is ambiguous between the following methods or properties: 'System.MemoryExtensions.AsSpan(string) [...]' and 'System.MemoryExtensions.AsSpan(string) [...]'
skipped Assembly-CSharp-Editor: dependency 'Assembly-CSharp' has errors
result: 5 errors, 0 warnings, 2 assemblies (1 skipped), exit 1
== 6000.3.2f1 player StandaloneWindows64
(the same five errors)
```

After the fix both Standalone cells are clean; the Android cells keep these five errors, because Android has
no per-group entry and stays on .NET Standard 2.1, as it would in the Editor.

## G2. A native plugin was passed to the compiler

**Root cause.** Every `.dll` with a `.meta` was a reference candidate. **Fix.** Discovery reads the PE headers
of every DLL (`PluginBinary`); one without a CLI header is a native plugin and never a reference.

**Fixture** `plugin-native` (a generated x86-64 PE with no COR20 header). Red run:

```
== 6000.0.30f1 editor StandaloneWindows64
error CS0009: Metadata file '<project>/Assets/ThirdParty/Plugins/x86_64/Ucl.Fixture.Native.dll' could not be opened -- PE image doesn't contain managed metadata.
result: 1 error, 0 warnings, 1 assembly (0 skipped), exit 1
== 6000.0.30f1 player StandaloneWindows64
(the same error)
```

## G3. A precompiled reference the project does not contain was an error

**Root cause.** `ucl` invented `UCL1004` as an error. Unity looks each `precompiledReferences` name up among
the precompiled assemblies it knows and skips a missing one without a message (UnityCsReference,
`EditorBuildRules`). **Fix.** `UCL1004` is info: reported, never counted. The fixture's Editor-only code-gen
asmdef compiles. Whether the Editor finds `Unity.IL2CPP.dll` somewhere `ucl` does not look is a question for
`ucl bee-diff` on the real project: the reference lists will differ if it does.

**Fixture** `precompiled-reference-absent`. Red run (the fixture's first draft also had a script error, fixed
before the green run):

```
== 6000.0.30f1 editor StandaloneWindows64
Packages/com.vendor.tools/Editor/CodeGen/Vendor.Tools.CodeGen.asmdef: error UCL1004: Assembly 'Vendor.Tools.CodeGen' lists precompiled reference 'Unity.IL2CPP.dll', which is not in the project
```

## G4. The facade gap

**Root cause.** A consequence of G1 (the wrong profile) plus a missing folder: `ucl`'s .NET Standard set lacked
`NetStandard/compat/2.1.0/shims/netstandard`, where `System.Runtime.dll` forwards to `netstandard`. **Fix.** G1's
reference sets. The stub editor now has `unity-4.8-api/Facades/System.Runtime.dll`,
`unity-4.8-api/Facades/netstandard.dll` (2.0.0.0), `shims/netstandard/System.Runtime.dll` and
`shims/netfx/mscorlib.dll`.

**Fixture** `facade-system-runtime` (`Vendor.Contracts.dll`, compiled against a `System.Runtime` 4.0.0.0
contract). Red run:

```
== 6000.0.30f1 player StandaloneWindows64
Assets/Scripts/Leaderboard.cs(10,19): error CS0012: The type 'Object' is defined in an assembly that is not referenced. You must add a reference to assembly 'System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
Assets/Scripts/Leaderboard.cs(10,25): error CS0012: (the same)
== 6000.0.30f1 player Android
(the same two errors)
```

## G5. Cascades

**Root cause.** Not a bug: when an assembly fails, its dependents are not compiled (Unity does the same), so
one failure made 73 of 100 assemblies "skipped". The report listed every diagnostic first and said only
"dependency 'X' has errors", naming the direct dependency even when it was itself skipped.

**Fix (report only; behaviour unchanged).** A skipped assembly's reason names the root failure
(`dependency 'Core' failed (through 'Mid', skipped)`) and JSON adds `blockedBy`. The text report leads each
cell with "root failures": every failed assembly, ranked by how many assemblies it blocks, with its three most
frequent error ids and the first instance of each. `ucl check --summary` prints only that block and the result
line. Tests: `CascadeReportTests`.

# Session 3: real-project compile parity (0.7.0)

The maintainer ran docs/real-project-checklist.md with 0.6.0 on a private 6000.3.19f1 project that the Editor
compiles cleanly (one editor dag, WebGL active; player checked as StandaloneWindows64) and reported counts
only: `bee-diff` exit 1 (104 assemblies, 0 agree, 8128 differences); `check` editor exit 1 (1286 errors, 10
failed, 63 skipped); player exit 1 (1151 errors); `ucl test` 0 cases. Six root causes, in order of blocked
assemblies. Every fixture below is synthetic, built from public package layouts and `.meta` settings; each
rule cites its source (UnityCsReference, the Unity manual, or public Unity-generated project files, [PUB] in
docs/defines.md). Red runs are 0.6.0's rules (`e3bde47`) on the 0.7.0 stub editor, which only adds files 0.6.0
ignores.

| Cause | Rule (source) | Fixture | Red run (0.6.0) | Green (0.7.0) |
|---|---|---|---|---|
| 1. uGUI assemblies missing | `UnityEngine.UI` (and `UnityEditor.UI` in editor cells) reach every asmdef assembly but the UI, test runner, `noEngineReferences` and code-gen ones (UnityCsReference `AutoReferencedPackageAssemblies`) | `ugui-auto-reference` | editor and player exit 1: `Example.Input` (lists only `"Unity.ugui"`, like `com.unity.inputsystem`) fails with CS0234 x3, CS0246 x2, CS0103 x1 on `EventSystems`, `UI`, `Selectable`, `PointerEventData`, `EventSystem` | exit 0; the `UCL1001` warning for `Unity.ugui` stays |
| 2. built-in symbols missing | E01-E15, `ENABLE_MONO` in every editor cell, D60 a compiler symbol (UnityCsReference `s_CSharpVersionDefines`, `UNITY_EDITOR_ONLY_COMPILATION`; [PUB] for the native lists) | `editor-builtin-defines` (10 cells) | every cell exit 1, `Example.Collections` fails with CS1029 (2 to 7 probe `#error`s per cell: E01, E04, E05, E06, D31, E14, D60) | exit 0 in all 10 cells; manifest holds each assembly's full set |
| 3. three `Unsafe.dll` copies | one precompiled DLL per file name, the highest assembly version wins (UnityCsReference `PrecompiledAssemblyProvider`; which copy wins: [REAL]) | `plugin-same-name` | exit 1: CS1704 (same simple name imported twice) in `Example.Pipeline` and `Unity.Collections` | exit 0; only the 6.0.1.0 copy is referenced; `UCL1005` info names the 4.0.4.1 and 6.0.0.0 copies |
| 4. package tests compiled, test framework helper omitted | package test assemblies only when embedded or in `testables`; `UNITY_TESTS_FRAMEWORK` assemblies out of players unless `--include-tests` (UnityCsReference `CustomScriptAssembly.IsCompatibleWith`; manual) | `package-testables` | exit 0 but wrong assembly sets: editor compiles `Example.Tools.Tests` and `Example.Tools.LegacyTests` (package not testable); player compiles `Example.Input.TestFramework` and `UnityEngine.TestRunner` | assembly sets as the manifest states |
| 5. editor references missing | `UnityEngine.dll` facade, platform module (`PlaybackEngines/<support>/Managed`), `UnityEditor.Graphs.dll`, every installed platform's `UnityEditor.*.Extensions.dll`, `Unity.CompilationPipeline.Common.dll` for code-gen ([PUB]; UnityCsReference `CompilationPipelineCommonHelper`) | `editor-reference-set` | editor WebGL exit 1: CS0012 (facade), CS0234 (`WebGLInput`), CS0246 x8 and CS0103 (Graphs, extensions, `ILPostProcessor`, `DiagnosticType`); players: CS0012 (and CS0234 on WebGL) | exit 0 in all 4 cells |
| 6. analyzers missing | owner-less analyzers reach every assembly, owned ones reach transitive referrers; the editor's own generators run everywhere (UnityCsReference `RoslynAnalyzers.SetAnalyzers`) | `analyzer-reach` | exit 1: CS0103 x2 in `Game.Core` and `Game.Tools` (no NuGet-style generator output); `Game.UI`'s `UFX001` missing | exit 0; `UFX001` on `Game.UI` |

Not reproduced with public shapes: the Editor compiled `Unity.InputSystem.TestFramework` and 0.6.0 left it out.
The fixture copies its asmdef (constraint `UNITY_TESTS_FRAMEWORK`, defined by its own `versionDefines` entry with an
empty expression) and 0.6.0 already compiled it in the Editor. If the next private run still lists it under
`assembly` differences, `ucl graph path/to/Project | grep "excluded Unity.InputSystem.TestFramework"` prints the reason without sharing project data.

Rules changed on the way, with the fixtures that assert them: analyzers outside every asmdef folder now reach
asmdef assemblies (`analyzer-global`) and owned analyzers reach transitive referrers (`analyzer-scoped`); with
the facade, a type of a disabled module is CS1069 rather than CS0246 (`builtin-module-disabled`); editor cells of
an IL2CPP project define `ENABLE_MONO` (`backend-il2cpp`); Editor-only assemblies get the test runners and
`nunit.framework.dll` (`test-editmode`, `test-assembly` unchanged).

# Session 4: the 0.7.0 private rerun (0.8.0)

Counts from the maintainer (same project class, 0.7.0 `1840155`): `bee-diff` 80 assemblies, 6180 differences
(assembly 2, references 6015, defines 163); `check` editor 20 errors, one root failure (`UnityEditor.UI`, blocks
72); player StandaloneWindows64 2 errors, one root failure (`Unity.Collections`, blocks 10); cold 80.4 s, warm
2.5 s; first player run 637 s. Red runs are 0.7.0's rules on the 0.8.0 stub editor.

| Item | Rule (source) | Fixture | Red run (0.7.0) | Green (0.8.0) |
|---|---|---|---|---|
| 1. editor cells took the platform's engine modules | one DLL per file name; editor cells use `Managed/UnityEngine/` plus the platform modules it lacks, players the platform's copy ([PUB]: editor project files reference only `PlaybackEngines/WebGLSupport/Managed/UnityEngine.WebGLModule.dll` from that folder) | `editor-engine-modules` | editor and player WebGL exit 1: CS1704 (`UnityEngine.InputLegacyModule` imported twice); `editor-reference-set` WebGL cells too | exit 0 in all 3 cells |
| 2. player collections symbol and engine build disagree | E16: a player cell whose platform folder has no engine build compiles against the editor's, built with `ENABLE_UNITY_COLLECTIONS_CHECKS`, so it defines that symbol (UnityCsReference `NativeArray.cs`: the `ReadOnly` constructor takes the `AtomicSafetyHandle` under that symbol) | `player-collections-checks` | release player cells (Windows, WebGL) exit 1: CS7036 on `NativeArray<int>.ReadOnly` in `Unity.Collections` | exit 0 in all 4 cells |
| 3. `Unity.InputSystem.TestFramework` excluded | D61: `UNITY_TESTS_FRAMEWORK` wherever D60 applies (editor cells with `com.unity.test-framework` installed; players with `--include-tests`); [REAL] | `tests-framework-symbol` | editor exit 0 but `Example.Input.TestFramework` excluded: `defineConstraints [UNITY_TESTS_FRAMEWORK] not satisfied` | compiled in the editor; excluded from the player |
| 4. version-gated symbols | D53: resource `Unity` bounds such as `2022.2.14f1` are valid (suffix ignored); E08 `ENABLE_AUDIO_SCRIPTABLE_PIPELINE` only before 6000.3.19, E17 `ENABLE_PROFILER_ASSISTANT_INTEGRATION` in editor cells from 6000.3.19 ([REAL] counts) | `version-gated-symbols` | all 3 cells exit 1: `UCL1021` (bound `2022.2.14f1` rejected) and CS1029 probes | exit 0 in all 3 cells |
| 5. extra assembly | an asmdef that owns no script is no assembly: skipped, references unresolved, `UCL1006` info | `asmdef-no-scripts` | exit 0 but `Example.Leftover` compiled, with `UCL1001` for its dead GUID reference | excluded, `UCL1006` only |
| 6. speed | analyzers and generators load once per run and are shared by every assembly; compiler and analyzer diagnostics come from one concurrent pass, so method bodies are bound once for both | R11 bench now includes `Ucl.Bench.Noisy`, a global analyzer (warning per method, info per call, hidden per local) on all 32 assemblies | cold 12.3 s (8,671 warnings); the slowdown stays linear: 31,759 warnings 12.7 s; a player cold run with a root failure 2.7 s | cold 11.6 s; the 637 s first player run is not reproduced synthetically |

Item 6 is measured, not closed: on the synthetic project analyzer cost is linear in diagnostics and about +60%
over `--analyzers off` (7.5 s), so the private run's 637 s comes from something the bench lacks (large real
analyzers or generators, or memory pressure). The checklist now asks for the cold time with `--analyzers off`.

# Session 6: the 0.8.0 private rerun

Counts from the maintainer (same project class, 0.8.0 `2a051a6`): `bee-diff` 79 assemblies, 0 agree, 1444
differences (references 1370, defines 74, no analyzer differences); `check` editor 20 errors (root failure
`UnityEditor.UI`, blocks 72); player StandaloneWindows64 exit 0, 32 assemblies, 203,661 warnings; cold player
522 s with analyzers, 53.5 s with `--analyzers off` (23 warnings); cold editor 53.7 s / 6.9 s; warm 2.9 s. For
scale, the Editor's own full recompile took 57.7 s.

| Item | Rule (source) | Fixture | Red run (0.8.0) | Green |
|---|---|---|---|---|
| 1. analyzer cost on package assemblies | Package assemblies do get the Assets analyzers: the 6000.3 manual says root analyzers apply to predefined assemblies, but UnityCsReference `RoslynAnalyzers.SetAnalyzers` gives an unowned analyzer to every script assembly, and bee-diff reported no analyzer differences. What differs: immutable package assemblies carry `AssemblyFlags.SuppressCompilerWarnings` (UnityCsReference 6000.3 `CustomScriptAssembly`), so none of their warnings is reported. `ucl` now reports only errors there and skips analyzers that cannot report an error | `analyzer-immutable-package` | both cells: UFX001 and CS0219 warnings reported for the registry package `Example.Inventory` | only the Assets and embedded-package warnings remain |
| 3. `UNITY_TESTS_FRAMEWORK` extra on 74 assemblies | not a compiler define; `Unity.InputSystem.TestFramework` declares it itself with the versionDefines entry `com.unity.test-framework` / `""` ([PUB] package asmdef), and defineConstraints are checked against the assembly's defines including version defines (UnityCsReference `EditorCompilation.GetTargetAssemblyDefines`). Row D61 removed | `tests-framework-symbol`, corrected to the public asmdef shape, plus an `#error` probe in `Assembly-CSharp` | editor exit 1: CS1029 in `Assembly-CSharp` (D61 defines the symbol everywhere) | exit 0; the test framework assembly still compiles in the Editor |
| 4. `System.Runtime.CompilerServices.Unsafe.dll` from the collections tests on 56 assemblies | a DLL inside the folder of a package test assembly that is not testable is no precompiled candidate, before the one-per-file-name rule ([REAL]; the native provider is not public) | `plugin-untestable-tests` (two copies, both 6.0.0.0) | both cells: the tests copy wins on path order, `UCL1005` on the `org.nuget` copy | the `org.nuget` copy is referenced, no `UCL1005` |
