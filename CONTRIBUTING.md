# Contributing

## Setup

Requires the .NET 10 SDK on Windows x64, the only platform CI verifies, Node.js (the pre-commit markdownlint check runs through `npx`) and, for the pre-push Super-Linter run, Docker.
Signed commits are required.
Run `make bootstrap` once per clone: it restores the .NET tools, installs the Git hooks, and fails until commit signing is set up.
The Git hooks need no Python; the `commit-msg` hook runs through the bash that Git for Windows ships.
A worktree or branch cut before the hook moved to bash runs the Python check through `scripts/dev/hook-python.sh` until it is rebased onto `main`.
That script stays until the remaining Python is removed.
The remaining Python tooling is tracked in [#108](https://github.com/paragon-stats/homecoming-build-companion/issues/108).

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
| `pre-commit` | the text-file check, the encoding check, the coverage-exclusion check on the staged files, the commit-signing check, the banned-words check, and `dotnet format --verify-no-changes` |
| `commit-msg` | `scripts/git/check-commit-message.sh` on the message, the check the commitlint workflow runs; the rule that release types change `src/` is left to CI, since the hook cannot tell an amend from a new commit |
| `pre-push` | the banned-words check, `dotnet build`, Super-Linter in Docker, and `dotnet test --no-build` |

Husky runs a group's checks one after another and stops at the first failure.
Each group therefore runs its quickest checks first, so a quick check that fails is reported in seconds.
Measured warm times, in run order:

| Stage | Check | Time |
| --- | --- | --- |
| `pre-commit` | text files | 0.5-0.6 s (2.2-2.5 s with a Markdown file staged) |
| `pre-commit` | encoding | 0.7 s |
| `pre-commit` | coverage exclusions | 0.8 s |
| `pre-commit` | commit signing | 0.8 s |
| `pre-commit` | banned words | 1.0-1.2 s |
| `pre-commit` | `dotnet format` | 30-36 s |
| `pre-push` | banned words | 1.0-1.2 s |
| `pre-push` | `dotnet build` | 8 s with nothing to rebuild |
| `pre-push` | Super-Linter | 58-67 s (0.6 s when it skips without Docker) |
| `pre-push` | `dotnet test --no-build` | 120 s; it needs the build, so it stays after it |

Husky adds about 0.3 s per check.
A script compiles once after it changes, and once in a new worktree, in 3.5-5 s.

The `pre-commit` and `pre-push` hooks run their group with Husky's `--quiet` switch, which leaves out Husky's banner and separator lines.
Each script check prints one line on standard output: `[OK] ...`, or `[SKIP] ...` with the reason.
Findings go to standard error.
`dotnet format`, `dotnet build`, `dotnet test` and Super-Linter print their own output.
To see Husky's banner and per-check lines, run the group by hand without the switch, for example `dotnet husky run --group pre-commit`.

The hooks do not restore the .NET tools; `make bootstrap` does that once per clone.
Without them, the hook fails with `Run "dotnet tool restore" to make the "husky" command available.`, and `make hooks-check` reports it.

Husky has none of the following, and the hooks do not imitate them:

- a summary table of the checks at the end of a run
- the time each check took, while `--quiet` is on
- running the remaining checks after one fails
- running checks side by side
- a plain-ASCII failure line: Husky's `Task '<name>' failed in <n>ms` line starts with a non-ASCII cross mark, with or without `--quiet`

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
  `feat`, `fix`, `perf`, `security` and `revert` must change a file under `src/`; the full rules are in [repository-conventions.md](docs/style-guides/repository-conventions.md#gitops-conventions--branches-commits-prs).
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
