# `ucl test`: EditMode tests without Unity

`ucl test [<project>]` compiles a project's test assemblies the way the Editor does, runs every NUnit case that
needs no live engine under .NET (CoreCLR), and says exactly which cases need Unity and why. Most EditMode
suites have a large engine-free share; those cases run in seconds, without starting the Editor.

```sh
ucl test path/to/Project                                   # text: counts per assembly, every case that did not pass
ucl test path/to/Project --format json -o tests.json       # also junit, nunit3
ucl test path/to/Project --filter 'Inventory\.'            # regular expression over test full names
ucl test path/to/Project --emit-unity-filter skip.txt      # -testFilter value for the Unity run (below)
```

Other options as for `check`: `--editor`, `--unity-version`, `--platform` (the active build target, default
StandaloneWindows64), `--editor-os`, `--analyzers`, `--cache-dir`, `--no-cache`, `--jobs`, `--timings`, `--output`.

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

For native failures, the reason includes the first UnityEngine/UnityEditor type and member from
NUnit's exception stack, including calls made through helpers. JSON exposes nullable `engineMember`
and `summary.needsUnityByMember` (top 20); text prints the same descending-count ranking. Unknown
frames stay unknown; the tool does not infer a native member from a test name. Use counts across
projects to choose shim candidates, then require Unity-equivalence evidence before patching them.

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
| Culture | `CultureInfo.CurrentCulture` from the machine (or invariant with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`), ICU data on Linux and macOS | the machine's culture, Mono's own data | none; use `InvariantCulture` in tests |
| `Dictionary<,>` enumeration order | insertion order until a removal; a removed slot is reused by the next insert | the same algorithm (reference source) | none: not a divergence in practice, but order is unspecified in both |
| Floating-point arithmetic | SSE2/AVX, IEEE 754 per operation | Mono JIT, also SSE2 on x64 | none known |

The fixture's divergence cases pass under `ucl test`, so their class is in the emitted Unity filter: exactly
the risk described above, kept visible on purpose.

## Benchmark

`scripts/bench-test.sh` times `ucl test` on fixture `test-editmode` (6 assemblies, 31 cases) against
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

* EditMode only: Play Mode assemblies are classified unity-only, never run.
* No per-test timeout: a test that never returns blocks the run (NUnit's timeout needs thread abort, which
  CoreCLR lacks).
* Direct `LogAssert` calls are identified from IL; helper failures require the runtime scope exception and a framework frame.
* Discovery failures before any case is known cannot identify an individual case; the host crash still returns exit 1.
