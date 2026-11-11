# Contributing

## Setup

Requires the .NET 10 SDK on Windows x64, the only platform CI verifies. Python tooling remains until
[#108](https://github.com/paragon-stats/homecoming-build-companion/issues/108); its venv is created by `scripts/dev/bootstrap_venv.py`.
Signed commits are required.

## Build and test

```text
dotnet build
dotnet test
```

CI runs the same checks plus the coverage gate.

## Git hooks

Install the hooks once per clone, from the main checkout:

```text
dotnet tool restore && dotnet husky install
```

This sets `core.hooksPath=.husky` in the repository's shared Git configuration.
Every `git worktree` of the clone then runs the hooks committed in its own `.husky/` folder, with no setup of its own.
`make hooks-check` fails with the command above when the hooks are not installed.
`dotnet husky install` also creates an ignored `.husky/_/` folder; the committed hooks do not use it.

| Hook | Checks |
| --- | --- |
| `pre-commit` | `dotnet format`, the encoding check, the banned-words check, the coverage-exclusion check (`[ExcludeFromCodeCoverage]` only in `*JsonContext.cs`), and the Python `pre-commit` checks in `.pre-commit-config.yaml` (commit signing, ruff, mypy, markdownlint, editorconfig, file hygiene) |
| `commit-msg` | the Conventional Commit check that the commitlint workflow runs |
| `pre-push` | `dotnet build`, `dotnet test --no-build` and the banned-words check |

The Python checks need the venv from `make bootstrap`.
A worktree uses the main checkout's `.venv`, or the one on `PATH`; with neither, the hook fails and prints the bootstrap command.
Super-Linter stays manual: `make super-linter`.

## Commits and pull requests

- Branch from `main`; open pull requests against `main`.
- Use [Conventional Commits](https://www.conventionalcommits.org): `feat:`, `fix:`, `chore:`, `docs:`.
- Every commit references its issue (`Refs #N` or `Closes #N`).
- All status checks must pass before merge.

The full conventions are in [git-standards.md](docs/repository-standards/git-standards.md) and [git-branching.md](docs/repository-standards/git-branching.md).

## Style

The guides in [docs/style-guides/](docs/style-guides/README.md) are the source of truth: build authoring, engine architecture, naming and words, dependencies.
`dotnet run scripts/dev/check-banned-words.cs` enforces the naming rules; set `COH_BANNED_NAMES` (comma-separated) locally to add names it should reject.

## Dependencies

The project is [GPL-3.0-only](LICENSE).
A dependency's license must allow distribution under GPL-3.0: permissive licenses such as MIT, BSD, ISC, Apache-2.0 and CC0 qualify, and a license that forbids GPL distribution does not.
Record each dependency's license in [dependencies.md](docs/style-guides/dependencies.md).

## AI assistance

Welcome. Agent instruction files (`CLAUDE.md`, `.claude/`, and the like) are not committed; keep yours local.
See the [AI-assistance policy](docs/style-guides/ai-assistance-policy.md).
