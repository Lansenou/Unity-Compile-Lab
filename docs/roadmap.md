# Roadmap

Out of scope for v1.0, recorded so the boundary is explicit.

* Editors before Unity 6 (2022.3 LTS and older): different define series, `Standard Assets` rules, C#
  8 language level and older API compatibility levels. The graph rules would mostly carry over; the define
  table and the oracle corpus would need a second set of rows.
* Shader compilation.
* IL2CPP code generation and IL2CPP-only errors (generic sharing limits, AOT restrictions).
* Burst compilation and Burst-only errors.
* Running tests (Unity Test Framework).
* Asset or scene validation, serialized-field checks, missing-script detection.
* Domain reload behaviour, `[InitializeOnLoad]` side effects, `RuntimeInitializeOnLoadMethod` ordering.
* Platforms beyond the six of docs/platforms.md (tvOS, visionOS, UWP, consoles, Linux server): the name
  tables are the only change; consoles need their own (licensed) reference DLLs.
