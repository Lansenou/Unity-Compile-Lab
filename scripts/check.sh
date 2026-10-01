#!/usr/bin/env bash
# Local and CI quality gate. CI runs exactly this script.
# Needs the .NET 10 SDK and bash (Git Bash on Windows). Network is used only to
# restore the pinned NuGet packages (Directory.Packages.props).
#
#   scripts/check.sh              full gate
#   scripts/check.sh --mutation   also run the define-table mutation check
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

mutation=0
for arg in "$@"; do
  case $arg in
    --mutation) mutation=1 ;;
    *) echo "unknown argument: $arg"; exit 2 ;;
  esac
done

step() { printf '\n== %s\n' "$*"; }

step "format (dotnet format --verify-no-changes)"
dotnet format Ucl.slnx --verify-no-changes --severity warn

step "build (warnings are errors)"
dotnet build Ucl.slnx -c Release -warnaserror

step "fixtures: SHA256SUMS"
scripts/fixtures-sums.sh --check

step "stub editors and fixture DLLs"
dotnet run --project tests/Ucl.StubBuilder -c Release --no-build -- artifacts/stubs

rm -rf coverage
mkdir -p coverage
# MSBuild on Windows needs C:/ paths, not Git Bash's /c/ form.
native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }
cov=$(native "$PWD/coverage")

step "unit tests: Ucl.Core (coverage gate: 90% line)"
dotnet test tests/Ucl.Core.Tests -c Release --no-build \
  -p:CollectCoverage=true -p:Include='[Ucl.Core]*' -p:Threshold=90 -p:ThresholdType=line \
  -p:CoverletOutput="$cov/core.json"

step "discovery tests"
dotnet test tests/Ucl.Discovery.Tests -c Release --no-build \
  -p:CollectCoverage=true -p:Include='[Ucl.*]*' -p:Exclude='[Ucl.*.Tests]*' \
  -p:CoverletOutput="$cov/discovery.json" -p:MergeWith="$cov/core.json"

step "integration tests: fixtures, read-only, determinism, architecture (coverage gate: 75% line overall)"
dotnet test tests/Ucl.Integration.Tests -c Release --no-build \
  -p:CollectCoverage=true -p:Include='[Ucl.*]*%2c[ucl]*' -p:Exclude='[Ucl.*.Tests]*%2c[Ucl.StubBuilder]*' \
  -p:MergeWith="$cov/discovery.json" -p:CoverletOutput="$cov/" \
  -p:CoverletOutputFormat='json%2ccobertura' -p:Threshold=75 -p:ThresholdType=line -p:ThresholdStat=total

if [ -d verify ]; then
  step "verify: independent define table and assembly graph"
  scripts/verify.sh
fi

if [ "$mutation" = 1 ]; then
  step "mutation check: remove one platform define, at least one fixture must fail"
  scripts/mutation.sh
fi

step "all checks passed"
