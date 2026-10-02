# Platforms

`ucl` supports six build platforms. One name table maps the CLI name to the names Unity uses in each file
format. Sources: asmdef platform names from
https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definition-file-format.html, plugin
platform keys from `.meta` files written by the Unity 6 Plugin Inspector
(https://docs.unity3d.com/6000.3/Documentation/Manual/plug-in-inspector.html), build target groups from
`PlayerSettings` (`NamedBuildTarget`).

| `--platform` | asmdef platform | plugin `.meta` key | build target group (`scriptingDefineSymbols` key, numeric) | default backend |
|---|---|---|---|---|
| `StandaloneWindows64` | `WindowsStandalone64` | `Win64` | `Standalone` (1) | Mono |
| `StandaloneOSX` | `macOSStandalone` | `OSXUniversal` | `Standalone` (1) | Mono |
| `StandaloneLinux64` | `LinuxStandalone64` | `Linux64` | `Standalone` (1) | Mono |
| `iOS` | `iOS` | `iOS` | `iOS` (4) | IL2CPP |
| `Android` | `Android` | `Android` | `Android` (7) | IL2CPP |
| `WebGL` | `WebGL` | `WebGL` | `WebGL` (13) | IL2CPP |
| (editor) | `Editor` | `Editor` | n/a | n/a |

## Targets

* `--target editor`: what the Editor compiles with `--platform` as the active build target. An asmdef is
  compiled when it is compatible with the `Editor` platform (empty lists; `Editor` in `includePlatforms`;
  `Editor` not in `excludePlatforms`). The build platform does not filter asmdefs here; it only selects
  platform defines and `scriptingDefineSymbols`. All four predefined assemblies exist.
* `--target player`: what a player build compiles. An asmdef is compiled when it is compatible with the
  build platform. `Assembly-CSharp-Editor`, `Assembly-CSharp-Editor-firstpass`, scripts in `Editor`
  folders and Editor-only asmdefs are excluded, and no `UnityEditor` DLL is referenced.

Unknown names in `includePlatforms`/`excludePlatforms` (consoles, `tvOS`, `VisionOS`, ...) are accepted and
simply never match a supported cell. Setting both lists is `UCL3004` (bad asmdef).

## Plugin import settings

From a plugin's `.meta` (`PluginImporter`):

* `platformData` entry `Any` with `enabled: 1` means "Any Platform"; the entry keyed `: Any` holds
  `Exclude <key>: 1` lines that remove platforms. The same settings can appear in `Any:`;
  read both shapes, including a quoted empty key. `Exclude Editor: 1` removes Editor cells,
  independently of the active player target (sources in docs/architecture.md).
* Otherwise the plugin is compatible with each platform whose own entry has `enabled: 1`.
* No `platformData` at all: compatible everywhere (Unity's default for a managed DLL).
* `isExplicitlyReferenced: 1` is "Auto Reference" off; `validateReferences` is recorded but `ucl` does not
  load plugin dependencies; `defineConstraints` work like asmdef constraints.
* A top-level `labels:` list containing `RoslynAnalyzer` makes the DLL an analyzer.
