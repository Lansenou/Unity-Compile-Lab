# Define table

Every preprocessor symbol `ucl` defines, when, and the Unity documentation that says so. Each row has an id;
`tests/Ucl.Core.Tests/DefineTableTests.cs` has one test per id (`[Row("D07")]`), and `verify/` implements
the same table independently.

Status column:

* **doc**: stated by the Unity 6 manual page in the Source column.
* **observed**: not stated in the manual; taken from the compiler command lines Unity 6 writes to
  `Library/Bee/artifacts/*.rsp`. Until `oracle/` has recorded a real editor run for the row, the manifest
  marks assertions that depend on it `oracle: pending`.

Sources (Unity 6 manual, retrieved 2026-10-01):

* [SYM] https://docs.unity3d.com/6000.0/Documentation/Manual/scripting-symbol-reference.html
* [CC] https://docs.unity3d.com/6000.1/Documentation/Manual/platform-dependent-compilation.html
* [CUS] https://docs.unity3d.com/6000.3/Documentation/Manual/custom-scripting-symbols.html
* [ASM] https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definition-file-format.html
* [VD] https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definition-includes.html
* [CSC] https://docs.unity3d.com/6000.1/Documentation/Manual/csharp-compiler.html
* [NET] https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-support.html
* [SCW] https://docs.unity3d.com/6000.2/Documentation/ScriptReference/PlayerSettings-suppressCommonWarnings.html
* [ACL] https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ApiCompatibilityLevel.html
* [EACL] https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorAssembliesCompatibilityLevel.html
* [UCR] Unity C# reference source, https://github.com/Unity-Technologies/UnityCsReference (master, read
  2026-10-01; Unity Reference-Only License: read for facts, nothing copied): `Editor/Mono/PlayerSettings.bindings.cs`
  (enum values), `Editor/Mono/Scripting/ScriptCompilation/EditorBuildRules.cs` and `EditorCompilation.cs` (which
  assemblies use which level), `MonoLibraryHelpers.cs` and `Editor/Mono/Utils/NetStandardFinder.cs` (reference
  folders)
* [REAL] the compiler command lines (`Library/Bee/artifacts/*.rsp`) of a real 6000.3.19f1 project on Windows,
  reported by the maintainer on 2026-10-02

## Inputs

A compile cell is (Unity version `6000.M.P`, target `editor` or `player`, platform, scripting backend,
development flag, editor host OS). Project inputs are `ProjectSettings/ProjectSettings.asset`
(`scriptingDefineSymbols`, `scriptingBackend`, `apiCompatibilityLevel`, `apiCompatibilityLevelPerPlatform`,
`activeInputHandler`, `additionalCompilerArguments`), resolved package versions, and `csc.rsp` files.

## Version symbols

| Id | Symbol | When | Status | Source |
|---|---|---|---|---|
| D01 | `UNITY_6000` | always (Unity 6) | doc | [SYM] "UNITY_X" |
| D02 | `UNITY_6000_<M>` | always, minor M | doc | [SYM] "UNITY_X_Y" |
| D03 | `UNITY_6000_<M>_<P>` | always, patch P (numeric part of `33f1`) | doc | [SYM] "UNITY_X_Y_Z" |
| D04 | `UNITY_6000_<k>_OR_NEWER` for k = 0..M | always | doc | [SYM] "UNITY_X_Y_OR_NEWER" |
| D05 | `UNITY_5_3_OR_NEWER` ... `UNITY_2023_3_OR_NEWER` (5.3-5.6, 2017.1-4, 2018.1-4, 2019.1-4, 2020.1-3, 2021.1-3, 2022.1-3, 2023.1-3) | always | observed | [SYM] defines the `_OR_NEWER` form; the historical list is from Unity 6 Bee rsp files |

## Platform symbols

| Id | Symbol | When | Status | Source |
|---|---|---|---|---|
| D10 | `UNITY_EDITOR` | target editor | doc | [SYM] |
| D11 | `UNITY_EDITOR_WIN` / `UNITY_EDITOR_OSX` / `UNITY_EDITOR_LINUX` | target editor, by `--editor-os` (default: host OS) | doc | [SYM] |
| D12 | `UNITY_EDITOR_64` | target editor | observed | Bee rsp |
| D13 | `UNITY_STANDALONE` | platform StandaloneWindows64, StandaloneOSX, StandaloneLinux64 | doc | [SYM] |
| D14 | `UNITY_STANDALONE_WIN` | StandaloneWindows64 | doc | [SYM] |
| D15 | `UNITY_STANDALONE_OSX` | StandaloneOSX | doc | [SYM] |
| D16 | `UNITY_STANDALONE_LINUX` | StandaloneLinux64 | doc | [SYM] |
| D17 | `UNITY_IOS` | iOS | doc | [SYM] |
| D18 | `UNITY_ANDROID` | Android | doc | [SYM] |
| D19 | `UNITY_WEBGL` | WebGL | doc | [SYM] |
| D20 | `PLATFORM_STANDALONE`, `PLATFORM_STANDALONE_WIN`/`_OSX`/`_LINUX`, `PLATFORM_IOS`, `PLATFORM_ANDROID`, `PLATFORM_WEBGL` | same conditions as D13-D19 | observed | Bee rsp |
| D21 | `UNITY_64` | StandaloneWindows64, StandaloneOSX, StandaloneLinux64, iOS | doc | [SYM] "64-bit platforms" |

