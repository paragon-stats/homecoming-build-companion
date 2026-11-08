# AI-assistance policy

This project is built with AI assistance, and contributors are welcome to use it.
This policy is about what gets **committed**, not how you work.

## No committed AI meta-docs

Agent instruction files are intentionally **kept out of the repo**. Their absence is policy, not an oversight:

- `CLAUDE.md`, `AGENTS.md`, `.cursorrules`, `copilot-instructions.md`, `.github/copilot-instructions.md`, `.claude/`, and similar.

Why:

- They are **tool-specific**. Claude's rules are not Cursor's or Copilot's, and committing one maintainer's tooling imposes it on everyone else.
- They **drift** from the real sources of truth: the guides in this directory, the checks, and CI. Two copies of the rules is one too many.
- Rules a tool can check belong in a check that runs in hooks and CI; rules that need judgement belong in a committed, tool-neutral guide.

Every rule that used to live in `CLAUDE.md` and `.claude/rules/` now lives in this directory or in a check. See the [index](README.md).

## Bring your own

Use whatever AI tooling you like locally. Keep its config out of git: add it to `.git/info/exclude` so it is never staged.
Your local `CLAUDE.md` or `.cursorrules` is yours; it just does not get committed.

`/CLAUDE.md` and `/.claude/` are named in `.gitignore` rather than left to each contributor's exclude file.
Both were tracked once, so the entries are a tripwire: re-adding them takes a deliberate `git add -f`, not a stray `git add .`.

## One bar for all code

Every change, AI-assisted or hand-written, clears the same review and CI gate. Origin does not move the bar.

## What differs in this repository

- Python is still present until [#108](https://github.com/paragon-stats/homecoming-build-companion/issues/108) removes it, so the Python and pre-commit conventions in
  [repository-standards](../repository-standards/style-guides/python-style-guide.md) still apply to that code.
- `scripts/forum_scrape` stays: it builds the source-material manifest from the private source-material repository.
- The mechanical naming rules are enforced by `scripts/dev/check-banned-words.cs` (see [naming-and-words.md](naming-and-words.md)).
