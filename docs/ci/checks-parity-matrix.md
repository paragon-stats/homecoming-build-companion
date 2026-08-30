# Pre-commit and CI Parity Matrix

## Purpose

Provide a single source of truth (SSOT) for ensuring that local pre-commit
checks on Windows and Linux match GitHub Actions checks and use the same
configuration files wherever possible.

Multi-repo matrix: each table includes columns for all hub-spoke repos.
Empty cells indicate the repo has not yet populated its configuration.

## Scope

- Windows pre-commit and commit actions (local)
- Linux pre-commit and commit actions (local)
- GitHub Actions checks

Windows and Linux must run the same pre-commit hooks from the same
configuration. Differences are only allowed where the execution shell differs
(PowerShell vs bash) while using the same underlying scripts and configuration
files.

## Sources of truth

- Pre-commit hooks: `.pre-commit-config.yaml`
- Local Super-Linter runner: `scripts/linting/run_super_linter.py`
- Shared Super-Linter config: `.github/super-linter.env` (spoke-specific)
- GitHub Actions workflows: `.github/workflows/`
- Shared linter configs: `.github/linters/`

## Pre-commit hook enablement

Hooks are classified as hub-universal (all repos) or spoke-specific
(repo-local). Source of truth: `.github/precommit-hook-classification.yml`.

Spoke hooks live in `.pre-commit-config.local.yaml` and are merged into the
final `.pre-commit-config.yaml` by `scripts/ci/merge_precommit_config.py`
during hub-spoke sync. The merge deduplicates by hook ID (spoke wins).

| Hook                                 | Scope | Config location                                           | kloehnwars-homelab | docker-swarm-homelab | repo-template |
| ------------------------------------ | ----- | --------------------------------------------------------- | ------------------ | -------------------- | ------------- |
| check-commit-message                 | hub   | `scripts/testing/hooks/check_commit_message.py`           | ✓                  |                      |               |
| check-commit-grouping                | hub   | `scripts/testing/hooks/check_commit_grouping.py`          | ✓                  |                      |               |
| check-repo-location                  | hub   | `scripts/testing/hooks/check_repo_location.py`            | ✓                  |                      |               |
| prevent-unintended-deletions-pre     | hub   | `scripts/testing/hooks/prevent_unintended_deletions.py`   | ✓                  |                      |               |
| check-repo-layout                    | hub   | `scripts/testing/hooks/check_repo_layout.py`              | ✓                  |                      |               |
| check-doc-invariants                 | hub   | `scripts/testing/hooks/check_doc_invariants.py`           | ✓                  |                      |               |
| check-sync-directives-completeness   | hub   | `scripts/testing/hooks/check_sync_directives.py`          | ✓                  |                      |               |
| check-workflow-install-patterns      | hub   | `scripts/testing/hooks/check_workflow_install_patterns.py`| ✓                  |                      |               |
| check-git-signing                    | hub   | `scripts/testing/hooks/check_git_signing.py`              | ✓                  |                      |               |
| end-of-file-fixer                    | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| mixed-line-ending                    | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| trailing-whitespace                  | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| fix-byte-order-marker                | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| check-yaml                           | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| check-added-large-files              | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| check-merge-conflict                 | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| check-executables-have-shebangs      | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| check-shebang-scripts-are-executable | hub   | pre-commit-hooks defaults                                 | ✓                  |                      |               |
| check-json-key-order                 | hub   | `scripts/linting/check_json_key_order.py`                 | ✓                  |                      |               |
| validate-github-actions-architecture | hub   | `scripts/testing/validate_github_actions_architecture.py` | ✓                  |                      |               |
| validate-heading-numbers             | hub   | `scripts/linting/validate_heading_numbers.py`             | ✓                  |                      |               |
| check-mermaid-diagrams               | hub   | `scripts/linting/check_mermaid_diagrams.py`               | ✓                  |                      |               |
| check-filename-conventions           | hub   | `scripts/linting/check_filename_conventions.py`           | ✓                  |                      |               |
| check-cognitive-complexity           | hub   | `scripts/linting/check_cognitive_complexity.py`           | ✓                  |                      |               |
| check-short-identifier-names         | hub   | `scripts/linting/check_short_identifier_names.py`         | ✓                  |                      |               |
| check-platform-patch                 | hub   | `scripts/linting/check_platform_patch.py`                 | ✓                  |                      |               |
| check-init-docstrings                | hub   | `scripts/linting/check_init_docstrings.py`                | ✓                  |                      |               |
| mypy                                 | hub   | `pyproject.toml`                                          | ✓                  |                      |               |
| editorconfig-checker                 | hub   | `.editorconfig`                                           | ✓                  |                      |               |
| ruff                                 | hub   | `pyproject.toml`                                          | ✓                  |                      |               |
| ruff-format                          | hub   | `pyproject.toml`                                          | ✓                  |                      |               |
| yamllint                             | hub   | `.github/linters/.yaml-lint.yml`                          | ✓                  |                      |               |
| shfmt                                | hub   | pre-commit-shfmt defaults                                 | ✓                  |                      |               |
| shellcheck                           | hub   | shellcheck defaults                                       | ✓                  |                      |               |
| actionlint                           | hub   | actionlint defaults                                       | ✓                  |                      |               |
| markdownlint                         | hub   | `.github/linters/.markdownlint.yml`                       | ✓                  |                      |               |
| super-linter                         | hub   | `scripts/linting/run_super_linter.py`                     | ✓                  |                      |               |
| pytest                               | hub   | `pyproject.toml` (manual stage)                           | ✓                  |                      |               |
| pytest-affected                      | hub   | `scripts/precommit/pytest_affected.py`                    | ✓                  |                      |               |
| prevent-unintended-deletions-post    | hub   | `scripts/testing/hooks/prevent_unintended_deletions.py`   | ✓                  |                      |               |
| dclint                               | spoke | dclint defaults                                           | ✓                  |                      |               |
| validate-service-sheets              | spoke | `scripts/linting/validate_service_sheets.py`              | ✓                  |                      |               |
| check-bound-ports                    | spoke | `scripts/linting/check_bound_ports.py`                    | ✓                  |                      |               |
| check-compose-network-mode-conflicts | spoke | `scripts/linting/check_compose_network_mode_conflicts.py` | ✓                  |                      |               |
| lint-compose                         | spoke | `scripts/linting/lint_compose.py`                         | ✓                  |                      |               |
| lint-traefik-swarm                   | spoke | `scripts/linting/lint_swarm.py`                           | ✓                  |                      |               |
| lint-bash-conditional-tests          | spoke | `scripts/linting/check_bash_test_syntax.py`               | ✓                  |                      |               |
| check-powershell-script-naming       | spoke | `scripts/testing/hooks/check_powershell_naming.py`        | ✓                  |                      |               |
| validate-architecture                | spoke | `scripts/testing/validate_architecture.py`                | ✓                  |                      |               |
| psscriptanalyzer                     | spoke | PSScriptAnalyzer defaults                                 | ✓                  |                      |               |

