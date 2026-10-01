# Oracle: recording what real Unity reports

The fixture manifest (`fixtures/manifest.json`, see [fixtures.md](fixtures.md)) states what `ucl` must
report for each fixture cell. Most of it comes from the Unity manual; some rows of the define table are
`observed` ([defines.md](defines.md)). The **oracle** is the check that settles those expectations: it runs
a real, licensed Unity 6 Editor in batch mode on each fixture, records the diagnostics, the compiled
assemblies and their defines, and compares them with the manifest. Every manifest cell carries
`"oracle": "pending" | "agrees" | "disagrees"`; all start as `pending`.

## Why it does not run in CI

The Unity Editor terms do not clearly allow running the editor, or putting its DLLs, on a hosted runner
owned by a third party. CI therefore uses only the self-written stubs, and the oracle runs only on a
maintainer's own licensed machine or self-hosted runner. Details and quotations: [licensing.md](licensing.md).
The parser half of the oracle has no Unity dependency and is tested in CI against hand-written logs
(`tests/Ucl.Integration.Tests/OracleParserTests.cs`).

## Files

| Path | Purpose |
|---|---|
| `oracle/record.sh` | recorder for macOS, Linux and Git Bash (needs bash, jq, the .NET SDK) |
| `oracle/record.ps1` | the same recorder for PowerShell 7 (Windows; also runs on macOS/Linux); `-ParseOnly <log>` runs only the parser |
| `oracle/parse-log.sh` | Editor.log parser (bash + POSIX awk), used by `record.sh` |
| `oracle/package/com.ucl.oracle/` | embedded package injected into player cells (`Ucl.Oracle.CompilePlayer`) |
| `oracle/compare.sh` | compares recorded results with the manifest (needs jq) |
| `oracle/samples/*.log`, `*.expected.json` | hand-written Editor.log excerpts and the exact parser output |
| `oracle/results/<version>/<fixture>.json` | committed results (schema `ucl-oracle/1`) |
| `oracle/results/<version>/logs/` | raw Editor.log copies; git-ignored, never committed |

## Recording

Prerequisites:

* A Unity 6 Editor you are licensed to use, activated (Unity Hub sign-in or a serial). The manifest's
  versions are `6000.0.30f1` and `6000.3.2f1`; any other installed Unity 6 can be recorded under its own
  label (only cells whose `unityVersion` equals the label are run, so a new label first needs manifest
  cells).
* The build support module for each platform the cells use: every fixture has `StandaloneWindows64`
  cells (Windows Build Support (Mono) on macOS/Linux; built in on Windows), some have `Android`. A
  missing module shows up as status `unity-error`.
* The .NET 10 SDK (the recorder runs `dotnet run --project tests/Ucl.StubBuilder` to build the fixture
  analyzer and plugin DLLs into `artifacts/stubs/dlls/`; the editor itself is the real one).
* `jq` for `record.sh` and `compare.sh`.
* Network access the first time, if the editor needs to resolve packages (fixtures use built-in modules).

Run from the repository root:

```bash
# macOS (a .app bundle path is accepted)
oracle/record.sh "/Applications/Unity/Hub/Editor/6000.0.30f1/Unity.app" 6000.0.30f1
# Linux
oracle/record.sh ~/Unity/Hub/Editor/6000.3.2f1/Editor/Unity 6000.3.2f1
# one fixture or a glob, a longer timeout, keep the temporary projects for inspection
oracle/record.sh --timeout 1800 --keep ~/Unity/Hub/Editor/6000.0.30f1/Editor/Unity 6000.0.30f1 'asmdef-*'
```

```powershell
pwsh oracle/record.ps1 'C:\Program Files\Unity\Hub\Editor\6000.0.30f1\Editor\Unity.exe' 6000.0.30f1
pwsh oracle/record.ps1 $unity 6000.3.2f1 -Filter compile-error -TimeoutSeconds 1800 -Keep
```

Arguments: Unity executable, version label, optional fixture filter (glob; `-Filter` in PowerShell),
optional output directory (default `oracle/results/<version>/`). Each cell takes one editor start on a
fresh project (typically 1 to 3 minutes); the per-cell timeout is 900 s (`--timeout`/`-TimeoutSeconds`,
or `UCL_ORACLE_TIMEOUT`).

### What one cell does

1. Copies `fixtures/<name>/` to a temporary folder and places the fixture's `materialize` DLLs from
   `artifacts/stubs/dlls/` (their `.meta` files are part of the fixture).
