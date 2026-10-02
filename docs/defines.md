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
* [PUB] Unity-generated `.csproj` files committed to public GitHub repositories (the symbols and references
  Unity compiles each assembly with; read 2026-10-02): 6000.3.10f1 Windows editor, StandaloneWindows64
  (MasterAirscrachDev/Anki-Partydrive); 6000.3.5f2 Windows editor, StandaloneWindows (32-bit)
  (PierreMervaillie/GreasePencilToUnity); 6000.3.10f1 Linux editor, StandaloneLinux64
  (PizzaLovers007/AdofaiTweaks); 6000.0.43f1 Windows editor, WebGL (Nethereum/Unity3dSampleTemplate);
  6000.0.76f1 macOS editor, Android (videokit-ai/videokit); 6000.0.68f1 macOS editor, StandaloneOSX
  (JetBrains/resharper-unity test data). Only symbol names were taken; no project content.
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
| D31 | `ENABLE_MONO` | target editor (always: the Editor runs scripts on Mono), or backend mono | doc (backend), observed (editor: a WebGL editor compile, whose only backend is IL2CPP, has `ENABLE_MONO`) | [SYM], [PUB] |
| D32 | `ENABLE_IL2CPP` | target player and backend il2cpp | doc | [SYM] |
| D33 | `NET_STANDARD_2_0`, `NET_STANDARD_2_1`, `NET_STANDARD`, `NETSTANDARD2_1`, `NETSTANDARD` | the assembly's API compatibility is .NET Standard 2.1 (see "API compatibility level") | doc | [SYM], [NET] |
| D34 | `NET_4_6`, `NET_UNITY_4_8` | the assembly's API compatibility is .NET Framework (see "API compatibility level") | doc (`NET_4_6`), observed (`NET_UNITY_4_8`, [REAL]) | [SYM], [REAL] |
| D35 | `ENABLE_LEGACY_INPUT_MANAGER` | `activeInputHandler` 0 or 2 (absent means 0) | doc | [SYM] |
| D36 | `ENABLE_INPUT_SYSTEM` | `activeInputHandler` 1 or 2 | doc | [SYM] |
| D37 | `DEVELOPMENT_BUILD` | target player with `--development` | doc | [SYM] |
| D38 | `DEBUG`, `TRACE` | target editor, or player with `--development` | doc (`DEBUG`: "equivalent to UNITY_EDITOR or DEVELOPMENT_BUILD"), observed (`TRACE`) | [SYM] |
| D39 | `UNITY_ASSERTIONS` | target editor, or player with `--development` | doc | [SYM] |

Scripting backend: `scriptingBackend` in `ProjectSettings.asset` per build target group (0 Mono, 1 IL2CPP),
overridden by `--backend`. Defaults when absent: Standalone Mono; Android IL2CPP; iOS and WebGL IL2CPP
(the only backend those platforms support). The backend only picks D31/D32 in player cells: editor cells always define `ENABLE_MONO` (D31).

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
| D53 | asmdef `versionDefines[].define` when the named resource is present and its version is in `expression`. For resource `Unity`, versions may carry Unity's release suffix (`2022.2.14f1`); ucl compares without it, on both sides (fixture `version-gated-symbols`) | per assembly | doc | [VD] |

`versionDefines` resources: a package name (its resolved version) or `Unity` (the editor version, as
`6000.M.P` with the release suffix ignored for comparison). Expression forms ([VD]): `1.2` means `x >= 1.2`;
`[1.2]` exact; `[1.2,2.0)`, `(1.2,2.0]`, `[1.2,2.0]`, `(1.2,2.0)` intervals; `(,2.0)` and `[1.0,)`
open-ended. Missing components compare as 0 (`1.2` = `1.2.0`). Pre-release suffixes order below the release
(`2.1.0-preview.7 < 2.1.0`). An empty expression matches any present version (observed).

## Test symbols

