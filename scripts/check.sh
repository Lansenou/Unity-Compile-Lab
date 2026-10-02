#!/usr/bin/env bash
# Local and CI quality gate. CI runs exactly this script.
# Needs the .NET 10 SDK and bash (Git Bash on Windows). Network is used only to
# restore pinned NuGet packages and the local coverage report tool.
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
dotnet build verify -c Release -warnaserror
dotnet tool restore

step "fixtures: SHA256SUMS"
scripts/fixtures-sums.sh --check

step "stub editors and fixture DLLs"
dotnet run --project tests/Ucl.StubBuilder -c Release --no-build -- artifacts/stubs

rm -rf coverage
mkdir -p coverage
# The in-proc collector flushes before VSTest terminates its host. MSBuild's exit-time writer can lose hits.
settings=tests/coverage.runsettings
gate=verify/bin/Release/net10.0/verify.dll
require_report() {
  local reports=("$1"/*/coverage.cobertura.xml)
  if [ "${#reports[@]}" -ne 1 ] || [ ! -s "${reports[0]}" ]; then
    echo "coverage: expected one nonempty collector report under $1" >&2
    exit 1
  fi
}

step "unit tests: Ucl.Core (coverage gate: 90% line)"
dotnet test tests/Ucl.Core.Tests -c Release --no-build \
  --collect "XPlat Code Coverage" --settings "$settings" --results-directory coverage/core \
  -- 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile=**/*.g.cs'
require_report coverage/core

dotnet tool run reportgenerator '-reports:coverage/core/**/coverage.cobertura.xml' \
  -targetdir:coverage/core-merged '-reporttypes:Cobertura;JsonSummary'
dotnet "$gate" --coverage coverage/core-merged/Cobertura.xml 90

step "discovery tests"
dotnet test tests/Ucl.Discovery.Tests -c Release --no-build \
  --collect "XPlat Code Coverage" --settings "$settings" --results-directory coverage/discovery
require_report coverage/discovery

step "integration tests: fixtures, read-only, determinism, architecture (coverage gate: 75% line overall)"
# Synthetic shutdown regression: coverage must survive an exit handler slower than VSTest's shutdown deadline.
UCL_COVERAGE_SLOW_EXIT=1 dotnet test tests/Ucl.Integration.Tests -c Release --no-build \
  --collect "XPlat Code Coverage" --settings "$settings" --results-directory coverage/integration
require_report coverage/integration

dotnet tool run reportgenerator \
  '-reports:coverage/core/**/coverage.cobertura.xml;coverage/discovery/**/coverage.cobertura.xml;coverage/integration/**/coverage.cobertura.xml' \
  -targetdir:coverage/merged '-reporttypes:Cobertura;JsonSummary'
dotnet "$gate" --coverage coverage/merged/Cobertura.xml 75

if [ -d verify ]; then
  step "verify: independent define table and assembly graph"
  scripts/verify.sh
fi

if [ "$mutation" = 1 ]; then
  step "mutation check: remove one platform define, at least one fixture must fail"
  scripts/mutation.sh
fi

step "all checks passed"
