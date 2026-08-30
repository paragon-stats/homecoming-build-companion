# DevSecOps Workflow (Homelab)

## Purpose

Define a single, one-way-to-win workflow for doing work in this repository.

Scope:

- issue-driven changes
- local branch workflows
- pull requests and review
- merge and cleanup

This document is the single source of truth (SSOT) for workflow behavior. It is designed for a homelab environment where changes may impact live services.

## Non-negotiables

- Keep changes small and incremental.
- Never commit secrets.
- Prefer configuration over hardcoded environment-specific values.
- Commit `.pre-commit-config.yaml` separately (its own commit) before committing other files so hooks are updated before subsequent commits run.
  Still observe the one-file-per-commit rule.
  **Python exception**: implementation files and their corresponding test files MUST be committed together to maintain 100% test coverage.
- **Never bypass control tools without human approval.**
  Do not use `--no-verify`, `SKIP=<hook>`, inline suppressions (`# noqa`, `# nosec`, `# type: ignore`), or any mechanism that disables a linter, security scanner, or pre-commit hook.
  When a control tool fails repeatedly (>2 attempts), escalate to the user with error details instead of bypassing.
- Treat merges as squash merges.
- **All commits must be signed** (enforced by pre-commit hooks; CI skips signing checks).
- **Commit hygiene**: if a commit fails (pre-commit hook failure), immediately unstage the file (`git reset HEAD <file>`) before attempting fixes.
  Never leave files staged after a failed commit — it prevents accidental bundling of unrelated changes.

### Batch commit pattern (one file per commit)

When committing multiple files one-per-commit, use this loop that properly unstages on failure and captures error output for diagnosis:

```bash
mkdir -p tmp
for f in $(git diff --name-only); do
  base=$(basename "$f")
  git add "$f"
  result=$(git commit -m "$(cat <<EOF
type(scope): message in $base

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)" 2>&1)
  if [ $? -ne 0 ]; then
    git reset HEAD "$f" > /dev/null 2>&1
    echo "FAILED: $f"
    echo "$result" | grep -E "error|Failed" \
      > "tmp/commit-fail-$(echo "$base" | tr '.' '-').log"
    cat "tmp/commit-fail-$(echo "$base" | tr '.' '-').log"
  else
    echo "OK: $f"
  fi
done
```

Rules:

- **Approved use case**: bulk find-replace across many files with a reusable commit message. All other uses require explicit human approval per session.
- Always unstage immediately on failure (`git reset HEAD "$f"`).
- Never leave files staged after a failed commit.
- Never use placeholder commit messages (e.g., `git commit -m "test"`) to diagnose failures.
- Capture linter error output to `tmp/` for post-loop diagnosis.
- After the loop, diagnose failed files individually by running the specific hook that failed.

## Roles

- **Agent**: makes local changes, runs validations, creates commits, can create PRs after the user publishes a branch.
- **User**: owns git push and final merge operations.

## Workflow task list

