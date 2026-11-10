# Naming and words

**Status:** MUST. Applies to every identifier, filename, branch name, comment, document, commit message and conversation in this project.

The mechanical part is enforced by `scripts/dev/check-banned-words.cs`, which runs in the pre-commit hook and in CI.

## Refer to the user generically

In conversation use "you" and "your". In code, comments, prose, frontmatter values, log output and tooling strings use generic placeholders.
The operator's first name, or any other personally identifying name, stays out of project artifacts. The check reads the names to look for from the
`COH_BANNED_NAMES` environment variable (comma-separated), so the name itself never appears in the repository.
When the variable is unset locally the check skips the name test and says so.
In CI the variable comes from the `COH_BANNED_NAMES` repository secret, and the check fails if it is missing.

| Context | Use |
| --- | --- |
| `captured_by:` frontmatter | `local-capture` |
| Prose attribution | "house rule," "standing rule," "the user" |
| Code template defaults | `<your_handle>`, `local-capture`, or omit |
| Log messages, error strings | "the user" or omit personal reference |
| Build attribution | "user-supplied," "from local capture" |

How to apply:

- Templates embed the generic value, not a name, because a template is shipped verbatim.
- Prose addresses the reader as "you"; project preferences are a "house rule" or "standing rule"; content origin is "user-supplied" or "captured locally."
- An existing artifact that carries a name is scrubbed on first touch. Do not preserve the name for context.

```yaml
captured_by: local-capture
source: forums.homecomingservers.com
```

## Banned words

| Banned | Say instead |
| --- | --- |
| "oracle" | "MCP surface" (the tool interface), "Mids dump harness" (the program that dumps Mids data), "Mids reference dumps" (its output), "parity tests" (the tests that compare against those dumps) |

The list lives at the top of `scripts/dev/check-banned-words.cs`; add a word there and to this table together.
The check matches a word at the start of a word, so plurals and suffixed forms are caught too.

## Plain words

Use the plain word, not the jargon. Say "source material", and for builds "sample builds"; do not say "corpus".
The rule covers identifiers, filenames and branch names as well as prose.
