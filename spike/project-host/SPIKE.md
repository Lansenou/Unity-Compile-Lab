# Per-project Mono player test host

Verdict: **viable with graphics enabled**, using the requested sampled median
gates. **152/164 runnable sampled cases matched the Editor oracle (92.7%)**,
and median OS-process-start to first test was **2.974 s**, below 5 s.
The same host is **not viable with -nographics** under the parity gate:
102/164, or 62.2%. This is a measured spike, not a production test command.

Measured 2026-10-03 on a private 6000.3 project with about 6000 EditMode tests.
Unity 6000.3.19f1, Development StandaloneWindows64, Mono, managed stripping
disabled, IncludeTestAssemblies enabled, empty bootstrap scene, .NET SDK
10.0.401. The input project was never edited. Only generic sources and aggregate
results are published.

## Cohort and outcomes

The hashed classifier input actually contains 2316 needs-unity cases. The
requested 1829-case cohort is its first two test assemblies, which total 1823
and 6 cases. The other 487 cases were not measured. All 1829 names matched the
supplied Editor oracle by exact full name: 1822 Passed, 7 Skipped, 0 unmatched.

The current project was newer than the classifier and oracle. The externally
compiled assemblies discovered 1485 of the cohort's names. All 344 absent names
map to compilation-excluded current source classes, rather than removed or
renamed cases. There were 0 current-source name mismatches in that cohort.
54 whole source files were excluded because they require Editor APIs, APIs
compiled out of the player runtime, or helpers excluded for those reasons.
No test body was rewritten or mocked.

A deterministic 200-case sample, seed 1003, includes every observed engine
bucket before seeing compilation or execution results. At least one case per
bucket is selected; remaining slots follow proportional allocation. 164 sampled
cases were runnable and 36 were compilation exclusions. In this report,
runnable means compiled, discovered and scheduled by UTF; an intentional Skip
is an outcome and matches only an oracle Skip.

| Mode and scope | Runnable | Pass | Fail | Skip | Crash | Oracle match |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Graphics, sample, each of 3 runs | 164/200 | 151 | 12 | 1 | 0 | 152/164, 92.7% |
| Graphics, full cohort | 1485/1829 | 1361 | 117 | 7 | 0 | 1368/1485, 92.1% |
| Nographics, sample, each of 3 runs | 164/200 | 102 | 62 | 0 | 0 | 102/164, 62.2% |
| Nographics, full cohort | 1485/1829 | 889 | 580 | 16 | 0 | 893/1485, 60.1% |

The ceiling with these unmodified whole-file exclusions is
**1485/1829, or 81.2%** of needs-unity cases. Actual graphics-enabled oracle
coverage is **1368/1829, or 74.8%**. The ceiling is specific to this conservative
compilation procedure, not a claim about what test refactoring could recover.

## Timing

Boot is OS Process.StartTime to the listener's first leaf TestStarted timestamp,
including DLL loading, discovery and any preceding fixture setup. Total is
OS process start to observed process exit. Test interval ends after UTF result
serialization. These timings include reporting overhead and shared-machine load.

| Run | Graphics boot | Graphics total | Nographics boot | Nographics total |
| --- | ---: | ---: | ---: | ---: |
| Sample 1 | 2.974 s | 12.861 s | 3.968 s | 11.150 s |
| Sample 2 | 3.279 s | 13.586 s | 3.893 s | 9.123 s |
| Sample 3 | 2.722 s | 13.765 s | 3.193 s | 7.802 s |
| Median | **2.974 s** | **13.586 s** | **3.893 s** | **9.123 s** |
| Full cohort, one run | 5.546 s | 340.675 s | 2.382 s | 79.247 s |

Median sampled test interval: graphics 9.990 s, nographics 5.112 s.
Full test interval: graphics 318.275 s, nographics 76.668 s.
Both samples were below ten minutes, so all 1829 requested names were submitted
in both modes. The full graphics run's **5.546 s boot misses the 5 s target**:
the sampled median gate passed, but a sub-five-second boot is not guaranteed.

## Host construction and cache

The final cold reproduction copied ProjectSettings, Packages and Assets into
a fresh scratch project outside both repositories. It seeded only resolved
Library/PackageCache, with no imported asset cache or compiled script assemblies.
Machine filesystem, package and compiler caches were warm; other jobs shared
the machine. Hashing and copying took 44.251 s. The cold Editor host build took
**464.216 s** wall time, including first import; Unity's BuildPipeline portion
reported 110.456 s.

The fresh host then reproduced the sampled outcomes exactly: 151 Pass, 12 Fail,
1 matching Skip, 0 crashes. Its boot was 2.986 s and total was 21.616 s.
The test DLL compilation took 18.392 s without an Editor invocation.

