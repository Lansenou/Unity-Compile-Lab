#!/usr/bin/env bash
# Records what a real, licensed Unity 6 Editor reports for the fixture corpus (docs/oracle.md).
# Run it only on a machine where you are licensed to run Unity; never in hosted CI (docs/licensing.md).
#
#   oracle/record.sh [--timeout SECONDS] [--keep] <unity-executable> <version-label> [fixture-filter] [out-dir]
#
#   unity-executable  the Unity binary (.../Editor/Unity, .../Editor/Unity.exe, or a Unity.app bundle)
#   version-label     e.g. 6000.0.30f1; only manifest cells whose unityVersion equals it are recorded
#   fixture-filter    bash glob on the fixture name, default '*' (e.g. 'asmdef-*', 'compile-error')
#   out-dir           default oracle/results/<version-label>/
#   --timeout N       seconds per cell before Unity is killed (default 900, or $UCL_ORACLE_TIMEOUT)
#   --keep            keep the temporary project copies (path printed per cell)
#
# Writes <out-dir>/<fixture>.json (schema ucl-oracle/1) and <out-dir>/logs/*.log (do not commit logs).
# Needs bash, jq, the .NET SDK (to build the fixture DLLs) and the Unity build support module for every
# platform the selected cells use. macOS, Linux and Git Bash on Windows; record.ps1 is the PowerShell twin.
set -euo pipefail

usage() { sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'; }

timeout=${UCL_ORACLE_TIMEOUT:-900}
keep=0
positional=()
while [ $# -gt 0 ]; do
  case $1 in
    --timeout) timeout=${2:?--timeout needs a value}; shift 2 ;;
    --keep) keep=1; shift ;;
    -h|--help) usage; exit 0 ;;
    -*) echo "record.sh: unknown option $1" >&2; usage >&2; exit 2 ;;
    *) positional+=("$1"); shift ;;
  esac
done
if [ ${#positional[@]} -lt 2 ] || [ ${#positional[@]} -gt 4 ]; then usage >&2; exit 2; fi
unity=${positional[0]}
version=${positional[1]}
filter=${positional[2]:-*}
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
outdir=${positional[3]:-$root/oracle/results/$version}
manifest="$root/fixtures/manifest.json"
here="$root/oracle"

case $timeout in ''|*[!0-9]*) echo "record.sh: --timeout must be a whole number of seconds" >&2; exit 2 ;; esac
command -v jq >/dev/null || { echo "record.sh: jq is required" >&2; exit 2; }
case $unity in *.app|*.app/) unity="${unity%/}/Contents/MacOS/Unity" ;; esac
[ -x "$unity" ] || { echo "record.sh: not an executable: $unity" >&2; exit 2; }

case "$(uname -s)" in
  Darwin) hostos=macos ;;
  Linux) hostos=linux ;;
  MINGW*|MSYS*|CYGWIN*) hostos=windows ;;
  *) hostos=unknown ;;
esac

# Paths handed to Unity: Windows form under Git Bash.
native() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi; }

# manifest platform -> Unity -buildTarget name
build_target() {
  case $1 in
    StandaloneWindows64) echo Win64 ;;
    StandaloneOSX) echo OSXUniversal ;;
    StandaloneLinux64) echo Linux64 ;;
    iOS) echo iOS ;;
    Android) echo Android ;;
    WebGL) echo WebGL ;;
    *) return 1 ;;
  esac
}

