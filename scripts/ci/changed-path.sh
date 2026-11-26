#!/usr/bin/env bash
# changed-path.sh <git args...> - run git to list changed paths and print the one
# path the commit-message validator's src/ rule needs: the first under src/, else
# the first of any. The list goes through a file, never a variable, so a change of
# thousands of files cannot overflow the environment the validator starts with;
# a failing git fails this script instead of reading as "no paths".
set -euo pipefail
list="$(mktemp)"
trap 'rm -f "$list"' EXIT
git "$@" >"$list"
grep -m 1 '^src/' "$list" || head -n 1 "$list"
