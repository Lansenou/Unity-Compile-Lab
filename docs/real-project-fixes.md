# Real-project findings G1 to G5

On 2026-10-02 the maintainer ran `ucl` 0.4.0 on a real Unity 6000.3.19f1 project (100 assemblies, NuGet DLLs in
`Assets/Plugins/`, one native plugin) that compiles with 0 errors in the Editor. `ucl check` reported 1581 false
errors. Each root cause below has a fixture that reproduced the failure on the stub editor before the fix
("red run", `ucl` built from `067e54c`, the `v0.4.0` code) and passes after it. Paths in the red output are
shortened: `<project>` is the temporary fixture copy, `<stubs>` is `artifacts/stubs/editors`.

## G1. The API compatibility level was ignored

**Root cause.** Two faults. (1) `SimpleYaml` ended a mapping at the first line indented deeper than a line
that already had a value. The Editor wraps long values onto such continuation lines
(`iOSLaunchScreenPortrait: {fileID: ..., guid: ...,\n    type: 3}`), so everything after the first wrapped
value, `apiCompatibilityLevelPerPlatform` included, was lost and `apiCompatibilityLevel: 6` (.NET Standard)
won in every cell. (2) The level was per cell; Unity's is per assembly: Editor-only assemblies follow
`editorAssembliesCompatibilityLevel` (docs/defines.md, rows A01-A06).

**Fix.** Continuation lines are joined to their value; the level, the D33/D34 defines and the reference set
are chosen per assembly; the .NET Framework reference set is Unity's 17 core libraries plus every facade, and
the .NET Standard set gained `shims/netstandard` and `Extensions/2.0.0`.

**Fixture** `api-compat-netfx` (and `realistic-netfx-nuget`). Red run:

```
== 6000.3.2f1 editor StandaloneWindows64
Assets/Scripts/ScoreBuffer.cs(12,9): error CS0433: The type 'Span<T>' exists in both 'System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=null' and 'netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Assets/Scripts/ScoreBuffer.cs(12,33): error CS0121: The call is ambiguous between the following methods or properties: 'System.MemoryExtensions.AsSpan<T>(T[]) [<stubs>/6000.3.2f1/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll]' and 'System.MemoryExtensions.AsSpan<T>(T[]) [<project>/Assets/Plugins/System.Memory.dll]'
Assets/Scripts/ScoreBuffer.cs(13,26): error CS0433: The type 'ArrayPool<T>' exists in both 'System.Buffers, Version=4.0.3.0, Culture=neutral, PublicKeyToken=null' and 'netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Assets/Scripts/ScoreBuffer.cs(14,9): error CS0433: The type 'ArrayPool<T>' exists in both 'System.Buffers, Version=4.0.3.0, Culture=neutral, PublicKeyToken=null' and 'netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Assets/Scripts/ScoreBuffer.cs(15,39): error CS0121: The call is ambiguous between the following methods or properties: 'System.MemoryExtensions.AsSpan(string) [...]' and 'System.MemoryExtensions.AsSpan(string) [...]'
skipped Assembly-CSharp-Editor: dependency 'Assembly-CSharp' has errors
result: 5 errors, 0 warnings, 2 assemblies (1 skipped), exit 1
== 6000.3.2f1 player StandaloneWindows64
(the same five errors)
```

After the fix both Standalone cells are clean; the Android cells keep these five errors, because Android has
no per-group entry and stays on .NET Standard 2.1, as it would in the Editor.

## G2. A native plugin was passed to the compiler

**Root cause.** Every `.dll` with a `.meta` was a reference candidate. **Fix.** Discovery reads the PE headers
of every DLL (`PluginBinary`); one without a CLI header is a native plugin and never a reference.

**Fixture** `plugin-native` (a generated x86-64 PE with no COR20 header). Red run:

```
== 6000.0.30f1 editor StandaloneWindows64
error CS0009: Metadata file '<project>/Assets/ThirdParty/Plugins/x86_64/Ucl.Fixture.Native.dll' could not be opened -- PE image doesn't contain managed metadata.
result: 1 error, 0 warnings, 1 assembly (0 skipped), exit 1
== 6000.0.30f1 player StandaloneWindows64
(the same error)
```

## G3. A precompiled reference the project does not contain was an error

**Root cause.** `ucl` invented `UCL1004` as an error. Unity looks each `precompiledReferences` name up among
the precompiled assemblies it knows and skips a missing one without a message (UnityCsReference,
`EditorBuildRules`). **Fix.** `UCL1004` is info: reported, never counted. The fixture's Editor-only code-gen
asmdef compiles. Whether the Editor finds `Unity.IL2CPP.dll` somewhere `ucl` does not look is a question for
`ucl bee-diff` on the real project: the reference lists will differ if it does.

**Fixture** `precompiled-reference-absent`. Red run (the fixture's first draft also had a script error, fixed
before the green run):

```
== 6000.0.30f1 editor StandaloneWindows64
Packages/com.vendor.tools/Editor/CodeGen/Vendor.Tools.CodeGen.asmdef: error UCL1004: Assembly 'Vendor.Tools.CodeGen' lists precompiled reference 'Unity.IL2CPP.dll', which is not in the project
```

## G4. The facade gap

**Root cause.** A consequence of G1 (the wrong profile) plus a missing folder: `ucl`'s .NET Standard set lacked
`NetStandard/compat/2.1.0/shims/netstandard`, where `System.Runtime.dll` forwards to `netstandard`. **Fix.** G1's
reference sets. The stub editor now has `unity-4.8-api/Facades/System.Runtime.dll`,
`unity-4.8-api/Facades/netstandard.dll` (2.0.0.0), `shims/netstandard/System.Runtime.dll` and
`shims/netfx/mscorlib.dll`.

**Fixture** `facade-system-runtime` (`Vendor.Contracts.dll`, compiled against a `System.Runtime` 4.0.0.0
contract). Red run:

```
== 6000.0.30f1 player StandaloneWindows64
Assets/Scripts/Leaderboard.cs(10,19): error CS0012: The type 'Object' is defined in an assembly that is not referenced. You must add a reference to assembly 'System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
Assets/Scripts/Leaderboard.cs(10,25): error CS0012: (the same)
== 6000.0.30f1 player Android
(the same two errors)
```

## G5. Cascades

**Root cause.** Not a bug: when an assembly fails, its dependents are not compiled (Unity does the same), so
one failure made 73 of 100 assemblies "skipped". The report listed every diagnostic first and said only
"dependency 'X' has errors", naming the direct dependency even when it was itself skipped.

**Fix (report only; behaviour unchanged).** A skipped assembly's reason names the root failure
(`dependency 'Core' failed (through 'Mid', skipped)`) and JSON adds `blockedBy`. The text report leads each
cell with "root failures": every failed assembly, ranked by how many assemblies it blocks, with its three most
frequent error ids and the first instance of each. `ucl check --summary` prints only that block and the result
line. Tests: `CascadeReportTests`.
