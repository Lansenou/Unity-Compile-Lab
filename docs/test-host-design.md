# Project player test host design

The optional `ucl test --host` path keeps managed cases on the existing child .NET runner and sends
eligible engine cases to a per-project Windows Mono player. Editor API and source-layout dependent
cases retain an explicit Editor exclusion reason. Graphics are enabled unless `--nographics` is
requested.

Two approaches were considered. Reusing an arbitrary prebuilt player is small but loses project
settings, resources and package identity. Building a keyed scratch copy preserves these inputs and
is the selected approach. The input project is read-only; the user-level cache owns scratch projects
and players.

The key includes the Unity version and revision, settings, assets, package inputs and resolved
package contents, inherited analyzer configuration, bootstrap sources, target, build options and
protocol. A separate manifest hashes every player output file and invalidates corrupt or incomplete
entries. Each build and run has a unique directory, and cache publication occurs only after a
successful build and manifest validation.

The first supported adapter pins Unity 6000.3.19f1 and the measured Unity Test Framework internal
API shape. Unsupported versions fail before executing cases. The production command emits existing
ucl summaries and NUnit-compatible XML, preserves case accounting, and returns failure for failed
cases or host infrastructure errors. Filters retain the existing regular-expression contract, with
full class and namespace names selecting their cases.

Measurements must compare the same source revision with fresh Editor results. Outcome mismatches
remain visible and must move to Editor ownership before a gate can claim parity. Cold build cost,
warm fingerprint cost and simultaneous-run behavior are reported separately. A previous spike timing
is not a production benchmark.

Audited Editor ownership is an explicit pre-execution input to the managed host. It retains
zero-loss discovery and does not rewrite any completed managed failure. The optional public
`TestHost.Run` and `NUnitHost.Run` input is backward compatible for existing callers.

## Source context ownership correction

Language: ApiDesignLanguage sections 1 (consistent concepts), 4 (member grammar), 7 (failure
handling), 13 (review) apply. Existing compiler and reporting vocabulary is retained. No new types,
CLI options, Unity discovery rules or compiler rules are introduced.

The player adapter must retain Editor ownership for source-layout dependent helpers
and inherited fixtures in recompiled source files, including aliases of
`UnityEngine.Application.dataPath`. Reused player and precompiled helper bodies are not scanned;
source-context cases using those helpers require explicit audited Editor ownership.
Matching only a leaf class name against source text misses shared bases and aliases.

Candidate A extends the CLI text matcher with inheritance and namespace matching.
It retains two independent interpretations of C# and still misses helper references.
Candidate B uses the player-test compiler's existing whole-file exclusions, with
Roslyn symbol resolution identifying `UnityEngine.Application.dataPath` references.
The compiler removes those files before emission and its ordinary diagnostic cascade
excludes diagnostic-reachable callers in the recompiled source graph. Candidate B is selected
because it preserves a single
compiler interpretation and existing exclusions without editing input sources.
This is conservative: even a legitimate player data-directory test remains Editor-owned.
The CLI Application.dataPath substring heuristic is retired: only compiler exclusions establish
source-context ownership. Comments, string literals, inactive code and unrelated types do not
establish ownership.

`TestCommand` still compiles the Editor graph and obtains the managed outcomes first.
`ProjectPlayerTest` chooses engine candidates, `ProjectPlayerCache` validates or builds
the keyed player, and `PlayerTestCompiler.Compile` owns mutable syntax trees and output
DLLs in the cache. Source-context files enter the exclusion dictionary before emission;
ordinary compiler diagnostics account for references that cannot survive that removal.
`ProjectPlayerTest.SourceReasons` assigns excluded fixture files to `needs-editor`.
Native UTF processes execute the remaining exact names, and `TestReport` retains
their XML/JSON routing and the existing final failure policy. Completed failures
are never changed by this correction. The compiler cache key includes adapter identity,
so changing this implementation invalidates old test DLLs. Player integrity remains
independent. No new process, resource or cancellation lifetime is introduced.

A maintainer can add another measured source-context exclusion in
`PlayerTestCompiler.cs`, preserving the public `Compile` input/output contract and
the public-surface regression in `PlayerHostContractTests.cs`. A replacement must
retain unchanged input bytes, whole-file diagnostic propagation, portable controls,
cache invalidation and explicit Editor ownership. Native parity measurements remain
required for rollout; this source correction alone does not establish a speedup.

The retained boundary hides the decision about which Editor source files can be
emitted unchanged against player references. The compiler already owns source
parsing, emission and diagnostic cascade; the CLI owns execution routing, not a
second C# interpretation. Production hand count for PlayerTestCompiler: Ca = 1
(ProjectPlayerTest), Ce = 2 (Ucl.Core graph/model and Ucl.Discovery disk/context),
CT = 3. Integration tests are another client, excluded from the production count.
No dependency direction, interface, thread or lifecycle changes.

