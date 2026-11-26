# Contributing

## Setup

Requires the .NET 10 SDK on Windows x64, the only platform CI verifies. Python tooling remains until
[#108](https://github.com/paragon-stats/homecoming-build-companion/issues/108); the hooks no longer need it.
Signed commits are required.
Run `make bootstrap` once per clone: it restores the .NET tools, installs the Git hooks and fails until commit signing is set up.
The pre-commit markdownlint check runs through `npx`, so install Node.js.
Docker is needed only for the pre-push Super-Linter run, which is skipped when Docker is missing.

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

| Hook | Checks |
| --- | --- |
| `pre-commit` | `dotnet format`, the encoding check, the banned-words check, the coverage-exclusion check on the staged files (`[ExcludeFromCodeCoverage]` only in a `*JsonContext.cs` file that derives from `JsonSerializerContext`; CI runs the same check on the working tree), the commit-signing check, and the text-file check (`scripts/dev/check-text-files.cs`: trailing whitespace, final newline and line endings as `.editorconfig` sets them, JSON syntax, files over 15 MB, markdownlint) on the staged files |
| `commit-msg` | the Conventional Commit check that the commitlint workflow runs |
| `pre-push` | `dotnet build`, `dotnet test --no-build`, the banned-words check and Super-Linter in Docker |

The text-file check skips the files the CI Super-Linter job excludes.
`scripts/dev/run-super-linter.cs` reads that pattern (`FILTER_REGEX_EXCLUDE`) and the image version from `.github/workflows/super-linter.yml`, so the workflow is the only list.
The pre-push Super-Linter run lints the changes against `origin/main` with the image CI uses; run it by hand with `make super-linter`.
YAML syntax is checked there, not at commit time.

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