Key:
0332afb0ef00bf192719273a7ba22898a6a7ced52443d89a5fa30058d73ca8f9

The key includes Unity version/revision, ProjectSettings tree hash, Assets tree
hash, Packages tree hash, the two bootstrap sources, parent analyzer config,
target/options and protocol version. Fresh key calculation took 10.158 s.
Reuse of the final host with a precomputed matching key took **23.354 ms** and
launched no Editor. A different key was refused before launching an Editor.
This sidecar prototype does not validate player binary integrity or hash
mutable contents of resolved PackageCache separately from package inputs.

The input compiler response file referenced a parent analyzer config. Only the
scratch copy rebased that reference to a copied config. Early attempts also
revealed that UTF player APIs require IncludeTestAssemblies and a non-null
FeatureFlags context. Those exploratory failures are excluded from the final
cold and sample timings. No ignored bootstrap was injected into the input.

## Runner and compilation

ucl check --target player --include-tests --development --backend mono passed:
0 errors, 38 warnings, 42 assemblies. Its platform graph excludes Editor-only
EditMode assemblies, so it cannot itself emit this cohort as player tests.

The generic compiler reads ucl's exported EditMode compiler inputs, retains the
original test defines so assertions are not compiled away, and compiles against
only the built player's Managed DLLs. It removes whole files with compiler
errors, records all diagnostics and exclusions, and compiles retained sources
without the Editor. Supporting fixture components and two EditMode assemblies
were emitted. The two external EditMode DLLs were absent from the built player's
Managed directory; they were loaded from outside the player.

The bootstrap uses UTF's own UnityTestAssemblyBuilder, UnityTestAssemblyRunner,
PlaymodeWorkItemFactory and execution context through reflection. UTF provides
NUnit parameterized cases, fixture setup/teardown, one-time setup/teardown,
coroutines and LogAssert scopes. Internal APIs make this version-sensitive. Player exit 0 means completion;
test failures must be read from the JSON outcome counts.

An ancillary check submitted the primary assembly's 25 UnityTest coroutine
names: 24 compiled and **24/24 passed**, 1 was Editor-source excluded, 0 crashes.
Those cases are outside the 1829-case needs-unity cohort and are not included
in its parity numerator. The coroutine run booted in 7.725 s and exited in
25.942 s. No coroutine adapter or fake LogAssert was used.

## Largest blockers

1. **Nographics state, 475 cases.** Switching the same host and cases to graphics
   restored oracle parity for 475 full-cohort cases, with 0 regressions:
   460 Failed-to-Passed, 12 Skipped-to-Passed, 3 Failed-to-matching-Skipped.
   ComputeBuffer, MaterialPropertyBlock and activeColorSpace dominate this gap.
2. **Editor/player API and helper compilation exclusions, 344 cases.**
   No UnityEditor assemblies were supplied and no excluded file was patched.
3. **Application.dataPath source-file expectations, 89 failed cases in that
   bucket.** A built player's data directory is not the project's Assets tree.

With graphics enabled, the next failure buckets are GameObject create (14)
and LogAssert (5). These include player lifecycle/log differences. Buckets
describe the classifier's first engine requirement, not necessarily the final
failure's cause. Copying settings and assets does not provide Editor semantics.

