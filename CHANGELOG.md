# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [Semantic Versioning](https://semver.org/). Public contracts: the CLI options, exit codes,
the JSON schema id `ucl-result/1`, the graph schema id `ucl-graph/1`, the fixture manifest schema
`ucl-fixtures/1` and the `UCLxxxx` diagnostic ids.

## [Unreleased]

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
