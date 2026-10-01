#!/usr/bin/env bash
# Extracts compiler diagnostics and oracle assembly lines from a Unity Editor.log.
#
#   oracle/parse-log.sh [--project-path DIR] [--section all|player] <Editor.log | ->
#
# Prints one JSON object: {"assemblies":[...],"diagnostics":[{"id","severity","file","line","column","message"}]}
#
# Recognised lines (after stripping leading "[Tag]" groups and timestamps):
#   <path>(<line>,<col>): error|warning <ID>: <message>      C# compiler and analyzer messages
#   <message> (<path>.asmdef|.asmref)                        Unity asmdef messages -> id UNITY-ASMDEF, line 0
#   UCL-ORACLE-ASSEMBLY: <name>                              written by the injected oracle package
# Everything else (Bee progress, "Scripts have compiler errors.", stack traces, ...) is ignored.
# Paths become project-relative with '/'. The project root is --project-path, else the value after
# -projectPath in the log's COMMAND LINE ARGUMENTS block. Diagnostics are deduplicated and sorted by
# file, line, column, id (byte order). --section player reads only the lines between the
# UCL-ORACLE-BEGIN and UCL-ORACLE-END markers that the oracle's CompilePlayer method prints.
# Needs bash, awk (POSIX), sort.
set -euo pipefail

root=""
section=all
log=""
while [ $# -gt 0 ]; do
  case $1 in
    --project-path) root=${2:?--project-path needs a value}; shift 2 ;;
    --section) section=${2:?--section needs a value}; shift 2 ;;
    -h|--help) sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -) log=-; shift ;;
    -*) echo "parse-log.sh: unknown option $1" >&2; exit 2 ;;
    *) [ -z "$log" ] || { echo "parse-log.sh: more than one log given" >&2; exit 2; }; log=$1; shift ;;
  esac
done
[ -n "$log" ] || { echo "usage: oracle/parse-log.sh [--project-path DIR] [--section all|player] <Editor.log | ->" >&2; exit 2; }
case $section in all|player) ;; *) echo "parse-log.sh: --section must be all or player" >&2; exit 2 ;; esac
[ "$log" = - ] || [ -f "$log" ] || { echo "parse-log.sh: no such file: $log" >&2; exit 2; }

records=$(LC_ALL=C awk -v root="$root" -v section="$section" '
function norm(p) {
  gsub(/\\/, "/", p)
  while (substr(p, 1, 2) == "./") p = substr(p, 3)
  return p
}
function isabs(p) { return substr(p, 1, 1) == "/" || p ~ /^[A-Za-z]:\// }
function rel(p,   r, cp, cr) {
  p = norm(p)
  if (!isabs(p) || root == "") return p
  r = norm(root); sub(/\/+$/, "", r)
  cp = p; cr = r
  if (cr ~ /^[A-Za-z]:\//) { cp = tolower(cp); cr = tolower(cr) }
  if (substr(cp, 1, length(cr) + 1) == cr "/") return substr(p, length(r) + 2)
  # macOS: /var/folders/... is reported as /private/var/folders/...
  if (substr(cp, 1, 9) == "/private/" && substr(cp, 9, length(cr) + 1) == cr "/") return substr(p, length(r) + 10)
  return p
}
function strip(s) {
  for (;;) {
    if (match(s, /^[ \t]+/)) { s = substr(s, RLENGTH + 1); continue }
    if (match(s, /^\[[^]]*\]/)) { s = substr(s, RLENGTH + 1); continue }
    if (match(s, /^[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9][T ][0-9][0-9]:[0-9][0-9]:[0-9][0-9]([.,][0-9]+)?(Z|[+-][0-9][0-9]:?[0-9][0-9])?(\|0x[0-9A-Fa-f]+)?[ |:]*/)) { s = substr(s, RLENGTH + 1); continue }
    if (match(s, /^[0-9][0-9]:[0-9][0-9]:[0-9][0-9]([.,][0-9]+)? [ |:]*/)) { s = substr(s, RLENGTH + 1); continue }
    return s
  }
}
function clean(m) { gsub(/\t/, " ", m); sub(/[ \t]+$/, "", m); return m }
BEGIN { inargs = 0; want = (section == "all"); fromlog = "" }
{
  line = $0
  sub(/\r$/, "", line)
  if (line == "COMMAND LINE ARGUMENTS:") { inargs = 1; next }
  if (inargs) {
    t = line; sub(/^[ \t]+/, "", t)
    if (t == "") { inargs = 0 }
    else if (pendingroot) { if (fromlog == "") fromlog = t; pendingroot = 0 }
    else if (tolower(t) == "-projectpath") pendingroot = 1
    next
  }
  if (root == "" && fromlog != "") root = fromlog
  s = strip(line)
  if (s ~ /^UCL-ORACLE-BEGIN/) { if (section == "player") want = 1; next }
  if (s ~ /^UCL-ORACLE-END/) { if (section == "player") want = 0; next }
  if (!want) next
  if (s ~ /^UCL-ORACLE-ASSEMBLY:/) {
    n = substr(s, 21); sub(/^[ \t]+/, "", n); n = clean(n)
    if (n != "") print "A\t" n
    next
  }
  if (match(s, /\([0-9]+,[0-9]+\): (error|warning) [A-Za-z_][A-Za-z0-9_]*: /) && RSTART > 1) {
    path = substr(s, 1, RSTART - 1)
    msg = clean(substr(s, RSTART + RLENGTH))
    split(substr(s, RSTART, RLENGTH), f, /[(),: ]+/)
    printf "D\t%s\t%d\t%d\t%s\t%s\t%s\n", rel(path), f[2], f[3], f[5], f[4], msg
    next
  }
  if (s ~ /ssembl/ && match(s, / \([^()]*\.(asmdef|asmref)\)$/) && RSTART > 1) {
    path = substr(s, RSTART + 2, RLENGTH - 3)
    msg = clean(substr(s, 1, RSTART - 1))
    sev = (msg ~ /non-existent assembly/) ? "warning" : "error"
    printf "D\t%s\t0\t0\tUNITY-ASMDEF\t%s\t%s\n", rel(path), sev, msg
    next
  }
}
' "$log")

tab=$(printf '\t')
{
  printf '%s\n' "$records" | grep "^A$tab" | LC_ALL=C sort -u || true
  printf '%s\n' "$records" | grep "^D$tab" \
    | LC_ALL=C sort -t "$tab" -k2,2 -k3,3n -k4,4n -k5,5 -k6,6 -k7,7 -u || true
} | LC_ALL=C awk -F '\t' '
function q(s) {
  gsub(/\\/, "\\\\", s); gsub(/"/, "\\\"", s)
  gsub(/[\001-\037]/, " ", s)
  return "\"" s "\""
}
$1 == "A" { a[++na] = $2; next }
$1 == "D" { d[++nd] = sprintf("{\"id\": %s, \"severity\": %s, \"file\": %s, \"line\": %d, \"column\": %d, \"message\": %s}", q($5), q($6), q($2), $3, $4, q($7)) }
END {
  printf "{\n  \"assemblies\": ["
  for (i = 1; i <= na; i++) printf "%s\n    %s", (i > 1 ? "," : ""), q(a[i])
  printf "%s],\n  \"diagnostics\": [", (na ? "\n  " : "")
  for (i = 1; i <= nd; i++) printf "%s\n    %s", (i > 1 ? "," : ""), d[i]
  printf "%s]\n}\n", (nd ? "\n  " : "")
}'
