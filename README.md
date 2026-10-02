# Unity Compile Lab (`ucl`)

`ucl` compiles the C# of a Unity 6 project in seconds, **without opening Unity**: Roslyn in process, against
your installed editor's DLLs, with Unity's own rules for assemblies, defines, platforms, packages, plugins,
response files and analyzers. Use Unity to check behaviour; use `ucl` to find compile errors.

```
$ ucl check .
== 6000.0.30f1 editor StandaloneWindows64
Assets/Scripts/Player.cs(9,9): error CS0103: The name 'transfrom' does not exist in the current context
result: 1 error, 0 warnings, 1 assembly (0 skipped), exit 1
exit 1
```

It is built for AI coding agents and CI first (deterministic JSON and SARIF, documented exit codes, no
prompts), then for developers who want a pre-commit check, and for package authors who want to compile for
every platform without a Unity install per platform.

Status: see [docs/status.md](docs/status.md). Every rule is implemented from Unity's documentation and
tested against a 62-project conformance corpus (153 matrix cells); recording those cells with a real,
licensed Unity Editor (the [oracle](docs/oracle.md)) is still pending, so rows marked "observed" in
[docs/defines.md](docs/defines.md) are not yet confirmed by Unity itself.

## Quick start (60 seconds)

Requires a Unity 6 (6000.x) editor installed on the machine. Unity is never started and needs no licence
activation; `ucl` only reads its managed DLLs.

```sh
# 1. Install (.NET 10 SDK), or download a single-file binary from the GitHub release.
dotnet tool install --global UnityCompileLab.Tool

# 2. Check that ucl finds your editor and packages.
ucl doctor path/to/MyUnityProject

# 3. Compile everything the Editor compiles.
ucl check path/to/MyUnityProject

# 4. Compile a player build for several platforms, as JSON.
ucl check path/to/MyUnityProject --target player --platform Android --platform iOS --format json
```

If the editor is not in a Unity Hub default folder, pass `--editor <install folder>` or set
`UNITY_EDITOR_PATH`. If the project was never opened in Unity (no `Library/PackageCache`), run
`ucl fetch <project>` once to download its registry packages.

## Commands

| Command | What it does |
|---|---|
| `ucl check [<project>]` | Compile every assembly of every matrix cell and report diagnostics. |
| `ucl graph [<project>] --format text\|dot\|json` | The assembly graph per cell (needs no editor). |
| `ucl explain <file.cs>` | Which assembly owns a file, why, with which defines (each with its rule) and references. |
| `ucl fetch [<project>]` | Fill the package download cache from the project's registries, for offline use. |
| `ucl doctor [<project>]` | Editors found, packages, cache, git: what is missing to check this project. |
| `ucl test [<project>]` | Run EditMode tests under .NET; every case passed, failed, skipped, ignored, needs-unity or unity-only ([docs/test.md](docs/test.md)). |
| `ucl bee-diff [<project>]` | Compare `ucl`'s compiler inputs with the Editor's own command lines in `Library/Bee` (the [Bee oracle](docs/oracle.md#bee-oracle-ucl-bee-diff)). |
| `ucl export-csproj [<project>] --out <dir>` | IDE-style `.csproj` files (optional; `ucl` itself never uses them). |

Matrix options, repeatable (every combination is one cell): `--unity-version <6000.x.y>`,
`--target editor|player`, `--platform StandaloneWindows64|StandaloneOSX|StandaloneLinux64|iOS|Android|WebGL`.
Other options: `--editor`, `--editor-os`, `--backend mono|il2cpp`, `--development`, `--include-tests`,
`--analyzers on|off`, `--warnaserror`, `--format text|json|sarif`, `--output`, `--cache-dir`, `--no-cache`,
`--changed <git-ref>`, `--timings`, `--jobs`. `ucl --help` has the full list.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Clean (warnings allowed). |
| 1 | Compile, analyzer or Unity-rule errors. |
| 2 | Only warnings promoted to errors (`-warnaserror` in `csc.rsp`, or `--warnaserror`). |
| 3 | Configuration problem: missing editor, unresolved package, bad asmdef, unsupported Unity version, bad arguments. |
| 4 | Internal error (a bug in `ucl`). |

## What it models

* Assemblies: `.asmdef` (every field: references by name and `GUID:`, include and exclude platforms,
  `allowUnsafeCode`, `overrideReferences` with `precompiledReferences`, `autoReferenced`,
  `defineConstraints`, `versionDefines`, `noEngineReferences`), `.asmref`, the four predefined
  `Assembly-CSharp*` assemblies with the `Plugins`, `Standard Assets` and `Editor` folder rules, cycles,
  hidden folders (`Samples~`, `.git`).
* Defines: the full Unity 6 table for editor and player targets on six platforms, scripting backends,
  API compatibility levels, `scriptingDefineSymbols`, `additionalCompilerArguments`, `csc.rsp`,
  `versionDefines` against resolved package versions. Every row is documented with its source in
  [docs/defines.md](docs/defines.md) and tested.
* Packages: `manifest.json`, `packages-lock.json`, scoped registries, embedded, `file:`,
  `Library/PackageCache`, the `ucl fetch` cache, built-in `com.unity.modules.*` mapped to the editor's
  module DLLs.
* Plugins: `.meta` import settings (Any Platform, per-platform, Auto Reference, define constraints) and the
  `RoslynAnalyzer` label: analyzers and source generators run with Unity's scoping and are reported
  separately.
* Compiler: C# 9, Unity's suppressed warnings, unsafe code, `csc.rsp` options, `.editorconfig` and
  rule sets.

Details: [docs/architecture.md](docs/architecture.md), [docs/platforms.md](docs/platforms.md).

## Speed

An incremental cache keyed by content hashes (sources, references' public surface, defines, options) lives
in `Library/ucl`. On a generated 30-asmdef, 1,720-file project on 4 cores: cold 9.5 s, warm 0.35 s, one
changed file 1.8 s. See [docs/benchmarks.md](docs/benchmarks.md).

## Read-only

`ucl` never writes inside `Assets/`, `Packages/` or `ProjectSettings/`, and in `Library/` only inside
`Library/ucl`. A test hashes every fixture before and after a run.

## Integration

Pre-commit hook, GitHub Actions, GitLab CI and AI agent usage: [docs/integration.md](docs/integration.md) and
[examples/](examples).

## How it is verified

* `tests/Ucl.Core.Tests`: unit and property tests of every rule (one test per define-table row).
* `fixtures/`: 56 minimal Unity 6 projects, one rule each, with `manifest.json` (expected assemblies, full
  define sets and exact diagnostics per cell) and `SHA256SUMS`. They compile against self-written reference
  stubs (`fixtures/_stubs/`), because Unity's DLLs may not be redistributed ([docs/licensing.md](docs/licensing.md)).
* `verify/`: a second, independent implementation of the define table and the assembly graph; CI fails if
  it disagrees with `ucl` on any cell.
* `scripts/check.sh --mutation`: removing one platform define from `ucl` must make fixtures fail.
* `oracle/`: scripts that record what a real Unity Editor reports for each cell ([docs/oracle.md](docs/oracle.md)).

## Development

```sh
scripts/check.sh            # the gate CI runs: format, build, tests, coverage, fixtures, verify
scripts/check.sh --mutation # plus the mutation check
scripts/bench.sh            # R11 benchmark
```

Requires the .NET 10 SDK and bash (Git Bash on Windows).

## Licence

Apache-2.0, see [LICENSE](LICENSE) and [NOTICE](NOTICE). Third-party components:
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Unity is a trademark of Unity Technologies; this project is
not affiliated with Unity and contains no Unity software.
