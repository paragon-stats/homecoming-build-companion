# GitHub MCP server (project-scoped, local Docker)

GitHub operations from an agent session go through GitHub's official MCP server, run locally
in Docker. It is configured at **project scope** in [`.mcp.json`](../../.mcp.json), so it loads
only in this repository — not for every Claude Code session on the machine.

## Why project scope

The two repositories that need it are `paragon-stats/homecoming-build-companion` and
`paragon-stats/paragon-stats`. A user-scoped server would be live in every project on the
machine, including ones with no GitHub involvement. Each of the two repositories carries its
own `.mcp.json` instead.

## Configuration

```json
{
  "mcpServers": {
    "github-local": {
      "type": "stdio",
      "command": "op",
      "args": [
        "run", "--",
        "docker", "run", "--rm", "-i",
        "-e", "GITHUB_PERSONAL_ACCESS_TOKEN",
        "ghcr.io/github/github-mcp-server@sha256:<digest>",
        "stdio",
        "--toolsets=context,repos,issues,pull_requests,actions,labels"
      ]
    }
  }
}
```

Mirrors the `gitlab-local` pattern: `op run --` resolves the `op://` reference in `env` and
injects it into the process environment, and `docker run -e NAME` (no `=value`) passes that
variable through to the container. The token is never written to disk.

### No secrets in this file

`.mcp.json` is committed and this repository is public, so it holds no secret material:

- `GITHUB_PERSONAL_ACCESS_TOKEN` is an `op://` **reference**, resolved at launch by `op run`.
- `OP_SERVICE_ACCOUNT_TOKEN` is `${OP_SERVICE_ACCOUNT_TOKEN}`, expanded from the environment.
  It must be set in the shell that launches Claude Code. Unlike the user-scoped `gitlab-local`
  entry, which stores the service-account token inline in `~/.claude.json`, the value cannot be
  inlined here.

### Image pinning

The image is pinned by digest rather than a tag, per
[`devsecops.md`](../../.claude/rules/devsecops.md) (pin third-party components in security
paths). The pinned digest is `github-mcp-server` v1.11.0. To upgrade:

```bash
docker pull ghcr.io/github/github-mcp-server:<new-tag>
docker image inspect ghcr.io/github/github-mcp-server:<new-tag> --format '{{index .RepoDigests 0}}'
```

then replace the digest in `.mcp.json`.

## Scoping the permissions

Two independent layers. The toolset list is convenience; the token is the enforcement boundary.

### Toolsets

`--toolsets` selects which tool groups load. This project enables `context`, `repos`, `issues`,
`pull_requests`, `actions`, `labels`. The server warns (`unrecognized toolsets ignored`) rather
than failing on a bad name, so verify changes:

```bash
docker run --rm ghcr.io/github/github-mcp-server --help          # available toolsets
docker run --rm ghcr.io/github/github-mcp-server list-scopes     # tools -> required scopes
```

Other useful flags: `--read-only` restricts the server to read operations, `--exclude-tools`
drops individual tools, and `--lockdown-mode` exists for further restriction.

### Token

Use a **fine-grained** personal access token, not a classic one — a classic PAT with `repo`
scope reaches every repository the account can see, which defeats the point. Grant repository
access to exactly `paragon-stats/homecoming-build-companion` and `paragon-stats/paragon-stats`,
with the repository permissions the enabled toolsets need:

| Permission | Level | Needed by |
| --- | --- | --- |
| Metadata | Read | required for all access |
| Contents | Read | `repos` |
| Issues | Read and write | `issues`, `labels` |
| Pull requests | Read and write | `pull_requests` |
| Actions | Read | `actions` |

Store it in 1Password at `op://Homelab/mcp-github-local/credential`, matching the
`mcp-gitlab-local` naming convention.

## Relationship to `scripts/github/`

The MCP server replaces the **agent-facing** GitHub helpers.

There is no longer a CI-facing one. `create_tag.py` existed solely for the CalVer workflow
and validated its argument against the CalVer tag pattern, so it could not have produced a
semver tag; both were removed when releases moved to `python-semantic-release`, which tags
through its own action. That leaves `gh_cli.py` and `cli_utils.py` in the package with no
production caller — a decision for the MCP adoption issue, not something this document
settles.

Note that GitHub Actions still cannot call an MCP server: MCP is an agent-side transport. Any
future CI task needing the GitHub API uses `gh` or the REST API directly, not this server.

Note that `.github/instructions/devsecops_workflow.instructions.md` and
`docs/automation/runbooks/fix-unsigned-commits-in-pr.md` reference several `scripts.github.*`
modules that were never imported into this repository (`pr_upsert`, `pr_auto_summary`,
`fix_unsigned_commits`, `list_pr_commit_verifications`, `list_ssh_signing_keys`,
`triage_review_comments`, `triage_ci_failures`). Those references should point at MCP tools or
be removed; tracked separately.
