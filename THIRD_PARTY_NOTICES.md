# Third-party notices

Dependencies of the `ucl` tool and its tests, restored from NuGet at build time (versions pinned in
`Directory.Packages.props` and `.config/dotnet-tools.json`). None is vendored in this repository.

| Package | Version | Licence | Used by |
|---|---|---|---|
| Microsoft.CodeAnalysis.CSharp (Roslyn) | 5.9.0 | MIT | `ucl` (compiler, analyzers) |
| Microsoft.CodeAnalysis.Common | 5.9.0 | MIT | `ucl` (transitive) |
| System.Collections.Immutable, System.Reflection.Metadata | (Roslyn's) | MIT | `ucl` (transitive) |
| .NET runtime (self-contained release binaries) | 10.0 | MIT | release binaries |
| NUnit | 3.14.0 | MIT | `ucl test` (the test host); tests: materialised as the fixtures' `nunit.framework.dll` |
| NETStandard.Library.Ref | 2.1.0 | MIT | tests only: `netstandard.dll` for the stub editors |
| xunit, xunit.runner.visualstudio | 2.9.3, 3.1.5 | Apache-2.0 | tests only |
| Microsoft.NET.Test.Sdk | 18.10.1 | MIT | tests only |
| coverlet.collector | 10.1.0 | MIT | tests only |
| dotnet-reportgenerator-globaltool | 5.5.11 | Apache-2.0 | development/CI coverage report merging (`.config/dotnet-tools.json`) |

Unity software is not a dependency and is not distributed; see [docs/licensing.md](docs/licensing.md).
