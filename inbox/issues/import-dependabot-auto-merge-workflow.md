---
title: "Import dependabot-auto-merge workflow + dependabot config"
labels: [enhancement, ci, gitops, devsecops]
status: draft
opened: 2026-05-02
---

## Summary

Import the Dependabot auto-merge workflow from `kloehnwars-homelab` so
minor/patch dependency PRs from Dependabot auto-merge after CI passes.
Pairs with adding `.github/dependabot.yml` to enable Dependabot itself.

## What to import

From `C:\Users\petek\repos\kloehnwars-homelab\`:

- `.github/workflows/dependabot-auto-merge.yml`
- `.github/dependabot.yml` (the Dependabot configuration — schedule, ecosystems, ignore rules)

## Why deferred

- Dependabot needs `dependabot.yml` configured first; that's its own scoping decision (which ecosystems to watch — Python pip via `pyproject.toml`, GitHub Actions, etc.).
- Auto-merge requires branch protection rules with required status checks; those don't exist yet here.
- A standalone consumer's auto-merge policy may differ from the homelab's (e.g., auto-merge patch only, not minor; require manual approval for major).

## DevSecOps considerations

Per [`docs/repository-standards/devsecops-workflow.md`](../../docs/repository-standards/devsecops-workflow.md):
dependencies are pinned and reviewed. Auto-merge is a convenience for
**patch-level** updates (no API changes); minor and major upgrades go
through manual review. The dependabot-auto-merge workflow should encode
this policy.

## Acceptance criteria

- [ ] `.github/dependabot.yml` configured for: pip (this repo's `pyproject.toml`), github-actions, and any other ecosystems used.
- [ ] Workflow auto-merges only patch-level updates by default.
- [ ] Branch protection rule on `main` requires the relevant status checks before auto-merge.
- [ ] Workflow includes signed-commit handling (the gitops rule applies to Dependabot commits too).

## Source repo for sourcing decisions

`kloehnwars-homelab` is the most current source as of 2026-05-02; re-verify at import time.