## Ownership-aware Editor filter

Language: ApiDesignLanguage sections 1, 4, 7 and 13 apply. The existing
`--emit-unity-filter` output is extended only for routed host reports. No new
CLI option, schema, discovery rule, type, or result category is introduced.

Candidate A retains fully-passing-class exclusions. It is small, but repeats
completed host/managed cases in mixed fixtures and reruns whole completed
fixtures containing legitimate failures or skips. Candidate B formats exclusions
from execution ownership, using whole-class regexes when all discovered cases
in that class are complete, and exact full-name regexes for completed names in
mixed fixtures. Candidate B is selected. Same-name/class collisions must remain
conservative: a name is excluded only when every discovered matching case is
complete. Class compression also checks every discovered FullName with the class prefix, including
other classes and assemblies; a pending nested-namespace case forces exact-name fallback. Exact-name
exclusions containing the UTF delimiter `;` are omitted conservatively, because regex escaping does
not escape UTF splitting. Class prefixes containing `;` are likewise omitted. The caller must use
the same discovery scope as the original run.

`TestReport.UnityFilter(TestRunReport)` owns this output formatting. `TestCommand`
passes the original report directly; ordinary reports without routing preserve
`UnityTestFilter.Build` behavior. Routed cases are complete only for `dotnet` or
`host` ownership and a category other than NeedsUnity/UnityOnly. `needs-editor`,
unknown and unassigned ownership remain eligible for the Editor. Regexes escape
NUnit names and use strict `\z` end anchors for exact mixed-fixture cases; a trailing-newline
identity cannot match another name. Output has deterministic order.
The original report, exit code and XML failures are unchanged. A hybrid collector
must preserve those failures; an Editor-only result can never replace the gate
result. Empty Editor ownership still excludes completed discovered cases while
leaving unrelated/unselected cases untouched.

The reporting boundary hides result/filter serialization, not execution or
classification. Production hand count for TestReport: Ca = 1 (TestCommand), Ce = 1 (Ucl.Core), CT =
2. Safe replacements touch TestReport.cs
and its public-surface tests; TestCommand's output-sink connection is unchanged.
The pure formatter owns no files, processes or cancellation lifetime. Inputs are
the complete discovered report; output is Unity's existing negated-regex filter
string. Callers own platform argument limits and may split Editor batches when
necessary. Assembly identity is not encoded by UTF name regexes, so collisions
retain conservative ownership rather than accidentally suppressing a case.

Implementation first installs the public formatter as a legacy forwarding seam,
then proves a synthetic mixed-fixture regression red before changing formatting.
Contract tests cover failure retention, name escaping, ownership collisions,
non-routed legacy behavior and deterministic output. Native UTF filter validation
on synthetic cases is required before publication. This optimization establishes
no full-gate speedup until a matching-source head-to-head measurement exists.

## Editor remainder as a test list

Language: ApiDesignLanguage sections 1, 4, 7 and 13 apply. One CLI option
(`--emit-unity-test-list`) and one formatter (`TestReport.UnityTestList`) are added;
no schema, discovery rule or category changes.

A phase-timed full hybrid run on a 6000.3 project with about 6,300 EditMode cases
found the Editor remainder executing 5,774 cases although ucl completed 4,889 of
6,271. The routed exclusion filter was 52 KB, beyond the Windows command line; a
241-class inclusion filter (12.6 KB) reached Unity, which logged it without
backslashes and cut at 8,186 characters, ending in a bare `^` that matches every
case. The hybrid gate therefore paid ucl plus a full Editor run.

Candidate A compresses the regex filter (shared prefixes, alternation, class
inclusion) to stay under the limit. It depends on project naming, still loses
escapes, and fails silently when a project grows. Candidate B writes the pending
full names to a file for UTF's documented `-orderedTestListFile`, which keeps only
the listed leaves. Candidate B is selected: no length limit, no escaping, the same
completion rule as the routed filter. Its limits are explicit: UTF resolves each
name to the first matching leaf and skips unknown names, so the collector checks
coverage; names with line breaks are counted and reported, never written.

`TestReport` owns the formatting (pure, no files or processes); `TestCommand`
writes it through the existing output sink after the report, only when the report
has no problems. Production hand count for TestReport is unchanged (Ca = 1, Ce = 1).

## Project layout in the player

Language: ApiDesignLanguage sections 1, 4, 7 and 13 apply. One optional public parameter
(`TestHost.Run` `imageRoot`) is added; no CLI option, schema or category changes. This section
supersedes the whole-file `Application.dataPath` ownership above.

