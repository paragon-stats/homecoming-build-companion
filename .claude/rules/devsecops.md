# Rule: DevSecOps — security in the development workflow

**Status:** MUST. Applies to every commit, dependency change, secret-handling task, and CI workflow change in this repository.

Standalone consumer of `kloehnwars-homelab`'s DevSecOps standards. Re-import manually when upstream evolves.

SSOT: [`docs/repository-standards/devsecops-workflow.md`](../../docs/repository-standards/devsecops-workflow.md).

Copilot instruction parallel: [`.github/instructions/devsecops_workflow.instructions.md`](../../.github/instructions/devsecops_workflow.instructions.md).

## Non-negotiables

- **No secrets in code, comments, commits, or PR descriptions.** Use env vars, gitignored config (`.env`, `*.local.yaml`), or a secret manager. CI's `gitleaks` step catches accidents.
- **Signed commits required.** Unsigned commits are a security failure, not a style preference. [`gitops.md`](./gitops.md) defines signing; this rule treats violations as incidents.
- **No hook bypass without authorization.** `--no-verify`, `--no-gpg-sign`, `-c commit.gpgsign=false` only with explicit user direction. Default: hooks run.
- **No destructive ops without confirmation.** `git push --force` (esp. to shared branches), `git reset --hard`, `git clean -f`, `rm -rf .git` need explicit user direction. See `CLAUDE.md`.
- **Dependencies pinned and reviewed.** `pyproject.toml` pins versions with constraints. Dependabot opens PRs for upgrades; review before auto-merge.

## CI security

- **Workflows: least privilege.** Default `permissions: read-all`; grant `write` only on the job that needs it (e.g., `contents: write` on the release job).
- **Pinned action versions.** SHA-pin third-party actions in security paths; tag pinning (`@v1`) only for first-party `actions/*` and trusted GitHub/Anthropic actions.
- **Secrets scoped to the job that needs them.** Never expose `GITHUB_TOKEN` or `secrets.*` to a job that doesn't operationally require them.

## Workflow file changes

Every change to `.github/workflows/*.yml` is reviewed as a security change, not just a CI change:

- New external action → verify the publisher and version.
- New secret reference → verify the secret exists and the job actually needs it.
- New `permissions:` block → grant the minimum.
- New `pull_request_target` trigger → red flag; usually wrong; default to `pull_request`.

## Reference

- [`docs/repository-standards/devsecops-workflow.md`](../../docs/repository-standards/devsecops-workflow.md) — SSOT for DevSecOps practices.
- [`.github/instructions/devsecops_workflow.instructions.md`](../../.github/instructions/devsecops_workflow.instructions.md) — Copilot instruction file mirroring the SSOT.
- [`gitops.md`](./gitops.md) — GitOps conventions (signing, branches, PR structure) that this rule complements.
- [`error-output.md`](./error-output.md) — diagnostic message standard; security-relevant errors follow the same convention.
