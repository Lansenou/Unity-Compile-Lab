# Conformance corpus

`fixtures/<name>/` is a minimal Unity 6 project (`Assets/`, `Packages/manifest.json`,
`ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset`) that exercises one rule.
`fixtures/manifest.json` (schema `ucl-fixtures/1`) holds, per fixture and per cell (Unity version, target,
platform, options), the expected exit code, the compiled assemblies with their full define sets, and the
exact diagnostics (id, file, line, column, severity). `fixtures/SHA256SUMS` covers every fixture file.

Unity binaries are never committed. `fixtures/_stubs/` holds a small self-written UnityEngine/UnityEditor
API surface (only what the fixtures use) and the sources of the fixture analyzer and plugin DLLs.
`tests/Ucl.StubBuilder` compiles them into fake editor installs (`artifacts/stubs/editors/<version>/`, the
Unity Hub layout) and into `artifacts/stubs/dlls/`. A fixture that needs a DLL lists it under
`materialize`; the integration tests copy such a fixture to a temporary folder and place the DLL (and its
committed `.meta`) there. Assertions that depend on real Unity DLLs are marked `realEditor: true` and run
only with `UCL_REAL_EDITOR=1`.

Every cell starts as `oracle: pending`; see [oracle.md](oracle.md).

## Fixture list