Platform symbols are defined for editor cells too: the editor compiles with the defines of the active
build target, which is the cell's `--platform`.

## Runtime, profile and build symbols

| Id | Symbol | When | Status | Source |
|---|---|---|---|---|
| D30 | `CSHARP_7_3_OR_NEWER` | always | doc | [SYM] |
| D31 | `ENABLE_MONO` | backend mono | doc | [SYM] |
| D32 | `ENABLE_IL2CPP` | backend il2cpp | doc | [SYM] |
| D33 | `NET_STANDARD_2_0`, `NET_STANDARD_2_1`, `NET_STANDARD`, `NETSTANDARD2_1`, `NETSTANDARD` | the assembly's API compatibility is .NET Standard 2.1 (see "API compatibility level") | doc | [SYM], [NET] |
| D34 | `NET_4_6`, `NET_UNITY_4_8` | the assembly's API compatibility is .NET Framework (see "API compatibility level") | doc (`NET_4_6`), observed (`NET_UNITY_4_8`, [REAL]) | [SYM], [REAL] |
| D35 | `ENABLE_LEGACY_INPUT_MANAGER` | `activeInputHandler` 0 or 2 (absent means 0) | doc | [SYM] |
| D36 | `ENABLE_INPUT_SYSTEM` | `activeInputHandler` 1 or 2 | doc | [SYM] |
| D37 | `DEVELOPMENT_BUILD` | target player with `--development` | doc | [SYM] |
| D38 | `DEBUG`, `TRACE` | target editor, or player with `--development` | doc (`DEBUG`: "equivalent to UNITY_EDITOR or DEVELOPMENT_BUILD"), observed (`TRACE`) | [SYM] |
| D39 | `UNITY_ASSERTIONS` | target editor, or player with `--development` | doc | [SYM] |

Scripting backend: `scriptingBackend` in `ProjectSettings.asset` per build target group (0 Mono, 1 IL2CPP),
overridden by `--backend`. Defaults when absent: Standalone Mono; Android IL2CPP; iOS and WebGL IL2CPP
(the only backend those platforms support). Editor cells use the platform's backend (observed).

## API compatibility level

The level is per assembly, not per cell. It picks both the D33/D34 symbols and the .NET reference assemblies.

| Id | Rule | Status | Source |
|---|---|---|---|
| A01 | `apiCompatibilityLevelPerPlatform[<group>]` of the cell's build target group (`apiCompatibilityLevel` when the group has no entry; default 6) is the level of every assembly that is not Editor-only, in editor and player cells alike | doc (enum), [UCR] (which assemblies) | [ACL], [UCR] |
| A02 | Editor-only assemblies (asmdef `includePlatforms` exactly `["Editor"]`, `Assembly-CSharp-Editor`, `Assembly-CSharp-Editor-firstpass`) use `editorAssembliesCompatibilityLevel` instead (default 1) | doc (enum), [UCR] (which assemblies) | [EACL], [UCR] |
| A03 | `ApiCompatibilityLevel` values: 3 `NET_Unity_4_8` (alias `NET_4_6`), 6 `NET_Standard` (alias `NET_Standard_2_0`); 1, 2, 4, 5 are obsolete and treated as 6 | doc (names), [UCR] (numbers) | [ACL], [UCR] |
| A04 | `EditorAssembliesCompatibilityLevel` values: 1 `Default` ("equivalent to `NET_Unity_4_8`"), 2 `NET_Unity_4_8`, 3 `NET_Standard` | doc (names, Default), [UCR] (numbers) | [EACL], [UCR] |
| A05 | .NET Framework references: from `Editor/Data/UnityReferenceAssemblies/unity-4.8-api/` the 17 libraries `mscorlib`, `System`, `System.Core`, `System.Runtime.Serialization`, `System.Xml`, `System.Xml.Linq`, `System.Numerics`, `System.Numerics.Vectors`, `System.Net.Http`, `System.IO.Compression`, `Microsoft.CSharp`, `System.Data`, `System.Data.DataSetExtensions`, `System.Drawing`, `System.IO.Compression.FileSystem`, `System.ComponentModel.Composition`, `System.Transactions` (when present), plus every DLL in `unity-4.8-api/Facades/` | observed (125 references from that folder for one assembly, [REAL]) | [UCR], [REAL] |
| A06 | .NET Standard references: `NetStandard/ref/2.1.0/netstandard.dll`, then every DLL in `NetStandard/compat/2.1.0/shims/netstandard/`, `NetStandard/Extensions/2.0.0/` and `NetStandard/compat/2.1.0/shims/netfx/`; Editor-only assemblies also `NetStandard/EditorExtensions/` | [UCR] | [UCR] |

