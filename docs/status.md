# Status

Updated at the end of every phase. Spend figures are estimates from token counts (the session has no
billing view); treat them as rough.

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
| `v0.4.0` | head of `claude/bold-ptolemy-f3272x` after the final review | |

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
