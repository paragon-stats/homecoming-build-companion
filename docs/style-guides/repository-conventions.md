# Repository conventions

Project-specific git, security, Python and Markdown conventions, moved here from the former agent rule files.
The longer standards they summarise are in [repository-standards](../repository-standards/).
Diagnostics emitted by tools follow the [diagnostic contract](engine-architecture.md#diagnostic-contract-summary).

## GitOps conventions — branches, commits, PRs

**Status:** MUST. Applies to every commit, branch, and pull request created in this repository.

Standalone consumer of `kloehnwars-homelab`'s GitOps standards. Re-import manually when upstream evolves.

Detail in [`docs/repository-standards/`](../repository-standards/) and [`docs/automation/runbooks/`](../automation/runbooks/), maintained locally.

### Baseline

- **Branch from `main`.** Short-lived feature/fix branches; no long-lived integration branches.
- **Conventional Commits.** `<type>(<scope>): <subject>`. The `commitlint` workflow enforces the format on PR.
  Allowed types are the cross-repo SSOT (`ci-templates/commit-types.yml`) plus the local `build`:
  `feat`, `fix`, `perf`, `security`, `revert`, `docs`, `test`, `ci`, `chore`, `style`, `refactor`, `build`.
  Write a revert by hand as `revert(<scope>): <what and why>`; the default `Revert "…"` message `git revert` produces is rejected.
- **Heredoc commit messages.** Use single-quoted heredoc when committing from a tool to avoid expansion. See [`docs/git/heredoc-commit-messages.md`](../git/heredoc-commit-messages.md).
- **Signed commits.** Every commit signed (SSH or GPG). Recovery procedure: [`docs/automation/runbooks/fix-unsigned-commits-in-pr.md`](../automation/runbooks/fix-unsigned-commits-in-pr.md).
- **No hook bypass.** `--no-verify`, `--no-gpg-sign`, `-c commit.gpgsign=false` forbidden without explicit user direction.
- **`.github/COMMIT_TEMPLATE`** sets `git config commit.template` for the repo. Use it.

### Merge strategy — merge commits only

The repository allows **merge commits only**. Squash and rebase merges are disabled in the
GitHub repository settings:

```text
allow_merge_commit:  true
allow_squash_merge:  false
allow_rebase_merge:  false
```

This mirrors the intent of `pete.kloehn/kloehnwars-homelab` on GitLab (`merge_method: ff`,
`squash_option: default_off`): every commit reaches `main` with its own message intact.

Why each option is set the way it is:

- **Squash is off** because it discards every commit message and replaces them with the PR
  title and body. That silently drops the per-commit `Closes #NNN` / `Refs #NNN` trailers this
  rule requires, so the issues they name never close. Squash was the default until it was
  found doing exactly that.
- **Rebase is off** because GitHub re-parents each commit with a new SHA, which does not carry
  the original SSH signature forward. Signed commits are a MUST (see above and
  [`devsecops.md`](#devsecops--security-in-the-development-workflow)), so linear history is not worth the signature chain on
  `main`.
- **Merge commits** preserve both the messages and the signatures. The cost is merge bubbles
  in the history, which is the accepted trade.

Do not re-enable squash to tidy a branch. Rewrite the branch's commits before merging instead.

### Issue grouping of commits in PRs

PRs that close more than one issue group commits by issue. The PR template at [`.github/pull_request_template.md`](../../.github/pull_request_template.md) defines the structure.

- Single-issue PR: bullet points describing the change.
- Multi-issue PR: per-issue bold headings (`**Issue #NNN — Short title:**`) followed by 1–2 sentence outcome descriptions.

The `<!-- auto-summary:start --><!-- auto-summary:end -->` markers in the PR template are owned by the PR Summary workflow
([`.github/workflows/pr-summary.yml`](../../.github/workflows/pr-summary.yml), running `python -m scripts.github.pr_summary --pr N`).
It regenerates the text between the markers from the PR's own commits on every push, grouping by the issues the commits reference;
text outside the markers is kept. Run it by hand with `--dry-run` to preview, or dispatch the workflow to refresh a PR after its linked issue changes.

Each commit in a multi-issue PR references its issue (`Refs #NNN`, `Closes #NNN`). The PR description's grouping must match the per-commit issue refs exactly.

### Linked issues

The PR description's **Linked issues** section uses `Closes #NNN` (closes on merge) or `Relates to #NNN` (informational). Always link.

### Pre-flight checklist before pushing

Walk this before opening a PR:

- [ ] Branch named per [`docs/repository-standards/git-branching.md`](../repository-standards/git-branching.md).
- [ ] Commits follow Conventional Commits format.
- [ ] Every commit signed (`git log --show-signature` shows valid signatures).
- [ ] Each commit references its issue (`Refs #NNN` / `Closes #NNN`).
- [ ] Local `pre-commit run --all-files` is clean.
- [ ] Local `make lint` and `make test` pass.
- [ ] PR description follows the template; multi-issue PRs group commits by issue.

Full checklist with rationale: [`docs/automation/runbooks/git-workflow-checklist.md`](../automation/runbooks/git-workflow-checklist.md).

### Reference (gitops)

- [`docs/repository-standards/git-standards.md`](../repository-standards/git-standards.md) — SSOT for git/GitOps conventions in this repo.
- [`docs/repository-standards/git-branching.md`](../repository-standards/git-branching.md) — branch naming and lifecycle.
- [`docs/automation/runbooks/git-workflow-checklist.md`](../automation/runbooks/git-workflow-checklist.md) — pre-flight checklist with detail.
- [`docs/automation/runbooks/fix-unsigned-commits-in-pr.md`](../automation/runbooks/fix-unsigned-commits-in-pr.md) — recovery when unsigned commits land.
- [`docs/git/heredoc-commit-messages.md`](../git/heredoc-commit-messages.md) — commit message heredoc format.
- [`devsecops.md`](#devsecops--security-in-the-development-workflow) — security-side counterpart to this rule.

## DevSecOps — security in the development workflow

**Status:** MUST. Applies to every commit, dependency change, secret-handling task, and CI workflow change in this repository.

Standalone consumer of `kloehnwars-homelab`'s DevSecOps standards. Re-import manually when upstream evolves.

SSOT: [`docs/repository-standards/devsecops-workflow.md`](../repository-standards/devsecops-workflow.md).

Copilot instruction parallel: [`.github/instructions/devsecops_workflow.instructions.md`](../../.github/instructions/devsecops_workflow.instructions.md).

### Non-negotiables

- **No secrets in code, comments, commits, or PR descriptions.** Use env vars, gitignored config (`.env`, `*.local.yaml`), or a secret manager. CI's `gitleaks` step catches accidents.
- **Signed commits required.** Unsigned commits are a security failure, not a style preference.
  [`gitops.md`](#gitops-conventions--branches-commits-prs) defines signing; this rule treats violations as incidents.
- **No hook bypass without authorization.** `--no-verify`, `--no-gpg-sign`, `-c commit.gpgsign=false` only with explicit user direction. Default: hooks run.
- **No destructive ops without confirmation.** `git push --force` (esp. to shared branches), `git reset --hard`, `git clean -f`, `rm -rf .git` need explicit user direction.
- **Dependencies pinned and reviewed.** `pyproject.toml` pins versions with constraints. Dependabot opens PRs for upgrades; review before auto-merge.

### CI security

- **Workflows: least privilege.** Default `permissions: read-all`; grant `write` only on the job that needs it (e.g., `contents: write` on the release job).
- **Pinned action versions.** SHA-pin third-party actions in security paths; tag pinning (`@v1`) only for first-party `actions/*` and trusted GitHub/Anthropic actions.
- **Secrets scoped to the job that needs them.** Never expose `GITHUB_TOKEN` or `secrets.*` to a job that doesn't operationally require them.

### Workflow file changes

Every change to `.github/workflows/*.yml` is reviewed as a security change, not just a CI change:

- New external action → verify the publisher and version.
- New secret reference → verify the secret exists and the job actually needs it.
- New `permissions:` block → grant the minimum.
- New `pull_request_target` trigger → red flag; usually wrong; default to `pull_request`.

### Reference (devsecops)

- [`docs/repository-standards/devsecops-workflow.md`](../repository-standards/devsecops-workflow.md) — SSOT for DevSecOps practices.
- [`.github/instructions/devsecops_workflow.instructions.md`](../../.github/instructions/devsecops_workflow.instructions.md) — Copilot instruction file mirroring the SSOT.
- [`gitops.md`](#gitops-conventions--branches-commits-prs) — GitOps conventions (signing, branches, PR structure) that this rule complements.
- [`error-output.md`](engine-architecture.md#diagnostic-contract-summary) — diagnostic message standard; security-relevant errors follow the same convention.

## Python conventions

**Status:** MUST. Applies to every Python file authored or edited in this project.

This project is Python-first. New tooling, validators, and migrations are written in Python. PowerShell remains only for legacy scripts under `scripts/` until the Phase 3 migration replaces them.

### Baseline (python)

- **Python 3.14+ required.** No support for older versions.
- **Layout:** `src/<package>/` with co-located `tests/` directory; or `scripts/<package>/<entrypoints>.py` with `scripts/<package>/tests/` for thin tools.
- **CLI entry:** `__main__.py` so packages run as `python -m <package>`.
- **Imports:** absolute within the project; relative within a package only when traversing siblings.

### Linting and formatting

- `line-length = 120`
- `target-version = "py314"`
- Lint rules selected: `E, W, F, I, B, C4, UP, SIM, N, D, RUF`
- Ignored: `E501` (handled by formatter), `SIM108`, `D100`-`D103`, `D107` (phased docstring rollout)
- `pydocstyle` convention: `google`
- Format quote style: `double`, indent `space`, line ending `lf`

Pylint is disabled — ruff is the sole linter.

### Type checking

**mypy** with `warn_return_any = true` and `warn_unused_configs = true`. `python_version = "3.14"`. Strict-leaning but not maximally strict; favor practical over pedantic.

### Testing — TDD mandatory

- **Write the failing test first.** Confirm it fails for the right reason before implementing.
- **100% coverage target.** `pytest --cov=<package> --cov-fail-under=100` in CI; coverage drops fail the build.
- **pytest** is the test runner. `python_files = ["test_*.py"]`, `python_functions = ["test_*"]`.
- **Co-locate tests** with the package under `tests/` immediately adjacent.
- **Fixtures live in `tests/fixtures/`** and are referenced by `conftest.py`.
- For each rule or unit of logic: author one passing fixture and one violating fixture, then write the test that exercises both.

Test discipline applies to **non-trivial logic**. Trivial wrappers (CLI plumbing, dataclass definitions) don't need bespoke tests, but integration tests must exercise them end-to-end.

### Pre-commit hooks

`.pre-commit-config.yaml` runs:

- `ruff check --fix`
- `ruff format`
- `mypy`

Hooks fail the commit on any violation. Hook bypassing (`--no-verify`) is **not** an option without explicit user direction.

### Make targets

- `make lint` — ruff check + format check
- `make fix` — ruff check --fix + ruff format
- `make test` — pytest with short tracebacks, no coverage
- `make cov` — pytest with coverage report and `--cov-fail-under=100`

VENV detection auto-handles Linux (`.venv/bin/`) vs Windows (`.venv/Scripts/`).

### Anti-patterns to reject

- `print()` debugging in landed code — use logging or remove.
- Global mutable state in rule modules — every check function is pure: `(build, canonical) -> list[Violation]`.
- Error-as-tradeoff conflation: errors get fixed, not disclosed in trade-offs sections.
- Bare `except:` — name the exception class.
- Untyped public function signatures — type-annotate every public function and method.

### Reference (python)

- [`docs/repository-standards/style-guides/python-style-guide.md`](../repository-standards/style-guides/python-style-guide.md) — full guide; this rule is the summary.
- [`error-output.md`](engine-architecture.md#diagnostic-contract-summary) — diagnostic message standard for tool output.

## Markdown style and lint conformance

**Status:** MUST. Applies to every Markdown file authored or edited in this project — README, rule files, knowledge tree files, lessons docs, captures, plan files, frontmatter content.

This is the auto-loaded summary.
The full canonical guide, imported from `repo-template` as the SSOT, is at
[`docs/repository-standards/style-guides/markdown-style-guide.md`](../repository-standards/style-guides/markdown-style-guide.md).

### Lint conformance

The repo's `.markdownlint.yaml` is the single source of truth for lint rules. Authored markdown passes `markdownlint` cleanly except for the explicit exceptions defined in that config:

- `MD007/ul-indent` — disabled. EditorConfig governs indentation (`indent_size = 2` for `*.md`).
- `MD029/ol-prefix` — disabled. Numbered lists with sub-lists confuse the linter; prefix style isn't enforced.
- `MD013/line-length` — line length 200 in prose; disabled inside code blocks and tables.
- `MD033/no-inline-html` — `div`, `span`, `img`, `br`, `p`, `a` allowed; other inline HTML is rejected.

Run the lint check before committing markdown. Pre-commit hook will enforce this once Phase 3 wires it in.

**Exemption (user-directed, 2026-07-02):** generated engine-extraction docs under [`docs/engine/`](../engine/) carry a file-level `<!-- markdownlint-disable -->`.
The assembler normalizes their heading flow; the body is verbatim deep-math extraction (long cited lines, dense tables) and is not hand-lint-fixed.
Keep the disable comment when regenerating.

### Core requirements (summary — see canonical guide for full text)

#### Structure

- Single top-level heading (`# Title`) per file.
- Headings form a stable outline; never skip levels (`##` directly to `####`).
- Use headings, not bold text, to mark structure.

#### Prose

- Paragraph = one topic, multiple sentences as needed. Don't fragment a single thought into many one-liners.
- Active voice; positive form; specific concrete language.
- When a line exceeds 200 chars, **shorten the prose**, don't insert mid-sentence line breaks.
- Default short sentences. Vary length for rhythm.

#### Lists

- `-` for unordered lists.
- Items short. Multi-step content becomes a subheading + sub-list, not a giant nested item.

#### Code blocks

- Always tag the language: ` ```bash `, ` ```yaml `, ` ```python `, ` ```text `, etc.
- Show the minimal snippet; link to the file rather than pasting full files.

#### Mermaid

- Fenced as ` ```mermaid `.
- Keep node labels plain — no curly-brace placeholders, no parens, no slashes inside labels (GitHub's renderer breaks).
- Put detailed REST endpoints / placeholders / URLs in surrounding prose.

#### Links

- Relative paths for repository content.
- Link to SSOT docs rather than duplicating procedures.

#### Verbatim Mids forum-export blocks

This project's specific exception to the "no abbreviations in prose" rule (see [`io-set-naming.md`](build-rules/io-set-naming.md)): inside ```text``` fences holding a verbatim Mids' Reborn forum-export block, IO set abbreviations stay as the export emits them. Outside the fence, prose uses full names.

### Tooling

- **markdownlint** (via the imported `.markdownlint.yaml`) — primary lint.
- **EditorConfig** (via the imported `.editorconfig`) — indentation, line endings, charset, final newline.
- **VS Code** picks up both automatically when the workspace is opened via `homecoming-build-companion.code-workspace`.

Future Phase 3 work: import the markdown-related Python checks from `repo-template/scripts/linting/` (heading numbering for runbook-style docs, mermaid label safety) and wire them as pre-commit hooks once the Python tooling is in place.

### Reference (markdown)

- [`docs/repository-standards/style-guides/markdown-style-guide.md`](../repository-standards/style-guides/markdown-style-guide.md) — full canonical guide.
- [`.markdownlint.yaml`](../../.markdownlint.yaml) — lint config (single source of truth for rule selections).
- [`.editorconfig`](../../.editorconfig) — editor settings driving indentation and line endings.
- [`io-set-naming.md`](build-rules/io-set-naming.md) — full IO set names in prose; abbreviations only inside verbatim Mids forum-export blocks.
- [`error-output.md`](engine-architecture.md#diagnostic-contract-summary) — diagnostic messages also follow this style guide.
