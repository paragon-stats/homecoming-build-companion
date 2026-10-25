# Git Branching Standards

## Purpose

Define consistent branch naming, when branches are created, and how to clean up branches under squash-merge workflows.

## Branch naming

REQUIRE:

- Use issue-based branch naming with a developer-oriented type prefix:
  - `<type>/<number>-<scope>-short-slug`

Guidance:

- `<number>` MUST be the GitHub issue number.
- `<type>` SHOULD match the issue title type (see "Issue titles"). Prefer:
  - `docs`, `chore`, `feat`, `fix`, `refactor`, `test`, `security`
- `<scope>` SHOULD be a short area identifier (kebab-case). Prefer alignment with `area/*` labels.
- `short-slug` SHOULD be a short kebab-case description (3-6 words).
- Keep names stable and predictable. Avoid personal prefixes.

Examples:

- `docs/78-repo-standards-git-branching`
- `chore/83-precommit-bom-hook`

## Issue titles

REQUIRE:

- Use Conventional Commits style issue titles:
  - `type(scope): short summary`

Guidance:

- `type` SHOULD map to the repo's `type/*` labels where applicable:
  - Source of truth for `type/*` definitions: [../../.github/labels-hub.yml](../../.github/labels-hub.yml) (see the `# Types` section)
  - `docs` -> `type/docs`
  - `chore` -> `type/chore`
  - `feat` -> `type/feat`
  - `fix` -> `type/fix`
  - `refactor` -> `type/refactor`
  - `test` -> `type/test`
  - `security` -> `type/security`

- `scope` SHOULD reflect the impacted area (for example: `ci`, `docs`, `docker`, `python`, `security`, `network`, `traefik`).

Examples:

- `docs(repo-standards): define git branching standard`
- `chore(ci): fix auto-labeler false positives`

## Templates

Use the repo templates when creating issues and pull requests:

- Issues (work-package + incident-rca): [../../.github/ISSUE_TEMPLATE/work-package.yml](../../.github/ISSUE_TEMPLATE/work-package.yml)
- Pull requests: [../../.github/pull_request_template.md](../../.github/pull_request_template.md)

PR issue links (`Closes #...` / `Relates to #...`) can be kept deterministic with:

```bash
python -m scripts.github.pr_sync_issue_links --close 123
```

## When to create a branch

REQUIRE:

- Create a branch only after the issue details have been fetched and the acceptance criteria are understood.

Rationale:

- Prevents creating branches for the wrong issue or outdated requirements.

## Versioning

This repository uses semantic versioning derived from Conventional Commits.
On every push to `main`, `.github/workflows/release.yml` runs python-semantic-release
through `paragon-stats/github-actions/release`: it reads the commits since the last `vX.Y.Z`
tag, pushes a signed annotated tag on the merge commit when one is due, and creates the
GitHub Release. Nothing is committed back.

REQUIRE:

- Tags apply to `main` only, after the merge.
- Feature branches carry no version information.
- Version tags are created by the workflow. A tag placed by hand is for seeding the series
  or back-filling a release the workflow did not see, in the same form.

Guidance:

- Tag format: `v{MAJOR}.{MINOR}.{PATCH}` (e.g., `v0.10.0`).
- A `feat` commit cuts a minor; `fix`, `perf`, `security` and `revert` cut a patch; every
  other type cuts nothing. A breaking-change marker bumps the minor while the major is 0.
- The policy lives in `pyproject.toml` under `[tool.semantic_release]` and is drift-checked
  against the org's `commit-types.txt` in the commitlint workflow.

### Namespace separation

Branch names use a `type/` prefix (e.g., `docs/301-...`). Tags
use a `v` prefix (e.g., `v0.10.0`). These namespaces do not
collide.

## Local cleanup after merge (squash merges)

This repository assumes squash merges. Squash merges do not preserve merge ancestry in a way that always satisfies git's "fully merged" checks.

REQUIRE:

- Fetch tags and updated main before cleanup:
  - `git fetch origin main --tags`
- Verify the branch contents are present on `origin/main` using a
  diff, not ancestry.
  - If `git diff --name-status origin/main..HEAD` is empty, treat the
    branch as already present in main.
- Delete the local branch after verification.

Guidance:

- If `git branch -d <branch>` refuses deletion due to "not fully merged", treat that as expected under squash merges.
  - Verify with a diff, then use `git branch -D <branch>` if appropriate.

## Remote cleanup

REQUIRE:

- The agent must not run `git push`.
- Remote branch deletion is a user operation.
