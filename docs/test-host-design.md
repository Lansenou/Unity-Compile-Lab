# Project player test host design

The optional `ucl test --host` path keeps managed cases on the existing child .NET runner and sends eligible engine cases to a per-project Windows Mono player. Editor API and source-layout dependent cases retain an explicit Editor exclusion reason. Graphics are enabled unless `--nographics` is requested.

Two approaches were considered. Reusing an arbitrary prebuilt player is small but loses project settings, resources and package identity. Building a keyed scratch copy preserves these inputs and is the selected approach. The input project is read-only; the user-level cache owns scratch projects and players.

The key includes the Unity version and revision, settings, assets, package inputs and resolved package contents, inherited analyzer configuration, bootstrap sources, target, build options and protocol. A separate manifest hashes every player output file and invalidates corrupt or incomplete entries. Each build and run has a unique directory, and cache publication occurs only after a successful build and manifest validation.

The first supported adapter pins Unity 6000.3.19f1 and the measured Unity Test Framework internal API shape. Unsupported versions fail before executing cases. The production command emits existing ucl summaries and NUnit-compatible XML, preserves case accounting, and returns failure for failed cases or host infrastructure errors. Filters retain the existing regular-expression contract, with full class and namespace names selecting their cases.

Measurements must compare the same source revision with fresh Editor results. Outcome mismatches remain visible and must move to Editor ownership before a gate can claim parity. Cold build cost, warm fingerprint cost and simultaneous-run behavior are reported separately. A previous spike timing is not a production benchmark.

Audited Editor ownership is an explicit pre-execution input to the managed host. It retains
zero-loss discovery and does not rewrite any completed managed failure. The optional public
`TestHost.Run` and `NUnitHost.Run` input is backward compatible for existing callers.
