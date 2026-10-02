# Real-project checklist (maintainer)

What to run on a licensed machine with a real Unity 6 project, and what to send back. Everything here is
read-only for the project: `ucl` writes only to `Library/ucl` (and to the file you pass to `-o`). Plan about
15 minutes. Use the build from the commit you want to test (a release binary, or `dotnet build -c Release` and
`dotnet src/Ucl.Cli/bin/Release/net10.0/ucl.dll` in place of `ucl` below).

## 0. Prepare

1. Open the project once in the Editor and let it finish compiling (this writes `Library/Bee/artifacts/*.dag`,
   the editor dag). For player dags, also make a player build (or "Build" to a scratch folder) for each
   platform you care about, development and not.
2. Note the Editor version (`ProjectSettings/ProjectVersion.txt`) and close the Editor (it is not needed, and
   closing it keeps `Library/Bee` stable while `ucl` reads it).
3. Record the project state so you can prove nothing changed: `git status --porcelain | sha256sum` (or
   `git status --porcelain | Get-FileHash` in PowerShell).

## 1. Environment

```sh
ucl --version
ucl doctor path/to/Project > ucl-doctor.txt
```

Expected: the project's editor version is found. If not, add `--editor "<install folder>"` to every command below.

## 2. The Bee oracle (most important)

```sh
ucl bee-diff path/to/Project --format json -o ucl-bee-diff.json
ucl bee-diff path/to/Project > ucl-bee-diff.txt
```

Exit 0: `ucl`'s command line equals the Editor's for every assembly in every dag. Exit 1: the differences are
listed per dag and assembly. Exit 3: no `Library/Bee` (step 0) or no editor.

There are no known, expected differences any more (0.7.0 runs Unity's own source generators). For a private
project, report counts only: the number of differences per category (`assembly`, `sources`, `references`,
`defines`, `options`, `nowarn`, `analyzers`, `additionalfiles`), which `--format json` gives per assembly.

## 3. The compile itself

```sh
ucl check path/to/Project --summary > ucl-check-editor-summary.txt
ucl check path/to/Project --format json -o ucl-check-editor.json
ucl check path/to/Project --target player --platform StandaloneWindows64 --summary > ucl-check-player-summary.txt
ucl check path/to/Project --target player --platform StandaloneWindows64 --format json -o ucl-check-player.json
```

Expected for a project that compiles cleanly in the Editor: exit 0 (warnings allowed). Anything else is a
`ucl` bug. `--summary` lists the root failures first (failed assemblies, ranked by how many others they block,
with their most frequent errors).

Time a cold and a warm run (for docs/benchmarks.md):

```sh
time ucl check path/to/Project --no-cache > /dev/null
time ucl check path/to/Project > /dev/null    # run twice; time the second
```

## 4. Read-only check

```sh
git status --porcelain | sha256sum    # must equal the hash from step 0
```

## 5. Send the result back

Attach to an issue (or the PR thread) on `Lansenou/unity-compile-lab`:

* `ucl-doctor.txt`, `ucl-bee-diff.txt`, `ucl-bee-diff.json`, the two `--summary` files and the two JSON reports;
* the cold and warm times, the machine (CPU, cores, OS), and the two `git status` hashes;
* the Editor version and the list of dag folders (`ls Library/Bee/artifacts/*.dag`).

### Private project: counts only

When the project's files, logs and names must stay private, send only these numbers (no file):

* `ucl bee-diff`: the exit code and the last two lines of `ucl-bee-diff.txt` (`by category: ...` and
  `result: ...`); the JSON has the same counts under `summary.byCategory`;
* `ucl check`, editor and player: the exit code and the `result:` line (errors, warnings, assemblies, skipped);
* `ucl test`: its `result:` line (case totals by category);
* the cold and warm times;
* if a cold run is slow, the same cold run with `--analyzers off` (time only), which splits analyzer time from
  compile time.

Privacy: the JSON files contain project-relative paths, assembly names, define names, and editor-relative
paths only for the editor side; reference locations outside the project and the editor appear as absolute
paths (for example a `file:` package elsewhere on disk). Compiler messages can quote code identifiers. Review
or redact before posting publicly; do not attach `Library/` files or Unity DLLs.

If `bee-diff` reports differences, also attach two or three of the Editor's own response files for the
assemblies it names (`Library/Bee/artifacts/<dag>/<Assembly>.rsp`), with absolute paths replaced by
`<EDITOR>` and `<PROJECT>` if you prefer. They are the evidence a fix starts from (docs/oracle.md, "Using a
difference").

## 6. If it is clean

When `bee-diff` agrees (or differs only in the known items) and `check` is clean in every cell the Editor
compiles cleanly, `v1.0.0` can be tagged after the oracle recording (docs/oracle.md, "Recording").