2. Editor cells run
   `Unity -batchmode -nographics -quit -projectPath <copy> -buildTarget <name> -logFile <log>`.
   Opening the project compiles the editor scripts (`Library/ScriptAssemblies`).
3. Player cells also get `Packages/com.ucl.oracle/` (an embedded package with one Editor-only asmdef,
   `Ucl.Oracle.Editor`, `includePlatforms: ["Editor"]`, no references) and run with
   `-executeMethod Ucl.Oracle.CompilePlayer -uclOutDir <dir>` (plus `-uclDevelopment` for the
   `--development` extra argument). The method calls
   `PlayerBuildInterface.CompilePlayerScripts` for the active build target and group, prints
   `UCL-ORACLE-BEGIN`, one `UCL-ORACLE-ASSEMBLY: <name>` per compiled assembly, one `UCL-ORACLE-RSP: <path>`
   per compiler response file it caused, `UCL-ORACLE-END`, and exits. Unity can only run the method when the
   editor scripts compiled, so a player cell of a project with editor compile errors is recorded as
   `editor-compile-failed` (with the editor diagnostics).
4. `-buildTarget` names: StandaloneWindows64 `Win64`, StandaloneOSX `OSXUniversal`, StandaloneLinux64
   `Linux64`, iOS `iOS`, Android `Android`, WebGL `WebGL`. Extra arguments other than `--development` have
   no Unity equivalent; such cells are skipped with a warning.
5. The log is parsed (below). Assemblies are the names `CompilePlayer` printed plus every assembly for
   which Unity's build backend wrote a compiler response file (`Library/Bee/artifacts/**/<name>.rsp`),
   with the `-define:` symbols from that file. These are Unity's own compiler inputs, which is what an
   `observed` row of the define table needs.

Status: `ok` (Unity ran; for editor cells this includes compile errors), `editor-compile-failed` (player
cells only), `timeout`, `unity-error` (licence, missing module, crash; read the log in `logs/`).

### Result format

```json
{
  "schema": "ucl-oracle/1",
  "unityVersion": "6000.0.30f1",
  "fixture": "compile-error",
  "cells": [
    {
      "target": "editor", "platform": "StandaloneWindows64", "extraArgs": [],
      "status": "ok", "unityExitCode": 1, "editorOs": "windows",
      "assemblies": [ { "name": "Assembly-CSharp", "defines": ["DEBUG", "UNITY_EDITOR", "..."] } ],
      "diagnostics": [
        { "id": "CS0103", "severity": "error", "file": "Assets/Scripts/Player.cs", "line": 9, "column": 9,
          "message": "The name 'transfrom' does not exist in the current context" }
      ]
    }
  ]
}
```

`editorOs` (editor cells) is the host the result was recorded on. `assemblies` is omitted when unknown.
Diagnostics are deduplicated (Unity prints each message more than once) and sorted by file, line, column,
id in byte order; paths are project-relative with `/`.

### The parser

`oracle/parse-log.sh [--project-path DIR] [--section all|player] <Editor.log>` and
`pwsh oracle/record.ps1 -ParseOnly <Editor.log> [-ProjectPath DIR] [-Section player]` print the same
text: `{"assemblies": [...], "diagnostics": [...]}`. Recognised lines, after stripping leading `[Tag]`
groups (such as `[ScriptCompilation]`) and timestamps:

* compiler and analyzer messages in Roslyn's format, `path(line,col): error|warning ID: message`
  (Unity Manual, "Log files"; the console shows the same text); `Assets/...`, `Packages/...` and
  absolute paths under the project (made relative; the root is `--project-path` or the `-projectPath`
  value in the log's `COMMAND LINE ARGUMENTS:` block; Windows paths compare case-insensitively, macOS
  `/private/var` is handled);
* Unity's asmdef messages ending in `(<path>.asmdef)` or `(<path>.asmref)`, such as
  `Assembly has reference to non-existent assembly 'X' (Assets/A/A.asmdef)`: id `UNITY-ASMDEF`, file
  the asmdef, line and column 0, severity `warning` for the non-existent-reference message and `error`
  otherwise (the log does not say; check against the console on the first real run);
* `UCL-ORACLE-ASSEMBLY: <name>`.

Everything else (Bee progress, `## Script Compilation Error for:` headers, `(Filename: ...)` lines,
`Compilation failed`, stack traces, `Scripts have compiler errors.`) is ignored. `--section player` reads
only the lines between `UCL-ORACLE-BEGIN` and `UCL-ORACLE-END`, so the editor compilation that precedes
the player compilation does not leak into a player cell.

