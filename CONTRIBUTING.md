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

Husky.Net hooks run `dotnet format`, the encoding check and the banned-words check on commit, and the build and tests on push.
CI runs the same checks plus the coverage gate.

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

Apache-2.0-compatible licenses only (MIT, BSD, ISC, Apache-2.0, CC0). No GPL, AGPL or MPL. See [dependencies.md](docs/style-guides/dependencies.md).

## AI assistance

Welcome. Agent instruction files (`CLAUDE.md`, `.claude/`, and the like) are not committed; keep yours local.
See the [AI-assistance policy](docs/style-guides/ai-assistance-policy.md).