Measured on the same project, 55 completed cases (36 player, 19 .NET) failed only because fixtures
were located from NUnit's `TestDirectory` (the test assembly's folder) or from a working-directory
relative `Assets/...` path, and 360 player candidates were Editor-owned only for reading
`Application.dataPath`. In the Editor all three resolve inside the project: the working directory
is the project root, `TestDirectory` is `Library/ScriptAssemblies`, and `dataPath` is
`<project>/Assets`.

Candidate A keeps Editor ownership and adds rules for every new path idiom; it grows with each
project and leaves those cases in the slowest runner. Candidate B maps the layout: the player's
working directory is the project root, test assemblies are staged under `Library/ucl/<run>` (the
one project folder ucl may write), the .NET host's image folder moves to the same place, and
symbol-resolved reads of `UnityEngine.Application.dataPath` in recompiled sources compile to the
Editor's string (`nameof` operands are not reads and stay as they are). Candidate B is selected. It changes no input file, applies to every read whatever
the test's outcome, and keeps the existing whole-file diagnostics. Reused player or precompiled
helpers still report the player's own folder; such cases need audited ownership. Tests that write
through these paths write where the Editor would.

`[UnityTest]`, `[RequiresPlayMode]` and Play Mode assembly cases become player candidates: the
bootstrap already installs UTF's coroutine work-item factory and runner. `[UnityPlatform]` stays
Editor-owned because it names the Editor platform: discovery records it as a separate flag
(`EditorPlatform`), since the unity-only reason reports the Play Mode assembly or `[UnityTest]` first.

UTF 1.6.0 keeps one test mode per run: `UnityTestAssemblyRunner.Load` sets
`UnityTestExecutionContext.TestMode` from its platform, and the Play Mode rejection of
`IEditModeTestYieldInstruction` (test bodies and `[UnitySetUp]`/`[UnityTearDown]`) reads it. One EditMode
load of every assembly would let Play Mode tests yield Edit Mode instructions and report a pass the Editor
fails. `ProjectPlayerTest` therefore lists each staged assembly with its platform (Editor-only assemblies
EditMode, the others PlayMode), and the bootstrap loads and runs each platform with its own runner and
context, EditMode first, as the Editor runs them separately. Each run writes its own NUnit XML. Checked on
Unity 6000.3 with a scratch project: a Play Mode test body and a `[UnitySetUp]` yielding an Edit Mode
instruction fail in the player as in the Editor (they passed under the single EditMode load), and an Edit
Mode `[UnityTest]` yielding one passes in both.

`PlayerTestCompiler` owns the substitution and keys it (`player-test-compiler/4`, the `dataPath`
value). `ProjectPlayerTest` owns staging, the working directory and candidate selection;
`TestHost` owns its image folder. No new process, thread or lifetime: staged folders are removed
after each run, best effort, like the existing image folder.

## Player cache key and cold build

The player key hashes the whole `Assets`, `Packages` and `ProjectSettings` trees, the resolved packages,
the bootstrap sources and the Editor binary, so any project change rebuilds the player. On one private
6000.3 project over 30 days, 909 of 1,255 commits under the Unity project changed the key; 653 of them
touched more than Editor or test scripts, 123 touched non-script files, and 15 touched only packages,
settings or plugins. A clean cold build there took 578 s: 263 s of Editor import (81 s of script
compilation, 32 s of domain reload, the rest asset and shader import), 254 s of `BuildPlayer` (207 s of
player script compilation) and about 61 s of copying, hashing, start and quit. The same build step
ranged from 53 s to 292 s of player script compilation across runs on a shared machine.

Two ways to change the key less often, neither implemented:

1. Leave Editor-only and test assemblies out of the key. ucl compiles the test assemblies itself,
   keyed on their own inputs (`tests-<key>`), so the player needs only runtime code. On that project
   this would have avoided 256 of the 909 rebuilds.
2. A minimal player: packages, settings and native plugins only; ucl compiles the project's runtime
   assemblies with the test assemblies. The key then covers what changed in 15 of the 1,255 commits.
   A first trial (Assets reduced to plugin binaries) kept 4 of 1,843 routed cases. ucl compiled the
   runtime assemblies from the Editor cell, with `UNITY_EDITOR` defined, against player DLLs; files with
   Editor-only code dropped out and their dependents with them. The runtime assemblies would need the
   player cell's defines (as `ucl check --target player` compiles them) while test assemblies keep the
   Editor cell's. The trial's cold build was not faster under load: packages still made up 72% of the
   compiled script items and most of the import.
