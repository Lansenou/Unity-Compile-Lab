# Status

Updated at the end of every phase. Spend figures are estimates from token counts (the session has no
billing view); treat them as rough.

## Session 6 (2026-10-02): the 0.8.0 private rerun

Input: counts from the 0.8.0 rerun (docs/real-project-fixes.md, "Session 6"). One pull request per item, each
with a fixture that is red on 0.8.0.

| Item | Fixture | State |
|---|---|---|
| 1 analyzer cost on package assemblies | `analyzer-immutable-package` | fixed: immutable package assemblies report only errors and skip analyzers that cannot report one; the analyzer scope itself was already right (bee-diff: no analyzer differences) |
| 2 editor engine module set | `editor-only-disabled-modules` | fixed for editor-only assemblies (all modules, disabled ones too); open: 5 modules the Editor omits on runtime assemblies of a WebGL project (VirtualTexturing, Insights, ClusterRenderer, ClusterInput, AR), rule unknown |
| 3 `UNITY_TESTS_FRAMEWORK` (D61) | `tests-framework-symbol` (corrected) | fixed: D61 removed; the assembly gets the symbol from its own versionDefines (public `Unity.InputSystem.TestFramework` asmdef) |
| 4 same-name DLL from an untestable package's tests | `plugin-untestable-tests` | fixed: such a DLL is no candidate ([REAL] rule); PR 6 was merged on main after the budget stop |

Item 3 was merged (PR 5) before the stop for budget. Unverified: the player speed gain (item 1) and the editor module
set (item 2) on the real project (maintainer rerun).

## Session 5 (2026-10-02): automatic releases

Work now goes through pull requests merged after the Linux, Windows and macOS gates pass; nothing is pushed
to `main` directly. Each push to `main` that passes the gates runs the CI `release` job, which tags and
publishes `v0.<minor>.<run number>` (README, "Releases"; design copied from the PMTiles conformance lab).
The job is idempotent: an existing tag at the same commit is left as is, a tag at another commit fails the
job and is never moved.

The tags `v0.5.0` to `v0.8.0` were made locally in earlier sessions and never pushed (the remote refuses tag
pushes from a session). They are not recreated; the first automatic release supersedes them. The commits
they would have tagged:

| Tag | Commit |
|---|---|
| `v0.5.0` | `f06fd51` |
| `v0.6.0` | `d96c285` |
| `v0.7.0` | `1840155` |
| `v0.8.0` | `2a051a6` |

