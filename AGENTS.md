# AGENTS.md

Notes for agents working in this repository. User-facing docs are in `README.md` and `docs/`; module
boundaries are in `docs/architecture.md`.

## What this is

`ucl` (C#, .NET 10, Roslyn) compiles a Unity 6 project's C# without Unity, with Unity's assembly, define,
platform, package, plugin and analyzer rules. `src/Ucl.Core` holds every rule as pure code;
`src/Ucl.Discovery` reads the disk; `src/Ucl.Compilation` runs Roslyn and the cache; `src/Ucl.Reporting`
renders; `src/Ucl.Cli` wires.

## Checks

* Run `scripts/check.sh` before pushing (CI runs it on Linux and in Git Bash on Windows). `--mutation` adds
  the mutation check. `scripts/bench.sh` checks the R11 speed targets.
* Warnings are errors; `dotnet format --verify-no-changes` must pass.

## Rules and contracts

* A Unity rule change starts in `docs/defines.md` or `docs/architecture.md` (with its source), then
  `src/Ucl.Core`, its unit test, `verify/Verify.cs` (the independent implementation), and a fixture.
* Fixture expectations follow the documented rule, never ucl's current output. After changing fixtures run
  `dotnet verify/bin/Release/net10.0/verify.dll --write-defines fixtures` (after `scripts/verify.sh`) and
  `scripts/fixtures-sums.sh`.
* Public contracts: CLI options, exit codes, `ucl-result/1`, `ucl-graph/1`, `ucl-fixtures/1`, `UCLxxxx` ids.
  Change them deliberately and record it in `CHANGELOG.md`.
* Never commit Unity binaries or package sources (`docs/licensing.md`). Fixture DLLs are built from
  `fixtures/_stubs` by `tests/Ucl.StubBuilder` and listed under `materialize`.
* ucl must never write under `Assets/`, `Packages/`, `ProjectSettings/`, or `Library/` outside `Library/ucl`.
