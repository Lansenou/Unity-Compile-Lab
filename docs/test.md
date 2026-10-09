# `ucl test`: EditMode tests without Unity

### What `ucl test` can and cannot run

`ucl test` runs code paths that never call the Unity engine's native side. Creating or using an
engine object, including through a helper, is `needs-unity` and must run in the Editor.
PlayMode tests and `[UnityTest]` tests never run under .NET (`unity-only`); with `--host` the project player runs them. Editor log scopes also need Unity.

The share depends on the project: plain C# logic can run most tests; tests that build GameObjects,
textures or meshes may run few. Run `ucl test` once and read its summary counts before relying on it.
Examples below name members, not whole types; the audit checks Unity 6000.3 C# bodies and native bindings.

| Runs under `ucl test` (managed code paths) | Needs Unity (native bindings or Editor log scope) |
|---|---|
| Plain C# / `System.*`: collections, LINQ, `Span<T>`, JSON, your classes, with their required references | Creating/using `GameObject`, `Component`, `MonoBehaviour`, `Transform` |
| Vector2/3/4 arithmetic; Vector3 `Distance`, `Dot`, `Cross`, `Lerp` | `ScriptableObject.CreateInstance`, `Object.Instantiate` / `Destroy` |
| Mathf `Clamp`, `Lerp`, `Approximately`, `Sin`, `ClosestPowerOfTwo` | Mathf `PerlinNoise`, gamma/linear color-space conversions |
| Color/Color32 constructors; Color arithmetic; Rect/RectInt value operations; Bounds construction / `Intersects` | Texture2D, RenderTexture, Mesh, Material, Shader, Sprite creation/use; Bounds `Contains` / `ClosestPoint` / `SqrDistance` |
| Quaternion `identity`, quaternion times vector | Quaternion `Euler`, `LookRotation`, `Slerp`, `Inverse`, `AngleAxis` |
| Matrix4x4 multiply, `MultiplyPoint3x4` | Matrix4x4 `TRS`, `inverse`, `Perspective` |
| Attributes (`SerializeField`, `Range`), enums, plain structs | `Debug.Log*`, `LogAssert`, Application / Time engine properties, `Resources.Load` |
| | AssetDatabase, EditorPrefs, EditorUtility engine operations |
| | NativeArray / UnsafeUtility allocation, Jobs scheduling, Burst compilation, Physics / Camera / Graphics engine operations |

Keep logic in classes that take plain values, and keep engine calls at the edge. Tests of that
logic can run in seconds without the Editor.
The original `test-editmode` fixture reports **45 cases: 25 passed, 1 failed (intentional),
2 skipped, 1 ignored, 11 needs-unity, 5 unity-only**; these counts are not a project estimate.
See the [API source audit](test-api-source-audit.md) and its original stub-editor cases for each table row.

`ucl test [<project>]` compiles a project's test assemblies the way the Editor does, runs every NUnit case that
needs no live engine under .NET (CoreCLR), and says exactly which cases need Unity and why. The runnable share depends on the project; the summary gives its actual counts.

```sh
ucl test path/to/Project                                   # text: counts per assembly, every case that did not pass
ucl test path/to/Project --format json -o tests.json       # also junit, nunit3
ucl test path/to/Project --filter 'Inventory\.'            # regular expression over test full names
ucl test path/to/Project --emit-unity-filter skip.txt      # -testFilter value for the Unity run (below)
```

Other options as for `check`: `--editor`, `--unity-version`, `--platform` (the active build target, default
StandaloneWindows64), `--editor-os`, `--analyzers`, `--cache-dir`, `--no-cache`, `--jobs`, `--timings`, `--output`.

