# ucl for AI coding agents

Run this before asking for review, from the Unity project root:

```sh
ucl check . --format json > ucl.json; echo "exit $?"
```

* Exit `0`: compiles (warnings allowed). `1`: compile, analyzer or Unity-rule errors. `2`: only warnings
  promoted to errors. `3`: configuration problem (no editor, unresolved package, bad asmdef, wrong Unity
  version): fix the environment, not the code. `4`: ucl bug.
* Read `cells[].diagnostics[]` where `severity` is `"error"`: `file` (project-relative), `line`, `column`,
  `id` (`CS0103`, ...), `message`. `origin` is `compiler`, `analyzer` or `ucl`.
* `cells[].assemblies[]` with `status: "skipped"` were not compiled because a dependency failed; fix the
  dependency's errors first.
* Fast loop on a branch: `ucl check . --changed origin/main --format json`.
* Before merging, the platform matrix:
  `ucl check . --target editor --target player --platform StandaloneWindows64 --platform WebGL --platform iOS --platform Android --format json`
* Why is a file in that assembly, with those defines? `ucl explain Assets/Path/File.cs`.