| Step | Phase            | Trigger                     | Agent responsibilities                                                                                                                                                                                                                           | User responsibilities                                                          | Evidence / artifacts                     | Gate (pass/fail)                                               |
| ---: | ---------------- | --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------ | ---------------------------------------- | -------------------------------------------------------------- |
|    0 | Setup            | First-time setup            | Verify repo venv is bootstrapped and Git signing is configured (pre-commit hook will check).                                                                                                                                                     | Run the bootstrap and Git signing setup commands (see "Setup commands" below). | Venv ready; Git signing config verified. | Signing is configured.                                         |
|    1 | Intake           | User requests work on issue | Fetch issue details via repo GitHub helpers. Restate acceptance criteria and scope.                                                                                                                                                              | Confirm scope, target role (edge/internal), and operational constraints.       | Issue JSON summary, short checklist.     | Scope is unambiguous.                                          |
|    2 | Branch           | Issue accepted              | Create branch using `scripts.devops.start_issue_work`. Do not manually construct branch names. Do not push.                                                                                                                                      | None.                                                                          | Local branch name and base.              | Branch exists, clean working tree.                             |
|    3 | PR bootstrap     | User has pushed             | Create PR (ready-for-review) via `scripts.github.pr_upsert` as soon as the user has pushed the branch. Do not defer to end of implementation — early PRs trigger CI checks (SonarQube, Super-Linter) that the IDE cannot reliably catch locally. | Push branch to remote.                                                         | PR URL.                                  | PR targets correct base/head; required contexts start running. |
|    4 | Plan             | Before edits                | Write a short plan with files + validation steps. Keep it testable.                                                                                                                                                                              | Confirm plan if scope is medium/high.                                          | Plan bullets.                            | Plan matches acceptance criteria.                              |
|    5 | Implement        | Plan approved               | Make smallest changes that satisfy the issue. Follow repo style guides.                                                                                                                                                                          | Provide environment values via env files/secrets at deploy time.               | Focused diffs.                           | No secrets; minimal scope.                                     |
|    6 | Tests            | Any non-trivial logic       | Add/extend unit tests for non-trivial logic.                                                                                                                                                                                                     | None.                                                                          | Test output.                             | Tests pass.                                                    |
|    7 | Local validation | Before commit               | Run repo hooks once via git commit (avoid double-running).                                                                                                                                                                                       | Ensure sudo token is primed if Docker-based hooks require it.                  | Pre-commit output.                       | All hooks pass.                                                |
|    8 | Security         | New/changed code            | Run required security checks for the touched tech (see references). Fix findings and re-run.                                                                                                                                                     | Authenticate tools if prompted (when needed).                                  | Scan outputs + remediation notes.        | No new high severity issues (or explicitly accepted).          |
|    9 | Review           | Feedback received           | Make focused fixes, keep diffs minimal, rerun validations as needed.                                                                                                                                                                             | Approve when ready.                                                            | Updated commits.                         | Required contexts are green.                                   |
|   10 | Merge            | PR approved                 | Do not merge unless asked. Provide diff-based verification guidance under squash merges. Semver tag `vX.Y.Z` and GitHub Release auto-created on push to main when warranted.                                                                     | Perform squash merge.                                                          | Merge completed.                         | Main contains the change.                                      |
|   11 | Cleanup          | After merge                 | Verify branch content is in main via diff. Delete local branch and prune remotes.                                                                                                                                                                | None.                                                                          | Clean local branches.                    | No stale work branches.                                        |

## Definition of done (homelab)

Use this checklist when relevant to the change type.

- Documentation
  - Any new operational procedure belongs in a single runbook location.
  - No duplicated procedures across docs.
- Docker Swarm / infrastructure
  - No image tags set to latest.
  - Secrets are handled via Docker secrets when applicable.
  - Persistent storage is explicit for stateful services.
  - Log rotation is configured.
- Security
  - Do not hardcode environment-sensitive values when a config or env var is the intended mechanism.
  - Run required scans for new or changed first-party code.
- Version tagging
  - Semver tag and GitHub Release created on main after merge, when the merged commit types warrant one (automated by CI).

## Setup commands

- Bootstrap repo venv:
  - Windows: `py -3 -m scripts.dev.bootstrap_venv`
  - Linux/macOS: `python3 -m scripts.dev.bootstrap_venv`
- Setup Git commit signing:
  - If venv is available: `python -m scripts.devops.setup_git_signing`
  - If venv is unavailable: `python3 -m scripts.devops.setup_git_signing`
- Ruff formatting (match Super-Linter config):
  - `ruff format --config .github/linters/.ruff.toml <paths>`

## PR review comment handling

When the agent receives PR review comments (from Copilot, reviewers, or automated tools), follow this sequence:

### 1. Analyze

Read each comment. Identify root cause, scope, and validity. Do not start coding before completing analysis.

### 2. Present recommendations (SSOT table format)

Present ALL findings in a single merged table. This is the canonical format for all finding/recommendation output across skills (`/review-pr`, `/triage-ci`) and code review instructions.

- Finding number occupies a merged first-column position. Options appear as sub-rows beneath it.
- Finding IDs are sequential integers starting at 1.
- State which option the agent recommends with `(rec)`.
- Rate each option's **Resilience**:
  - **Short-term**: quick fix, likely to break again or need revisiting soon.
  - **Medium-term**: decent solution with known flaws, holds for a while.
  - **Long-term**: resilient, handles future complexity.

