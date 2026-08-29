# Rule: GitOps conventions — branches, commits, PRs

**Status:** MUST. Applies to every commit, branch, and pull request created in this repository.

Standalone consumer of `kloehnwars-homelab`'s GitOps standards. Re-import manually when upstream evolves.

Detail in [`docs/repository-standards/`](../../docs/repository-standards/) and [`docs/automation/runbooks/`](../../docs/automation/runbooks/), maintained locally.

## Baseline

- **Branch from `main`.** Short-lived feature/fix branches; no long-lived integration branches.
- **Conventional Commits.** `<type>(<scope>): <subject>`. The `commitlint` workflow enforces the format on PR.
  Allowed types are the cross-repo SSOT (`ci-templates/commit-types.yml`) plus the local `build`:
  `feat`, `fix`, `perf`, `security`, `revert`, `docs`, `test`, `ci`, `chore`, `style`, `refactor`, `build`.
  Write a revert by hand as `revert(<scope>): <what and why>`; the default `Revert "…"` message `git revert` produces is rejected.
- **Heredoc commit messages.** Use single-quoted heredoc when committing from a tool to avoid expansion. See [`docs/git/heredoc-commit-messages.md`](../../docs/git/heredoc-commit-messages.md).
- **Signed commits.** Every commit signed (SSH or GPG). Recovery procedure: [`docs/automation/runbooks/fix-unsigned-commits-in-pr.md`](../../docs/automation/runbooks/fix-unsigned-commits-in-pr.md).
- **No hook bypass.** `--no-verify`, `--no-gpg-sign`, `-c commit.gpgsign=false` forbidden without explicit user direction.
- **`.github/COMMIT_TEMPLATE`** sets `git config commit.template` for the repo. Use it.

## Issue grouping of commits in PRs

PRs that close more than one issue group commits by issue. The PR template at [`.github/pull_request_template.md`](../../.github/pull_request_template.md) defines the structure.

- Single-issue PR: bullet points describing the change.
- Multi-issue PR: per-issue bold headings (`**Issue #NNN — Short title:**`) followed by 1–2 sentence outcome descriptions.

The `<!-- auto-summary:start --><!-- auto-summary:end -->` markers in the PR template are reserved for the auto-summary tool (`pr_upsert --auto-summary`).

That tool is not yet imported. Until it is, the auto-summary section is filled in by hand. See [`inbox/issues/`](../../inbox/issues/) for the import-pr-tooling follow-up drafts.

Each commit in a multi-issue PR references its issue (`Refs #NNN`, `Closes #NNN`). The PR description's grouping must match the per-commit issue refs exactly.

## Linked issues

The PR description's **Linked issues** section uses `Closes #NNN` (closes on merge) or `Relates to #NNN` (informational). Always link.

## Pre-flight checklist before pushing

Walk this before opening a PR:

- [ ] Branch named per [`docs/repository-standards/git-branching.md`](../../docs/repository-standards/git-branching.md).
- [ ] Commits follow Conventional Commits format.
- [ ] Every commit signed (`git log --show-signature` shows valid signatures).
- [ ] Each commit references its issue (`Refs #NNN` / `Closes #NNN`).
- [ ] Local `pre-commit run --all-files` is clean.
- [ ] Local `make lint` and `make test` pass.
- [ ] PR description follows the template; multi-issue PRs group commits by issue.

Full checklist with rationale: [`docs/automation/runbooks/git-workflow-checklist.md`](../../docs/automation/runbooks/git-workflow-checklist.md).

## Reference

- [`docs/repository-standards/git-standards.md`](../../docs/repository-standards/git-standards.md) — SSOT for git/GitOps conventions in this repo.
- [`docs/repository-standards/git-branching.md`](../../docs/repository-standards/git-branching.md) — branch naming and lifecycle.
- [`docs/automation/runbooks/git-workflow-checklist.md`](../../docs/automation/runbooks/git-workflow-checklist.md) — pre-flight checklist with detail.
- [`docs/automation/runbooks/fix-unsigned-commits-in-pr.md`](../../docs/automation/runbooks/fix-unsigned-commits-in-pr.md) — recovery when unsigned commits land.
- [`docs/git/heredoc-commit-messages.md`](../../docs/git/heredoc-commit-messages.md) — commit message heredoc format.
- [`devsecops.md`](./devsecops.md) — security-side counterpart to this rule.
