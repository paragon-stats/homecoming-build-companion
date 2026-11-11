# GitHub rulesets

`main-ruleset.json` is the request body for the `main` branch ruleset (id 15875338).
The repository admin applies it by hand; nothing in CI changes repository settings.

## What the ruleset enforces

- No deletion of `main` and no non-fast-forward pushes.
- Signed commits.
- Pull requests only, merge commits only, with one approving review, code-owner review (see `.github/CODEOWNERS`), resolved review threads and stale-review dismissal on push.
- Required status checks, with branches up to date before merge.
- Code quality findings at the `warnings` severity.
- CodeQL alerts block a pull request (see "Code-scanning merge gate" below).

## Bypass

The one bypass actor is the repository operator (user id 35460307) in `pull_request` mode.
The operator can merge their own pull request without a second reviewer.
Direct pushes to `main` stay blocked, and each bypass is recorded in the repository audit log.
Nobody else can bypass.

## Apply the ruleset

```bash
gh api -X PUT repos/paragon-stats/homecoming-build-companion/rulesets/15875338 --input docs/ci/github-rulesets/main-ruleset.json
```

Verify:

```bash
gh api repos/paragon-stats/homecoming-build-companion/rulesets/15875338 --jq '.bypass_actors, (.rules[]|select(.type=="pull_request").parameters)'
```

Expect `required_approving_review_count` 1, `require_code_owner_review` true, and exactly one bypass actor in `pull_request` mode.

The required status check list includes `sonar`.
Apply the ruleset only after the first green `sonar` run (see [`../sonar.md`](../sonar.md)): requiring a check that has never reported freezes every pull request.

The required status check list still names `All Tests Passed`.
Swap it for the dotnet checks when issue #108 lands.

## Enable secret scanning

Secret scanning, non-provider patterns and push protection are free on public repositories.
If the PATCH is refused because non-provider patterns is unavailable on this plan, run the same command with `--input docs/ci/github-rulesets/security-and-analysis.fallback.json`.
That body enables `secret_scanning` and `secret_scanning_push_protection` only.

```bash
gh api -X PATCH repos/paragon-stats/homecoming-build-companion --input docs/ci/github-rulesets/security-and-analysis.json
```

Verify:

```bash
gh api repos/paragon-stats/homecoming-build-companion --jq .security_and_analysis
```

To roll back, run the same command with each status set to `disabled`.

## Code-scanning merge gate

`main-ruleset.json` carries a `code_scanning` rule for the `CodeQL` tool (alerts threshold `all`, security alerts threshold `all`).
The rule is permanent.
On 2026-11-10 it was applied to ruleset 15875338 while `main` had 414 open alerts.
Pull request #149, which had no open alerts of its own, showed code scanning as satisfied and merged.
So the rule judges only the alerts a pull request introduces, not the backlog on the base branch.
GitHub's documentation does not say this, which is why it is recorded here.
The pages checked are the [REST rules reference][rest-rules], [available rules for rulesets][available-rules] and [set merge protection][merge-protection].

- Apply: run the command under "Apply the ruleset".
- Dismiss a deliberate alert: open it on the Security tab under Code scanning, choose Dismiss alert, pick a reason and write a comment that says why.
  The one exact float comparison in `src/CohEngine.Core/Maths/Exact.cs` is an example.
- Remove the rule, if ever needed: `main-ruleset.rollback.json` is the ruleset as it was before the rule, and this command restores it.

```bash
gh api -X PUT repos/paragon-stats/homecoming-build-companion/rulesets/15875338 --input docs/ci/github-rulesets/main-ruleset.rollback.json
```

[rest-rules]: https://docs.github.com/en/rest/repos/rules
[available-rules]: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets
[merge-protection]: https://docs.github.com/en/code-security/how-tos/find-and-fix-code-vulnerabilities/manage-your-configuration/set-merge-protection