With the defaults (level 6, editor level 1) runtime assemblies compile against .NET Standard 2.1 and Editor-only
assemblies against .NET Framework, also in editor cells. A project with `Standalone: 3` and editor level 2 (the
maintainer's project) compiles everything against `unity-4.8-api` in Standalone cells, where `Span<T>` comes only
from a project's `System.Memory.dll`; fixture `api-compat-netfx` covers both and `realistic-netfx-nuget` the
combination. The stub editor's `unity-4.8-api` (`fixtures/_stubs/profiles/`) has an `mscorlib.dll` without
`Span<T>`, `Facades/netstandard.dll` 2.0.0.0 and `Facades/System.Runtime.dll`, matching what [REAL] reports.

## Project symbols

| Id | Symbol | When | Status | Source |
|---|---|---|---|---|
| D50 | each entry of `scriptingDefineSymbols` for the cell's build target group (`Standalone`, `iOS`, `Android`, `WebGL`; numeric keys 1, 4, 7, 13 accepted) | always | doc | [CUS] |
| D51 | each `-define:`/`-d:` entry of `additionalCompilerArguments` for the group | always | doc | [CUS] |
| D52 | each `-define:` entry of the assembly's response file (see below) | per assembly | doc | [CUS] |
| D53 | asmdef `versionDefines[].define` when the named resource is present and its version is in `expression` | per assembly | doc | [VD] |

`versionDefines` resources: a package name (its resolved version) or `Unity` (the editor version, as
`6000.M.P` with the release suffix ignored for comparison). Expression forms ([VD]): `1.2` means `x >= 1.2`;
`[1.2]` exact; `[1.2,2.0)`, `(1.2,2.0]`, `[1.2,2.0]`, `(1.2,2.0)` intervals; `(,2.0)` and `[1.0,)`
open-ended. Missing components compare as 0 (`1.2` = `1.2.0`). Pre-release suffixes order below the release
(`2.1.0-preview.7 < 2.1.0`). An empty expression matches any present version (observed).

## Constraint-only symbols

| Id | Symbol | When | Status | Source |
|---|---|---|---|---|
| D60 | `UNITY_INCLUDE_TESTS` | target editor and `com.unity.test-framework` resolved; target player only with `--include-tests` | observed | test framework docs |

`defineConstraints` ([ASM]) are evaluated against the cell's full define set (D01-D53, plus D60). Each
entry must hold. An entry is one or more terms joined by `||`; a term is `SYMBOL` or `!SYMBOL`. An empty
entry is ignored.

## Response files

Order, later wins for single-valued options:

1. `additionalCompilerArguments` of the build target group (Player Settings);
2. `Assets/csc.rsp`, for every assembly that has no response file of its own;
3. `csc.rsp` in the folder of an asmdef, which replaces `Assets/csc.rsp` for that assembly.

Supported options: `-define:`/`-d:` (`;` or `,` separated), `-nowarn:`, `-warnaserror` / `-warnaserror+` /
`-warnaserror-` / `-warnaserror:<ids>`, `-langversion:`, `-nullable:`, `-unsafe`, `-additionalfile:`,
`-analyzerconfig:`, `-ruleset:`, `-r:`/`-reference:` (project-relative DLL). Paths are relative to the
project root. Options both `/x` and `-x` spellings. Anything else is warning `UCL1020` and is ignored.

## Compiler options

| Id | Setting | Value | Status | Source |
|---|---|---|---|---|
| C01 | language version | C# 9.0 | doc | [CSC] |
| C02 | nullable context | disabled unless `-nullable` | doc (no default stated) | [CSC] |
| C03 | warning level | 4 | observed | Bee rsp |
| C04 | suppressed warnings | CS0169, CS0649 when `suppressCommonWarnings` is 1 (default 1) | doc | [SCW] |
| C05 | suppressed warnings | CS1701, CS1702 (assembly unification) | observed | Bee rsp |
| C06 | unsafe | asmdef `allowUnsafeCode`; predefined assemblies `allowUnsafeCode` in Player Settings; `-unsafe` in rsp | doc | [ASM] |
| C07 | output kind | dynamically linked library, deterministic | observed | Bee rsp |
| C08 | warnings as errors | off unless rsp `-warnaserror` or `ucl --warnaserror` | doc | [CUS] |

C# features that compile under C# 9.0 syntax but need runtime support Unity lacks ([CSC]: init-only
setters, covariant return types, module initializers) are a runtime limitation: `ucl` reports what Roslyn
reports with the editor's reference assemblies, as Unity does.
