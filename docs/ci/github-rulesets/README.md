# GitHub rulesets

`main-ruleset.json` is the request body for the `main` branch ruleset (id 15875338).
The repository admin applies it by hand; nothing in CI changes repository settings.

## What the ruleset enforces

- No deletion of `main` and no non-fast-forward pushes.
- Signed commits.
- Pull requests only, merge commits only, with one approving review, code-owner review (see `.github/CODEOWNERS`), resolved review threads and stale-review dismissal on push.
- Required status checks, with branches up to date before merge.
- Code quality findings at the `warnings` severity.

## Bypass

The one bypass actor is the repository operator (user id 35460307) in `pull_request` mode.
The operator can merge their own pull request without a second reviewer.
Direct pushes to `main` stay blocked, and each bypass is recorded on the pull request.
Nobody else can bypass.

## Apply the ruleset

```bash
gh api -X PUT repos/paragon-stats/homecoming-build-companion/rulesets/15875338 --input docs/ci/github-rulesets/main-ruleset.json
```

Verify:

```bash
gh api repos/paragon-stats/homecoming-build-companion/rulesets/15875338 --jq '.bypass_actors, (.rules[]|select(.type=="pull_request").parameters)'
```

The required status check list still names `All Tests Passed`.
Swap it for the dotnet checks when issue #108 lands.

## Enable secret scanning

Secret scanning, non-provider patterns and push protection are free on public repositories.
If the non-provider patterns setting is rejected, enable `secret_scanning` and `secret_scanning_push_protection` only.

```bash
gh api -X PATCH repos/paragon-stats/homecoming-build-companion --input docs/ci/github-rulesets/security-and-analysis.json
```

Verify:

```bash
gh api repos/paragon-stats/homecoming-build-companion --jq .security_and_analysis
```

To roll back, run the same command with each status set to `disabled`.