#### Column definitions

| Pos | Column | Width | Content |
| --- | ------ | ----- | ------- |
| 1 | Finding | ~30 chars | `{id} {category}: {summary}` |
| 2 | Source | ~10 chars | `ci` or `sonarcloud` |
| 3 | Severity | ~8 chars | `ERROR`, `WARNING`, `INFO` |
| 4 | File:Line | ~25 chars | `path/file.py:42` (truncate deep paths with `…/`) |
| 5 | Rec | ~8 chars | `A (rec)`, `B`, `C` |
| 6 | Approach | ~12 chars | Short label for the fix strategy |
| 7 | Resilience | ~10 chars | `Long-term`, `Medium-term`, or `Short-term` |
| 8 | Description | Remainder | Scanner guidance first, agent analysis second |

#### Description column structure

For CI findings (no scanner guidance):

```text
{Agent's root cause analysis and fix description}
```

For SonarCloud findings (scanner guidance available):

```text
[SC] {risk from sonar_rule_description}. Fix: {sonar_fix_recommendation}. [Agent] {implementation context}.
```

Rules:

- `[SC]` content comes from `sonar_rule_description` and `sonar_fix_recommendation` fields verbatim.
  Condensing for table width is acceptable; changing the meaning is not.
- `[Agent]` content provides implementation specifics, alternatives, or tradeoffs.
- For option A (recommended), both `[SC]` and `[Agent]` sections are present.
- For options B/C, `[SC]` context is inherited from A; `[Agent]` carries the alternative.
- If `sonar_rule_description` or `sonar_fix_recommendation` is empty, note `[SC] No scanner guidance available`.

NEVER:

- Begin a SonarCloud finding's Description with `[Agent]` content. Scanner guidance always comes first.
- Omit `[SC]` content when the enrichment fields contain data.
- Paraphrase `sonar_rule_description` into a different risk assessment.

#### Continuation rows

For multi-option findings, continuation rows leave columns 1-4 empty.

#### Example

<!-- markdownlint-disable MD013 -->

```markdown
| Finding | Source | Severity | File:Line | Rec | Approach | Resilience | Description |
|---------|--------|----------|-----------|-----|----------|------------|-------------|
| 1 coverage-gap: 96.97% | ci | ERROR | test_file.py:67 | A (rec) | Fix gaps | Long-term | Uncovered branch in error handler. Rewrite test with tmp_path, remove dead code. |
| 2 security: Hardcoded IP | sonarcloud | WARNING | test_types.py:19 | A (rec) | RFC 5737 | Long-term | [SC] Hardcoded IPs expose topology. Fix: use documentation-range IPs. [Agent] Replace with 198.51.100.1 (TEST-NET-2). |
| | | | | B | conftest | Long-term | [Agent] Extract to conftest fixture with RFC 5737 address. |
| | | | | C | won't-fix | Short-term | [Agent] Suppress in SonarCloud. Hides without fixing. |
```

<!-- markdownlint-enable MD013 -->

### 3. User selects

Wait for user approval before implementing. Do not fix code before the user selects an option.

### 4. Implement and resolve

After the user selects options:

1. Implement the selected fix and commit (following one-file-per-commit rules).
2. Reply to each review comment with the fix commit SHA and a brief description.
3. Resolve the review thread.

Use the `triage_review_comments` flow script to reply and resolve in bulk:

```bash
python -m scripts.github.triage_review_comments \
  --repo <owner/name> --pr <NUMBER> --author-substring <author> \
  --replies-json '[{"comment_id": <ID>, "body": "Fixed in <SHA>. <description>"}]'
```

## CI failure triage

When CI checks fail on a PR, use the `/triage-ci` skill to diagnose and fix. The skill gathers failures from GitHub CI and SonarCloud in one call, classifies them, and presents a findings table.

Prescribed flow:

1. Run `/triage-ci` (or `/triage-ci --ci-only` / `/triage-ci --sonar-only`)
2. Review the findings table — same merged-row format as review comment triage
3. Select fix options per finding
4. Agent implements fixes and verifies locally

