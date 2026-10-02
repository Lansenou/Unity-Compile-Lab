# Proposals

Things that are not in the task description, recorded instead of built. Each has a recommended default.

1. **Unity's built-in source generators.** Done in 0.7.0: the DLLs in `Tools/BuildPipeline/Unity.SourceGenerators/`
   (`Tools/Unity.SourceGenerators/` before 6000.3) run on every assembly when analyzers are on, are listed as
   `editor:` analyzers in the JSON, and `--analyzers off` turns them off with the project's. No separate flag.
2. **`ucl` as a pre-review gate for many worktrees.** A shared content-addressed cache across worktrees
   (`--cache-dir ~/.cache/ucl/build`) would make the second worktree's cold run warm. Recommended: allow it,
   since the cache key already contains every input; document it in integration.md.
3. **Schema validation in CI.** Validate every JSON report of the fixture run against
   `schema/result.schema.json` with an independent validator. Recommended: add a small Python or Node
   step only if a dependency-free validator is acceptable.
4. **`ucl mutate`: nightly mutation testing driven by `ucl test`** (session 2, phase C; proposal only, not built).

   *Goal.* Tell a Unity team which of their EditMode tests would notice a bug, on the code those tests can
   actually exercise under .NET, every night, without a Unity licence on the runner.

   *Loop.* For each mutant: change one syntax node of one source file in memory, recompile only the mutated
   assembly with the incremental cache (`--changed`-style invalidation). A body mutation leaves the assembly's
   public surface unchanged, so its dependents need no recompile if their inputs hash uses the metadata-only
   image hash even when full images are emitted; today `ucl test` keys dependents on the full image, so that
   split (emit both, key on the metadata one) is the first change this needs. Then run only the test cases that cover the mutated method (coverage map
   below), classify: killed (a case failed), survived (all passed), timeout, compile error (discarded, as
   Stryker does). Mutants are generated per assembly from the Roslyn syntax tree `ucl` already parses, with the
   cell's defines, so code under an inactive `#if` is never mutated.

   *Coverage map.* One instrumented `ucl test` run first: the full images are compiled with a per-method hit
   counter (a static array increment injected by a Roslyn syntax rewriter, the same trick Stryker.NET uses), and
   each test case's hits are recorded. A mutant is then run only against the cases that hit its method; a
   mutant no case hits is reported "no coverage" without running anything. This is what keeps a night bounded.

   *Stryker.NET reuse versus own mutators.* Recommended: own mutators, about a dozen operators taken from
   Stryker.NET's documented set (arithmetic, equality, boundary, logical, boolean literal, unary, string
   literal, `null` coalescing, statement removal for void calls, LINQ method swaps), applied to `ucl`'s own
   compilations. Stryker.NET (Apache-2.0) is built around MSBuild projects and `dotnet test`/VsTest: it would
   need the export-csproj files and a test adapter, would compile without Unity's per-assembly defines and
   profiles, and would run the engine-touching cases as failures that kill mutants for the wrong reason. Its
   mutator *catalogue* and report format are worth copying (its HTML/JSON mutation report schema, so existing
   viewers work); its runner is not. If a team already runs Stryker, `ucl mutate --format stryker-json` keeps
   the dashboard.

   *Time budget per mutant.* Default: the covering cases' baseline time times 3, plus 2 s (Stryker's default
   formula is similar), capped at 30 s; `--mutant-timeout` overrides. Recompiling one assembly warm costs about
   0.5 to 2 s on the benchmark project (docs/benchmarks.md), so a nightly budget of 2 hours buys roughly
   2,000 to 5,000 mutants on a mid-size project; `--max-mutants` and `--since <ref>` (mutate only methods
   changed since a ref, reusing `--changed`) keep it inside the budget. A per-test timeout needs a test host
   that can stop a case: run mutants in a child `ucl` process per batch and kill it on timeout (CoreCLR has no
   thread abort), which also contains a mutant that crashes the process or loops forever.

   *needs-unity and unity-only cases.* They never kill a mutant here: a mutant covered only by `needs-unity`
   or `unity-only` cases is reported as "not covered here" (its own state, next to killed, survived, timeout
   and no coverage), with the names of those cases, so the report says "this code is tested, but only in
   Unity" instead of claiming a survivor. The mutation score is computed over mutants with at least one
   runnable covering case, and the count of "not covered here" mutants is printed beside it. A `needs-unity`
   case that starts passing or a `failed` baseline case makes the run refuse to start (mutation needs a green
   baseline).

   *Outputs.* Text summary per assembly, JSON (`ucl-mutate/1`) with every mutant (file, line, column,
   operator, original and mutated text, state, covering cases), SARIF results for survivors (so code review
   tools show them on the line), and an optional Stryker-compatible JSON.

   *Cost to build.* About a phase of session 2's size: mutators and rewriter, coverage instrumentation,
   the child-process batch runner with timeouts, reports, fixtures with known kill/survive outcomes. Needs no
   new dependency.
