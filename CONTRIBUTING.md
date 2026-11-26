# Contributing

## Setup

Requires the .NET 10 SDK on Windows x64, the only platform CI verifies, Node.js (the pre-commit markdownlint check runs through `npx`) and, for the pre-push Super-Linter run, Docker.
Signed commits are required.
Run `make bootstrap` once per clone: it restores the .NET tools, installs the Git hooks, and fails until commit signing is set up and the Python below exists.

The `commit-msg` hook still runs Python (`scripts/dev/hook-python.sh`, the Conventional Commit check) until the commit-message check is ported to .NET.
Until then a fresh clone needs a Python with `pre-commit` installed, or its commits are refused:

```text
python -m venv .venv
.venv/Scripts/python.exe -m pip install -e ".[dev]"
```

On Linux and macOS use `.venv/bin/python` for the second line.
`make bootstrap` and `make hooks-check` check for it and print these commands when it is missing.
The other Python tooling is tracked in [#108](https://github.com/paragon-stats/homecoming-build-companion/issues/108).

## Build and test

```text
dotnet build
dotnet test
```

CI runs the same checks plus the coverage gate.

## Git hooks

`make bootstrap` installs the hooks once per clone, from the main checkout; it runs:

```text
dotnet tool restore && dotnet husky install
```

This sets `core.hooksPath=.husky` in the repository's shared Git configuration.
Every `git worktree` of the clone then runs the hooks committed in its own `.husky/` folder, with no setup of its own.
`make hooks-check` fails with the fix unless git will run the hooks in the current checkout.
It checks two things: `core.hooksPath` resolves to the checkout's `.husky`, and the three hook files exist and are executable there.
A worktree or branch cut before the hooks were merged has no `.husky/pre-commit`; git then runs no hook at all and does not fall back to `.git/hooks`.
`make hooks-check` reports that as "this checkout predates the Husky hooks: merge or rebase onto main".
`dotnet husky install` also creates an ignored `.husky/_/` folder; the committed hooks do not use it.

| Stage | What runs |
| --- | --- |
| `pre-commit` | `dotnet format --verify-no-changes`, the encoding check, the banned-words check, the coverage-exclusion check on the staged files, the commit-signing check, and the text-file check |
| `commit-msg` | the Conventional Commit check that the commitlint workflow runs (Python, see Setup) |
| `pre-push` | `dotnet build`, `dotnet test --no-build`, the banned-words check, and Super-Linter in Docker |

The text-file check (`scripts/dev/check-text-files.cs`) reads each staged file as staged and checks:
trailing whitespace, final newline, extra blank lines at the end and line endings as `.editorconfig` sets them; JSON syntax of every staged `.json` file; files over 15 MB; and markdownlint.
It skips the files the CI Super-Linter job excludes, except for JSON syntax and size.
markdownlint reads the file in the working tree, and a Markdown file name with a shell character (`& | ^ % < > " !`) is refused.
The signing check (`scripts/dev/check-git-signing.cs`) needs `commit.gpgsign=true`, `gpg.format=ssh` and a `user.signingkey` that is a key file or a literal key.
It also needs either a `gpg.ssh.program` that exists or a successful `ssh-keygen` signature of a scratch file.

Not run at commit time any more:

- **Pre-push, when Docker is installed:** Super-Linter checks YAML syntax, EditorConfig (indentation and the rest), ruff and the other linters CI runs, on the changes against `origin/main`.
  Without Docker the step prints a notice and passes; with Docker installed but not running it fails and says to start Docker.
- **CI only:** mypy, the TOML syntax check and the Python `pre-commit` workflow.

`scripts/dev/run-super-linter.cs` reads the image version and `FILTER_REGEX_EXCLUDE` from `.github/workflows/super-linter.yml`, so the workflow is the only exclusion list.
Run it by hand with `make super-linter`.

## Sign your commits

Every commit is signed with an SSH key. Set it up once:

```text
ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519_signing -C "you@example.com"
git config --global gpg.format ssh
git config --global user.signingkey ~/.ssh/id_ed25519_signing.pub
git config --global commit.gpgsign true
```

Then add the public key to your GitHub account as a **signing key** (Settings, SSH and GPG keys).
With a 1Password or other signing program, set `gpg.ssh.program` to it instead of running `ssh-keygen`.
The pre-commit `check-git-signing` task signs a test file with the key and fails with the diagnostic `H-SIGN-001` when signing does not work.

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
