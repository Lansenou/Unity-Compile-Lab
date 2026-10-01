# Architecture

`ucl` compiles the C# of a Unity 6 project with Roslyn, in process, the way the Unity Editor would, without
running Unity. This page is the design: module boundaries, the data flow of one run, and the rules each
module owns. Unity's own rules (with sources) are in [defines.md](defines.md) and [platforms.md](platforms.md).

## Modules

Dependencies point inward. `Ucl.Core` depends on the BCL only; an architecture test
(`tests/Ucl.Core.Tests/ArchitectureTests.cs`) reflects over assembly references and fails on any other edge.

```
Ucl.Cli ──> Ucl.Reporting ──┐
   │    ──> Ucl.Compilation ─┼──> Ucl.Core
   └──────> Ucl.Discovery ───┘
Ucl.Compilation ──> Ucl.Discovery (file system port only), Microsoft.CodeAnalysis.CSharp
```

| Module | Input | Output | Owns | Does not own |
|---|---|---|---|---|
| `Ucl.Core` | text of asmdef, asmref, `.meta`, `ProjectSettings.asset`, `ProjectVersion.txt`, `manifest.json`, `packages-lock.json`, `csc.rsp`; a `ProjectInventory` (relative paths and parsed models) and a `CompileCell` | `AssemblyGraph` (one `AssemblyPlan` per compiled assembly), `DefineSet` with a reason per define, `Problem` values | every Unity rule: special folders, asmdef fields, GUID references, platform include and exclude, plugin import settings, analyzer scope, define table, version ranges, define constraints, rsp parsing, exit-code policy | the file system, Roslyn, output formats |
| `Ucl.Discovery` | a project path, environment variables, `IFileSystem` | `ProjectInventory`, `EditorInstall`, resolved packages | walking `Assets/` and package roots with Unity's hidden-asset rules, `.meta` GUID index, package resolution order, editor install discovery | any rule about what the files mean |
| `Ucl.Compilation` | `AssemblyGraph`, `EditorInstall`, `IFileSystem` | per-assembly `CompileResult` (diagnostics, inputs hash, metadata image) | Roslyn options, reference resolution to files, analyzers and generators, the incremental cache, parallel scheduling | deciding which assemblies or defines exist |
| `Ucl.Reporting` | `RunResult` | text, JSON (`schema/result.schema.json`), SARIF 2.1.0 | stable ordering, relative paths, the JSON schema | exit codes (Core decides) |
| `Ucl.Cli` | argv | stdout, stderr, exit code | argument parsing, wiring, matrix expansion | logic beyond wiring |

## Data flow of `ucl check`

1. **Discover.** `ProjectLocator` checks `Assets/`, `Packages/`, `ProjectSettings/` and reads
   `ProjectVersion.txt`. `PackageResolver` resolves every package in `manifest.json` and
   `packages-lock.json` (section "Packages" below). `ProjectScanner` walks `Assets/` and each package root
   once and produces a `ProjectInventory`: `.cs` files, `.asmdef`, `.asmref`, `.dll` with their `.meta`,
   `csc.rsp` files, `.editorconfig`/`.globalconfig`, and a GUID index from every `.meta` it saw. Paths are
   project-relative with `/` separators (`Packages/com.foo/Runtime/A.cs` for package files, whatever their
   physical location), which is what keeps output identical across machines.
2. **Locate the editor.** `EditorLocator` picks an install for each requested Unity version
   (`--editor`, `UNITY_EDITOR_PATH`, `UCL_EDITOR_ROOTS`, Unity Hub default folders) and lists its managed
   reference DLLs and its .NET reference profile. A missing editor is a `Problem` (exit 3).
3. **Plan.** For each matrix cell (Unity version x target x platform), `AssemblyGraphBuilder` (Core) turns
   the inventory into an `AssemblyGraph`: which assemblies exist in this cell, their sources, references,
   precompiled references, analyzers, defines and compiler options. Pure function, no I/O; this is where
   all the Unity rules live and where almost all unit tests point.
4. **Compile.** `CompilationRunner` compiles assemblies in dependency order, in parallel waves. Each
   assembly is a `CSharpCompilation` with Unity's options; dependents reference the producer's emitted
   metadata-only image. Analyzers and source generators run when `--analyzers on`. Results are cached by
   an inputs hash (section "Incremental cache").
5. **Report.** `Ucl.Reporting` renders the `RunResult`. `ExitCodePolicy` (Core) computes the exit code.

## Assembly graph rules (Core)

* **Ownership of a script.** A `.cs` file belongs to the asmdef or asmref in the nearest ancestor folder.
  If there is none, it goes to a predefined assembly by its path: under `Assets/Plugins/`,
  `Assets/Standard Assets/` or `Assets/Pro Standard Assets/` it is firstpass; inside any folder named
  `Editor` it is an Editor assembly; combining both gives `Assembly-CSharp-Editor-firstpass`. A script in a
  package with no asmdef is not compiled (Unity warns; `ucl` reports `UCL1010` as a warning).
* **Hidden assets.** Like the Asset Database, the scan skips files and folders whose name starts with `.`,
  ends with `~`, is `cvs`, or has the `.tmp` extension.
* **References.** asmdef `references` resolve by name, or by `GUID:<32 hex>` through the `.meta` index. An
  unresolved name is warning `UCL1001` and is dropped (Unity warns and compiles without it). Predefined
  assemblies reference every `autoReferenced` asmdef that is compiled in the cell, earlier predefined
  phases, and every auto-referenced precompiled DLL. asmdefs never see predefined assemblies.