The samples in `oracle/samples/` are hand-written from that format. When a real log contains a line
shape the parser misses, add a trimmed excerpt as a new sample with its `.expected.json`, add the name to
`OracleParserTests.Samples`, and fix both parsers (they must print byte-identical output).

## Committing results

Commit `oracle/results/<version>/*.json` only (the `logs/` folders are git-ignored; they contain local
paths). One commit per recording session, message naming the version, host OS and date, for example
`oracle: record 6000.0.30f1 on Windows 11 (2026-10-01)`. Re-recording overwrites the files; the git diff
is the change in Unity's behaviour.

## Comparing

```bash
oracle/compare.sh                                  # every oracle/results/*/*.json
oracle/compare.sh oracle/results/6000.3.2f1        # one version
oracle/compare.sh oracle/results/6000.0.30f1/compile-error.json
```

One line per recorded cell, `agree`, `disagree` (with one indented line per difference) or `skip`
(status not `ok`, no matching manifest cell, or an exit-3 configuration cell, which Unity has no
equivalent of), followed by the cell's current manifest `oracle` value. Exit 1 when any cell disagrees.
Compared:

* diagnostics by id, severity, file, line and column. `UCLxxxx` ids are `ucl`'s own and are not
  compared, except a `UCL1xxx` on an `.asmdef`/`.asmref` file, which must match a `UNITY-ASMDEF` message
  on the same file (severity not compared);
* assemblies, when recorded: each manifest assembly exists, no `excluded` one does; with recorded
  defines, `definesInclude` are present, `definesExclude` absent, `defines` equal. Defines of an editor
  cell are compared only when it was recorded on the cell's `editorOs` (the `UNITY_EDITOR_<OS>` symbols
  follow the host).

### Flipping a manifest cell

After reviewing the output, set the cell's `"oracle"` to `"agrees"` or `"disagrees"`, by hand or with
`oracle/compare.sh --write [results...]` (updates only compared cells), then run
`scripts/fixtures-sums.sh` (the manifest is covered by `fixtures/SHA256SUMS`) and commit the manifest,
the sums and the results together. A cell that is `disagrees` stays so until the disagreement is
resolved below; then it becomes `agrees`.

## Triage of disagreements

The oracle (real Unity) is the reference for observable behaviour: diagnostics, which assemblies are
compiled, and the defines on Unity's own compiler command lines.

1. **Oracle disagrees with the manifest.** The manifest is wrong. Fix the manifest cell and the rule it
   encodes: in [defines.md](defines.md) change the row's status from `observed` to `doc` (if the manual
   states it) or `oracle` (confirmed only by a recorded run, naming the result file), add or fix the unit
   test for that row or graph rule, then fix `ucl` until the fixture and the unit test pass.
2. **ucl disagrees with the manifest but agrees with the oracle.** The manifest was wrong; fix it (and
   the doc row or rule it came from), keep `ucl`.
3. **ucl and the manifest agree, the oracle differs.** Same as 1: both are wrong.
4. **Differences the oracle cannot settle** (Unity's wording of asmdef messages, host-OS defines, cells
   skipped as exit 3): note them in the cell's fixture description or in [defines.md](defines.md); do not
   flip the cell.

Record each resolved disagreement in `CHANGELOG.md` under `Unreleased` ("Fixed: ... (oracle
6000.0.30f1, fixture `x`)"), so the change in expected behaviour is visible to users.

## Real-editor test mode (`UCL_REAL_EDITOR=1`)

Some manifest assertions depend on real Unity DLLs rather than the stubs (types or members the stubs do
not declare, the exact reference set of an editor install). Those cells are marked `"realEditor": true`.

* Default (CI): cells with `realEditor: true` are not run; everything else runs against the stub editors
  in `artifacts/stubs/editors/` through `UCL_EDITOR_ROOTS`.
* `UCL_REAL_EDITOR=1`: the integration tests also run the `realEditor` cells, and every cell uses the
  real editor at `UNITY_EDITOR_PATH` (the editor install folder, as `ucl` itself reads it) instead of the
  stub editors; cells whose `unityVersion` differs from that editor's version are skipped. Only on a
  licensed machine or self-hosted runner, for the same reason as the oracle.

```bash
UCL_REAL_EDITOR=1 UNITY_EDITOR_PATH=~/Unity/Hub/Editor/6000.0.30f1 \
  dotnet test tests/Ucl.Integration.Tests
```

This mode checks `ucl` against the real reference assemblies; the oracle checks the manifest against
Unity itself. Run both before flipping cells for a new Unity version.