`--timings` also writes one `timing: <phase> <seconds> s` line to stderr per sequential phase: `session`,
`graph`, `compile`, `managed`, with `--host` `player-cache` (key and integrity check, or the cold build),
`player-compile`, `player-routing` and `player-run` (with `player-boot`, `player-cases` and `player-exit`
details from the player's own clock), and `report`. Each phase is the wall time since the previous one, so
the phases add up to the run; reports and exit codes are unchanged.

Exit codes: 0 every case that ran passed (or was classified needs-unity, unity-only, skipped or ignored); 1 a
case failed for a real reason, or a test assembly does not compile (nothing runs then); 3 configuration
problem (bad `--filter` included); 4 internal error.

## What is compiled

The editor cell (EditMode tests run in the Editor), with the cell's exact define set, so every `#if UNITY_*`
branch is compiled as the Editor compiles it (`UNITY_EDITOR`, `UNITY_5_3_OR_NEWER`, `NET_UNITY_4_8` for
Editor-only assemblies, ...). `UNITY_INCLUDE_TESTS` holds when `com.unity.test-framework` is resolved
(docs/defines.md, D60).

* **Test assemblies** are the assemblies compiled against `nunit.framework.dll`, as for the Unity Test Runner:
  asmdefs that list it in `precompiledReferences` (the modern form, with `UnityEngine.TestRunner`/
  `UnityEditor.TestRunner` in `references` and `defineConstraints: ["UNITY_INCLUDE_TESTS"]`), legacy asmdefs
  with `optionalUnityReferences: ["TestAssemblies"]`, and every Editor-only assembly (`Assembly-CSharp-Editor`
  included), which Unity gives the test runner assemblies and `nunit.framework.dll` without listing them
  (UnityCsReference `TestRunnerHelpers`, `EditorBuildRules.AddTestRunnerCustomReferences` and
  `AddTestRunnerPrecompiledReferences`; `ucl` does the same in `ReferenceResolver` and `PluginResolver`, and
  `verify/` independently; docs/architecture.md, "Test runner references"). A package's test assemblies exist
  only when the package is embedded or listed in `testables`.
* `nunit.framework.dll` comes from the project: the `com.unity.ext.nunit` package (or wherever the project has it).
* Test assemblies and their dependencies are emitted as full images (IL included) and cached under their own
  inputs hash (`image full`), so a warm run recompiles nothing.

## Test host: NUnit's framework API, in a child process

`ucl` runs NUnit 3.14.0's own framework API (`NUnitTestAssemblyRunner` with `DefaultTestAssemblyBuilder`), the
layer NUnitLite drives, in a child process: the test host, which is `ucl` itself started with the hidden
command `__test-host <request.json>` (a .NET tool or `ucl.dll` runs under `dotnet`). Why a child process:

* **A test can end a process.** Outside Unity an engine type with a finalizer (`CommandBuffer`, from the
  0.8.50 private run) can be constructed, its constructor fails in the engine call, and its finalizer later runs
  on a native object that never existed: `Finalize` calls `Dispose(false)`, whose binding throws
  `NullReferenceException` on the GC finalizer thread. That is an unhandled exception, which ends the process.
* **Results are written as they happen.** The host writes every discovered case, each case as it starts and
  each result to a results file, one JSON line each, flushed as written. When the host dies, the parent keeps
  the completed cases, reports the case in flight as `failed` (reason "test host crashed during this
  case: ..."), records the crash (`hostCrashes` in `ucl-test/1`: the last case reported before it, the case in
  flight, the error text) and starts a new host that skips every case already reported. A host that dies
  before reaching any case ends the run; the cases it never ran are `failed` ("not run: the test host crashed
  before this case"). The crash may come from an earlier case (a finalizer runs whenever the GC does), so the
  report names both cases.

Why NUnit's framework API and not the NUnit engine or NUnitLite:

* **No adapter.** The engine (and `dotnet test`) need an agent process, a runtimeconfig per test
  assembly and a test adapter; the test assemblies here are in-memory images of a Unity project, not .NET
  projects with a `deps.json`. The framework API loads them directly.
* **Classification needs the test tree.** Discovery returns NUnit's own test objects (with `MethodInfo`), so
  attributes and IL can be inspected before anything runs, and only the runnable cases are passed to `Run`
  through an id filter. NUnitLite only reports after the fact.
* **Isolation.** Each run uses a collectible `AssemblyLoadContext` (`TestLoadContext`): project assemblies load
  from their images, plugin and editor DLLs by simple name from disk, everything else from the .NET runtime
  (which supplies `mscorlib` and `netstandard` facades for assemblies built against Unity's profiles).
  `nunit.framework` always resolves to the host's NUnit, whatever version the project compiled against (Unity's
  custom NUnit is 3.5), so tests and runner share one NUnit. The context is unloaded after the run.
* NUnit 3.x, not 4.x: Unity's tests use the classic `Assert.AreEqual` API that NUnit 4 moved.

The editor's real `UnityEngine*.dll` and `UnityEditor*.dll` (or the stub editor's, in CI) are what engine
types resolve to at run time.

## Classification

Every case NUnit discovers gets exactly one category, decided by what actually happened, never by searching
source text:

| Category | When |
|---|---|
| `unity-only` | Never run. The assembly is a Play Mode assembly (not Editor-only: the Unity Test Framework runs it in Play Mode); or the method, its class or its assembly carries `[UnityTest]`, `[UnityPlatform]` or `[RequiresPlayMode]` (`UnityEngine.TestTools`, subclasses included; read from metadata). |
| `skipped` | `[Explicit]` (never selected by name here), NUnit `Skipped` without the Ignored label, `Inconclusive` (`Assume`, `Assert.Inconclusive`). |
| `ignored` | `[Ignore]`: NUnit `Skipped` with label `Ignored`. |
| `passed` | NUnit `Passed` or `Warning`. |
| `needs-unity`, never run | The method, or a project method it calls (followed through the IL of the compiled project assemblies), constructs an engine type (`UnityEngine*`/`UnityEditor*` assembly) that declares a finalizer, itself or in a base type: its finalizer would end the test host (previous section). A construction the IL does not show (`new T()` in a generic, reflection) is not seen; the child host covers it. |
| `needs-unity` | NUnit `Failed` (or error) whose failure names an engine-call exception as `Type : message`, directly, as an inner exception (`----> Type : ...`) or after a setup prefix (`OneTimeSetUp: Type : ...`): `System.Security.SecurityException` (CoreCLR: "ECall methods must be packaged into a system module", an `InternalCall` outside the runtime), `System.MissingMethodException` (Mono's form of a missing internal call), `System.EntryPointNotFoundException` and `System.DllNotFoundException` (native bindings, `__Internal`), `UnityEngine.UnityException`, or a `System.TypeLoadException` naming `UnityEngine`/`UnityEditor`. |
| `failed` | Every other failure: a real failure, which makes the exit code 1. |

The case running when the test host dies is classified from the crash text (previous section).

A `[SetUp]`/`[OneTimeSetUp]` that calls the engine fails its cases with the setup prefix: they are
`needs-unity` too.

### How the stub editor fails like the real one

Outside the Editor a real engine binding fails in the runtime itself: Unity's native-backed members are
`extern` methods marked `[MethodImpl(MethodImplOptions.InternalCall)]`, which CoreCLR refuses to call from a
non-system module with `System.Security.SecurityException` (verified on .NET 10). The stub editor
(`fixtures/_stubs/editor/`) reproduces exactly that: every member the real engine implements natively calls
`Native.Unavailable()`, a real `InternalCall` extern, so the same exception type, from the same runtime check,
reaches the test. Members Unity implements in C# (`Vector3`, `Mathf`, attributes) have managed bodies in the
stubs too, so engine-free tests that use them pass as they would in the Editor.

IL scanning records load failures per method, including type/method identity, exception type and message
in the affected case's `needs-unity` reason. Missing body types or unresolved callees do not abort classification
of later cases. Classification start/end events are flushed too, so a host crash during scanning names
its case and the replacement host skips that case and resumes the remainder. All host crashes force exit
1, even if discovery had not produced any cases. Original `TestHostLoadFailureTests` compile a type-bearing
dependency, then load an original replacement without that type: GetMethodBody throws TypeLoadException
for both a test body and a helper reached during scanning. No external binaries are used.

Finalizer prescans name the constructed engine type as `Type..ctor (finalizer prescan)` in
`engineMember`, so cases that never execute still contribute to the member ranking.
For native failures, the reason includes the first UnityEngine/UnityEditor type and member from
NUnit's exception stack, including calls made through helpers. JSON exposes nullable `engineMember`
and `summary.needsUnityByMember` (top 20); text prints the same descending-count ranking. Unknown
frames stay unknown; the tool does not infer a native member from a test name. Use counts across
projects to choose shim candidates, then require Unity-equivalence evidence before patching them.

Unity-only and Explicit eligibility is decided before method-body scanning; an unloadable
body cannot overwrite that decision.

## Zero-loss accounting

The sum of the categories equals the number of cases NUnit discovers in the compiled assemblies (after
`--filter`): `NUnitHost` classifies every discovered leaf before or after the run and throws (exit 4) if the
numbers differ. Because the assemblies are compiled with Unity's defines, cases under
`#if UNITY_5_3_OR_NEWER` or `#if UNITY_EDITOR` are discovered, and a branch the Editor does not compile
(`#else` of `UNITY_EDITOR`) is not. `TestCommandTests.Every_case_is_reported_with_its_category` compares the
full case list of fixture `test-editmode` with `fixtures/manifest.json` (`tests`), so a case that disappears
fails the build.

The count is NUnit 3.14's discovery. The Editor discovers with its NUnit 3.5 build and the Unity Test
Framework's builders. For `[Test]`, `[TestCase]` and `[TestCaseSource]` both use NUnit's standard builders,
so the counts are expected to agree; `[UnityTest]` is counted through the stand-in attribute (one case per
method), and a parameterised `[UnityTest]` or a custom `ITestBuilder` may count differently. This is not yet
confirmed against the Editor: compare the totals on the real project (docs/real-project-checklist.md).

## Skipping what passed in the Unity run

`--emit-unity-filter <file>` writes a Unity Test Framework `-testFilter` value that excludes every test class
whose cases all passed under `ucl test`: `!^Ns\.Class\.;!^Ns\.Other\.` (one anchored regular expression over
the test full name per class; a class with any failed, skipped, ignored, needs-unity or unity-only case is not
listed, so it runs in Unity in full). Pass it to the Editor run:

```sh
Unity -batchmode -runTests -testPlatform EditMode -testFilter "$(cat skip.txt)" -projectPath ...
```

Check the first time that the Editor's filter syntax treats a list of negated patterns as "none of these"
(the Unity Test Framework documents `!` as negation and `;` as a list); a class excluded wrongly would hide
tests. Divergence-sensitive classes (next section) must not be skipped: put such tests in a class with a
Unity-only marker, or keep them out of the filter by hand.

The CLI/test-host executable uses full globalization, so named cultures such as `de-DE` are
available. Linux requires ICU; a machine missing its globalization data cannot run the host.
Report formatting remains explicitly invariant.

The test host runs with the project root as its working directory, including after a restart.
Relative `Assets/...` paths resolve within that project; the parent process directory is unchanged.
`ucl test` writes the compiled images to a private folder under `<project>/Library/ucl` (deleted after the
run), so NUnit's `TestContext.CurrentContext.TestDirectory` is a folder under the project, as the Editor's
`Library/ScriptAssemblies` is, and fixtures found by walking up from it resolve the same way.

`LogAssert` requires Unity's log scope and is classified `needs-unity`, without running direct
calls. Helpers reaching a missing scope are classified from the framework's exact
`InvalidOperationException` message and its `LogScope`/`LogAssert` stack frame; unrelated
exceptions remain failures. No log capture or expectation shim is provided.
Source: [test-framework 1.4.5 LogScope.Current](https://github.com/needle-mirror/com.unity.test-framework/blob/117ede6d83ffc332b51c90d40df38451b914aabf/UnityEngine.TestRunner/Assertions/LogScope/LogScope.cs).

## Divergences: CoreCLR versus Mono

`ucl test` runs on CoreCLR; the Editor runs tests on Mono with Unity's .NET Framework 4.8 class library. Known
differences that change outcomes:

| Area | CoreCLR (`ucl test`) | Mono (Editor) | Fixture |
|---|---|---|---|
| `double.ToString()` | shortest round-trippable: `(0.1 + 0.2)` gives `0.30000000000000004` (.NET Core 3.0 change, "Floating-point parsing and formatting improvements in .NET Core 3.0") | 15 significant digits: `0.3` | `DivergenceTests.Double_ToString_is_shortest_round_trip` passes here, fails in the Editor |
| `float.ToString()` | shortest round-trippable: `1f/3f` gives `0.33333334` | 7 significant digits: `0.3333333` | `DivergenceTests.Float_ToString_is_shortest_round_trip` |
| `string.GetHashCode()` | randomised per process | stable across runs | none (do not assert hash values) |
| Reflection write to an initialized readonly static field | `FieldAccessException`: CoreCLR prohibits this since .NET Core 3.0; the specific initonly-static exception becomes `needs-unity`, with a runtime-divergence reason | rerun in the Editor; no readonly-field emulation | `TestHostLoadFailureTests.Readonly_static_reflection_is_a_runtime_divergence_and_later_cases_run` |
| Allocation probe interrupted by GC | different collector and allocation behavior can invalidate a probe; a generic allocation assertion is not proof of a runtime divergence | rerun under the Editor's runtime | sanitized exception/invalid-probe signal still needed; never treat such a measurement as a portability oracle |
| Culture | full named cultures; `CultureInfo.CurrentCulture` from the machine, ICU data on Linux and macOS | the machine's culture, Mono's own data | none; use `InvariantCulture` in tests |
| `Dictionary<,>` enumeration order | insertion order until a removal; a removed slot is reused by the next insert | the same algorithm (reference source) | none: not a divergence in practice, but order is unspecified in both |
| Floating-point arithmetic | SSE2/AVX, IEEE 754 per operation | Mono JIT, also SSE2 on x64 | none known |

The readonly restriction is documented in
[FieldInfo.SetValue](https://learn.microsoft.com/dotnet/api/system.reflection.fieldinfo.setvalue#remarks).
Only that explicit FieldAccessException is classified; other field-access errors and ordinary
allocation assertions remain failures. The four reported GC-window cases need an identifiable
invalid-probe signal before a safe classifier can be added.

The fixture's divergence cases pass under `ucl test`, so their class is in the emitted Unity filter: exactly
the risk described above, kept visible on purpose.

## Benchmark

The recorded `scripts/bench-test.sh` run used the earlier `test-editmode` fixture (6 assemblies,
31 cases, before the API-scope cases were added), comparing `ucl test` against
`dotnet test` on an equivalent hand-written csproj (the same sources and stub engine DLLs in one net10.0 test
project with NUnit 3.14.0 and NUnit3TestAdapter 4.6.0). 4 cores, Linux, .NET 10.0.401, 2026-10-02:

| run | wall clock | cases |
|---|---|---|
| `ucl test --no-cache` (compile 6 assemblies + run) | 1.68 s | 31 classified |
| `ucl test`, cold cache | 1.73 s | 31 classified |
| `ucl test`, warm cache | 0.42 s | 31 classified |
| `dotnet test` (build + run, after restore) | 1.93 s | 29 run: 22 passed, 6 failed, 1 skipped |
| `dotnet test --no-build` | 1.49 s | the same |

`dotnet test` reports the two engine calls, the `LogAssert` case and the three `[UnityTest]`/Play Mode cases
as failures next to the one real failure, and cannot apply Unity's defines per assembly; `ucl test` separates
them. The fixture is small, so these times are dominated by process start and Roslyn warm-up; the real-project
numbers (docs/real-project-checklist.md) are the ones that matter.

## Limits

* EditMode only: Play Mode assemblies are classified unity-only, never run under .NET (`--host` runs them in the project player).
* No per-test timeout: a test that never returns blocks the run (NUnit's timeout needs thread abort, which
  CoreCLR lacks).
* Direct `LogAssert` calls are identified from IL; helper failures require the runtime scope exception and a framework frame.
* Discovery failures before any case is known cannot identify an individual case; the host crash still returns exit 1.

## Optional project player host (Windows)

`ucl test <project> --host --format nunit3 -o <results.xml>` uses the existing .NET path for managed
cases and a cached per-project Mono player for compatible native-engine cases. The XML records the
`ucl-route` property (`dotnet`, `host`, `needs-editor`) per case; JSON adds the optional `route` field.
With `-o`, the usual text summary is printed as well. Failed cases and infrastructure problems remain red.

The adapter currently requires Windows, Unity **6000.3.19f1**, Mono standalone support, and Unity Test
Framework **1.6.0**. Building requires a licensed Editor. Other combinations must use the Editor; the
internal UTF runner is deliberately pinned. Host runs are not performed by the public CI machines.
The existing Linux, Windows and macOS checks continue covering the managed path and cache contracts.

The first engine run builds a scratch project under the user-level cache
`<LocalApplicationData>/ucl/player-hosts/v1`. It copies settings, assets and resolved packages, adds an
empty bootstrap scene, disables stripping and Burst compilation, and includes test assemblies.
Burst is disabled with a process-local build argument; the input project and global Editor preferences
are unchanged. This host validates Mono behavior. Cases whose assertions require Burst execution need
explicit Editor ownership and a separate Editor run. It preserves ancestor directory
names and config precedence so relative analyzer-config globs continue matching. It never injects bootstrap code
into the input project. Host-mode compilation defaults to `<LocalApplicationData>/ucl/test-compile`.
A cold build may take several minutes. Warm use recomputes a content key and verifies all player bytes;
changing assets, settings, packages, Unity revision, inherited config, options or bootstrap invalidates it.
Overlapping package trees share file hashes within one key calculation, using at most eight concurrent
reads. Every invocation reads content afresh; timestamps never authorize reuse.
A corrupt cache entry is retained separately and rebuilt. Concurrent builds use per-key locks; runs use
unique evidence directories and local result files, with no shared server port.

Graphics stay enabled. `--nographics` is optional and can change rendering, color-space and buffer
outcomes. The spike's full-cohort boot exceeded five seconds; this mode does not promise sub-five-second
boot. Discovery XML and whole-report rewrites were removed from the hot path, but discovery and fixture
setup still count toward boot. Each finished case is appended to a durable JSONL progress file.

The adapter uses the existing language, nullable, diagnostic and analyzer-config options. Assemblies
with enabled source generators/analyzers retain explicit Editor ownership until the player adapter
supports them. `--analyzers off` matches the existing managed command and disables those inputs in both
paths. `--no-cache` disables the managed image cache; player and player-test integrity caches remain enabled.

Editor-only source files and unsupported helper dependencies are excluded as whole files, with compiler
diagnostics retained in the cache. Compiler exclusions propagate to inherited fixtures and helper callers when they cannot compile without those files. Cases absent from player discovery retain an explicit exclusion reason.

The player sees the project layout the Editor sees. Reads of `UnityEngine.Application.dataPath` in
recompiled sources (aliases and `using static` included, resolved by symbol) compile to the Editor's value,
the input project's `Assets` folder with forward slashes; `nameof` operands are names, not reads, and keep
their text. The input files are unchanged. The player runs
with the project root as its working directory, and its test assemblies are copied to a private folder
under `<project>/Library/ucl` for the run, so `TestDirectory` and relative `Assets/...` paths resolve as
in the Editor. Helpers reused from player or precompiled DLLs keep the player's own `dataPath`; cases
that depend on them need audited Editor ownership. A test that writes through these paths writes where
it would in the Editor.

The player also runs `[UnityTest]` and `[RequiresPlayMode]` cases (its coroutine runner drives them
frame by frame) and the cases of Play Mode assemblies. `[UnityPlatform]` on the method, its class or its
assembly keeps a case with the Editor, whose platform it names, even in a Play Mode assembly or next to
`[UnityTest]`. As in the Editor, Editor-only test assemblies load and run under the EditMode test
platform and the others under PlayMode, one run each, so the `platform` test parameter matches the Editor
and a Play Mode test or `[UnitySetUp]` that yields an Edit Mode instruction (`IEditModeTestYieldInstruction`)
fails as it does there.

The player writes each result as the case ends and writes `results.json` to a temporary file it then
renames. If it dies or reaches the 60-minute run limit before a complete `results.json` exists, the
completed cases keep their results (a record cut off by the kill is dropped), the case in flight fails
("player ... during this case", matched by assembly and name), the remaining selected cases fail as not
run, and the report records a host crash.
There is no per-case time limit: a slow Play Mode case holds the run until it ends or the limit is reached.

Source and test bodies are never rewritten to make them pass; the `dataPath` substitution is a layout
mapping, applied to every read whatever its outcome. Player settings and runtime lifecycle still differ
from the Editor; validate outcome parity on the exact revision before switching a gate.

`--filter` keeps the existing regular expression over NUnit full names. A fully qualified class or
namespace selects its cases as with Unity's class filter. It does not implement Unity's semicolon or
negated-filter syntax. `--editor-cases <file>` assigns listed exact full names (or `assembly|full name` keys) to Editor ownership before managed execution, after a
project's independent parity audit. This is an explicit ownership input, not an automatic failure retry:
an existing managed failure or host failure is never silently rerun or converted to an exclusion. Retain the reason and revision with
that audit file, and recheck it whenever tests change.

Without `--host`, `--emit-unity-filter` keeps the existing fully-passing-class behavior.
With routed host results, it excludes completed `dotnet` and `host` cases, including
failures and skips already retained in the ucl report. Mixed fixtures use escaped,
anchored full-name exclusions; wholly completed fixtures use class exclusions when
their prefix matches no pending discovered case. Same-name collisions across
assemblies retain conservative ownership. Exact names containing UTF's `;` delimiter
stay eligible for the Editor rather than emitting an ambiguous filter. Use the same
discovery scope as the ucl run and respect platform command-line limits, splitting
Editor batches when needed.

On Windows, Unity 6000.3.19f1 receives a `-testFilter` value without its backslashes and cut to its
first 8,186 characters (observed in the Editor log's echoed command line; the same argument reached a
native child of the same shell intact). A cut filter can end in a bare `^`, which matches every case,
so a long exclusion or inclusion filter silently reruns almost the whole suite. Check the filter's
length, or use the test list below.

`--emit-unity-test-list <file>` writes the full name of every case `ucl test` did not complete, one per
line in discovery order: the cases a hybrid gate leaves for the Editor. Completion uses the routed
filter's rule (`dotnet` or `host` route, a category other than needs-unity and unity-only; without
`--host`, every case that ran under .NET). Pass the file to the Unity Test Framework's
`-orderedTestListFile`, which runs only the listed cases. Use an absolute path: Unity resolves a relative
one against the project folder, and a missing file is a run error.

```sh
Unity -batchmode -runTests -testPlatform EditMode -orderedTestListFile "$PWD/editor-cases.txt" -projectPath ...
```

A file has no command-line limit and needs no regex escaping. A name shared by a completed and a
pending case is listed. The framework runs the first case it finds with a listed name and skips
names it cannot find, so the hybrid collector must still check that every pending case has an
Editor result. Names containing a line break cannot be listed; they are counted in a warning.

A hybrid gate must merge results by assembly and full name, retain every real failure
from the original ucl report, and check complete case coverage. An Editor-only XML
result cannot replace the hybrid result. Excluding an already failed case from a
duplicate Editor run never makes the gate green. No gate migration is implied by
installing this optional backend.