# Prints "<assembly>\t<define>" (and "<assembly>\t" once) for a Bee compiler response file.
rsp_defines() {
  awk '
    { sub(/\r$/, "") }
    /^[-\/]out:/ { o = substr($0, 6); gsub(/"/, "", o); gsub(/\\/, "/", o); sub(/.*\//, "", o); sub(/\.dll$/, "", o); name = o }
    /^[-\/](define|d):/ { d = $0; sub(/^[-\/](define|d):/, "", d); gsub(/"/, "", d); n = split(d, parts, /[;,]/); for (i = 1; i <= n; i++) if (parts[i] != "") defs[++nd] = parts[i] }
    END { if (name != "") { print name "\t"; for (i = 1; i <= nd; i++) print name "\t" defs[i] } }
  ' "$1"
}

# Runs Unity with a wall-clock limit; sets run_exit and run_timed_out.
run_unity() {
  run_timed_out=0
  "$unity" "$@" >/dev/null 2>&1 &
  local pid=$! waited=0
  while kill -0 "$pid" 2>/dev/null; do
    if [ "$waited" -ge "$timeout" ]; then
      run_timed_out=1
      kill "$pid" 2>/dev/null || true
      sleep 10
      kill -9 "$pid" 2>/dev/null || true
      break
    fi
    sleep 1
    waited=$((waited + 1))
  done
  set +e
  wait "$pid" 2>/dev/null
  run_exit=$?
  set -e
}

echo "== building fixture DLLs (tests/Ucl.StubBuilder)"
(cd "$root" && dotnet run --project tests/Ucl.StubBuilder -- artifacts/stubs >/dev/null)
dlls="$root/artifacts/stubs/dlls"

recorded=0

for name in $(jq -r '.fixtures[].name' "$manifest"); do
  # shellcheck disable=SC2053 # the filter is a glob on purpose
  [[ $name == $filter ]] || continue
  fixture=$(jq -c --arg n "$name" '.fixtures[] | select(.name == $n)' "$manifest")
  indexes=$(jq -r --arg v "$version" '.cells | to_entries[] | select(.value.unityVersion == $v) | .key' <<<"$fixture")
  [ -n "$indexes" ] || continue
  cells='[]'
  for i in $indexes; do
    cell=$(jq -c ".cells[$i]" <<<"$fixture")
    target=$(jq -r .target <<<"$cell")
    platform=$(jq -r .platform <<<"$cell")
    extra=$(jq -c '.extraArgs // []' <<<"$cell")
    label="$name $target $platform$(jq -r 'map(" " + .) | join("")' <<<"$extra")"
    if ! bt=$(build_target "$platform"); then echo "skip $label: unknown platform" >&2; continue; fi
    development=0
    unsupported=""
    for a in $(jq -r '.[]' <<<"$extra"); do
      case $a in --development) development=1 ;; *) unsupported="$unsupported $a" ;; esac
    done
    if [ -n "$unsupported" ]; then echo "skip $label: no Unity equivalent for$unsupported" >&2; continue; fi

    tmp=$(mktemp -d "${TMPDIR:-/tmp}/ucl-oracle.XXXXXX")
    project="$tmp/project"
    cp -R "$root/fixtures/$name" "$project"
    while IFS=$'\t' read -r dll to; do
      [ -n "$dll" ] || continue
      mkdir -p "$(dirname "$project/$to")"
      cp "$dlls/$dll.dll" "$project/$to"
    done < <(jq -r '(.materialize // [])[] | "\(.dll)\t\(.to)"' <<<"$fixture")

    log="$tmp/editor.log"
    args=(-batchmode -nographics -quit -projectPath "$(native "$project")" -buildTarget "$bt" -logFile "$(native "$log")")
    if [ "$target" = player ]; then
      mkdir -p "$project/Packages"
      cp -R "$here/package/com.ucl.oracle" "$project/Packages/com.ucl.oracle"
      args+=(-executeMethod Ucl.Oracle.CompilePlayer -uclOutDir "$(native "$tmp/player-out")")
      [ "$development" = 0 ] || args+=(-uclDevelopment)
    fi

    echo "== $label ($version)"
    run_unity "${args[@]}"
    touch "$log"
    tr -d '\r' <"$log" >"$tmp/log.txt"
    safe=$(printf '%s' "$name.$target.$platform$(jq -r 'map("." + ltrimstr("--")) | join("")' <<<"$extra")" | tr -c 'A-Za-z0-9._-' '_')
    mkdir -p "$outdir/logs"
    cp "$log" "$outdir/logs/$safe.log"

    all=$(bash "$here/parse-log.sh" --project-path "$(native "$project")" "$tmp/log.txt")
    compile_errors=0
    grep -q '^Scripts have compiler errors' "$tmp/log.txt" && compile_errors=1
    [ "$(jq '[.diagnostics[] | select(.severity == "error")] | length' <<<"$all")" = 0 ] || compile_errors=1

    rsp_lines=""
    if [ "$run_timed_out" = 1 ]; then
      status=timeout
      parsed=$all
    elif [ "$target" = player ]; then
      if grep -q 'UCL-ORACLE-END' "$tmp/log.txt"; then
        status=ok
        parsed=$(bash "$here/parse-log.sh" --project-path "$(native "$project")" --section player "$tmp/log.txt")
        while IFS= read -r rsp; do
          if [ -f "$project/$rsp" ]; then rsp_lines+=$(rsp_defines "$project/$rsp")$'\n'; fi
        done < <(sed -n 's/.*UCL-ORACLE-RSP: \(.*\)$/\1/p' "$tmp/log.txt" | sort -u)
      elif [ "$compile_errors" = 1 ]; then
        status=editor-compile-failed
        parsed=$all
      else
        status=unity-error
        parsed=$all
      fi
    else
      parsed=$all
      if [ "$run_exit" = 0 ] || [ "$compile_errors" = 1 ]; then status=ok; else status=unity-error; fi
      if [ "$status" = ok ] && [ -d "$project/Library/Bee/artifacts" ]; then
        while IFS= read -r rsp; do
          rsp_lines+=$(rsp_defines "$rsp")$'\n'
        done < <(find "$project/Library/Bee/artifacts" -type f -name '*.rsp' | LC_ALL=C sort)
      fi
    fi

    # assemblies: names printed by CompilePlayer plus every assembly a Bee response file was written for,
    # with that file's defines. Omitted when unknown.
    assemblies=$( { jq -r '.assemblies[] + "\t"' <<<"$parsed"; printf '%s' "$rsp_lines"; } | jq -Rn '
      [inputs | select(length > 0) | split("\t") | {name: .[0], define: (.[1] // "")}]
      | group_by(.name)
      | map({name: .[0].name, defines: ([.[].define | select(. != "")] | unique)})
      | map(if (.defines | length) == 0 then del(.defines) else . end)')
    result=$(jq -n --arg target "$target" --arg platform "$platform" --argjson extra "$extra" \
      --arg status "$status" --argjson exit "$run_exit" --arg hostos "$hostos" \
      --argjson parsed "$parsed" --argjson assemblies "$assemblies" '
      {target: $target, platform: $platform, extraArgs: $extra, status: $status, unityExitCode: $exit}
      + (if $target == "editor" then {editorOs: $hostos} else {} end)
      + (if ($assemblies | length) > 0 then {assemblies: $assemblies} else {} end)
      + {diagnostics: $parsed.diagnostics}')
    cells=$(jq -c --argjson c "$result" '. + [$c]' <<<"$cells")
    echo "   $status, $(jq '.diagnostics | length' <<<"$result") diagnostic(s), log: $outdir/logs/$safe.log"

    if [ "$keep" = 1 ]; then echo "   kept $tmp"; else rm -rf "$tmp"; fi
  done
  [ "$cells" != '[]' ] || continue
  jq -n --arg v "$version" --arg n "$name" --argjson cells "$cells" \
    '{schema: "ucl-oracle/1", unityVersion: $v, fixture: $n, cells: $cells}' >"$outdir/$name.json"
  recorded=$((recorded + 1))
done

echo "== recorded $recorded fixture(s) into $outdir"
[ "$recorded" -gt 0 ] || { echo "record.sh: no manifest cell matched version '$version' and filter '$filter'" >&2; exit 1; }
