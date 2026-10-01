#!/usr/bin/env bash
# Maintains fixtures/SHA256SUMS: one "<sha256>  <path relative to fixtures/>" line per fixture file, sorted by
# path (byte order). Covers every file under fixtures/ except SHA256SUMS itself and build output (bin/, obj/)
# under fixtures/_stubs.
#
#   scripts/fixtures-sums.sh           rewrite fixtures/SHA256SUMS
#   scripts/fixtures-sums.sh --check   verify it; exit 1 and list the differences when it is stale
#
# Works in Git Bash (needs sha256sum, find, sort, diff).
set -euo pipefail

mode=write
case "${1:-}" in
  "") ;;
  --check) mode=check ;;
  -h|--help) sed -n '2,9p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
  *) echo "usage: $0 [--check]" >&2; exit 2 ;;
esac

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fixtures="$root/fixtures"
sums="$fixtures/SHA256SUMS"

generate() {
  (
    cd "$fixtures"
    find . -type f \
      ! -path ./SHA256SUMS \
      ! -path './_stubs/*/bin/*' \
      ! -path './_stubs/*/obj/*' \
      -print0 \
    | LC_ALL=C sort -z \
    | while IFS= read -r -d '' file; do
        rel="${file#./}"
        # Hash from stdin so the output never depends on sha256sum's text/binary mode or on path escaping.
        hash="$(sha256sum < "$file")"
        printf '%s  %s\n' "${hash%% *}" "$rel"
      done
  )
}

if [ "$mode" = write ]; then
  tmp="$(mktemp)"
  generate > "$tmp"
  mv "$tmp" "$sums"
  echo "wrote $(wc -l < "$sums" | tr -d ' ') entries to fixtures/SHA256SUMS"
  exit 0
fi

if [ ! -f "$sums" ]; then
  echo "fixtures/SHA256SUMS is missing; run scripts/fixtures-sums.sh" >&2
  exit 1
fi

if diff -u --label SHA256SUMS --label actual "$sums" <(generate) >&2; then
  echo "fixtures/SHA256SUMS is up to date"
else
  echo "fixtures/SHA256SUMS is stale; run scripts/fixtures-sums.sh" >&2
  exit 1
fi
