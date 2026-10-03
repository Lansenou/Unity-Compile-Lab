# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [Semantic Versioning](https://semver.org/). Public contracts: the CLI options, exit codes,
the JSON schema id `ucl-result/1`, the graph schema id `ucl-graph/1`, the fixture manifest schema
`ucl-fixtures/1` and the `UCLxxxx` diagnostic ids.

## [Unreleased]

### Fixed

* Preserve NUnit failure details, including static initializer inner exceptions and assertion "But was" lines.

* Include constructors detected by the finalizer prescan in needs-unity member rankings.

* Decide unity-only/Explicit eligibility before IL scans so excluded unloadable bodies keep
  their category and are never scanned or run.

* Classify CoreCLR's initialized readonly-static reflection restriction as needs-unity with
  a runtime-divergence reason; unrelated field-access errors remain failed.

* Report unavailable Unity log scopes as needs-unity, including helper calls; direct LogAssert
  calls now use that category instead of unity-only. PlayMode and UnityTest remain unity-only.

* Enable full globalization in the CLI/test host so named cultures do not produce false test failures.

* `ucl test` starts each host in the project root so relative asset paths resolve as in EditMode.

* `ucl test` records IL-scan load failures as needs-unity with the affected method and continues. A crash during
  classification records its case and resumes in a replacement host. Host crashes are errors (exit 1),
  including crashes with engine frames or before discovery; crash text is retained.

* Accept map-form plugin `platformData` as well as list-form entries. Apply the same Any Platform
  exclusions and explicit platform enables, including Editor-only and Exclude Editor settings.

### Changed

* Put the member-specific scope of ucl test upfront in the README and test docs, with public
  source links and actual original stub-editor fixture counts.

* `ucl test` needs-unity reasons include the first UnityEngine/UnityEditor member from the runtime
  exception stack. `ucl-test/1` adds nullable per-case `engineMember` and top-20 summary
  `needsUnityByMember`; text reports the same count ranking. Missing frames remain explicitly unknown.

* Analyzer timing JSON adds supported `ruleIds` and `timeScope: "analyzer"` to assembly and summary
  entries. Text timings show a descending-time assembly/analyzer/rule-ID table; README shows sortable
  CSV export. Rules share their analyzer's measured time; no individual rule costs are inferred.

* Dependents compile once an assembly's reference image is emitted, while its analyzers finish in a
  separate bounded queue. Source generators and compiler errors remain on the emission path. Late
  analyzer diagnostics are reported and cached, and final dependency failure cascades are preserved.
  Cache format v3 retains a compiler-valid image even when analysis fails; older entries rebuild once.

* `ucl check --format json --timings` adds `analyzerTimings` (`path`, `analyzer`, `timeMs`) to each assembly
  and to cell/run summaries in `ucl-result/1`. Roslyn's logged callback times are summed per analyzer,
  not wall-clock latency. Cache hits report no current-run analyzer time; cached diagnostics still appear.

* A test framework assembly (its `defineConstraints` has `UNITY_TESTS_FRAMEWORK`) gets the symbol `UNITY_TESTS_FRAMEWORK`
  in the Editor when the test framework is installed (row D61), so the released `Unity.InputSystem.TestFramework`,
  which has no versionDefines, compiles again. No other assembly gets it.

* Compiler and analyzer Info diagnostics are no longer reported, and an analyzer whose every diagnostic is Info,
  Hidden or off at its effective severity is not run, as csc does without `-errorlog` (fixture
  `analyzer-info-severity`).

* Warm runs load each cached assembly's diagnostics apart from its image, off the dependency chain (cache
  format v2; old entries are rebuilt once). On the benchmark with about 100,000 cached warnings the warm run
  goes from 1.53 s to 1.26 s; `scripts/bench.sh` has a row for it.