## Per-bucket graphics results
| Engine bucket | Needs-unity | Sampled | Runnable | Pass | Fail | Skip | Crash | Oracle match |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Application | 42 | 4 | 42 | 42 | 0 | 0 | 0 | 42 |
| Application.dataPath | 138 | 14 | 92 | 3 | 89 | 0 | 0 | 3 |
| Button | 2 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| Camera | 6 | 1 | 6 | 3 | 3 | 0 | 0 | 3 |
| CommandBuffer | 22 | 2 | 22 | 22 | 0 | 0 | 0 | 22 |
| ComputeBuffer | 185 | 19 | 182 | 181 | 1 | 0 | 0 | 181 |
| CreatePrimitive | 34 | 3 | 29 | 29 | 0 | 0 | 0 | 29 |
| Debug.Log | 8 | 1 | 8 | 8 | 0 | 0 | 0 | 8 |
| DownloadHandlerScript | 1 | 1 | 1 | 1 | 0 | 0 | 0 | 1 |
| EditorScreen | 4 | 1 | 4 | 4 | 0 | 0 | 0 | 4 |
| Event | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| FindObjectsByType | 108 | 11 | 81 | 80 | 1 | 0 | 0 | 80 |
| GameObject create | 510 | 55 | 439 | 425 | 14 | 0 | 0 | 425 |
| Input | 1 | 1 | 1 | 1 | 0 | 0 | 0 | 1 |
| JsonUtility | 28 | 3 | 3 | 3 | 0 | 0 | 0 | 3 |
| Label | 5 | 1 | 3 | 3 | 0 | 0 | 0 | 3 |
| LogAssert | 139 | 14 | 128 | 123 | 5 | 0 | 0 | 123 |
| MaterialPropertyBlock | 281 | 30 | 278 | 274 | 0 | 4 | 0 | 278 |
| Mesh | 1 | 1 | 1 | 1 | 0 | 0 | 0 | 1 |
| Networking | 1 | 1 | 1 | 1 | 0 | 0 | 0 | 1 |
| Quaternion | 2 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| Resources | 1 | 1 | 1 | 1 | 0 | 0 | 0 | 1 |
| ResourcesAPIInternal | 14 | 1 | 12 | 12 | 0 | 0 | 0 | 12 |
| ScriptableObject | 4 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| Shader | 2 | 1 | 2 | 2 | 0 | 0 | 0 | 2 |
| SystemInfo | 48 | 5 | 48 | 45 | 0 | 3 | 0 | 48 |
| TestTools | 7 | 1 | 3 | 3 | 0 | 0 | 0 | 3 |
| TextField | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| Time | 7 | 1 | 7 | 7 | 0 | 0 | 0 | 7 |
| UnityEditor | 17 | 1 | 0 | 0 | 0 | 0 | 0 | 0 |
| VisualElement | 117 | 12 | 6 | 6 | 0 | 0 | 0 | 6 |
| activeColorSpace | 31 | 3 | 31 | 31 | 0 | 0 | 0 | 31 |
| unknown | 61 | 6 | 54 | 50 | 4 | 0 | 0 | 50 |
| Total | 1829 | 200 | 1485 | 1361 | 117 | 7 | 0 | 1368 |

Crashes are zero in every bucket. Counts describe the full requested cohort;
Sampled records the fixed pre-execution allocation.

## Reproduce

Requirements: Windows, Git Bash, PowerShell 7, .NET 10, Python 3 and a licensed
Unity Editor with Mono Windows standalone support. Build the lab CLI first.
Keep evidence outside the public repository or under ignored artifacts:
these tools emit private test names, source paths and failure messages.

Use bash spike/project-host/run.sh with these subcommands:

- prepare: classifier text, oracle XML, evidence directory, --cohort-size 1829.
  Omit the cohort limit to prepare all input needs-unity cases.
- stage: -Project, a new -Scratch path, and -Evidence. This copies the project
  and records the key without changing the input.
- build: -Editor, -Project (scratch), -Player (output EXE), -Evidence,
  -CacheKeyFile (the staged cache-key.txt), optional -Label.
  Run the shared machine's Unity capacity guard before every real Editor build.
- key: -Project (input), -Evidence. Recalculate the fingerprint before reuse.
- build with -Reuse: supply the fresh key and existing player. A miss refuses
  reuse; invoke a guarded real build explicitly.
- compile: exported-csproj directory, player Managed directory, private file
  listing selected project filenames, output DLL directory.
- measure: -Player, -AssemblyDirectory, -Assemblies (built-assemblies.txt),
  -Cases (sample-cases.txt or all-cases.txt), -Output and -Runs.
  Add -Graphics for the viable mode; the default is -nographics.
  Use a separate -Output with -Discover -Runs 1 before measurements.
- summarize: evidence directory, --run graphics-all/run-1.json,
  and --label graphics. The output directory conventions match this report.

Export compiler inputs using the lab CLI's export-csproj command with
--target editor --analyzers off. Select the desired EditMode assemblies and
their fixture-component projects; the compiler orders selected dependencies.
Input lists and generated projects remain private. These scripts are explicit
spike steps, not automatic test discovery or a new supported ucl CLI contract.

Only sources are distributed. No player, test DLL, Unity installation files,
project copy, package list, case list or private log is committed. No IL2CPP,
stripped player, other OS, licence changes or player redistribution were tested.

## Repository checks

scripts/check.sh ran. Formatting, warning-free solution and verify builds,
fixture checksums, 636 core tests (96.75% line coverage), and 75 discovery tests
passed. Integration tests: 309 passed, 1 failed, 310 total. The unchanged
version-gated-symbols manifest expects ENABLE_UNITY_COLLECTIONS_CHECKS in a
cell where the implementation omits it. Spike 1 recorded the same baseline
failure. The script exits 1 before combined coverage and independent verify;
this is not a full green gate.

Focused checks passed: compiler build, shell/PowerShell/Python syntax, exact
sample reproduction, unchanged cache-key reproduction, wrong-key refusal,
cold host build, cold-host sample reproduction and actual UTF coroutine run.
