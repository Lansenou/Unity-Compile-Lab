# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [Semantic Versioning](https://semver.org/). Public contracts: the CLI options, exit codes,
the JSON schema id `ucl-result/1`, the graph schema id `ucl-graph/1`, the fixture manifest schema
`ucl-fixtures/1` and the `UCLxxxx` diagnostic ids.

## [Unreleased]

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
