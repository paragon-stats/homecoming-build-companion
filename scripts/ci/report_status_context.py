#!/usr/bin/env python3
"""Report a required status context using the GitHub CLI.

Executed by the report-status-context composite action on every runner OS
(via ``python -m scripts.ci.report_status_context``). The bash companion
``scripts/ci/report_status_context.sh`` is the upstream (kloehnwars-homelab)
variant retained for re-import parity; it is not wired into this repo's action.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
import time

# Seconds to wait before the second and third attempts. One bad API reply must not fail
# a job whose own work passed (#75); a sustained failure still surfaces after the last try.
_RETRY_DELAYS = (2.0, 4.0)


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Report a required status context to merge_commit_sha")
    # Optional: prefer the GH_TOKEN env var so the secret never appears in the
    # runner's process argv. The flag remains for upstream compatibility.
    parser.add_argument("--github-token", default=None)
    parser.add_argument("--pr-number", required=True, type=int)
    parser.add_argument("--job-status", required=True)
    parser.add_argument("--status-context", required=True)
    parser.add_argument("--target-url", required=True)
    return parser.parse_args(argv)


def _job_status_to_state(job_status: str) -> str:
    normalized = (job_status or "").strip().lower()
    if normalized == "success":
        return "success"
    if normalized == "failure":
        return "failure"
    return "error"


def _run_gh(args: list[str], env: dict[str, str]) -> str:
    # Deliberately NOT consolidated with scripts.github.gh_cli's runner: this
    # module must stay a standalone CI entry point, and it injects an explicit
    # env (token scoping) that GhRunner.run() does not model.
    attempts = len(_RETRY_DELAYS) + 1
    attempt = 1
    while True:
        result = subprocess.run(
            args,
            check=False,
            env=env,
            capture_output=True,
            text=True,
        )
        if result.returncode == 0:
            return result.stdout.strip()
        stderr_text = (result.stderr or "").rstrip()
        if attempt == attempts:
            raise RuntimeError(
                f"gh command failed with exit code {result.returncode} after {attempt} attempts. stderr:\n{stderr_text}"
            )
        delay = _RETRY_DELAYS[attempt - 1]
        print(
            f"gh command failed with exit code {result.returncode}, retrying in {delay:g} s "
            f"(attempt {attempt} of {attempts}): {stderr_text}",
            file=sys.stderr,
        )
        time.sleep(delay)
        attempt += 1


def main(argv: list[str]) -> int:
    """Main."""
    args = _parse_args(argv)

    repo = (os.environ.get("GITHUB_REPOSITORY") or "").strip()
    if not repo:
        print("Error: GITHUB_REPOSITORY is required (owner/repo)", file=sys.stderr)
        return 2

    token = args.github_token or (os.environ.get("GH_TOKEN") or "").strip()
    if not token:
        print("Error: GH_TOKEN env var (or --github-token) is required", file=sys.stderr)
        return 2

    env = dict(os.environ)
    env["GH_TOKEN"] = token

    try:
        sha = _run_gh(
            [
                "gh",
                "api",
                f"repos/{repo}/pulls/{args.pr_number}",
                "--jq",
                ".head.sha",
            ],
            env=env,
        )
    except RuntimeError as exc:
        print(f"Error resolving head SHA for PR #{args.pr_number}: {exc}", file=sys.stderr)
        return 2

    if not sha or sha == "null":
        print(
            f"Error: No head SHA available for PR #{args.pr_number} in {repo}. "
            "The PR may not exist or the API response is missing .head.sha.",
            file=sys.stderr,
        )
        return 2

    state = _job_status_to_state(args.job_status)
    description = f"Reported by GitHub Actions ({args.job_status})"

    print(
        f"Posting status context '{args.status_context}' (state={state}) to {repo}@{sha[:12]}",
        file=sys.stderr,
    )

    try:
        _run_gh(
            [
                "gh",
                "api",
                "-X",
                "POST",
                f"repos/{repo}/statuses/{sha}",
                "-f",
                f"state={state}",
                "-f",
                f"context={args.status_context}",
                "-f",
                f"description={description}",
                "-f",
                f"target_url={args.target_url}",
            ],
            env=env,
        )
    except RuntimeError as exc:
        print(
            f"Error posting status context '{args.status_context}' to {repo}@{sha[:12]}: {exc}",
            file=sys.stderr,
        )
        return 1

    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main(sys.argv[1:]))