## Super-Linter validator enablement

All Super-Linter v8 validators, enabled and disabled. The shared
`VALIDATE_*` flags live in `.github/super-linter.env` (spoke-specific).

| Validator                   | Config location                                                           | kloehnwars-homelab | docker-swarm-homelab | repo-template |
| --------------------------- | ------------------------------------------------------------------------- | ------------------ | -------------------- | ------------- |
| VALIDATE_ANSIBLE            | `.github/linters/.ansible-lint.yml`                                       | ✓                  |                      |               |
| VALIDATE_BASH               | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_BASH_EXEC          | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_CHECKOV            | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_CLOJURE            | ---                                                                       | ✗                  |                      |               |
| VALIDATE_COFFEE             | ---                                                                       | ✗                  |                      |               |
| VALIDATE_CSS                | ---                                                                       | ✗                  |                      |               |
| VALIDATE_DART               | ---                                                                       | ✗                  |                      |               |
| VALIDATE_DOCKERFILE         | ---                                                                       | ✗                  |                      |               |
| VALIDATE_EDITORCONFIG       | `.editorconfig`, `.github/linters/.editorconfig-checker.json`             | ✓                  |                      |               |
| VALIDATE_ENV                | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_GITHUB_ACTIONS     | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_GITLEAKS           | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_GO                 | ---                                                                       | ✗                  |                      |               |
| VALIDATE_GROOVY             | ---                                                                       | ✗                  |                      |               |
| VALIDATE_HTML               | ---                                                                       | ✗                  |                      |               |
| VALIDATE_JAVA               | ---                                                                       | ✗                  |                      |               |
| VALIDATE_JAVASCRIPT         | ---                                                                       | ✗                  |                      |               |
| VALIDATE_JSCPD              | `.github/linters/.jscpd.json`                                             | ✓                  |                      |               |
| VALIDATE_JSON               | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_JSONC              | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_KOTLIN             | ---                                                                       | ✗                  |                      |               |
| VALIDATE_KUBERNETES         | ---                                                                       | ✗                  |                      |               |
| VALIDATE_LATEX              | ---                                                                       | ✗                  |                      |               |
| VALIDATE_LUA                | ---                                                                       | ✗                  |                      |               |
| VALIDATE_MARKDOWN           | `.github/linters/.markdownlint.yml`, `.github/linters/.markdownlint.json` | ✓                  |                      |               |
| VALIDATE_PERL               | ---                                                                       | ✗                  |                      |               |
| VALIDATE_PHP                | ---                                                                       | ✗                  |                      |               |
| VALIDATE_POWERSHELL         | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_PROTOBUF           | ---                                                                       | ✗                  |                      |               |
| VALIDATE_PYTHON_RUFF        | `.github/linters/.ruff.toml`                                              | ✓                  |                      |               |
| VALIDATE_PYTHON_RUFF_FORMAT | `.github/linters/.ruff.toml`                                              | ✓                  |                      |               |
| VALIDATE_RUBY               | ---                                                                       | ✗                  |                      |               |
| VALIDATE_RUST               | ---                                                                       | ✗                  |                      |               |
| VALIDATE_SHELL_SHFMT        | Super-Linter defaults                                                     | ✓                  |                      |               |
| VALIDATE_SWIFT              | ---                                                                       | ✗                  |                      |               |
| VALIDATE_TERRAFORM          | ---                                                                       | ✗                  |                      |               |
| VALIDATE_TYPESCRIPT         | ---                                                                       | ✗                  |                      |               |
| VALIDATE_XML                | ---                                                                       | ✗                  |                      |               |
| VALIDATE_YAML               | `.github/linters/.yaml-lint.yml`                                          | ✓                  |                      |               |

