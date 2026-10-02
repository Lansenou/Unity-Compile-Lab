# Status

Updated at the end of every phase. Spend figures are estimates from token counts (the session has no
billing view); treat them as rough.

## Current follow-up (2026-10-02): map-form plugins and test-host resilience

### Item 1: map-form plugin metadata — merged in PR 23

Accept both list-form first/second entries and serializedVersion 3 map-form platformData;
map keys name the platforms. Both shapes share Any Platform exclusions and explicit platform
selection. Auto Reference, Validate References and define constraints retain their existing rules.
Sources: the [Unity plugin inspector manual](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-in-inspector.html)
and the three original repository fixtures encoding the reported map shape.

Synthetic `plugin-map-exclude-editor`, `plugin-map-any` and `plugin-map-editor-only` reuse only
the repository's original Vendor.Math stub. Six editor/player cells cover excluded Editor,
universal inclusion and Editor-only inclusion. Before the fix, six focused unit cases and two
fixture cells failed; after it, all pass. Existing list-form parser/graph tests also pass.
The independently implemented verifier agrees on all 211 cells. No external sample data or
private project identifiers were added. Coverage thresholds remain Core 90% / overall 75%.
Local full gate: 622 Core, 75 Discovery and 297 Integration tests pass; Core line coverage
96.10%, overall 91.82%. [PR 23](https://github.com/Lansenou/Unity-Compile-Lab/pull/23)
is merged only after Linux, Windows and macOS CI pass; its merge commit also runs the full gate
and automatic release on main.
A counts-only private rerun is still needed; this fixes the serialization cause, not an unproven
blanket API-profile-name filter or duplicate-plugin precedence rule.

### Item 2: IL scan load failures and host recovery — open, next session

`IlScanner.Callees` catches invalid/not-supported bodies, but GetMethodBody does not catch
TypeLoadException, FileNotFoundException or FileLoadException. Build an original synthetic
assembly whose method-body type cannot load; first prove the run aborts before the fix. Catch
load failures per scanned method, report method/type/exception message, and complete all cases.
Existing TestHost persists results and restarts after an execution-time crash, but scanning
happens before case-start events: prove a crash during classification can identify its case,
report it as an error and resume the next case. Preserve existing zero-loss accounting tests.
No test-host code changed in item 1.

### Item 3: sortable assembly/analyzer/rule timings — open, next session

Current JSON already contains per-assembly analyzer callback totals (PR 17). Next inspect the
Roslyn timing API to establish whether individual rule timing is available: callbacks can report
multiple rule IDs, so never repeat or divide a shared analyzer total as purported per-rule time.
Add a sortable breakdown with proven attribution, a deliberately slow analyzer fixture, and a
short README example; explicitly document any API limitation. No timing code changed in item 1.

Budget stop: finish item 1 with green Linux/Windows/macOS PR and post-merge main CI, then stop.
Items 2 and 3 remain open as above.

## CI follow-up: Windows coverage collection (2026-10-02) — merged in PR 22

The post-merge main run for PR 21 failed on Windows after all 973 tests passed: merged line
coverage was 52.67%, with compilation and reporting at 0%. Release was skipped. PR checks
had passed; checking PR CI alone did not catch the later main failure.

Public [Coverlet shutdown issue](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/KnownIssues.md#vstest-stops-process-execution-early):
VSTest can kill its host before the MSBuild driver's ProcessExit hit-file writes finish. The
original synthetic CoverageShutdownFixture installs a delayed exit handler before product
modules execute. Before the fix, all integration tests pass but coverage falls to 3.79% and
the unchanged 75% gate fails; with the in-proc collector, the same delayed-shutdown workload
retains 88.82% line coverage. The quality gate always exercises this fixture.

Replace coverlet.msbuild with pinned coverlet.collector; merge collector Cobertura reports
with pinned ReportGenerator. Preserve 90% Core / 75% overall line gates, checked against exact
covered/valid counts. Missing, empty and invalid reports fail closed, including a percentage
that rounds to 75% while remaining below it. Core unit coverage still excludes generated sources; overall collection retains its original
source scope. Test/stub assemblies stay excluded. No production compilation behavior changes. Platform PR CI and the post-merge
main run must both pass before reporting completion. Local gate: Core 614, Discovery 75,
Integration 291, independently verified cells 205; Core line coverage 96.09%, merged 91.81%.

Separate open CI issue: the earlier Linux failure before PR 19's successful rerun was
FetchTests.Missing_package_exit_3: Address already in use during LoopbackRegistry.Dispose.
Its log is now readable. It was not a coverage or analyzer timeout failure. A deterministic
synthetic port-ownership repro is still needed before changing the test server; do not call a
successful rerun a proven fix.

## Session 9 (2026-10-02): plugin exclusions and profile conflicts

Counts-only rerun of v0.8.72 (`07e8093`): 357 errors, 47/79 Editor assemblies skipped;
player 17/32 skipped. Reported failures include CS0433 for memory types, CS0121 for AsSpan,
CS1503 and CS1705 for a 4.2.1.0 dependency versus a referenced 4.2.0.1 copy. Timings from
this run are not comparable because assemblies were skipped; a clean-project rerun is pending.
No private project identity, location or package inventory is recorded.

1. Editor plugin exclusion: fixed in PR 19, merged after green Linux, Windows and macOS CI. Synthetic `plugin-exclude-editor` and parser tests reproduce
   ignored Exclude Editor settings under the Any entry. Sources and importer semantics are in
   docs/architecture.md. The new Any-entry parser case and Editor fixture fail before the fix,
   then pass after parsing exclusions from both supported entries. Validate References does not
   grant compatibility. The ordinary player fixture receives the plugin; profile suppression
   is a separate unresolved rule. Full gate: Core 614, Discovery 75, Integration 284; coverage
   gates and independent verification of 205 cells pass. A two-core run also passes the mutation check.
2. Player API-profile suppression: open after public-rule audit; evidence below. No assembly-name blacklist added.
3. Duplicate dependency selection: open after public-rule audit, related to the Session 8
   precedence question; evidence below. No package-versus-Assets or version selection change
   is inferred from CS1705.

### Item 2: player API-profile plugin suppression — open

[Unity 6000.3 EditorBuildRules](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/EditorBuildRules.cs)
`AddScriptAssemblyReferences` adds selected precompiled references, then appends the API compatibility
profile libraries from `MonoLibraryHelpers.GetSystemLibraryReferences`. `GetPrecompiledReferences`
checks precompiled Editor-only/build-target flags. Neither method exposes a profile-name blacklist
or a rule saying that a user plugin with a matching assembly name must always be discarded.
[MonoLibraryHelpers](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/MonoLibraryHelpers.cs)
selects reference libraries, compatibility shims, extensions and .NET Framework facades; their presence
can explain conflicting memory type definitions, but does not prove plugin suppression. Assembly name,
DLL file name and the assembly that defines/forwards a type are different inputs.

[PrecompiledAssemblyProvider](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/PrecompiledAssembly.cs)
receives its candidate list and `Redirected` flag from native `GetPrecompiledAssembliesManaged`.
Its public filename dictionary retains the first candidate unless that existing candidate is redirected.
The native redirection criteria/profile mapping are not public in this source revision. This is a
possible place to investigate, not a proven explanation for System.Memory suppression.

No public version/profile-specific fixture establishes when a compatible, auto-referenced
System.Memory plugin is omitted versus forwarded or retained, so there is no justified red fixture
for a profile-name filter and no code change. The ordinary-player plugin fixture in item 1 stays
referenced: Exclude Editor alone must not remove a player plugin. Needed: a public minimal project
with original overlapping plugin/profile stubs and recorded Unity compiler references for both
.NET Standard 2.1 and .NET Framework profiles, plus a non-overlapping control; or a public native
redirection implementation/table. The private rerun confirms a difference but cannot supply a new
Unity rule. Maintainer follow-up stays counts/booleans only (API profile, affected cell count, plugin
compatible/auto-reference/constraint result, and whether a profile reference supplies the memory types).
Do not infer this rule from the NuGet installer choosing not to install an already-provided library.

### Item 3: CS1705 and duplicate dependency selection — open

CS1705 establishes an assembly identity/version mismatch; it does not identify why the lower copy
was selected. The reported 4.2.1.0 versus 4.2.0.1 dependency versions cannot prove package precedence,
Assets precedence or a highest-version rule. A file, NuGet package or target-framework version is not
necessarily its CLR assembly version. Import compatibility, constraints, Auto Reference and explicit
precompiledReferences must be considered before comparing candidates. A profile/facade copy may also
participate separately from the project plugin candidates.

The pinned [Unity 6000.3 PrecompiledAssemblyProvider](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/PrecompiledAssembly.cs)
`FilenameToPrecompiledAssembly` iterates the native provider's list: it keeps the first file-name
candidate and replaces it only if the existing candidate has `Redirected` set. It does not read or sort
assembly versions, and does not test package-versus-Assets paths. The native list order and redirected
classification remain unavailable. This public dictionary behavior therefore cannot establish the
winning physical System.Threading.Tasks.Extensions copy in the reported case.

ucl currently picks the highest CLR assembly version among discovered compatible same-file-name
plugins, with lexical path as a tie-breaker, and retains the separate observed untestable-package-test
copy exclusion. `plugin-same-name` records the earlier observed newest-copy case;
`plugin-untestable-tests` records that test-folder case. Neither fixture establishes a general
package-versus-Assets precedence rule, and neither proves this CS1705 cause. If two discovered,
compatible candidates survive those filters and expose 4.2.1.0/4.2.0.1 CLR versions, ucl's current
sort already chooses 4.2.1.0. A lower reference can therefore require investigating filtering,
identity/version reading, or a separate profile reference before changing precedence.

No selector change or claimed CS1705 fix is made, and no red fixture is fabricated with a guessed
winner. Needed public evidence: a minimal package/Assets duplicate project with original plugin
sources, CLR identities and importer settings, recorded compiler reference selection for Editor
and player, and a same-version control (also reverse locations to distinguish version from source).
Counts-only maintainer follow-up can report candidate counts grouped by discovered/managed,
compatible, constraints satisfied, Auto Reference/explicit selection, CLR version and redirected
status, plus whether the selected copy is profile/package/Assets; no paths or inventories are needed.
This is the Session 8 duplicate-precedence question, not a new dependency-version workaround.

## Session 8 (2026-10-02): references and analyzer execution

Item 4a: fixed in PR 13 (merged after Linux, Windows and macOS CI). Reproduced with `package-plugin-auto-reference` (editor and player), using a synthetic package
plugin with the importer settings of the public Collections 2.6.7 `System.IO.Hashing.dll`. The existing
untestable-package filter wrongly removed unique DLLs along with duplicate copies. Narrowed that filter
to duplicate file names; a unique managed plugin is now selected from its own Auto Reference, platform
and define-constraint settings, independently of whether the containing package test asmdef compiles.
Validate References checks dependencies, not compiler visibility. The package test assembly remains
excluded. Existing duplicate-copy behavior is retained; its general precedence rule remains an open
question rather than being inferred from this fix.

Sources: [Unity 6.3 plugin inspector](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-in-inspector.html),
[public Collections 2.6.7 importer](https://github.com/needle-mirror/com.unity.collections/blob/2.6.7/Unity.Collections.Tests/System.IO.Hashing/System.IO.Hashing.dll.meta),
and [UnityCsReference 6000.3](https://github.com/Unity-Technologies/UnityCsReference/tree/2f6cef60096cf50741d933becf101bf3186719dd)
(`EditorBuildRules.AddScriptAssemblyReferences` and `GetPrecompiledReferences`). No package implementation
or Unity binary was copied. Before the fix: the new Core test failed with an empty plugin reference set,
and both fixture cells failed with missing `System.IO.Hashing` compile errors. After the fix: Core 611, Discovery 75, Integration 274 tests pass;
`scripts/check.sh` passes with coverage gates and independent verification of 202 cells. PR merge is
gated on green Linux, Windows and macOS CI. Whether the private reference difference is fully resolved
requires the maintainer's counts-only rerun.

Reference audits merged with green Linux, Windows and macOS CI: PR 14 (NUnit), PR 15 (editor reference
membership), PR 16 (native module exclusions). Their causes remain open with public evidence below;
no speculative reference filters were added. Network access is working.

### Analyzer timing

Merged in PR 17 after green Linux, Windows and macOS CI. `analyzer-slow` and AnalyzerTimingTests: the slow-analyzer JSON test was red before the
change (missing analyzerTimings), then green. `check --format json --timings` reports Roslyn's logged
callback execution time per analyzer type/DLL on each assembly, and totals in cell and run summaries.
These cumulative callback times exclude queueing and source generators; they are not wall-clock time.
Cache hits retain diagnostics but report no current-run analyzer timing. Untimed output stays
byte-identical. A synthetic suppressor test verifies compiler warnings remain suppressible under the
tracked analysis API. The internal compiler-diagnostics adapter is excluded from analyzer totals.
The full gate passed (Core 611, Discovery 75, Integration 277), including coverage and independent
verification of 203 cells.

### Analyzer scheduling

Dependents now start after the producer's compiler-valid reference image is emitted. Source generators
and compiler diagnostics remain before emission. Analyzer completion/timings and cache writes run in a
separate bounded queue; each queue has at most --jobs assemblies. The runner joins all finalizations
before returning, preserves the final late-error dependency cascade and excludes failed/blocked images
from ucl test. Cache v3 retains compiler-valid images for analyzer failures, with checksum verification;
old v2 entries rebuild once. Cached diagnostics still replay identically.

AnalyzerPipelineTests was red before the change for both one and four compile slots: the root analyzer
timed out waiting for the leaf's generator. Both cases now pass. A late root error is preserved across
cold/warm runs and still blocks final dependent results; neither failed nor blocked full images are
returned, on cold or warm runs. On the same controlled 32-assembly/1,720-file benchmark, three-run median
cold wall time fell from 27.67s to 9.76s (64.7%), with the same 8,702 diagnostics and identical diagnostic
hashes in all six runs. Configuration: jobs 4, original 750ms-per-assembly analyzer, stub editor; details
and reproduction in docs/benchmarks.md. The full gate passes (Core 611, Discovery 75, Integration 282),
including coverage and independent verification of 203 cells. Suppressed promoted-warning tests protect
metadata-image behavior and authoritative full-emission errors. R11 passes: cold 8.54s, warm 0.29/0.30s,
body edit 1.76s, API edit 3.60s, and about 100,000 cached warnings 0.60s. Platform CI gates this merge.

Open after these changes: actual NUnit/editor-extra reference causes, the native runtime module table,
the general duplicate-plugin precedence rule, and confirmation of the private counts-only rerun.
No reference parity or private-project speed claim is made for those unproven cases.

### Item 4b: NUnit references — open after public-rule audit

Public evidence: [NUnit package 2.0.5 importer](https://github.com/needle-mirror/com.unity.ext.nunit/blob/2.0.5/net40/unity-custom/nunit.framework.dll.meta)
has Auto Reference on, no define constraints, and Editor/Any Platform compatibility. It is not restricted
to assemblies in testable packages. [Unity 6000.3 TestRunnerHelpers](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/TestRunnerHelpers.cs)
adds NUnit independently of Auto Reference to Editor-only assemblies, or every assembly when
playModeTestRunnerEnabled is set; an explicit overrideReferences/precompiledReferences entry already
supplies it. Legacy optionalUnityReferences TestAssemblies also supplies the explicit NUnit reference.

These rules are already implemented by PluginResolver and AsmdefParser. Existing GraphUnityRulesTests
cover Editor-only, play-mode enabled and legacy test assemblies; GraphPluginTests covers explicit NUnit
and the ordinary Auto Reference path. testables controls package test source assemblies, not an
unconstrained NUnit plugin. noEngineReferences does not suppress user plugin references. No public
fixture reproduces the reported 22 missing references, so no code change or claimed fix is justified.
Next evidence needed: counts of affected assemblies grouped by overrideReferences, an explicit NUnit
entry, Editor-only status and playModeTestRunnerEnabled, plus whether their NUnit plugin is discovered
and compatible. The maintainer can report only counts/booleans; no project names, paths or package lists.
This PR records the unresolved cause instead of manufacturing a failing fixture.

## Session 7 (2026-10-02): the v0.8.50 private rerun

| Item | Fixture | State |
|---|---|---|
| 1 `ucl test` crash (CommandBuffer finalizer) | `test-host-crash` | fixed (PR 8): child test host, results written per case, crash reported, run resumes; cases constructing an engine type with a finalizer are needs-unity |
| 2 editor warm 2.9 s to 7.7 s | bench row "about 100,000 cached warnings" | no regression on the bench (0.8.0 0.62 s, main 0.56 s); warm cost scales with replayed warnings, and the 0.8.0 run compiled far fewer assemblies. Diagnostics now load off the dependency chain (PR 9): 1.53 s to 1.26 s |
| 3 analyzer cost | `analyzer-info-severity` | analyzer scope already matches the dag (bee-diff: no analyzer differences). Fixed a real gap (PR 10): like csc without `-errorlog`, no Info diagnostics and no Info/Hidden-only analyzers |
| 4 reference differences | - | open, see below |
| 5 `Unity.InputSystem.TestFramework` excluded | `tests-framework-symbol` | fixed (PR 11): D61 now gives `UNITY_TESTS_FRAMEWORK` to test framework assemblies only (released Input System asmdefs have no versionDefines) |

Open (item 4), with the evidence found so far:

* Engine modules on runtime assemblies: UnityCsReference `EditorBuildRules.GetUnityReferences` skips modules flagged
  `ExcludedForRuntimeCode` for assemblies that are not Editor-only (the flag is native). Public Unity-generated
  project files: Windows 6000.0 to 6000.3 leave out AMD and NVIDIA (and Hierarchy on 6000.3); WebGL 6000.0.43 leaves
  out AMD, NVIDIA, AR, ClusterInput, ClusterRenderer and VirtualTexturing; the private 6000.3.19 WebGL run adds
  Insights. Audit for item 4d (Session 8):
  [GetUnityReferences in Unity 6000.3](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/EditorBuildRules.cs)
  explicitly excludes ExcludedForRuntimeCode modules for runtime code, including runtime assemblies
  compiled for the Editor, while allowing them for Editor-only code. ucl models enabled built-in
  package names but not this separate native flag. VirtualTexturing, Insights, ClusterRenderer,
  ClusterInput and AR are not represented by matching package names in BuiltInModules.KnownPackages;
  they therefore pass that filter regardless of manifest dependencies. This explains why manifest
  filtering alone cannot reproduce the native exclusions. It does not prove a version/target table:
  public generated project files show different sets, and the public C# source does not expose the
  producer of the native flags. In particular, the 6000.0 WebGL sample does not establish Insights
  membership in 6000.3.19. No global blacklist, deprecation guess or extrapolated table is added.
  Open: obtain a public version/target-specific module flag list or compiler response-file corpus
  sufficient to fixture the table, including both runtime and Editor-only assemblies. Counts-only
  private reruns can confirm a public fixture's result, but are not the source of a new rule.
* `nunit.framework.dll` missing on 22 assemblies: the published `com.unity.ext.nunit` 2.0.5 meta is Auto Reference on
  (`isExplicitlyReferenced: 0`), unlike the fixture's. ucl already references it on runtime assemblies with that meta,
  so the 22 are not explained yet; the maintainer could say what they have in common (overrideReferences,
  noEngineReferences, Editor-only).
* `UnityEditor.Graphs.dll` and the platform `Extensions.dll` extra on 15 assemblies: UnityCsReference adds
  `EditorAssemblyReferences` to every assembly in editor builds, yet generated project files omit them for some
  (Assembly-CSharp, the test runners). Rule not found. Audit for item 4c (Session 8): pinned
  [Unity 6000.3 EditorBuildRules](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/EditorBuildRules.cs)
  adds this list outside the noEngineReferences condition, with no asmdef/predefined/test distinction.
  [ModuleUtils](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Modules/ModuleManager.cs)
  has separate lists for user-script compilation and Editor C# project generation; generated csproj
  membership alone cannot prove which list Bee used. EditorLocator currently gathers all installed
  platform extension files and Graphs without the native provider's membership metadata. The open
  question is which files belong to the user-script list for the selected installation, not a proven
  receiving-assembly predicate. No receiving/non-receiving fixture can be justified yet. Do not
  special-case Assembly-CSharp, test runners, noEngineReferences or active-platform names to hide the
  difference. Next: a public Bee response-file pair and its editor version/target showing the actual
  membership, or a public implementation of the platform provider's reference lists. No code change.
* `System.IO.Hashing.dll` missing on 55 assemblies: needs the package path and .meta settings from the maintainer.

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
