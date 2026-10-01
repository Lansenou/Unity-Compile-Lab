# Licensing

`ucl` is Apache-2.0 (see `LICENSE` and `NOTICE`). This page records why the repository and its CI contain
no Unity software, and what a user is responsible for.

## What is never committed or published

* Unity Editor DLLs (`UnityEngine.*.dll`, `UnityEditor*.dll`), the Unity Editor, and any Unity reference
  assemblies.
* Unity registry package sources or tarballs (`com.unity.*`).

`ucl` reads these at run time from the user's own machine: an installed editor, `Library/PackageCache`, or
the download cache `~/.cache/ucl/packages` that the user fills with `ucl fetch`. Nothing is redistributed.

## Unity terms that decide this (retrieved 2026-10-01)

Unity Editor Software Terms, https://unity.com/legal/editor-terms-of-service/software, last updated
2026-06-30:

* Section 2.1: "You may not make available all or any part of the Unity Editor or its functionality ...
  through any cloud, hosting, application services provider, service bureau, software-as-a-service
  (SaaS) ... without a separate grant of rights from Unity."
* Section 2.8: "In no event may third party service providers directly or indirectly distribute, run or
  use the Unity Editor ... executed on or simulated by the cloud or a remote server."
* Section 2.7 (Unity Package Manager): "Your use of a Package may be subject to separate or additional
  terms and conditions and/or privacy policy, as indicated in such Package."

The terms do not clearly allow downloading editor DLLs onto a hosted CI runner owned by a third party
(GitHub). **Decision: CI uses only the self-written reference stubs** in `fixtures/_stubs/`. The optional
real-editor job (`UCL_REAL_EDITOR=1`) runs only on a maintainer's own licensed machine or self-hosted
runner; see [oracle.md](oracle.md). If Unity grants explicit permission later, the job can be added to
`.github/workflows/ci.yml` and this page updated.

## The stubs

`fixtures/_stubs/` holds original C# declarations with the same names as a handful of Unity types
(`MonoBehaviour`, `Rigidbody`, `EditorWindow`, ...) so that fixtures compile. They contain no Unity code,
no copied documentation text, and no implementation. Names of public APIs are used only for
interoperability with user code. They are Apache-2.0 like the rest of the repository.

## Using ucl on your project

* You need a Unity 6 install that you are licensed to use; `ucl` only reads its managed DLLs.
* `ucl fetch` downloads packages from the registries your `Packages/manifest.json` names, into your own
  cache, for your own use with that project. The packages' own licences (for most `com.unity.*` packages
  the Unity Companion License) apply to them as they would inside Unity.
* `ucl` does not activate, emulate or bypass Unity licensing, and needs no Unity account.
