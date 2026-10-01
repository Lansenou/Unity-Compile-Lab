# Proposals

Things that are not in the task description, recorded instead of built. Each has a recommended default.

1. **Unity's built-in source generators.** Unity 6 ships `Editor/Data/Tools/Unity.SourceGenerators/*.dll`
   (for example the UI Toolkit `[UxmlElement]` generator). Code that uses generated members fails under
   `ucl` until these run. Recommended: load them from the editor install for every assembly when present,
   behind `--unity-generators on|off` (default on), and record which ones ran in the JSON.
2. **`ucl` as a pre-review gate for many worktrees.** A shared content-addressed cache across worktrees
   (`--cache-dir ~/.cache/ucl/build`) would make the second worktree's cold run warm. Recommended: allow it,
   since the cache key already contains every input; document it in integration.md.
3. **Schema validation in CI.** Validate every JSON report of the fixture run against
   `schema/result.schema.json` with an independent validator. Recommended: add a small Python or Node
   step only if a dependency-free validator is acceptable.