| Id | Symbol | When | Status | Source |
|---|---|---|---|---|
| D60 | `UNITY_INCLUDE_TESTS` | target editor and `com.unity.test-framework` resolved; target player only with `--include-tests`. A compiler symbol like any other (until 0.6.0 `ucl` used it for `defineConstraints` only) | observed | test framework docs, [PUB], [REAL] |
| D61 | (removed) | `UNITY_TESTS_FRAMEWORK` is not a compiler define (bee-diff on a real 6000.3 project). Assemblies constrained on it declare it themselves: `Unity.InputSystem.TestFramework` has the versionDefines entry `com.unity.test-framework` / `""` / `UNITY_TESTS_FRAMEWORK`, and defineConstraints see version defines (D53) | - | [PUB] (`com.unity.inputsystem` asmdef; UnityCsReference `EditorCompilation.GetTargetAssemblyDefines`) |

## Built-in symbols

Symbols Unity 6 defines that the manual does not list. They come from the editor's native code, so the
lists are what [PUB] shows, per platform; [REAL] confirms many of them for a 6000.3 WebGL project. Player
rows are inferred (no public player command line was found): engine feature and platform symbols apply to
players too, editor service symbols do not, and E04 follows the Collections and Profiler documentation
("Editor and development builds"). Until `oracle/` records a real run, the manifest marks these
assertions `oracle: pending`.

