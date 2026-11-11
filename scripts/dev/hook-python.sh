#!/bin/sh
# Run the repository's Python for a Husky task: hook-python.sh <python args...>
# Looks for a Python that has pre_commit installed: this checkout's .venv, the main
# checkout's .venv (a git worktree has none of its own), then python on PATH.
# Fails with the bootstrap command when none is found; it never skips.
common="$(git rev-parse --path-format=absolute --git-common-dir)"
for py in .venv/Scripts/python.exe .venv/bin/python "${common%/.git}/.venv/Scripts/python.exe" "${common%/.git}/.venv/bin/python" python python3; do
  if "$py" -m pre_commit --version >/dev/null 2>&1; then
    exec "$py" "$@"
  fi
done
echo "hook-python: no Python with pre_commit found. Create the venv: make bootstrap (or put the venv's Scripts/bin directory on PATH)." >&2
exit 1
