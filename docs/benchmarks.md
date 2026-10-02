# Benchmarks (R11)

`scripts/bench.sh` generates the benchmark project with `scripts/gen-bench.sh` (not committed) and times
`ucl check` on it. The project: 30 asmdefs (2 embedded packages, 6 Core in a chain, 12 Runtime, 6 Editor-only,
4 test-like Editor-only and not auto-referenced), the 4 predefined assemblies' worth of scripts (Assembly-CSharp
and Assembly-CSharp-Editor), 1,720 C# files, one `RoslynAnalyzer` DLL scoped to `Game.Core.A` and its referrers,
`scriptingDefineSymbols` and a `versionDefines` entry. Compiled against the stub editor (the real editor's
DLLs are larger, so reference loading is somewhat slower; record real-editor numbers with `UCL_EDITOR_ROOTS`
pointing at a real install).

The script fails when a run exceeds its target; CI runs it on the Linux job.

## 2026-10-01, cloud container, 4 cores, Linux, .NET SDK 10.0.401, Release build

| run | wall clock | target |
|---|---|---|
| cold, full editor compile (32 assemblies, 1,720 files) | 9.51 s | 60 s |
| warm, no change | 0.36 s | 3 s |
| warm, no change (again) | 0.34 s | 3 s |
| one file changed (method body, Runtime layer) | 1.84 s | 10 s |
| one file changed (public API, bottom of the graph) | 5.48 s | 10 s |

Why the edits are cheap: the cache key of an assembly includes its dependencies' metadata-only image hash,
not their sources. A method-body edit changes one assembly's sources but not its public surface, so only
that assembly recompiles. A public-API edit recompiles the assembly and its direct referrers; the chain stops
wherever a referrer's own public surface is unchanged (here 8 of 32 assemblies recompiled).

Wall clock includes `dotnet` host start-up (about 0.15 s); the single-file release binary starts slightly
faster.

## `ucl test`

`scripts/bench-test.sh` compares `ucl test` with `dotnet test` on an equivalent csproj; results and method in
[test.md](test.md#benchmark).
