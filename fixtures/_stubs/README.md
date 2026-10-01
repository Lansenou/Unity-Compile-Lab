# Fixture stubs

Everything under `fixtures/_stubs/` is original source code written for the `ucl` conformance fixtures and
licensed under Apache-2.0, like the rest of the repository. None of it is Unity code, and no Unity binary is
ever committed.

* `editor/<Assembly>/` - a tiny stand-in for the Unity 6 API surface (`UnityEngine.CoreModule`,
  `UnityEngine.PhysicsModule`, `UnityEngine.InputLegacyModule`, `UnityEditor.CoreModule`). It declares only
  the types and members the fixtures use, with Unity's namespaces and signatures; every body is `throw null;`
  or trivial. It exists so that fixtures compile against something shaped like the real editor DLLs.
* `dlls/<Name>/` - sources of stand-in third-party plugin DLLs (for example `Vendor.Math`).
* `analyzers/<Name>/` - sources of the fixture Roslyn analyzer (`Ucl.Fixture.Analyzer`, reports `UFX001`
  on any type whose name contains `Bad`) and source generator (`Ucl.Fixture.Generator`, adds
  `Ucl.Generated.GeneratedMarker`).

`tests/Ucl.StubBuilder` compiles these sources deterministically into fake editor installs
(`artifacts/stubs/editors/<version>/Editor/Data/...`, the Unity Hub layout) and into `artifacts/stubs/dlls/`:

```
dotnet run --project tests/Ucl.StubBuilder            # writes artifacts/stubs
dotnet run --project tests/Ucl.StubBuilder -- <dir>   # writes <dir>
```

Assertions that need the real Unity DLLs are marked `realEditor: true` in `fixtures/manifest.json` and run
only with `UCL_REAL_EDITOR=1`.
