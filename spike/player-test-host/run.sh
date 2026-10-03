#!/usr/bin/env bash
# Windows-only reproduction: Git Bash, PowerShell 7, .NET 10, licensed Unity.
set -euo pipefail
cd "$(dirname "$0")/../.."
editor=${1:?usage: run.sh "<editor>"}
spike=spike/player-test-host
scratch=artifacts/player-spike
mkdir -p "$scratch/baseline" "$spike/host/Assets/Plugins"
filtered=()
IFS=: read -ra entries <<< "$PATH"
for entry in "${entries[@]}"; do
  case "${entry,,}" in *sherpa-onnx*) ;; *) filtered+=("$entry") ;; esac
done
PATH=$(IFS=:; echo "${filtered[*]}")
export PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet restore "$spike/tests/PlayerSpikeTests.csproj"
nuget_root=${NUGET_PACKAGES:-$(cygpath -u "$USERPROFILE")/.nuget/packages}
cp "$nuget_root/nunit/3.14.0/lib/netstandard2.0/nunit.framework.dll" "$spike/host/Assets/Plugins/"
project=$(cygpath -m "$PWD/$spike/host")
scratch_abs=$(cygpath -m "$PWD/$scratch")
pwsh -NoProfile -File "$spike/check-editor.ps1" -Project "$project"
"$editor" -batchmode -nographics -quit -projectPath "$project" \
  -executeMethod SpikeBuild.Build -playerOutput "$scratch_abs/baseline/PlayerHost.exe" \
  -oracle "$scratch_abs/oracle.json" -logFile "$scratch_abs/build-reproduce.log"
dotnet build "$spike/tests/PlayerSpikeTests.csproj" -c Release \
  "-p:PlayerManaged=$scratch_abs/baseline/PlayerHost_Data/Managed"
cp "$spike/host/Assets/Plugins/nunit.framework.dll" "$spike/tests/bin/Release/netstandard2.1/"
pwsh -NoProfile -File "$spike/measure.ps1" \
  -Player "$scratch_abs/baseline/PlayerHost.exe" \
  -TestAssembly "$(cygpath -m "$PWD/$spike/tests/bin/Release/netstandard2.1/PlayerSpikeTests.dll")" \
  -Output "$scratch_abs/reproduced-runs"
