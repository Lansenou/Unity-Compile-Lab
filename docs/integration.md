# Integration

`ucl` compiles a Unity 6 project's C# the way the Editor does, in seconds, without starting Unity. Use it
to find compile errors; run Unity to check behaviour. This page shows how to wire it into hooks, CI and AI
coding agents. Ready-to-copy files are in [`examples/`](../examples).

## Requirements on the machine

* A Unity 6 editor install (any 6000.x). `ucl` reads its managed DLLs; Unity never runs, so no licence
  activation or login happens. Found through `--editor`, `UNITY_EDITOR_PATH`, `UCL_EDITOR_ROOTS` or the
  Unity Hub default folders; `ucl doctor` lists what it finds.
* Packages: whatever Unity resolved into `Library/PackageCache`, embedded and `file:` packages, or the
  download cache filled by `ucl fetch` (for a fresh clone that Unity never opened).
* `git` on `PATH` for `--changed`.

## Pre-commit hook

[`examples/pre-commit`](../examples/pre-commit) checks the assemblies the commit touches, and their
dependents:

```sh
cp examples/pre-commit .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
```

It runs `ucl check <repo> --changed HEAD`; a warm run takes well under a second when nothing changed (see
[benchmarks.md](benchmarks.md)). The cache lives in `Library/ucl`, which Unity projects already ignore.

## GitHub Actions

[`examples/github-actions.yml`](../examples/github-actions.yml): a self-hosted runner with Unity 6 installed
(see [licensing.md](licensing.md) for why not a hosted runner), `ucl doctor`, then an editor plus player
matrix written as SARIF and uploaded to code scanning.

## GitLab CI

[`examples/gitlab-ci.yml`](../examples/gitlab-ci.yml): the same on a tagged runner, with `ucl fetch` and the
download cache kept between pipelines.

## For AI coding agents

The one command to run before review:

```sh
ucl check . --format json
```

How to read it (schema [`schema/result.schema.json`](../schema/result.schema.json), id `ucl-result/1`):

* `exitCode`: `0` clean (warnings allowed), `1` errors, `2` only warnings-as-errors, `3` configuration
  problem (fix the environment: `problems[]` says what), `4` internal error.
* `cells[]`: one per (Unity version, target, platform). Each has `assemblies[]` (name, `status`
  `compiled`/`failed`/`skipped`, `defines`, `references`, `diagnosticCounts`) and `diagnostics[]`.
* `diagnostics[]`: `id`, `severity`, `origin` (`compiler`, `analyzer` or `ucl`), `assembly`, `file`
  (project-relative, `/` separators), 1-based `line` and `column` (0 when there is no position),
  `message`. Ordering is deterministic and there are no timestamps, so two runs can be diffed.
* A `skipped` assembly was not compiled because a dependency has errors, as in Unity: fix the dependency.

The full platform matrix (each combination is a cell):

```sh
ucl check . --target editor --target player --platform StandaloneWindows64 --platform WebGL --platform iOS --platform Android --format json
```

Faster loops:

* `ucl check . --changed origin/main`: only assemblies whose inputs changed since a ref, plus dependents.
* `ucl explain Assets/Path/File.cs`: which assembly owns a file, why, and every define with its rule.
* `ucl graph . --format dot`: the assembly graph.

A ready snippet for an agent's instructions file is in
[`examples/agent-usage.md`](../examples/agent-usage.md).

## Package authors

Check a package in every platform cell: create a minimal Unity 6 host project whose
`Packages/manifest.json` references the package with `file:../path/to/package`, then

```sh
ucl check host-project --target editor --target player --platform StandaloneWindows64 --platform StandaloneOSX \
  --platform StandaloneLinux64 --platform iOS --platform Android --platform WebGL --format json
```

Add `--unity-version` once per Unity 6 minor you support (each needs its editor installed).

## What ucl does not check

Behaviour, shaders, IL2CPP and Burst code generation, serialized data and scenes, and anything that only
happens at domain reload. See [roadmap.md](roadmap.md).