* `ucl test` runs the tests in a child test host (the same program, started again). A test that ends the
  process (an engine type's finalizer throwing on the GC thread) no longer loses the run: the completed cases
  are kept, the case in flight is classified from the crash text, a new host runs the rest, and `ucl-test/1`
  gains `hostCrashes` (`after`, `during`, `text`), also printed in the text report. A case that constructs an
  engine type with a finalizer, directly or through project code, is `needs-unity` and never run (fixture
  `test-host-crash`).

* In editor cells, editor-only assemblies reference every engine module, including disabled built-in
  packages' modules (fixture `editor-only-disabled-modules`).

* `UNITY_TESTS_FRAMEWORK` is no longer a compiler define (row D61 removed, a 0.8.0 mistake). Test framework
  assemblies declare it through their own versionDefines, which their defineConstraints see.
* A DLL inside the folder of a package test assembly that is not testable is no longer a precompiled
  candidate, so it cannot shadow a same-name copy (fixture `plugin-untestable-tests`).

* Assemblies of immutable packages (registry, git, built-in) report only errors, as Unity compiles them with
  `SuppressCompilerWarnings`; analyzers that cannot report an error are not run on them (fixture
  `analyzer-immutable-package`). This removes most analyzer time on projects with many package assemblies.

* Releases are automatic: each push to `main` that passes the three CI gates is released as
  `v0.<minor>.<run number>` (README, "Releases"). Pushed `v*` tags no longer trigger a release.
* `ucl --version` and the tool version in `ucl-result/1`, `ucl-graph/1` and SARIF output now print the release
  string unchanged (`v0.8.41`, or `dev-<sha>` for pull request builds); local builds still print `0.8.0`.

## [0.8.0]

Session 4: the 0.7.0 private rerun (docs/real-project-fixes.md, "Session 4").

### Added

* `UCL1006` (info): an asmdef with no scripts is skipped, as Unity compiles no assembly for it.
* Built-in symbols: E16 (`ENABLE_UNITY_COLLECTIONS_CHECKS` in player cells compiled against the editor's engine
  build), E17 (`ENABLE_PROFILER_ASSISTANT_INTEGRATION`, editor cells from 6000.3.19), D61
  (`UNITY_TESTS_FRAMEWORK` wherever `UNITY_INCLUDE_TESTS` applies).

### Changed

* Editor cells reference the editor's `Managed/UnityEngine/` engine modules whatever the active platform is,
  plus only the platform modules it lacks; player cells use the platform's copy. One DLL per file name.
* `ENABLE_AUDIO_SCRIPTABLE_PIPELINE` only on 6000.3 before 6000.3.19.
* Speed: analyzers and generators are loaded once per run and shared by all assemblies; compiler and analyzer
  diagnostics come from one concurrent pass. The R11 benchmark project gains a noisy global analyzer.

### Fixed

* CI coverage uses the in-proc collector to prevent lost hit data when VSTest terminates its host.
  The Core 90% and overall 75% line gates remain enforced; delayed-shutdown coverage is regression-tested.

* Honour plugin Any-entry platform exclusions, including Exclude Editor for Assets plugins.

* `versionDefines` with resource `Unity` accept bounds with a release suffix (`2022.2.14f1`) instead of
  rejecting them with `UCL1021`.

## [0.7.0]

Session 3: real-project compile parity. The six root causes of the second real-project run
(docs/real-project-fixes.md, "Session 3"), each with a fixture that was red on 0.6.0.

### Added

* Built-in symbols (docs/defines.md, rows E01-E15): `CSHARP_7_OR_LATER`, `UNITY_EDITOR_ONLY_COMPILATION` for
  Editor-only assemblies, and the engine feature, editor service and per-platform sets Unity 6 defines
  (`ENABLE_UNITY_COLLECTIONS_CHECKS`, `ENABLE_PROFILER`, `ENABLE_PHYSICS`, `UNITY_WEBGL_API`, ...).
* Auto-referenced uGUI: `UnityEngine.UI` (and `UnityEditor.UI` in editor cells) reach every asmdef assembly
  without being listed, as in Unity.
* Test runner references: Editor-only assemblies (all with `playModeTestRunnerEnabled`) get
  `UnityEngine.TestRunner`, `UnityEditor.TestRunner` and `nunit.framework.dll`.
* Editor references: the `UnityEngine.dll` facade, the platform's engine module from
  `PlaybackEngines/<support>/Managed`, and in editor cells `UnityEditor.Graphs.dll` and every installed
  platform's `UnityEditor.*.Extensions.dll`; `Unity.CompilationPipeline.Common.dll` for code-gen assemblies.
* The editor's own source generators (`Tools/BuildPipeline/Unity.SourceGenerators`) run on every assembly
  (docs/proposals.md, item 1).
* `UCL1005` (info): a precompiled DLL left out because another DLL with the same file name wins.
* `ucl bee-diff`: a `by category:` line in text and `summary.byCategory` in JSON (`ucl-beediff/1`, additive), so a
  private project can report counts only.
* Fixtures `ugui-auto-reference`, `editor-builtin-defines`, `plugin-same-name`, `package-testables`,
  `editor-reference-set`, `analyzer-reach` (68 fixtures, 176 cells); the stub editor gains the facade, a
  WebGL module, editor extensions, `UnityEditor.Graphs`, `Unity.CompilationPipeline.Common` and a stub
  source generator (`fixtures/_stubs/editor-extra/`).

### Changed

* Editor cells always define `ENABLE_MONO` (the Editor runs Mono), also for IL2CPP-only platforms.
* `UNITY_INCLUDE_TESTS` (D60) is passed to the compiler, no longer used for `defineConstraints` only.
* A package's test assemblies compile only when the package is embedded or listed in `testables`; test
  framework assemblies (`UNITY_TESTS_FRAMEWORK`) are left out of players unless `--include-tests`.
* One precompiled DLL per file name: the highest assembly version wins (no more CS1704/CS0433 from copies).
* Analyzer scope follows UnityCsReference: an analyzer outside every asmdef folder applies to every assembly
  (it applied to predefined assemblies only), and an owned analyzer reaches transitive referrers (it reached
  direct ones only).
* Legacy test assemblies (`optionalUnityReferences`) are rewritten on load as Unity does: `overrideReferences`
  with `nunit.framework.dll` only.

## [0.6.0]

Session 2, phase B: `ucl test`.

### Added

* `ucl test [<project>] [--filter <regex>] [--format text|json|junit|nunit3] [--emit-unity-filter <file>]`:
  compiles the editor cell's test assemblies (those compiled against `nunit.framework.dll`) as full images,
  runs them in process with NUnit 3.14.0's framework API in an isolated load context, and classifies every
  discovered case as passed, failed, skipped, ignored, needs-unity (an engine call failed) or unity-only
  (`[UnityTest]`, `[UnityPlatform]`, `[RequiresPlayMode]`, `LogAssert`, Play Mode assemblies; never run).
  Zero-loss accounting; exit 1 only for real failures. JSON schema `ucl-test/1` (`schema/test.schema.json`).
  docs/test.md.
* New module `Ucl.Testing` (depends on Core and NUnit).
* Legacy test assemblies (`optionalUnityReferences: ["TestAssemblies"]`) get `UnityEngine.TestRunner`,
  `UnityEditor.TestRunner` and `nunit.framework.dll` implicitly, as in Unity.
* Fixture `test-editmode` (62 fixtures, 153 cells) with the expected outcome of all 31 cases;
  `scripts/bench-test.sh`.

### Changed

* The stub editor fails like the real one outside Unity: native-backed members call an `InternalCall` extern
  (CoreCLR throws `SecurityException`), managed ones (`Vector3`, `Mathf`, attributes) have real bodies.

## [0.5.0]

Session 2, phase A: the findings of the first real-project run (docs/real-project-fixes.md, G1-G5).

### Added

* `ucl bee-diff [<project>]`: compares every compiler command line the Editor wrote under
  `Library/Bee/artifacts/*.dag/*.rsp` with `ucl`'s for the same assembly and cell (sources, references by
  identity, defines, language version, unsafe, nowarn, analyzers, additional files). Text, JSON (new schema
  `ucl-beediff/1`, `schema/beediff.schema.json`) and SARIF; exit 0 agree, 1 differences, 3 no Bee folder.
  New ids `UCL3010` (no Bee folder) and `UCL5001`-`UCL5008` (difference categories). docs/oracle.md, "Bee oracle".
