# GitHub rulesets

`main-ruleset.json` is the request body for the `main` branch ruleset (id 15875338).
The repository admin applies it by hand; nothing in CI changes repository settings.

## What the ruleset enforces

- No deletion of `main` and no non-fast-forward pushes.
- Signed commits.
- Pull requests only, merge commits only, with one approving review, code-owner review (see `.github/CODEOWNERS`), resolved review threads and stale-review dismissal on push.
- Required status checks, with branches up to date before merge.
- Code quality findings at the `warnings` severity.
- CodeQL alerts block a pull request (on trial; see below).

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

## Trial of the code-scanning gate

`main-ruleset.json` carries a `code_scanning` rule for the `CodeQL` tool (alerts threshold `all`, security alerts threshold `all`).
It is a trial.
GitHub's documentation does not say whether the rule counts alerts already on the base branch or only those a pull request introduces.
It does not say whether the rule is available to a public repository on the Free plan.
The pages checked are the [REST rules reference][rest-rules], [available rules for rulesets][available-rules] and [set merge protection][merge-protection].
`main` carries 361 open alerts today (issue #140 tracks clearing them).
`main-ruleset.rollback.json` is the ruleset as it was before the rule, so the gate can be removed with one command.

- Step 1, apply: run the command under "Apply the ruleset".
  If the API rejects the rule for this plan, the error says so and nothing has changed.
- Step 2, check a pull request that introduces no alerts with `gh pr view <N> --json mergeStateStatus,statusCheckRollup`.
  A code-scanning block shows in the merge box as a failed or pending code scanning result, and `mergeStateStatus` is `BLOCKED`.
- Step 3, outcome A: the pull request stays mergeable, so the rule counts only new alerts. Keep it.
- Step 3, outcome B: the pull request is blocked with no alerts of its own, so the rule counts the backlog.
  Roll back with the command below, and apply the ruleset again once #140 has cleared `main`.
- Step 4, dismiss a deliberate alert: open it on the Security tab under Code scanning, choose Dismiss alert, pick a reason and write a comment that says why.
  The one exact float comparison in `src/CohEngine.Core/Maths/Exact.cs`, arriving with the chain-planner PR, is an example.

```bash
gh api -X PUT repos/paragon-stats/homecoming-build-companion/rulesets/15875338 --input docs/ci/github-rulesets/main-ruleset.rollback.json
```

[rest-rules]: https://docs.github.com/en/rest/repos/rules
[available-rules]: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets
[merge-protection]: https://docs.github.com/en/code-security/how-tos/find-and-fix-code-vulnerabilities/manage-your-configuration/set-merge-protection