The compound script `scripts.github.triage_ci_failures` writes findings to `tmp/triage-findings.json`. Idempotent (overwritten each run), schema `triage-findings-v1`.

Skill definition: `.claude/skills/triage-ci/SKILL.md`

## PR checklist handling (SSOT)

- When drafting PR bodies, mark checklist items as complete only when verified.
- If a checklist item does not apply to the change, leave the checkbox unchecked and append "(N/A)" to the item text.
- Do not guess on items that require local validation unless the validation was run.

## PR body standards for multi-issue PRs

When a PR closes 2+ issues, use the structured format below. Prefer the helper: `scripts.github.pr_upsert --auto-summary --issue N --issue M` generates it automatically.

### Summary

Use per-issue H3 headings with a 1-2 sentence outcome, H4 category sub-headings, and a markdown `---` horizontal rule between blocks:

```markdown
### Issue #NNN — Short title

One-two sentence outcome description (from the linked issue's Objective).

#### 🚀 Features

- _(scope)_ commit summary (abcdef1)

#### 🧪 Testing

- _(scope)_ commit summary (abcdef2)

---

### Issue #NNN — Short title

One-two sentence outcome description.

#### 🐛 Bug Fixes

- _(scope)_ commit summary (abcdef3)
```

Each commit groups under the issue its `Refs #N` or `Closes #N` trailer names (see `.github/COMMIT_TEMPLATE`). Unattributed commits land under `### Other changes` with the same H4 structure.

Single-issue PRs retain the existing flat format (`### 🚀 Features` at H3 with bullets under it).

### When to group issues in a single PR

- Group issues when they share implementation dependencies (e.g., a migration that triggers linting fixes).
- Keep issues in separate PRs when they are independently testable and have no shared code changes.
- Default: 1 issue = 1 PR. Only combine when the issues are genuinely coupled.

## References (source of truth)

- Dependabot image update verification: docs/automation/runbooks/dependabot-image-update-verification-sop.md
- Documentation standards: docs/repository-standards/documentation-standards.md
- Git branching: docs/repository-standards/git-branching.md
- Docker Compose style: docs/repository-standards/style-guides/docker-yaml-style-guide.md
- Issue template (work-package + incident-rca): .github/ISSUE_TEMPLATE/work-package.yml
- Pull request template: .github/pull_request_template.md
- Data management (config, secrets, persistence): .github/instructions/data_management.instructions.md
- GitHub automation constraints (helpers, no git push): .github/copilot-instructions.md
- GitHub platform standards (reviews, required contexts): docs/repository-standards/github-platform-standards.md
- Checks parity matrix (local vs CI): docs/ci/checks-parity-matrix.md
- CI runs pre-commit hooks in `.github/workflows/pre-commit.yml` with `SKIP` derived from `.github/precommit-hook-classification.yml` `ci_skip` and `SKIP_GIT_SIGNING_CHECK=1`.
- Semver releases: .github/workflows/release.yml

Bootstrap helpers:

```bash
# Bootstrap repo venv (cross-platform)
python -m scripts.dev.bootstrap_venv

# Setup Git commit signing (one-time, cross-platform)
python -m scripts.devops.setup_git_signing

# Start work on an issue (required for issue-driven branches)
python -m scripts.devops.start_issue_work --repo owner/name --issue 123
```

## Git Commit Signing

This repository requires all commits to be signed. The pre-commit hooks will verify signing configuration before allowing commits.

**One-time setup (cross-platform):**

```bash
python -m scripts.devops.setup_git_signing
```

This script will:

- Create an SSH signing key if needed
- Configure Git for SSH commit signing
- Set up allowed signers file for local verification
- Provide instructions for registering the key on GitHub

**Verification:**

```bash
# Check current configuration
python -m scripts.devops.setup_git_signing --check-only

# Verify a commit is signed
git log --show-signature -n 1
```

**Troubleshooting:**

If commits are unsigned in a PR, use the automated fix script:

```bash
python -m scripts.github.fix_unsigned_commits --pr <NUMBER> --apply
```

See `docs/automation/runbooks/fix-unsigned-commits-in-pr.md` for detailed procedures.