First automatic release: [`v0.8.39`](https://github.com/Lansenou/Unity-Compile-Lab/releases/tag/v0.8.39),
built from the merge commit `e71fa0b` of pull request 2 by CI run 39 after the three gates passed. Checked
from this session: `sha256sum --check SHA256SUMS` passes for the Linux archive and its `ucl --version` prints
`ucl v0.8.39`.

## Session 4 (2026-10-02): the 0.7.0 private rerun, `0.8.0`

Input: counts from the 0.7.0 rerun (docs/real-project-fixes.md, "Session 4"). Each item gets a synthetic
fixture that is red on 0.7.0, then the fix, one commit each.

| Item | Fixture | State |
|---|---|---|
| 1 editor cells took the platform's engine modules | `editor-engine-modules` | fixed |
| 2 player collections safety: references and defines disagree | `player-collections-checks` | fixed: row E16 |
| 3 `Unity.InputSystem.TestFramework` excluded (`UNITY_TESTS_FRAMEWORK`) | `tests-framework-symbol` | fixed: row D61 (source: the private run only) |
| 4 version-gated built-in symbols, versionDefines | `version-gated-symbols` | fixed: D53 suffixes, E08 split, E17. `UNITY_XR_VISIONOS_SUPPORTED` and the two Input System symbols are not reproduced; possibly the same suffix bug, unverified |
| 5 script-less asmdef compiled | `asmdef-no-scripts` | fixed: skipped, `UCL1006` info |
| 6 speed (analyzer reach) | R11 bench with a global noisy analyzer | improved, not closed: analyzers shared per run, one diagnostic pass (cold 12.3 s to 11.6 s); the 637 s is not reproduced, analyzer cost is linear here. Next: the cold time with `--analyzers off` from the private run |

Unverified: everything against the real editor; the private rerun reports the counts.

Gate (2026-10-02, Linux): `scripts/check.sh`: Core 602, Discovery 75, Integration 261 tests; 73 fixtures, 190
cells; verify 190 cells agree; benchmark (now with a noisy global analyzer) cold 11.59 s, warm 0.56 s, body
edit 2.57 s, API edit 5.65 s (targets 60 / 3 / 10 / 10). CI green on Linux, Windows, macOS for each item
commit (runs 30-35) and for the version commit `2a051a6` (run 36).

Tag: superseded by the first automatic release (Session 5).

Maintainer rerun: docs/real-project-checklist.md, "Private project: counts only", plus the cold player time
with `--analyzers off`. Open: item 6 (637 s first player run) is not reproduced; E08/E17 version bounds come from
one private run; D61 and E16 are ucl rules, not read from Unity source.

## Session 3 (2026-10-02): real-project compile parity, `0.7.0`

Input: the maintainer's counts from a private 6000.3.19f1 project (WebGL active) with 0.6.0: `bee-diff` exit 1
(104 assemblies, 0 agree, 8128 differences), `check` editor exit 1 (1286 errors, 10 failed, 63 skipped),
player exit 1 (1151 errors), `ucl test` 0 cases. Six root causes, all fixed, each with a synthetic fixture that
was red on 0.6.0 (red runs and sources in docs/real-project-fixes.md, "Session 3"):

| Cause | Fixture | State |
|---|---|---|
| 1 uGUI assemblies not auto-referenced | `ugui-auto-reference` | fixed (UnityCsReference `AutoReferencedPackageAssemblies`) |
| 2 ~70 built-in symbols missing | `editor-builtin-defines` (10 cells) | fixed: rows E01-E15, `ENABLE_MONO` in editor cells, D60 a compiler symbol |
| 3 three `Unsafe.dll` copies (CS0433) | `plugin-same-name` | fixed: one DLL per file name, highest version; `UCL1005` info |
| 4 package test assemblies, test framework helper | `package-testables` | fixed: `testables`; test framework assemblies out of players. The `Unity.InputSystem.TestFramework` omission did not reproduce with its public asmdef |
| 5 editor references missing | `editor-reference-set` | fixed: facade, platform module, Graphs, platform extensions, `Unity.CompilationPipeline.Common` |
| 6 analyzers missing | `analyzer-reach` | fixed: Unity's analyzer scope; the editor's own source generators run |

Sources: UnityCsReference (rules), the Unity 6.3 manual (`testables`), and six public Unity-generated `.csproj`
files for the native symbol lists and reference folders (docs/defines.md, [PUB]); nothing from the private project.

Gate (2026-10-02, Linux): `scripts/check.sh --mutation`: Core 595, Discovery 75, Integration 247 tests; 68
fixtures, 176 cells; verify 176 cells agree; benchmark cold 8.63 s, warm 0.41 s, body edit 2.30 s, API edit
4.21 s (targets 60 / 3 / 10 / 10).

### Maintainer: rerun privately, report counts only

docs/real-project-checklist.md, "Private project: counts only":

```sh
ucl bee-diff path/to/Project > bee.txt; echo "exit $?"; tail -2 bee.txt
ucl check path/to/Project --summary > editor.txt; echo "exit $?"; tail -1 editor.txt
ucl check path/to/Project --target player --platform StandaloneWindows64 --summary > player.txt; echo "exit $?"; tail -1 player.txt
ucl test path/to/Project > test.txt; echo "exit $?"; tail -1 test.txt
```

`bee-diff` now ends with `by category: ...` (also `summary.byCategory` in JSON).

### Tag

| Tag | Commit | Note |
|---|---|---|
| `v0.7.0` | `1840155` | session 3; CI run 28 green on Linux, Windows, macOS |

```sh
git tag -a v0.7.0 1840155 -m v0.7.0 && git push origin v0.7.0
```

Not `v1.0.0`.

### Open questions (recommended default first)

1. Player rows of the built-in symbols are inferred (no public player command line): feature and platform
   symbols in players too, editor service symbols not. Recommended: keep until the private rerun's player
   `bee-diff` (a player build's dag) shows otherwise.
2. iOS has no public sample, so iOS cells get only E01-E08. Recommended: add E-rows when an iOS command line
   is available.
3. Which same-name DLL wins is observed once (highest version). Recommended: keep; `UCL1005` names every copy
   left out, so a wrong pick is visible.
4. `Unity.InputSystem.TestFramework`: if still listed by `bee-diff` (`assembly` category), run
   `ucl graph path/to/Project | grep "excluded Unity.InputSystem.TestFramework"` and report only that line.

## Session 2 (2026-10-02): real-project correctness, Bee oracle, `ucl test`

Budget 93 USD (83 for work, 10 reserve). Cumulative phase caps: A 40, B 80, C 83.

| Phase | Cap (USD, cumulative) | Estimated spend (cumulative) | State |
|---|---|---|---|
| A real-project correctness, `ucl bee-diff` | 40 | ~27 | done, `v0.5.0` (`f06fd51`, CI run 25 green on Linux, Windows, macOS) |
| B `ucl test` | 80 | ~45 | done, `v0.6.0` (`d96c285`, CI run 26 green on Linux, Windows, macOS) |
| C `ucl mutate` (proposal only) | 83 | ~47 | done: docs/proposals.md, item 4 |
| final status | (reserve 10) | ~49 | this file |

### Maintainer: what to run on the real machine

docs/real-project-checklist.md, in short (add `--editor "<install>"` if `doctor` does not find the editor):

```sh
ucl doctor   path/to/Project > ucl-doctor.txt
ucl bee-diff path/to/Project > ucl-bee-diff.txt
ucl bee-diff path/to/Project --format json -o ucl-bee-diff.json
ucl check    path/to/Project --summary > ucl-check-editor-summary.txt
ucl check    path/to/Project --target player --platform StandaloneWindows64 --summary > ucl-check-player-summary.txt
ucl test     path/to/Project --format json -o ucl-test.json
ucl test     path/to/Project --emit-unity-filter ucl-unity-filter.txt > ucl-test.txt
```

### Phase A

| Finding | State | Fixture | Proof |
|---|---|---|---|
| G1 API compatibility level ignored | fixed: wrapped YAML lines lost every later key; level is per assembly (Editor-only assemblies follow `editorAssembliesCompatibilityLevel`); Unity's reference sets | `api-compat-netfx`, `realistic-netfx-nuget` | red run in docs/real-project-fixes.md; rows A01-A06 tested; verify agrees |
| G2 native plugin passed as reference | fixed: PE CLI header decides | `plugin-native` | red run; `PluginBinaryTests` |
| G3 missing precompiled reference was an error | fixed: `UCL1004` is info, as Unity is silent | `precompiled-reference-absent`, `override-references-missing` | red run; `GraphPluginTests` |
| G4 facade gap (CS0012 System.Runtime) | fixed: profile facades and NetStandard shims | `facade-system-runtime` | red run |
| G5 cascades | report improved (root failures first, `blockedBy`, `--summary`); behaviour unchanged | built in the test | `CascadeReportTests` |

`ucl bee-diff` is built, documented (docs/oracle.md, "Bee oracle") and tested on hand-written response files.
What the stubs cannot show and `bee-diff` on the real project will: whether the Editor's reference lists match
`ucl`'s (the 125 `unity-4.8-api` references, the module DLLs, where `Unity.IL2CPP.dll` comes from) and Unity's
16 analyzers (its own source generators, which `ucl` does not run: proposal 1).

### Phase B: `ucl test` on the fixtures

Fixture `test-editmode` (3 test assemblies, 31 cases): 19 passed, 1 failed (the deliberate real failure),
2 skipped (explicit, inconclusive), 1 ignored, 2 needs-unity (`new GameObject`, `Debug.Log`), 6 unity-only
(`[UnityTest]`, `LogAssert`, `[UnityPlatform]`, `[RequiresPlayMode]`, 2 in the Play Mode assembly); exit 1.
Every case is listed in `fixtures/manifest.json` and compared exactly. Benchmark (docs/test.md): `ucl test`
1.68 s cold, 0.42 s warm; `dotnet test` on an equivalent csproj 1.93 s, and it reports the 6 Unity-dependent
cases as failures.

### Gate (2026-10-02, Linux)

`scripts/check.sh --mutation`: Core 568, Discovery 72, Integration 224 tests passed; line coverage Core 96.5%,
overall 92.7%; 62 fixtures, 153 cells; verify 153 cells agree; mutation killed; R11 benchmark within targets;
release single-file binary smoke test (now including `ucl test`) passed.

### Tags (push refused in this environment; the owner runs these)

| Tag | Commit | Note |
|---|---|---|
| `v0.5.0` | `f06fd51` | phase A (local tag created) |
| `v0.6.0` | `d96c285` | phase B (local tag created) |

```sh
git tag -a v0.5.0 f06fd51 -m v0.5.0 && git push origin v0.5.0
git tag -a v0.6.0 d96c285 -m v0.6.0 && git push origin v0.6.0
```

Not `v1.0.0`: that waits for the real-project `bee-diff` to agree and for the oracle recording.

### Open questions (recommended default first)

1. Unity's own source generators (`Editor/Data/Tools/Unity.SourceGenerators`) appear on every Bee command line.
   Run them in `check` and `test`? Recommended: yes, once `bee-diff` on the real project lists exactly which
   (proposal 1); until then `bee-diff` reports them as the one known difference.
2. Should `--emit-unity-filter` leave out classes with known CoreCLR/Mono divergences? Recommended: no automatic
   rule (it cannot know); document (done) and let teams mark such classes with `[UnityPlatform]` or move them.
3. The Unity Test Framework's `-testFilter` with a list of `!` patterns: confirm on the real Editor that it
   means "none of these" before relying on it. Recommended: check once with the fixture's filter.
4. `ucl test` loads the real `UnityEngine*.dll` into CoreCLR. Untested here (no Unity in CI): engine static
   constructors may throw on load, which `ucl` classifies needs-unity. Recommended: run `ucl test` in the
   checklist and send `ucl-test.json`.
5. `UCL1004` is now info. Recommended: keep; if the maintainer wants silence, add `--hide-info` later.
6. Test case counting uses NUnit 3.14, the Editor NUnit 3.5 with the Test Framework's builders. Recommended:
   compare totals once on the real project (the Test Runner window shows them).

# Session 1

## Phase reached: 4 (final review), released as `v0.4.0`, not `v1.0.0`

Every requirement R1 to R13 is implemented and tested and every check is green, but none of the 135 fixture
cells has been confirmed against a real Unity 6 Editor yet, and `ucl` has never compiled against a real
editor install (only against the self-written stubs). A 1.0 that claims "exactly like Unity" should wait for
that one run; see "Next steps for a maintainer". Recommended: tag `v1.0.0` after the oracle run agrees.

| Phase | Cap (USD, cumulative) | Estimated spend (cumulative) | State |
|---|---|---|---|
| 0 design | 5 | ~3 | done |
| 1 core, discovery, compile, 15 fixtures | 45 | ~25 | done (`v0.1.0`) |
| 2 corpus, verify, oracle, analyzers, SARIF, matrix | 75 | ~50 | done (`v0.2.0`) |
| 3 cache, benchmarks, extras, binaries | 95 | ~60 | done (`v0.3.0`; built in parallel with phase 2) |
| 4 integration docs, final review | 110 | ~70 | done (`v0.4.0`) |

## Tags

This session's git proxy accepts pushes to the working branch only; tag pushes are refused (HTTP 403). Tags
exist locally only. The owner creates them; a pushed `v*` tag runs the CI `release` job, which builds the
binaries and attaches them to a GitHub release.

| Tag | Commit | Note |
|---|---|---|
| `v0.1.0` | `508f36f` | phase 1 |
| `v0.2.0` | `29ef819` | phases 2 and 3 were developed in parallel and completed together |
| `v0.3.0` | `29ef819` | same commit as `v0.2.0` |
| `v0.4.0` | `3c49b99` | final review; CI green on Linux, Windows and macOS (run 17) |

```sh
git tag -a v0.4.0 <commit> -m v0.4.0 && git push origin v0.4.0
```

## Requirements

| Req | Status | Proof |
|---|---|---|
| R1 project discovery, version check | done | `ProjectLoaderTests`, `CliTests`, fixture `unsupported-version` (UCL3002) |
| R2 assembly graph | done | `Graph*Tests` (Core), fixtures 1-15, 19-23, 39-47, 54-56 |
| R3 packages | done | `PackageResolutionTests`, `PackageFetcherTests`, `FetchTests`, fixtures 16, 17, 32-36, 47 |
| R4 editor references | done against stub installs; real install not yet exercised | `EditorLocatorTests`, every fixture cell; real-editor mode `UCL_REAL_EDITOR=1` wired (`RealEditor.cs`) |
| R5 defines | done | `DefineTableTests` (one test per row of docs/defines.md), full define sets per assembly in all 135 manifest cells, `verify/` agreement |
| R6 compiler settings | done | `GraphOptionsTests`, `RspParserTests`, fixtures 22-27, 38, 51, 53 |
| R7 editor/player, matrix | done | fixture cells over 6 platforms and 2 versions, `GraphPlatformTests` |
| R8 analyzers | done | fixtures 28, 29, 42; analyzer diagnostics carry `origin: analyzer` and a separate text section |
| R9 output | done | `DeterminismTests` (text, JSON, SARIF byte-identical), `schema/result.schema.json` |
| R10 exit codes | done | `ExitCodesTests`, `CliTests`, fixtures with exit 1, 2 and 3 |
| R11 speed, incremental cache | done | `CacheTests`; `scripts/bench.sh` in CI enforces the targets (docs/benchmarks.md) |
| R12 graph, explain, export-csproj, fetch, doctor, --changed | done | `ExplainTests`, `ExportCsprojTests`, `FetchTests`, `DoctorTests`, `ChangedTests`, `ReadOnlyTests` (graph) |
| R13 read-only | done | `ReadOnlyTests` (hashes every fixture copy before and after), `PhysicalFileSystemTests` (write guard) |

## scripts/check.sh (2026-10-01, Linux, `--mutation`)

* format, build with warnings as errors, fixture SHA256SUMS: pass
* Ucl.Core.Tests: 514 passed; Ucl.Core line coverage 99.9% (gate 90%)
* Ucl.Discovery.Tests: 64 passed
* Ucl.Integration.Tests: 188 passed (135 fixture cells, cache, changed, explain, fetch, doctor, export,
  read-only, determinism, oracle parser, architecture, CLI)
* overall line coverage 91.2% (gate 75%)
* verify/: 135 cells agree
* mutation check: killed (removing `UNITY_ANDROID` fails 12 fixture tests)
* CI: Linux (full gate, mutation, benchmark, release-binary smoke), Windows (Git Bash, full gate), macOS
  (single-file binary smoke).

## Benchmarks (4 cores, Linux, stub editor)

Cold full editor compile 9.5 s (target 60), warm 0.35 s (3), one-file body edit 1.8 s (10), one-file API
edit at the bottom of the graph 5.5 s (10). Details in docs/benchmarks.md.

## Open questions for the owner (with recommended default)

1. Release `v1.0.0` before a real-editor run? Recommended: no; record the oracle first (one evening on a
   licensed machine), then tag 1.0.
2. Observed define rows (D05 historical series, D12, D20 `PLATFORM_*`, D34 `NET_UNITY_4_8`, D38 `TRACE`,
   D60, backend define in editor cells) are from Unity 6 Bee command lines, not the manual. Recommended: keep
   until the oracle confirms or corrects them.
3. Analyzer scope is "the owning asmdef and its direct referrers", per the manual's wording. Recommended:
   keep; the oracle fixture `analyzer-scoped` settles transitive referrers.
4. Unity's built-in source generators (`Editor/Data/Tools/Unity.SourceGenerators`) are not run.
   Recommended: add them (docs/proposals.md, item 1).
5. NuGet publishing: no API key was provided, so releases attach the `.nupkg` to the GitHub release only.
   Recommended: add a `NUGET_API_KEY` secret and a publish step to the `release` job.
6. Package id `UnityCompileLab.Tool`. Recommended: keep.

## Next steps for a maintainer with a licensed Unity 6 install

1. Record the oracle: `oracle/record.sh <path to Unity> 6000.0.30f1` (and `6000.3.2f1`), then
   `oracle/compare.sh`; commit `oracle/results/` and flip manifest cells (docs/oracle.md).
2. Run the real-editor mode: `UCL_REAL_EDITOR=1 UNITY_EDITOR_PATH=<install> dotnet test tests/Ucl.Integration.Tests`.
3. Run `ucl check` and `scripts/bench.sh` with `UCL_EDITOR_ROOTS` pointing at real installs, and record
   real-editor benchmark numbers in docs/benchmarks.md.
4. Resolve any disagreements per docs/oracle.md, then tag `v1.0.0`.
