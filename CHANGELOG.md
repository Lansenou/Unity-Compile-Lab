# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [Semantic Versioning](https://semver.org/). Public contracts: the CLI options, exit codes,
the JSON schema id `ucl-result/1`, the graph schema id `ucl-graph/1`, the fixture manifest schema
`ucl-fixtures/1` and the `UCLxxxx` diagnostic ids.

## [Unreleased]

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
