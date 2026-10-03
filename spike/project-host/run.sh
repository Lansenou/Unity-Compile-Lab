#!/usr/bin/env bash
# Windows-only spike tools: Git Bash, PowerShell 7, .NET 10, licensed Unity.
# Call the shared machine's Unity capacity guard before this build command.
set -euo pipefail
cd "$(dirname "$0")/../.."
filtered=()
IFS=: read -ra entries <<< "$PATH"
for entry in "${entries[@]}"; do
  case "${entry,,}" in *sherpa-onnx*) ;; *) filtered+=("$entry") ;; esac
done
PATH=$(IFS=:; echo "${filtered[*]}")
export PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
command=${1:?usage: run.sh prepare|key|stage|build|compile|measure|summarize [arguments]}
shift
case "$command" in
  prepare) "${PYTHON:-python}" spike/project-host/prepare.py "$@" ;;
  summarize) "${PYTHON:-python}" spike/project-host/summarize.py "$@" ;;
  key) pwsh -NoProfile -File spike/project-host/key.ps1 "$@" ;;
  stage) pwsh -NoProfile -File spike/project-host/stage.ps1 "$@" ;;
  build) pwsh -NoProfile -File spike/project-host/build.ps1 "$@" ;;
  compile)
    dotnet build spike/project-host/compiler -c Release
    dotnet spike/project-host/compiler/bin/Release/net10.0/Compiler.dll "$@"
    ;;
  measure) pwsh -NoProfile -File spike/project-host/measure.ps1 "$@" ;;
  *) echo "unknown command: $command" >&2; exit 2 ;;
esac