* `ucl check --summary`: text output with only each cell's root failures and result line.
* JSON: skipped assemblies carry `blockedBy`, the failed assemblies they wait on (additive; still `ucl-result/1`).
* Fixtures `api-compat-netfx`, `plugin-native`, `precompiled-reference-absent`, `facade-system-runtime`,
  `realistic-netfx-nuget` (61 fixtures, 151 cells). The stub editor gained a `unity-4.8-api` profile (stub
  `mscorlib`, `Facades/netstandard.dll` 2.0.0.0, `Facades/System.Runtime.dll`) and the NetStandard shims.
* docs/real-project-checklist.md: what a maintainer runs on a real project and how to send the result back.

### Changed

* The API compatibility level is per assembly: Editor-only assemblies follow `editorAssembliesCompatibilityLevel`
  (default: .NET Framework), all others the build target group's level (docs/defines.md, A01-A06). With default
  settings, Editor-only assemblies now get `NET_4_6`/`NET_UNITY_4_8` and compile against `unity-4.8-api`.
* Reference sets per profile follow Unity's: .NET Framework is the 17 listed core libraries of `unity-4.8-api`
  plus every facade; .NET Standard adds `shims/netstandard` and `Extensions/2.0.0`.
* `UCL1004` (a `precompiledReferences` entry that does not exist) is info, not an error: Unity skips such an
  entry without a message (contract change of the id's severity).
* `CS0282` is suppressed on every assembly, as on the Editor's command lines (C09, observed).
* Text output of `ucl check` leads each cell with its root failures; a skipped assembly's reason names the
  failed assembly it waits on (`dependency 'Core' failed (through 'Mid', skipped)`).

### Fixed

* G1: values the Editor wraps onto continuation lines in `ProjectSettings.asset` hid every later key,
  including `apiCompatibilityLevelPerPlatform`, so real projects always got .NET Standard.
* G2: a native (unmanaged) DLL is never passed to the compiler (was CS0009 in every assembly). Detection reads
  the PE CLI header, not the path.
* G3: a missing explicit precompiled reference no longer fails the build.
* G4: DLLs built against `System.Runtime` resolve through the profile facades (was CS0012).

## [0.4.0]

Phase 4: integration and final review. Not 1.0.0 yet: no cell has been confirmed against a real Unity
Editor (the oracle is pending, see docs/status.md).

### Added

* docs/integration.md and examples/: pre-commit hook, GitHub Actions and GitLab CI jobs, AI agent usage.
* README with a 60-second quick start.
* Real-editor test mode: `UCL_REAL_EDITOR=1` with `UNITY_EDITOR_PATH` runs the fixture cells of that
  editor's version against the real install.
* CI `release` job: a pushed `v*` tag builds the four single-file binaries, the global tool package and
  SHA256SUMS and attaches them to a GitHub release.

### Fixed

* Physical paths use native separators throughout on Windows.

## [0.3.0]

### Added

* Incremental cache in `Library/ucl` (or `--cache-dir`, `--no-cache`): one entry per inputs hash, with the
  metadata-only image dependents compile against, so a method-body edit recompiles one assembly. A persisted
  (path, size, mtime) memo avoids rehashing unchanged files.
* `--changed <git-ref>`: report only assemblies whose inputs changed since a ref (committed, staged, unstaged,
  untracked), plus dependents.
* `ucl explain`, `ucl doctor`, `ucl fetch` (npm-protocol registries, scoped registries, SHA-1 checked,
  path-safe extraction), `ucl export-csproj`.
* `scripts/gen-bench.sh` and `scripts/bench.sh` with R11 targets enforced in CI; docs/benchmarks.md.
* Single-file binaries for win-x64, osx-arm64, osx-x64 and linux-x64 (`scripts/build-release.sh`), macOS
  smoke test in CI.

### Changed

* UCL3006 messages name project-relative and symbolic paths only, so reports are identical on every machine.

## [0.2.0]

### Added

* Conformance corpus complete: 56 fixtures, 135 cells, with full define sets per assembly, `problems` for
  configuration cells, and SHA256SUMS.
* `verify/`: an independent define-table and assembly-graph implementation; CI fails on any disagreement.
* `scripts/mutation.sh` (`scripts/check.sh --mutation`): removing `UNITY_ANDROID` must fail fixtures.
* `oracle/`: Unity batch-mode recorders (bash and PowerShell), a log parser tested on hand-written samples,
  and a compare script; docs/oracle.md.
* Analyzer and source generator execution with Unity's scoping, `.editorconfig`/`.globalconfig` and rule
  sets; SARIF 2.1.0 output; player targets and the platform matrix.

### Changed

* Runtime predefined assemblies (`Assembly-CSharp`, `Assembly-CSharp-firstpass`) no longer reference
  Editor-only asmdefs, in editor cells too.
* Exit code 2 is only for warnings promoted by `-warnaserror`/`--warnaserror`; `.editorconfig` or rule-set
  escalations are ordinary errors (exit 1).
* Plugin `defineConstraints` see `UNITY_INCLUDE_TESTS`.

## [0.1.0]

### Added

* `ucl check`: compiles a Unity 6 project's C# with Roslyn in process, without Unity: asmdef and asmref
  assemblies, the four predefined assemblies with Unity's special-folder rules, `.meta` GUID references,
  platform include/exclude, define constraints, version defines, precompiled DLLs with plugin import
  settings, package resolution (embedded, `file:`, `Library/PackageCache`, download cache), built-in
  modules, editor discovery (Unity Hub folders, `UNITY_EDITOR_PATH`, `UCL_EDITOR_ROOTS`, `--editor`).
* The Unity 6 define table (docs/defines.md) for editor and player targets on six platforms, response
  files and `additionalCompilerArguments`, Unity's compiler options (C# 9, CS0169/CS0649 suppression).
* Output: Unity console text, JSON (`schema/result.schema.json`), SARIF 2.1.0; `ucl graph` (text, DOT,
  JSON). Exit codes 0-4.
* Conformance corpus with self-written reference stubs, fixture manifest and SHA256SUMS.
* `scripts/check.sh` gate; CI on Linux and Windows (Git Bash), macOS smoke.
