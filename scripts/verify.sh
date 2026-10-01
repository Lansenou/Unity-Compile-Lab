#!/usr/bin/env bash
# Independent check of the define table and the assembly graph: builds verify/ and the CLI (Release), then
# compares verify's own plan with `ucl graph --format json` for every cell of fixtures/manifest.json.
# Exit code is verify's: 0 all cells agree, 1 a difference, 2 usage.
#   scripts/verify.sh
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
root=$PWD

dotnet build verify/Verify.csproj -c Release -nologo -v quiet >/dev/null
dotnet build src/Ucl.Cli/Ucl.Cli.csproj -c Release -nologo -v quiet >/dev/null

set +e
dotnet "$root/verify/bin/Release/net10.0/verify.dll" "$root/fixtures" dotnet "$root/src/Ucl.Cli/bin/Release/net10.0/ucl.dll"
code=$?
set -e
exit $code