| Id | Symbols | When | Status | Source |
|---|---|---|---|---|
| E01 | `CSHARP_7_OR_LATER` | always | [UCR] (`EditorBuildRules.s_CSharpVersionDefines`) | [UCR], [PUB] |
| E02 | `UNITY_EDITOR_ONLY_COMPILATION` | Editor-only assemblies (asmdef `includePlatforms` exactly `["Editor"]`, `Assembly-CSharp-Editor`, `Assembly-CSharp-Editor-firstpass`) | [UCR] (`EditorBuildRules.ToScriptAssemblies`) | [UCR], [PUB] |
| E04 | `ENABLE_PROFILER`, `ENABLE_UNITY_COLLECTIONS_CHECKS` | target editor, or player with `--development` | observed | [PUB], Collections and Profiler docs |
| E16 | `ENABLE_UNITY_COLLECTIONS_CHECKS` | other player cells whose platform folder has no engine build of its own (`PlaybackEngines/<support>/Managed/UnityEngine.CoreModule.dll`): ucl then compiles against the editor's engine DLLs, built with this symbol (`NativeArray<T>.ReadOnly`'s internal constructor takes the `AtomicSafetyHandle`). A deliberate difference from Unity's release-player set, which pairs that set with the player's engine build; `bee-diff` compares against Unity's set (fixture `player-collections-checks`) | ucl rule | UnityCsReference `NativeArray.cs` |
| E05 | `EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED`, `ENABLE_ACCELERATOR_CLIENT_DEBUGGING`, `ENABLE_BURST_AOT`, `ENABLE_CLOUD_LICENSE`, `ENABLE_EDITOR_GAME_SERVICES`, `ENABLE_EDITOR_HUB_LICENSE`, `ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API`, `ENABLE_MARSHALLING_TESTS`, `UNITY_TEAM_LICENSE` | target editor | observed | [PUB] |
| E06 | `ENABLE_AUDIO`, `ENABLE_CLOTH`, `ENABLE_CLOUD_SERVICES`, `ENABLE_CLOUD_SERVICES_ADS`, `ENABLE_CLOUD_SERVICES_ANALYTICS`, `ENABLE_CLOUD_SERVICES_BUILD`, `ENABLE_CLOUD_SERVICES_CRASH_REPORTING`, `ENABLE_CLOUD_SERVICES_PURCHASING`, `ENABLE_CLOUD_SERVICES_USE_WEBREQUEST`, `ENABLE_CRUNCH_TEXTURE_COMPRESSION`, `ENABLE_CUSTOM_RENDER_TEXTURE`, `ENABLE_DIRECTOR`, `ENABLE_DIRECTOR_AUDIO`, `ENABLE_DIRECTOR_TEXTURE`, `ENABLE_LOCALIZATION`, `ENABLE_MANAGED_ANIMATION_JOBS`, `ENABLE_MANAGED_AUDIO_JOBS`, `ENABLE_MANAGED_JOBS`, `ENABLE_MANAGED_TRANSFORM_JOBS`, `ENABLE_MANAGED_UNITYTLS`, `ENABLE_MULTIPLE_DISPLAYS`, `ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK`, `ENABLE_PHYSICS`, `ENABLE_SPRITES`, `ENABLE_TERRAIN`, `ENABLE_TEXTURE_STREAMING`, `ENABLE_TILEMAP`, `ENABLE_TIMELINE`, `ENABLE_UNITYEVENTS`, `ENABLE_UNITYWEBREQUEST`, `ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT`, `ENABLE_VIDEO`, `ENABLE_VR`, `ENABLE_WEBCAM`, `ENABLE_WEBSOCKET_CLIENT`, `ENABLE_WWW`, `TEXTCORE_1_0_OR_NEWER`, `TEXTCORE_FONT_ENGINE_1_5_OR_NEWER`, `TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER` | always (all six samples) | observed | [PUB] |
| E07 | `ENABLE_UNITY_CLOUD_IDENTIFIERS`, `ENABLE_UNITY_CONSENT` | Unity 6000.0.76 or newer (seen from 6000.0.76; absent in 6000.0.68 and 6000.0.43) | observed | [PUB] |
| E08 | `TEXTCORE_FONT_ENGINE_1_6_OR_NEWER` | Unity 6000.3 or newer (every 6000.3 sample, no 6000.0 one) | observed | [PUB] |
| E08 | `ENABLE_AUDIO_SCRIPTABLE_PIPELINE` | Unity 6000.3 before 6000.3.19 (seen on 6000.3.5 and 6000.3.10; absent on a 6000.3.19 editor; the exact first version without it is unknown) | observed | [PUB], [REAL] |
| E17 | `ENABLE_PROFILER_ASSISTANT_INTEGRATION` | editor cells, Unity 6000.3.19 or newer (absent on 6000.3.10; players unknown) | observed | [REAL] |
| E10 | `ENABLE_CACHING`, `ENABLE_CLUSTERINPUT`, `ENABLE_CLUSTER_SYNC`, `ENABLE_LZMA`, `ENABLE_MICROPHONE`, `ENABLE_MOVIES`, `ENABLE_NETWORK`, `ENABLE_RUNTIME_GI`, `ENABLE_SCRIPTING_GC_WBARRIERS`, `ENABLE_VIRTUALTEXTURING`, `INCLUDE_DYNAMIC_GI`, `PLATFORM_ARCH_64`, `PLATFORM_SUPPORTS_MONO`, `RENDER_SOFTWARE_CURSOR` | every Standalone platform (all 64-bit) | observed | [PUB] |
| E11 | `ENABLE_ACCESSIBILITY_SCREEN_READER`, `ENABLE_AMD`, `ENABLE_AR`, `ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS`, `ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING`, `ENABLE_EVENT_QUEUE`, `ENABLE_NVIDIA`, `ENABLE_OUT_OF_PROCESS_CRASH_HANDLER`, `GFXDEVICE_WAITFOREVENT_MESSAGEPUMP`, `PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS`, `PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION`, `PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP`, `PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER` | StandaloneWindows64 | observed | [PUB] |
| E12 | `ENABLE_MODULAR_UNITYENGINE_ASSEMBLIES`, `ENABLE_SPATIALTRACKING`, `PLATFORM_SUPPORTS_DISPLAYINFO_API`, `PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS`, `PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER`, `UNITY_STANDALONE_LINUX_API` | StandaloneLinux64 | observed (one sample) | [PUB] |
| E13 | `ENABLE_AR`, `ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING`, `ENABLE_GAMECENTER`, `ENABLE_SPATIALTRACKING`, `PLATFORM_HAS_CUSTOM_MUTEX`, `PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP` | StandaloneOSX | observed (one sample) | [PUB] |
| E14 | `ENABLE_ENGINE_CODE_STRIPPING`, `ENABLE_ONSCREEN_KEYBOARD`, `ENABLE_SPATIALTRACKING`, `RENDER_SOFTWARE_CURSOR`, `UNITY_DISABLE_WEB_VERIFICATION`, `UNITY_GFX_USE_PLATFORM_VSYNC`, `UNITY_WEBGL_API` | WebGL | observed (one 6000.0 sample) | [PUB], [REAL] (`UNITY_WEBGL_API`) |
| E15 | `ENABLE_ACCESSIBILITY`, `ENABLE_ANDROID_ADVERTISING_IDS`, `ENABLE_ANDROID_APP_SET_ID`, `ENABLE_AR`, `ENABLE_CACHING`, `ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS`, `ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING`, `ENABLE_EGL`, `ENABLE_ENGINE_CODE_STRIPPING`, `ENABLE_ETC_COMPRESSION`, `ENABLE_EVENT_QUEUE`, `ENABLE_FIREBASE_IDENTIFIERS`, `ENABLE_INSIGHTS_PLATFORM_SPECIFIC_RESOURCES`, `ENABLE_LZMA`, `ENABLE_MICROPHONE`, `ENABLE_NETWORK`, `ENABLE_ONSCREEN_KEYBOARD`, `ENABLE_RUNTIME_GI`, `ENABLE_SCRIPTING_GC_WBARRIERS`, `ENABLE_SPATIALTRACKING`, `ENABLE_UNITYADS_RUNTIME`, `INCLUDE_DYNAMIC_GI`, `PLATFORM_EXTENDS_VULKAN_DEVICE`, `PLATFORM_EXTENDS_VULKAN_PIPELINE_CACHE`, `PLATFORM_HAS_ADDITIONAL_API_CHECKS`, `PLATFORM_HAS_BUGGY_MSAA_RESOLVE`, `PLATFORM_HAS_MULTIPLE_SWAPCHAINS`, `PLATFORM_IMPLEMENTS_INSIGHTS_ANR`, `PLATFORM_REQUIRES_TETHERED_VULKAN_COMMAND_POOL`, `PLATFORM_SUPPORTS_INSIGHTS_DEVICE_INFO`, `PLATFORM_SUPPORTS_MONO`, `PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS`, `PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP`, `UNITY_ANDROID_API`, `UNITY_ANDROID_SUPPORTS_SHADOWFILES`, `UNITY_CAN_SHOW_SPLASH_SCREEN`, `UNITY_HAS_GOOGLEVR`, `UNITY_HAS_TANGO`, `UNITY_UNITYADS_API` | Android | observed (one 6000.0 sample) | [PUB] |

