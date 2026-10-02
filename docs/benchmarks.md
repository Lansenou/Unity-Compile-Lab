# Benchmarks (R11)

`scripts/bench.sh` generates the benchmark project with `scripts/gen-bench.sh` (not committed) and times
`ucl check` on it. The project: 30 asmdefs (2 embedded packages, 6 Core in a chain, 12 Runtime, 6 Editor-only,
4 test-like Editor-only and not auto-referenced), the 4 predefined assemblies' worth of scripts (Assembly-CSharp
and Assembly-CSharp-Editor), 1,720 C# files, one `RoslynAnalyzer` DLL scoped to `Game.Core.A` and its referrers,
`scriptingDefineSymbols` and a `versionDefines` entry. Compiled against the stub editor (the real editor's
DLLs are larger, so reference loading is somewhat slower; record real-editor numbers with `UCL_EDITOR_ROOTS`
pointing at a real install).

The script fails when a run exceeds its target; CI runs it on the Linux job.

## 2026-10-02: analyzers off the dependency path

Linux, 5 available cores, --jobs 4, .NET 10.0.100, Release, stub editor. The same generated 32-assembly,
1,720-file benchmark includes the existing noisy analyzer and an additional original Ucl.Fixture.Slow
analyzer configured to sleep 750ms per assembly callback. Both versions use the exact same project and
analyzer DLLs. Baseline: per-analyzer timing implementation (`71cb9c9`), still waiting for each assembly's
analysis before starting dependents. Three fresh-process --no-cache runs per version:

| pipeline | samples (s) | median (s) | diagnostics |
|---|---|---|---|
| analysis on dependency path | 27.45, 27.68, 27.67 | 27.67 | 8,702 |
| dependents start after image emission | 9.76, 9.93, 9.14 | 9.76 | 8,702 |

Median reduction: 64.7%. The sorted diagnostic JSON arrays have the same SHA-256 hash in all six runs;
not just the same count. This measures a controlled slow-analyzer workload, not a claim about every
project. The ordinary R11 benchmark is validated separately.

To reproduce the controlled workload after building the CLI and stub DLLs:

```sh
work=$(mktemp -d)
scripts/gen-bench.sh "$work/project"
cp artifacts/stubs/dlls/Ucl.Fixture.Slow.dll "$work/project/Assets/Plugins/Analyzers/Slow.dll"
cp fixtures/analyzer-slow/Assets/Analyzers/Ucl.Fixture.Slow.dll.meta "$work/project/Assets/Plugins/Analyzers/Slow.dll.meta"
printf 'is_global = true\nucl_fixture.delay_ms = 750\n' > "$work/project/.globalconfig"
export UCL_EDITOR_ROOTS="$PWD/artifacts/stubs/editors"
time dotnet src/Ucl.Cli/bin/Release/net10.0/ucl.dll check "$work/project" --no-cache --jobs 4 --editor-os linux --format json --timings > "$work/result.json"
```

Run three times per CLI version against the same prepared project; compare only the diagnostic arrays,
which exclude measured timings and version metadata. AnalyzerPipelineTests separately proves dependency
progress by signaling, with one/four compile slots; it does not assert a wall-clock speed threshold.

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
