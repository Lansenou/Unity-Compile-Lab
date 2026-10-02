# Headless Mono player test spike

Verdict: **viable**. The prebuilt Windows Mono player ran 8/8 engine tests from an
assembly unknown at player build time. Median OS-process-start to first test was
**0.568 s**, below 3 s; median observed process exit was **0.775 s**.

Measured 2026-10-03: Unity 6000.3.19f1, Development StandaloneWindows64, Mono,
managed stripping disabled, empty scene, .NET SDK 10.0.401. Tests target
netstandard2.1 with NUnit 3.14.0. No production code or PR.

## Numbers

These are three consecutive corrected measurements. They are warm cache runs
after three exploratory runs, not cold-start promises.

| Run | OS process start to first test | OS process start to observed exit | Passed | Exit code |
| --- | ---: | ---: | ---: | ---: |
| 1 | 490 ms | 716 ms | 8/8 | 0 |
| 2 | 568 ms | 775 ms | 8/8 | 0 |
| 3 | 619 ms | 861 ms | 8/8 | 0 |
| Median | **568 ms** | **775 ms** | **8/8** | **0** |

Successful host build: 35.815 s wall time; Unity reported 26.128 s for
BuildPipeline.BuildPlayer. An earlier compile failed because the minimal manifest
omitted the built-in JSON module; adding it fixed compilation. External test DLL
build: 20.77 s, zero warnings/errors. The Editor was not used to compile or run
the test DLL. No Editor was running at the process inventory during the initial
player measurements; unrelated Editor jobs ran earlier during project setup.

All three initial exploratory runs passed 8/8. Their invocation-to-first-test
times were 3573, 4853, 737 ms and invocation-to-exit times were 6536.828,
5282.946, 969.926 ms. That timing included PowerShell process-launch overhead,
so these are end-to-end exploratory observations, not OS-process-start measurements.
The corrected harness records both origins. The initial variation was not
diagnosed. Do not read the warm median as a guarantee for every cold invocation.

measure.ps1 reads OS Process.StartTime, subtracts it from the host UTC timestamp
captured immediately before the first test, and observes exit immediately after
WaitForExit. Millisecond wall-clock resolution and observer scheduling apply.
Boot includes initialization, DLL loading and reflection discovery. The first
timestamp precedes fixture construction and SetUp; exit includes all tests,
JSON output, cleanup and shutdown. All six runs completed.

## Engine cases

Every corrected run had these results. Every exception field and the fatal field
was the empty string.

| Test | Result | Exception text / observation |
| --- | --- | --- |
| GameObject plus built-in BoxCollider | Pass | Empty |
| AddComponent of DLL-defined MyBehaviour | Pass | Empty; non-null, Number = 42 |
| Debug.Log and Debug.LogError | Pass | Empty; both exact markers captured |
| Quaternion.Euler times Vector3.forward | Pass | Empty; Editor reference below, tolerance 0.00001 |
| Physics.Raycast after Physics.SyncTransforms | Pass | Empty; hit the new BoxCollider |
| CreateInstance of DLL-defined MySo | Pass | Empty; non-null, Number = 42 |
| Texture2D(4,4), SetPixel/GetPixel | Pass | Empty; red pixel round trip |
| Application.dataPath | Pass | Empty; <player>/PlayerHost_Data |

The Editor recorded (0.6123723983764648, -0.4999999403953552,
0.6123724579811096) for Quaternion.Euler(30, 45, 60) * Vector3.forward.
Oracle.cs stores the corresponding single-precision values.

PlayerSpikeTests.dll was absent from both the built player's Managed directory
and its ScriptingAssemblies.json. It was loaded from outside the player with
Assembly.LoadFrom. Both custom engine-derived types worked. The placeholder
assembly/swap fallback was **not run**, because its failure trigger did not occur.
NUnit was a copied host plugin. Attributes are found by full type name; DLL
dependencies can also resolve beside the external test DLL.

## Reproduce locally

Only sources are committed. No player binaries, Unity installation files,
downloaded NUnit DLLs, caches or private logs are committed. Ignored output is
under artifacts/player-spike. From the repository root, with Git Bash,
PowerShell 7, .NET 10 and a licensed Windows Unity Editor with standalone support:

    bash spike/player-test-host/run.sh "<editor>"

The script restores/copies NUnit, builds the host once, compiles the test DLL
against the player's UnityEngine.CoreModule.dll and UnityEngine.PhysicsModule.dll,
then launches three runs. The commands measured were:

    <editor> -batchmode -nographics -quit -projectPath <project> -executeMethod SpikeBuild.Build -playerOutput <player> -oracle <json> -logFile <file>
    <player> -batchmode -nographics -logFile <file> -testAssembly <dll> -results <json> -startUtc <unix-ms>

The Editor build creates the empty scene through Editor APIs. No test DLL is
copied into Assets for this baseline. The stored quaternion oracle is from the
measured Editor version; a reproduced oracle.json lets callers compare it.

The reflection runner covers synchronous parameterless NUnit [Test] methods
and simple SetUp/TearDown. It is not the NUnit engine or Unity Test Runner:
no UnityTest coroutines, async tests, TestCase parameters, fixture inheritance
ordering, or Unity Test Runner log-failure semantics. Debug.LogError deliberately
does not fail the case. Exit zero confirms host completion; callers must inspect
JSON tests/fatal fields for pass/fail. No IL2CPP, other OS or stripped-player tests.

Redistributing player binaries would need Unity licence review. No binaries were
distributed and no licence review or licensing changes were attempted.

## Reproduction and repository checks

The source reproduction script was run end to end. Its three additional runs
passed 8/8 with boot times 1914, 574, 695 ms and observed exit times
2116, 786, 920 ms. Their medians are 695 ms and 920 ms. Across the initial,
corrected and reproduced samples, all nine runs passed all eight cases.
Exact line checks confirmed both logging markers in every captured player log.

The required scripts/check.sh was run. Formatting, warning-free solution/verify
builds, fixture checksums, 636 core tests (96.75% line coverage) and 75 discovery
tests passed. Integration tests: 309 passed, 1 failed, 310 total. The failure is
FixtureTests.Cell_matches_manifest for version-gated-symbols, index 2: expected
ENABLE_UNITY_COLLECTIONS_CHECKS is absent. It arises in the unchanged solution
and fixture sources; only this standalone spike folder is added. The check exits
1 before the later combined coverage and independent-verify stages. Those later
stages were not run. This is not a full green repository gate.
A focused fixture-only reproduction also failed the same case: 210 passed, 1 failed, 211 total. Its raw TRX is retained in ignored artifacts.