* **Cell membership.** An asmdef is compiled in a cell when its platforms match (editor target: the
  `Editor` platform only; player target: the build platform; see [platforms.md](platforms.md)) and its
  `defineConstraints` hold against the cell's defines. A reference to an assembly not compiled in the cell
  is dropped silently, as Unity does; the resulting compile errors are the user's signal. Player cells
  never contain Editor assemblies or `Editor` folders.
* **Cycles** are error `UCL1002` on every assembly in the cycle; those assemblies are not compiled.
* **Precompiled DLLs.** A `.dll` with a `.meta` is a plugin. It is a reference when its plugin import
  settings are compatible with the cell (Any Platform with excludes, or explicit per-platform enable), its
  plugin `defineConstraints` hold, and it is not labelled `RoslynAnalyzer`. `Auto Reference` off
  (`isExplicitlyReferenced: 1`) means only asmdefs that list it in `precompiledReferences` (with
  `overrideReferences`) see it. An asmdef with `overrideReferences: true` sees only the DLLs it lists; a
  listed DLL that does not exist is error `UCL1004`.
* **Analyzers.** A DLL labelled `RoslynAnalyzer` is an analyzer and source generator, never a reference.
  Scope: if its folder (or an ancestor) holds an asmdef, it applies to that assembly and to every assembly
  that references it directly; otherwise it applies to all predefined assemblies.
* **Engine references.** `noEngineReferences: true` removes the editor's UnityEngine and UnityEditor DLLs.
  Built-in modules come from `com.unity.modules.*` packages: a module DLL whose name matches a
  `com.unity.modules.<x>` package is referenced only when that package is resolved; modules with no
  package (CoreModule, SharedInternalsModule, ...) are always referenced. Editor cells also reference the
  `UnityEditor` DLLs, for every assembly (runtime code may use `#if UNITY_EDITOR`).

## Compiler options

From [defines.md](defines.md#compiler-options): C# 9.0, nullable disabled, warning level 4, `-unsafe`
from the asmdef (`allowUnsafeCode`) or, for predefined assemblies, `PlayerSettings.allowUnsafeCode`;
CS0169 and CS0649 suppressed when `suppressCommonWarnings` is on (the default); then
`additionalCompilerArguments` for the platform; then `Assets/csc.rsp`; then a `csc.rsp` next to the asmdef.
Later options win for `-langversion` and `-nullable`; `-define` and `-nowarn` accumulate.
`.editorconfig` and `.globalconfig` files found next to sources are passed as analyzer config, which gives
severity overrides to both compiler and analyzer diagnostics.

## Packages

`PackageResolver` reads `Packages/manifest.json` (dependencies, `scopedRegistries`, `testables`) and,
when present, `Packages/packages-lock.json` (the resolved version, `source`, `dependencies`, `url`). For
each package it tries, in order:

1. embedded: a folder `Packages/<dir>/` whose `package.json` has that `name`;
2. local: `file:` paths, relative to `Packages/`;
3. `Library/PackageCache/<name>@<hash or version>/` (Unity 6 uses a hash suffix; both are accepted, the
   lock-file version decides);
4. the download cache `~/.cache/ucl/packages/<name>@<version>/` (or `UCL_PACKAGE_CACHE`), filled by
   `ucl fetch`.

`com.unity.modules.*` are built-in: they have no folder and map to editor module DLLs. A package that
cannot be found is `UCL3006` (exit 3). Git packages resolve only through `Library/PackageCache`.

## Incremental cache

Per assembly, `inputsHash = SHA-256(` tool version, sorted (relative path, content hash) of sources,
defines, options, analyzer file hashes, and for each reference either the file hash (DLL) or the
producer's metadata-image hash `)`. A dependent's key changes only when a dependency's public surface
changes, so editing a method body recompiles one assembly. Entries live in `<project>/Library/ucl/cache`
(or `--cache-dir`): the metadata image plus diagnostics as JSON. File content hashes are memoised by
(path, size, mtime) in `Library/ucl/files.json` so a warm run hashes nothing that did not change.

## Read-only contract

`ucl` never writes under `Assets/`, `Packages/` or `ProjectSettings/`, and under `Library/` only inside
`Library/ucl`. All writes go through `IFileSystem.WriteAllBytes`, whose physical implementation refuses any
path outside the cache directory; `ReadOnlyTests` hashes a fixture tree before and after a full run.

## Error handling

Configuration problems are values (`Problem` with an id, a message and an optional file), collected across
the whole run and reported together with exit 3. Exceptions are reserved for bugs and map to exit 4 with
the stack trace on stderr.

## Ports

Interfaces exist only at real boundaries: `IFileSystem` (disk), `IEnvironment` (environment variables, OS,
home folder), `IProcessRunner` (git for `--changed`), `IHttpClient` (fetch), `IClock` (`--timings`).

## Files over 400 lines

None yet. Any file that grows past 400 lines must be listed here with its reason.

## Verification layers

* `tests/Ucl.Core.Tests`: unit tests per rule, property tests for version ranges and define constraints.
* `tests/Ucl.Integration.Tests`: the CLI over every fixture in `fixtures/`, compared with
  `fixtures/manifest.json`.
* `verify/`: a separate, deliberately small implementation of the define table and the assembly graph
  that shares no code with `src/`. `scripts/check.sh` fails if it disagrees with `ucl graph --format json`
  on any fixture cell.
* `oracle/`: scripts that record what a real, licensed Unity Editor reports, to settle disagreements.