## Per-repo Super-Linter configuration overrides

Spoke-specific env var values that differ by repo. These override or extend
the shared configuration in `.github/super-linter.env`.

| Variable                 | Config location                                 | kloehnwars-homelab                                | docker-swarm-homelab | repo-template |
| ------------------------ | ----------------------------------------------- | ------------------------------------------------- | -------------------- | ------------- |
| VALIDATE_ANSIBLE         | `.github/super-linter.env`                      | `true`                                            |                      |               |
| ANSIBLE_DIRECTORY        | `.github/super-linter.env`, `_build_env_pairs`  | `automation`                                      |                      |               |
| ANSIBLE_COLLECTIONS_PATH | `_build_env_pairs` (local), workflow env (CI)   | Local: `.github/linters/.ansible/collections`     |                      |               |
| FILTER_REGEX_EXCLUDE     | `_maybe_append_default_filter_excludes` (local) | `.venv`, `.tox`, caches, `automation/collections` |                      |               |
| FILTER_REGEX_EXCLUDE     | workflow env (CI)                               | `automation/collections`                          |                      |               |
| CHECKOV_SKIP_PATH        | `_build_env_pairs` (local), workflow env (CI)   | Includes `automation/collections`                 |                      |               |

## Universal Super-Linter variances (all repos)

Differences between local and CI that apply to every repo and do not require
alignment action.

| Variance                           | Local        | CI                  | Config location                          |
| ---------------------------------- | ------------ | ------------------- | ---------------------------------------- |
| Container workspace path           | `/workspace` | `/github/workspace` | `_build_env_pairs` (local), Actions (CI) |
| SAVE_SUPER_LINTER_SUMMARY          | Not set      | `true`              | workflow env                             |
| ENABLE_GITHUB_ACTIONS_STEP_SUMMARY | Not set      | `true`              | workflow env                             |

## Super-Linter configuration parity (CI vs local)

The table below captures explicit differences and bypasses that affect whether
local runs can pass code that CI would reject, or vice versa. Items marked as
`gap` require a deliberate decision to keep, tighten, or remove.