| # | Fixture | Rule | Requirement |
|---|---|---|---|
| 1 | `basic-predefined` | scripts with no asmdef compile into `Assembly-CSharp` | R2 |
| 2 | `compile-error` | a typo reports CS0103 in Unity's console format | R9, R10 |
| 3 | `asmdef-ref-by-name` | reference by assembly name | R2 |
| 4 | `asmdef-ref-by-guid` | `GUID:` reference through the `.meta` index | R2 |
| 5 | `asmdef-missing-reference` | unresolved name: UCL1001 warning, then CS0246 | R2 |
| 6 | `asmdef-circular` | A -> B -> A: UCL1002 | R2 |
| 7 | `asmdef-include-platforms` | Android-only asmdef present in Android player, absent elsewhere | R2, R7 |
| 8 | `asmdef-exclude-platforms` | asmdef excluded from WebGL | R2, R7 |
| 9 | `editor-folder-depth` | `Assets/A/B/Editor/X.cs` goes to `Assembly-CSharp-Editor` | R2 |
| 10 | `plugins-firstpass` | `Assets/Plugins` is firstpass; firstpass cannot see `Assembly-CSharp` | R2 |
| 11 | `plugins-editor-firstpass` | `Assets/Plugins/Editor` is `Assembly-CSharp-Editor-firstpass` | R2 |
| 12 | `standard-assets-firstpass` | `Assets/Standard Assets` is firstpass | R2 |
| 13 | `define-constraints-true` | constraint holds, assembly compiled | R2 |
| 14 | `define-constraints-false` | constraint fails, assembly absent, consumer gets CS0246 | R2 |
| 15 | `define-constraints-or-not` | `A \|\| B` and `!C` forms | R2 |
| 16 | `version-defines-hit` | package version inside range defines the symbol | R2, R5 |
| 17 | `version-defines-miss` | version outside range, symbol absent | R2, R5 |
| 18 | `version-defines-unity` | `Unity` resource against the editor version | R5 |
| 19 | `override-references-dll` | `overrideReferences` with the listed DLL | R2 |
| 20 | `override-references-missing` | listed DLL absent: UCL1004 info, skipped as Unity does; the code needing it fails | R2 |
| 21 | `no-engine-references` | `noEngineReferences`: MonoBehaviour is CS0246 | R2 |
| 22 | `unsafe-allowed` | `allowUnsafeCode: true` compiles unsafe code | R6 |
| 23 | `unsafe-refused` | unsafe code without the flag is CS0227 | R6 |
| 24 | `csc-rsp-define` | `Assets/csc.rsp -define:` | R5, R6 |
| 25 | `csc-rsp-nowarn` | `-nowarn:` silences a warning | R6 |
| 26 | `csc-rsp-asmdef-local` | `csc.rsp` beside an asmdef replaces the global one for it | R6 |
| 27 | `csc-rsp-warnaserror` | `-warnaserror` turns a warning into exit 2 | R6, R10 |
| 28 | `analyzer-scoped` | `RoslynAnalyzer` DLL in an asmdef folder analyses that assembly and every assembly that reaches it | R8 |
| 29 | `analyzer-global` | analyzer outside any asmdef folder analyses every assembly | R8 |
| 30 | `scripting-defines-per-platform` | `scriptingDefineSymbols` differ per build target group | R5 |
| 31 | `unity-minor-paths` | `UNITY_6000_3_OR_NEWER` branch differs between 6000.0 and 6000.3 | R5, R7 |
| 32 | `package-embedded` | embedded package asmdef with a `versionDefines` entry | R3 |
| 33 | `package-scoped-registry` | scoped registry package from `Library/PackageCache` | R3 |
| 34 | `package-local-file` | `file:` package | R3 |
| 35 | `package-missing` | unresolvable package: UCL3006, exit 3 | R3, R10 |
| 36 | `builtin-module-disabled` | no `com.unity.modules.physics`: `Rigidbody` is CS1069 (the `UnityEngine.dll` facade forwards it to a module that is not referenced) | R3 |
| 37 | `player-editor-type` | runtime script uses `UnityEditor` unguarded: fine in editor, CS0246 in player | R7 |
| 38 | `langversion-too-new` | C# 10 file-scoped namespace fails under C# 9 | R6 |
| 39 | `asmref-sources` | `.asmref` adds a folder to another assembly | R2 |
| 40 | `plugin-platform` | DLL enabled for Android only | R2 |
| 41 | `plugin-auto-reference-off` | Auto Reference off: invisible to `Assembly-CSharp`, visible to an asmdef that lists it | R2 |
| 42 | `plugin-analyzer-label` | analyzer DLL is not a reference | R2, R8 |
| 43 | `auto-referenced-false` | `autoReferenced: false` asmdef invisible to `Assembly-CSharp` | R2 |
| 44 | `unsupported-version` | 2022.3 project: exit 3 | R1, R10 |
| 45 | `bad-asmdef-json` | malformed asmdef: UCL3004, exit 3 | R2, R10 |
| 46 | `hidden-assets` | `Samples~/` and `.hidden/` are ignored | R2 |
| 47 | `package-script-no-asmdef` | package script outside an asmdef: UCL1010, not compiled | R3 |
| 48 | `input-system-defines` | `activeInputHandler: 2` defines both input symbols | R5 |
| 49 | `backend-il2cpp` | `scriptingBackend` IL2CPP for Standalone players; editor cells stay `ENABLE_MONO` | R5 |
| 50 | `development-build` | `--development` defines `DEVELOPMENT_BUILD` | R5 |
| 51 | `nullable-rsp` | `-nullable:enable` gives CS8600-family warnings | R6 |
| 52 | `path-unicode` | `Assets/My Scripts/Ünïcode/` with a space and non-ASCII | R9 |
| 53 | `editorconfig-severity` | `.editorconfig` raises a warning to error | R6 |
| 54 | `duplicate-assembly-name` | two asmdefs with one name: UCL3005 | R2, R10 |
| 55 | `test-assembly` | `UNITY_INCLUDE_TESTS` constraint with the test framework package | R2, R5 |
| 56 | `editor-asmdef-from-runtime` | runtime asmdef references an Editor-only asmdef: dropped in player | R2, R7 |
| 57 | `api-compat-netfx` | G1: per-group API level 3 and editor level 2 with wrapped ProjectSettings lines; System.Memory/System.Buffers NuGet DLLs | R4, R5 |
| 58 | `plugin-native` | G2: a native x86-64 DLL is never a reference (no CS0009) | R2 |
| 59 | `precompiled-reference-absent` | G3: a package code-gen asmdef lists a DLL that does not exist: info, not an error | R2, R3 |
| 60 | `facade-system-runtime` | G4: a DLL built against System.Runtime resolves through the 4.8 facades and the NetStandard shims | R4 |
| 61 | `realistic-netfx-nuget` | G1-G4 together, shaped like the maintainer's real project; clean in every Standalone cell | R2-R5 |
| 62 | `test-editmode` | `ucl test`: every kind of case (pure, parameterised, guarded by defines, engine, unity-only, failing, ignored, explicit, inconclusive, divergent); `tests` in the manifest lists all 31 | B |
| 63 | `ugui-auto-reference` | session 3, cause 1: `UnityEngine.UI` (and `UnityEditor.UI` in the Editor) reach every asmdef assembly without being listed | R2, R3 |
| 64 | `editor-builtin-defines` | session 3, cause 2: built-in symbols E01-E15, `ENABLE_MONO` in every editor cell, `UNITY_INCLUDE_TESTS` as a compiler symbol | R5 |
| 65 | `plugin-same-name` | session 3, cause 3: one precompiled DLL per file name, the highest version (UCL1005 info for the others) | R2, R3 |
| 66 | `package-testables` | session 3, cause 4: package tests only when embedded or in `testables`; test framework assemblies out of players | R2, R3 |
| 67 | `editor-reference-set` | session 3, cause 5: `UnityEngine.dll` facade, platform module, `UnityEditor.Graphs`, platform editor extensions, `Unity.CompilationPipeline.Common` for code-gen | R4 |
| 68 | `analyzer-reach` | session 3, cause 6: global analyzers reach asmdefs, owned ones reach transitive referrers, the editor's own generators run everywhere | R8 |
| 69 | `editor-engine-modules` | session 4, item 1: editor cells take engine modules from `Managed/UnityEngine/`, players from the platform folder, one DLL per name | R4 |
| 70 | `player-collections-checks` | session 4, item 2: players compiled against the editor's engine build define `ENABLE_UNITY_COLLECTIONS_CHECKS` (E16) | R5 |
| 71 | `tests-framework-symbol` | session 4, item 3: `UNITY_TESTS_FRAMEWORK` (D61) in editor cells with `com.unity.test-framework` installed | R2, R5 |
| 72 | `version-gated-symbols` | session 4, item 4: `versionDefines` bounds with Unity suffixes, a pre-release package range, E08 and E17 by editor patch (adds the 6000.3.19f1 stub editor) | R5 |
