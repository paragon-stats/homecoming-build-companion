# SonarQube Cloud analysis

The `sonar` workflow (`.github/workflows/sonar.yml`) analyses the repository with SonarQube Cloud on every pull request to `main` and every push to `main`.
It follows the workflow in paragon-stats/paragon-stats.

## What is analysed

- The .NET solution, through the MSBuild scanner: begin, build, tests with coverage, end.
- Coverage comes from `coverage.xml`, the Visual Studio XML report `dotnet-coverage` writes for the `build` workflow's 100% coverage gate.
  The scanner reads it through `sonar.cs.vscoveragexml.reportsPaths`.
- The remaining Python (`scripts/**`, `src/coh_engine`) is analysed with `sonar.python.version=3.14`.
  It has no coverage in this job: the Python coverage workflow writes no coverage XML, and re-running its matrix here would double the cost.

## What is excluded

| Property | Pattern | Why |
| --- | --- | --- |
| `sonar.exclusions` and `sonar.cpd.exclusions` | `data/**`, `tests/fixtures/**`, `samples/**`, `knowledge/**` | Generated or third-party data, not code under review. |
| same | `**/obj/**`, `**/bin/**` | Build output. |
| same | `tools/mids-dump*/**` | The Mids dump harness is outside the solution, so this job does not build it. |
| `sonar.coverage.exclusions` | `**/*JsonContext.cs` | Source-generated JSON contexts, marked `[ExcludeFromCodeCoverage]`, which the 100% gate leaves out too. |
| same | `scripts/**`, `src/coh_engine/**` | Python with no coverage report in this job; left in, it would count as uncovered. |

Sonar's coverage figure and the repository's gate therefore measure the same .NET code.

## How the quality gate blocks

The begin step sets `sonar.qualitygate.wait=true`, so the end step waits for SonarQube Cloud and fails the `sonar` job when the project's quality gate fails.
A failed job is a failed `sonar` check.

Pull requests from forks cannot read `SONAR_TOKEN`, so the job is skipped for them and reports as skipped.
Dependabot pull requests run the scan, using the organisation's Dependabot secret of the same name.

## The rule gate

SonarQube Cloud's pull-request analysis reports only issues a pull request introduces, so a pull request that leaves an old finding in place still shows zero.
The `Sonar rule gate` step, after the end step, runs `python -m scripts.ci.sonar_gate` and fails the job while a gated rule has open findings.
The `SonarAnalyzer.CSharp` package is not added to the build instead.
Its SONAR Source-Available License v1.0 limits use to a "Non-competitive Purpose", which does not allow distribution under GPL-3.0.

The rules are the lines of `scripts/ci/sonar_gate_rules/*.txt`: `<rule-key> <path-prefix>`, for example `csharpsquid:S3776 src/CohEngine.Core/Assembly/`.
`#` starts a comment and blank lines are skipped.
Any other line makes the gate exit 2 and name the file and line.
Each backlog issue adds its own file, so parallel pull requests do not conflict.
No entry names `src/coh_engine/` or `scripts/forum_scrape/tests/fixtures/`.

Three checks, printed as one count per entry:

- **Scanner output (`scanner`, every run, `csharpsquid` rules).**
  The scanner runs Sonar's C# analyzer inside `dotnet build` and writes each project's results to `.sonarqube/out/<n>/Issues.json`.
  The workflow's build is a full build, so the files hold every result for every analysed file, old or new, in source and test projects, suppressed ones included.
  A result of a gated rule under the entry's prefix fails the gate.
  The gate also fails, closed, when no `Issues.json` exists, one is unreadable or has no runs, or all hold no result.
  The log line `read N Issues.json files with M csharpsquid results` shows what it read.
- **Baseline (`baseline`, pull requests).**
  Every finding open on `main` under an entry's prefix must be in code the pull request changes.
  For `csharpsquid` rules and any `:S3776` rule the finding's file must be changed.
  For other rules the finding's line must be a changed or deleted line on the old side of `git diff -U0 origin/main...HEAD`.
  A finding with no line needs its file changed.
- **Pull-request analysis (`pull-request`, pull requests, rules outside C#).**
  The pull request's own open issues, from the public API, must hold none of an entry's rule under its prefix.
- **Main (`main`, push to `main`).**
  Every finding open on `main` for a gated rule under an entry's prefix fails the gate.
  For rules outside C# this is the full check.

For rules outside C#, Sonar can match a still-present issue on a changed line to the old one by rule, message and line number.
The pull-request checks are therefore line-level evidence, and the push to `main` is the full check.
For cognitive complexity (`S3776`) outside C# the pull-request check is per file only, because the finding sits on the unchanged function-name line.

## One-time setup (operator)

1. In SonarQube Cloud, organisation `paragon-stats`, create the project `paragon-stats_homecoming-build-companion` and bind it to this repository.
2. Choose its quality gate.
3. The organisation secret `SONAR_TOKEN` must be visible to this repository and, for Dependabot pull requests, present in the organisation's Dependabot secrets.
4. Open the pull request and wait for the first green `sonar` run.

## Make the check required

Only after the first green `sonar` run: the ruleset change is a separate commit, and the operator applies it with the command in [`github-rulesets/README.md`](github-rulesets/README.md).
Requiring a check that has never reported freezes every pull request.

## First-run failures

- Project not created yet: the begin step fails with a message that the project key `paragon-stats_homecoming-build-companion` was not found or that the token has no permission on it.
  Create and bind the project, then re-run the job.
- Coverage report not found: the end step logs a warning that no coverage report matched `coverage.xml`, and the project shows 0% coverage.
  Check that the "Test with coverage" step wrote `coverage.xml` in the workspace root.
