#!/usr/bin/env bash
# Compares recorded oracle results with the expectations in fixtures/manifest.json (docs/oracle.md).
#
#   oracle/compare.sh [--write] [result.json | results-dir ...]     default: every oracle/results/*/*.json
#
# Prints one line per recorded cell:
#   agree     <fixture> <version> <target> <platform> [extraArgs]  [manifest: <oracle field>]
#   disagree  ...  followed by one indented line per difference
#   skip      ...  with the reason (status not ok, no matching manifest cell, exit-3 cell, ...)
# What is compared:
#   * diagnostics: id, severity, file, line, column. Manifest UCLxxxx ids are ucl's own and are not
#     compared, except a UCL1xxx reported on an .asmdef/.asmref file, which must match Unity's asmdef
#     message there (id UNITY-ASMDEF, severity not compared).
#   * assemblies (when the oracle knows them): every manifest assembly exists, no "excluded" one does;
#     with defines from Unity's Bee response files: definesInclude present, definesExclude absent,
#     "defines" equal. Defines of an editor cell are compared only when it was recorded on the cell's
#     editorOs.
# --write sets the manifest cell's "oracle" to "agrees"/"disagrees" for compared cells (skipped cells are
# left alone); run scripts/fixtures-sums.sh afterwards. Exit 1 when any cell disagrees. Needs jq.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manifest="$root/fixtures/manifest.json"
write=0
inputs=()
for a in "$@"; do
  case $a in
    --write) write=1 ;;
    -h|--help) sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "compare.sh: unknown option $a" >&2; exit 2 ;;
    *) inputs+=("$a") ;;
  esac
done
command -v jq >/dev/null || { echo "compare.sh: jq is required" >&2; exit 2; }
[ ${#inputs[@]} -gt 0 ] || inputs=("$root/oracle/results")

files=()
for i in "${inputs[@]}"; do
  if [ -d "$i" ]; then
    while IFS= read -r f; do files+=("$f"); done < <(find "$i" -name '*.json' -not -path '*/logs/*' | LC_ALL=C sort)
  else
    files+=("$i")
  fi
done
[ ${#files[@]} -gt 0 ] || { echo "compare.sh: no oracle results found (record some with oracle/record.sh)" >&2; exit 2; }

# One JSON verdict per cell: {fixture, version, target, platform, extraArgs, manifestIndex, verdict, reasons, oracleField}
verdicts=$(jq -c --slurpfile m "$manifest" '
  def key: "\(.severity) \(.id) \(.file)(\(.line),\(.column))";
  def asmkey: "UNITY-ASMDEF \(.file)";
  def comparable_manifest:
    [ .[] | if (.id | startswith("UCL")) then
              (if (.id | test("^UCL1")) and ((.file // "") | test("\\.(asmdef|asmref)$")) then asmkey else empty end)
            else key end ];
  def comparable_oracle: [ .[] | if .id == "UNITY-ASMDEF" then asmkey else key end ];
  . as $r
  | $m[0].fixtures | map(.name == $r.fixture) | index(true) as $fi
  | $r.cells[] as $c
  | ($m[0].fixtures[$fi].cells // [] | to_entries
     | map(select(.value.unityVersion == $r.unityVersion and .value.target == $c.target
                  and .value.platform == $c.platform and (.value.extraArgs // []) == ($c.extraArgs // []))))
    as $match
  | {fixture: $r.fixture, version: $r.unityVersion, target: $c.target, platform: $c.platform,
     extraArgs: ($c.extraArgs // []), fixtureIndex: $fi}
  + if $fi == null or ($match | length) == 0 then {verdict: "skip", reasons: ["no matching manifest cell"]}
    elif $c.status != "ok" then {verdict: "skip", reasons: ["oracle status " + $c.status], cellIndex: $match[0].key, oracleField: $match[0].value.oracle}
    elif $match[0].value.exitCode == 3 then {verdict: "skip", reasons: ["manifest expects a configuration error (exit 3); Unity has no equivalent"], cellIndex: $match[0].key, oracleField: $match[0].value.oracle}
    else
      $match[0].value as $e
      | (($e.diagnostics // []) | comparable_manifest | unique) as $want
      | (($c.diagnostics // []) | comparable_oracle | unique) as $got
      | ([$c.assemblies // [] | .[] | {key: .name, value: .}] | from_entries) as $asm
      | (($e.editorOs // null) == null or $e.editorOs == ($c.editorOs // null)) as $sameOs
      | ( [ ($want - $got)[] | "missing diagnostic: " + . ]
        + [ ($got - $want)[] | "unexpected diagnostic: " + . ]
        + if ($c.assemblies // null) == null then [] else
            [ ($e.assemblies // [])[] | select($asm[.name] == null) | "missing assembly: " + .name ]
          + [ ($e.excluded // [])[] | select($asm[.] != null) | "assembly should be excluded: " + . ]
          + if $sameOs | not then [] else
              [ ($e.assemblies // [])[] | . as $a | ($asm[$a.name].defines // null) as $d | select($d != null)
                | ( [ ($a.definesInclude // [])[] | select(. as $x | $d | index($x) | not) | "\($a.name): define missing: " + . ]
                  + [ ($a.definesExclude // [])[] | select(. as $x | $d | index($x)) | "\($a.name): define present: " + . ]
                  + if $a.defines != null and (($a.defines | unique) != ($d | unique)) then ["\($a.name): defines differ"] else [] end
                  )[] ]
            end
          end
        ) as $diff
      | {verdict: (if ($diff | length) == 0 then "agree" else "disagree" end), reasons: $diff,
         cellIndex: $match[0].key, oracleField: $e.oracle}
      + if $sameOs then {} else {note: "defines not compared: recorded on \($c.editorOs // "?"), cell expects \($e.editorOs)"} end
    end
' "${files[@]}")

agree=0 disagree=0 skip=0
while IFS= read -r v; do
  [ -n "$v" ] || continue
  line=$(jq -r '"\(.verdict)\t\(.fixture) \(.version) \(.target) \(.platform)\(.extraArgs | map(" " + .) | join(""))"
    + (if .oracleField then "  [manifest: \(.oracleField)]" else "" end)' <<<"$v")
  printf '%-9s %s\n' "${line%%$'\t'*}" "${line#*$'\t'}"
  jq -r '(if .verdict == "disagree" then .reasons[] else (.reasons[]? | "(" + . + ")") end), (.note // empty) | "          " + .' <<<"$v"
  case $(jq -r .verdict <<<"$v") in agree) agree=$((agree + 1)) ;; disagree) disagree=$((disagree + 1)) ;; *) skip=$((skip + 1)) ;; esac
done <<<"$verdicts"
echo "== $agree agree, $disagree disagree, $skip skipped"

if [ "$write" = 1 ]; then
  tmp=$(mktemp)
  jq --slurpfile v <(printf '%s\n' "$verdicts") '
    reduce ($v[] | select(.verdict == "agree" or .verdict == "disagree")) as $x (.;
      .fixtures[$x.fixtureIndex].cells[$x.cellIndex].oracle = (if $x.verdict == "agree" then "agrees" else "disagrees" end))
  ' "$manifest" >"$tmp"
  mv "$tmp" "$manifest"
  echo "== updated $manifest; now run scripts/fixtures-sums.sh and review the diff"
fi

[ "$disagree" = 0 ]
