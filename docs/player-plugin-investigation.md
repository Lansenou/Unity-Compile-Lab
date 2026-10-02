# Player plugin reference investigation

The v0.8.85 counts-only report establishes a compiler-input discrepancy: 12 plugins omitted by
Unity on two player targets, 344 duplicate-type/overload errors in ucl, and zero Editor errors.
It does not establish a general exclusion rule. Do not turn correlation with Editor settings
into a player-wide filter or filter all System.* assemblies by prefix.

## Public evidence

Use [Unity 6.3 plugin inspector](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-in-inspector.html):
Any Platform changes individual platform toggles to exclusion; Editor applies to Play/Edit mode;
Standalone applies to Windows/Linux/macOS. Auto Reference controls compiler references separately
from build inclusion. Validate References checks existence and strong-name compatibility of dependencies.
The supplied platformData fragment does not show Auto Reference, validation or define constraints.

Pinned [UnityCsReference 6000.3.25f1](https://github.com/Unity-Technologies/UnityCsReference/tree/2f6cef60096cf50741d933becf101bf3186719dd):

* [PrecompiledAssembly.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/PrecompiledAssembly.cs):
  GetPrecompiledAssembliesDictionary forwards compilation options, target and extra defines to
  GetPrecompiledAssembliesInternal. That calls GetPrecompiledAssembliesNative, bound to the
  unpublished C++ GetPrecompiledAssembliesManaged. FilenameToPrecompiledAssembly consumes
  that returned list. Duplicate filenames keep the first entry unless the retained entry has
  native Redirected set, in which case a later entry replaces it. This establishes a filename/
  redirected-order rule, not framework-identity suppression; the native list order and flag
  derivation remain unpublished. The managed implementation does not disclose platform/profile
  suppression.
* [EditorBuildRules.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/EditorBuildRules.cs):
  GetPrecompiledReferences excludes native-flagged EditorOnly assemblies from runtime script
  assemblies and checks target compatibility through UseForMono (UseForDotNet for WSA).
  Neither this loop nor its target check filters a framework assembly name or ValidateAssembly.
  Native flag derivation is not published, so this does not explain the reported omissions.
* [AssemblyFlags.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Editor/Mono/Scripting/ScriptCompilation/AssemblyFlags.cs):
  EditorOnly, UseForMono, UseForDotNet and ValidateAssembly are separate bits, synchronized with
  native ManagedAssemblyFlags. Treating validation as reference permission has no public basis.

[NuGetForUnity issue 512](https://github.com/GlitchEnzo/NuGetForUnity/issues/512) explains its detection
of already imported libraries through CompilationPipeline.GetAssemblies; it concerns dependency
installation, not a Unity compiler exclusion rule. It is not evidence for dropping installed DLLs.

## Experiment needed before a fix

Use a fresh public project with only original repository stubs; never copy a private project,
package source or real framework DLL into a fixture. Use the existing Vendor.Math stub for the
unique-identity control. For identity tests, compile the same original API under a framework
assembly name; avoid overlapping BCL types so identity and type overlap are separate experiments.
Record exact Unity patch, API compatibility profile, target and backend.

| Variable | Receiving control | Changed case | What it distinguishes |
|---|---|---|---|
| Editor compatibility | Any on, Exclude Editor off, Editor on | Any on, Exclude Editor on, Editor off | Editor interaction with player references |
| Assembly identity | Unique original assembly name | Profile assembly name, same original API | Name/profile suppression |
| File name | Name agrees with assembly identity | Rename DLL only, preserve PE identity | File-name versus assembly-name filtering |
| Validation | On, all dependencies available | Off, same DLL and platform settings | Validation effects |
| Dependencies | All original dependencies available | One original dependency unavailable | Validation failure versus profile collision |

Keep Auto Reference on and defineConstraints empty for the first matrix. Confirm the serialized
Importer settings after Unity import; do not assume a legacy platform key was interpreted as intended.
Then vary only one row at a time. Compile for StandaloneWindows64 and WebGL; retain the actual
player *P.dag response files locally. Verify references even if the plugin API is unused, since
Auto Reference is meant to select references independently of API usage.

## Acceptance

Once a public experiment establishes the rule, commit an original receiving/dropped fixture with
expectations derived from that experiment. It must fail before the rule change and pass afterward;
update Core, the independent verifier, docs and fixture checksums together.

Run `ucl bee-diff <public-project> --format json` against each recorded player DAG and require no
plugin reference differences in both receiving and dropped cases. The existing BeeDiffTests exercise
player DAG comparisons; a hand-written response file tests the comparer but does not prove Unity's
exclusion rule. Do not call such a synthetic file actual Unity output.

For a private rerun, the maintainer keeps paths and inventories private and reports counts/booleans
only: tested cells, omitted/received plugin count, reference-difference count and error count. Until
that experiment is available, the player-reference mismatch remains open.