iOS has no public sample: an iOS cell gets E01-E08 only. Symbols seen in a sample whose condition is a project
setting or package (`UNITY_POST_PROCESSING_STACK_V2`, `ENABLE_INPUT_SYSTEM`) are not built-in and are left out.
The 32-bit StandaloneWindows sample lacks `UNITY_64` and `PLATFORM_ARCH_64`, which is why `PLATFORM_ARCH_64`
is in E10 (64-bit Standalone) rather than in E06.

`defineConstraints` ([ASM]) are evaluated against the cell's full define set (D01-D60, E01-E15). Each
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
| C09 | suppressed warnings | CS0282 (field order of a partial struct declared in several files) | observed | [REAL] (`/nowarn:0282` on every command line) |
| C06 | unsafe | asmdef `allowUnsafeCode`; predefined assemblies `allowUnsafeCode` in Player Settings; `-unsafe` in rsp | doc | [ASM] |
| C07 | output kind | dynamically linked library, deterministic | observed | Bee rsp |
| C08 | warnings as errors | off unless rsp `-warnaserror` or `ucl --warnaserror` | doc | [CUS] |

C# features that compile under C# 9.0 syntax but need runtime support Unity lacks ([CSC]: init-only
setters, covariant return types, module initializers) are a runtime limitation: `ucl` reports what Roslyn
reports with the editor's reference assemblies, as Unity does.
