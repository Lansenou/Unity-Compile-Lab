# Status

Updated at the end of every phase. Spend figures are estimates from token counts (the session has no
billing view); treat them as upper-bound guesses.

## Phase reached: 1 (v0.1.0)

| Phase | Cap (USD, cumulative) | Estimated spend | State |
|---|---|---|---|
| 0 design | 5 | ~3 | done |
| 1 core, discovery, compile, 15 fixtures | 45 | ~25 | done, tagged `v0.1.0` |
| 2 corpus, verify, oracle, analyzers, SARIF, matrix | 75 | | not started |
| 3 cache, benchmarks, extras | 95 | | not started |
| 4 integration docs, final review | 110 | | not started |

## Requirements

| Req | Status | Proof |
|---|---|---|
| R1 project discovery, version check | done | `ProjectLoaderTests`, `CliTests.Not_a_project_exit_3`; non-6000 gate in `Session.Open` (fixture `unsupported-version` in phase 2) |
| R2 assembly graph | done (rules), partial (fixtures: 11 of the R2 fixtures) | `Graph*Tests` (Core), fixtures 1-6, 9, 10, 13, 14, 21 |
| R3 packages | done (resolution), partial (no package fixtures yet) | `PackageResolutionTests` |
| R4 editor references | done | `EditorLocatorTests`, every fixture cell (stub editors) |
| R5 defines | done | `DefineTableTests` (one test per row id), fixtures 13, 14, 24, 37 |
| R6 compiler settings | done | `GraphOptionsTests`, `RspParserTests`, fixtures 23, 38 |
| R7 editor/player targets, matrix | done (single process, cells per target x platform x version) | fixture cells, `GraphPlatformTests` |
| R8 analyzers | partial: implemented, not yet covered by fixtures | phase 2 |
| R9 output | done: text, JSON (schema), SARIF | `DeterminismTests`, `FixtureTests` |
| R10 exit codes | done | `ExitCodesTests`, `CliTests` |
| R11 speed / incremental cache | not started (inputs hash computed, no cache) | phase 3 |
| R12 extras | partial: `graph` done; `explain`, `export-csproj`, `fetch`, `doctor`, `--changed` not started | phase 3 |
| R13 read-only | done | `ReadOnlyTests`, `PhysicalFileSystemTests` (write guard) |

## scripts/check.sh (2026-10-01, Linux)

* format, build with warnings as errors, SHA256SUMS: pass
* Ucl.Core.Tests: 513 passed; Ucl.Core line coverage 99.8% (gate 90%)
* Ucl.Discovery.Tests: 52 passed
* Ucl.Integration.Tests: 59 passed (33 fixture cells, read-only, determinism, architecture, CLI)
* overall line coverage 86.9% (gate 75%)
* mutation check, verify/: not yet (phase 2)
* release binary linux-x64 single file: `scripts/smoke.sh` passes

## Benchmarks

Not measured yet (phase 3).

## Open questions for the owner (with recommended default)

1. Does Assembly-CSharp in editor cells reference auto-referenced Editor-only asmdefs? Implemented: yes.
   Recommended: keep until the oracle says otherwise.
2. `ENABLE_IL2CPP` in editor cells when the active platform uses IL2CPP: implemented as yes (observed
   row). Recommended: confirm with the oracle.
3. NuGet package id for the global tool: `UnityCompileLab.Tool`. Recommended: keep, unless you prefer a
   different id; publishing needs a NuGet key (not provided), so releases attach artifacts only.

## For a maintainer with a licensed Unity 6 install

* Phase 2 adds `oracle/record.sh`; until then nothing to do.