| Aspect                | Local (pre-commit runner)                                                                           | CI (GitHub Actions)                                    | Impact                                                                                                         | Status  |
| --------------------- | --------------------------------------------------------------------------------------------------- | ------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------- | ------- |
| Image tag             | `ghcr.io/super-linter/super-linter:v8` (default)                                                    | `ghcr.io/super-linter/super-linter:v8`                 | Same linter version                                                                                            | aligned |
| Default branch        | Resolved from repo (or `SUPER_LINTER_DEFAULT_BRANCH`)                                               | `origin/${{ github.event.repository.default_branch }}` | Same target when remote refs exist                                                                             | aligned |
| Scope                 | `VALIDATE_ALL_CODEBASE=false` unless default branch ref missing                                     | `VALIDATE_ALL_CODEBASE=false`                          | Local becomes stricter when branch ref missing                                                                 | gap     |
| Staged-file blindness | `RUN_LOCAL=true` sets `GITHUB_SHA=HEAD` (last commit); `git diff-tree` cannot see staged-only files | N/A (commit exists when CI runs)                       | New files on first branch commit are invisible to local Super-Linter; mitigated by standalone pre-commit hooks | gap     |
| Ruff config           | `./.github/linters/.ruff.toml` (sync copy of `pyproject.toml`)                                      | `./.github/linters/.ruff.toml`                         | Same config; `pyproject.toml` is source of truth                                                               | aligned |
| JSCPD config          | Auto-sets `JSCPD_LINTER_RULES` if not already set                                                   | Explicit `JSCPD_LINTER_RULES` set                      | Same config path, consistent rules                                                                             | aligned |
| Local-only excludes   | Sets `FILTER_REGEX_EXCLUDE` for `.venv`, `.tox`, caches when not specified                          | Not set                                                | Local can skip scanning dev-only artifacts                                                                     | gap     |

## CI workflow enablement

All CI workflows and their triggers.

| Workflow                    | Trigger                | kloehnwars-homelab | docker-swarm-homelab | repo-template |
| --------------------------- | ---------------------- | ------------------ | -------------------- | ------------- |
| pre-commit.yml              | push, PR, merge_group  | ✓                  |                      |               |
| super-linter.yml            | push, PR, merge_group  | ✓                  |                      |               |
| tdd-testing-pipeline.yml    | push, PR               | ✓                  |                      |               |
| commitlint.yml              | PR                     | ✓                  |                      |               |
| auto-fix-markdown.yml       | push to main           | ✓                  |                      |               |
| auto-labeler.yml            | PR, issues             | ✓                  |                      |               |
| labels-sync.yml             | schedule, manual       | ✓                  |                      |               |
| dependabot-auto-merge.yml   | schedule               | ✓                  |                      |               |
| sync-directives-push.yml    | push to main           | ✓                  |                      |               |
| sync-directives-pull.yml    | schedule               | ✓                  |                      |               |
| issue-priority-triage.yml   | issues                 | ✓                  |                      |               |
| validate-copilot-limits.yml | push, PR               | ✓                  |                      |               |
| release.yml                 | push to main           | ✓                  |                      |               |

## Sync-directives commit ordering

When adding new files to `.github/sync-directives.yml`, the manifest update
must be committed **before** the files it references. The
`check-sync-directives-completeness` hook validates that all tracked files
are listed in the manifest. Committing files before their manifest entry
causes the hook to fail.

Order of operations:

1. Commit `.github/sync-directives.yml` with the new file entries.
2. Commit the actual files (Python, YAML, etc.).

This mirrors the existing rule for `.pre-commit-config.yaml`: commit config
changes before the files they govern.

## Alignment targets

- Local Windows and Linux must continue to use identical hook definitions from
  `.pre-commit-config.yaml`.
- CI runs the full pre-commit hook suite (`.github/workflows/pre-commit.yml`) with:
  - `SKIP` derived from `.github/precommit-hook-classification.yml` `ci_skip`
    list (currently: super-linter, psscriptanalyzer)
  - `SKIP_GIT_SIGNING_CHECK=1` (CI cannot configure commit signing)
- Where `Parity status` is `gap`, either:
  - add the check to GitHub Actions with the same config, or
  - add a local hook equivalent when a CI check exists without a local hook.

## Ruff configuration sync

Super-Linter passes `--config` to ruff, which bypasses `pyproject.toml`
discovery. The file `.github/linters/.ruff.toml` is a sync copy of
`pyproject.toml` `[tool.ruff]` settings for Super-Linter consumption. When
ruff configuration changes in `pyproject.toml`, update `.ruff.toml` to match.
