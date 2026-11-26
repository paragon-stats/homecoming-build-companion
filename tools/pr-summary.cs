// pr-summary.cs - regenerate a pull request's auto-summary block from the pull request's own commits.
//
//   dotnet run tools/pr-summary.cs -- --pr <N> [--repo owner/name] [--dry-run]
//   dotnet run tools/pr-summary.cs -- --selftest
//
// The commit list comes from the GitHub API, never the local checkout, which may be on a different branch
// than the pull request being summarised. Conventional commits are grouped by type; a pull request that
// references several issues (Closes/Refs trailers) gets one block per issue in the template's format
// ("**Issue #N — Title:**", the issue's first Objective paragraph, then its commits). The result replaces the
// text between the <!-- auto-summary:start --> / <!-- auto-summary:end --> markers and nothing else.
// The repository comes from --repo, then GITHUB_REPOSITORY, then `gh repo view`.
// Exit codes: 0 done (or nothing to change), 2 bad arguments or a failed gh call.
// The body format follows .github/pull_request_template.md and docs/style-guides/repository-conventions.md.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

if (args.Contains("--selftest"))
{
    return Summaries.SelfTest();
}

return Summaries.Run(args, Summaries.RunGh, Console.Out, Console.Error);

/// <summary>Runs gh with an argv (the first element is "gh") and optional stdin; returns stdout or throws InvalidOperationException.</summary>
internal delegate string Gh(string[] argv, string? input = null);

internal sealed record Commit(string Sha, string Type, string? Scope, string Summary, int[] Issues);

internal sealed record IssueInfo(int Number, string Title, string Objective);

internal static class Summaries
{
    public const string Start = "<!-- auto-summary:start -->";
    public const string End = "<!-- auto-summary:end -->";
    private const string CommitsJq = ".[] | select((.parents|length) < 2) | [.sha[0:7], .commit.message] | @json";
    private const string Other = "Other";

    private static readonly Regex Subject = new(@"^(?<type>\w+)(?:\((?<scope>[^)]+)\))?!?:\s*(?<summary>.+)$");
    private static readonly Regex IssueRef = new(@"(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?|refs?)\s*:?\s+#(\d+)", RegexOptions.IgnoreCase);

    // Conventional Commit types in the order they are listed; anything else lands under "Other" so that no
    // commit on the pull request is ever left out of the summary.
    private static readonly (string Type, string Heading)[] Headings =
    [
        ("feat", "Features"), ("fix", "Bug Fixes"), ("perf", "Performance"), ("security", "Security"), ("revert", "Reverts"),
        ("refactor", "Refactor"), ("docs", "Documentation"), ("test", "Testing"), ("ci", "CI/CD"), ("build", "Build"),
        ("style", "Style"), ("chore", "Chores"),
    ];

    public static int Run(string[] args, Gh gh, TextWriter output, TextWriter error)
    {
        string? repo = null;
        int? pr = null;
        bool dryRun = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dry-run")
            {
                dryRun = true;
            }
            else if (args[i] == "--repo" && i + 1 < args.Length)
            {
                repo = args[++i];
            }
            else if (args[i] == "--pr" && i + 1 < args.Length && int.TryParse(args[i + 1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number))
            {
                pr = number;
                i++;
            }
            else
            {
                error.WriteLine($"error: unexpected argument '{args[i]}'");
                pr = null;
                break;
            }
        }

        if (pr is null)
        {
            error.WriteLine("error: --pr is required\nusage: --pr <N> [--repo owner/name] [--dry-run] | --selftest");
            return 2;
        }

        try
        {
            return Handle(repo ?? ResolveRepo(gh), pr.Value, dryRun, gh, output);
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    public static int Handle(string repo, int pr, bool dryRun, Gh gh, TextWriter output)
    {
        List<Commit> commits = FetchCommits(gh, repo, pr);
        Dictionary<int, IssueInfo> issues = commits.SelectMany(commit => commit.Issues).Distinct().Order().ToDictionary(number => number, number => FetchIssue(gh, repo, number));
        string summary = BuildSummary(commits, issues);
        string body = (string?)JsonNode.Parse(gh(["gh", "api", $"repos/{repo}/pulls/{pr}"]))!["body"] ?? "";
        string updated = ReplaceMarkers(body, summary);
        if (dryRun)
        {
            output.WriteLine(updated);
            return 0;
        }

        if (updated == body)
        {
            output.WriteLine("No auto-summary markers found (or no change); PR body left untouched.");
            return 0;
        }

        gh(["gh", "pr", "edit", pr.ToString(System.Globalization.CultureInfo.InvariantCulture), "--repo", repo, "--body-file", "-"], updated);
        output.WriteLine($"Updated PR #{pr} auto-summary ({Plural(commits.Count, "commit")}, {Plural(issues.Count, "issue")}).");
        return 0;
    }

    public static (string Subject, string Body) SplitMessage(string message)
    {
        string normalized = message.Replace("\r\n", "\n").Trim('\n');
        int newline = normalized.IndexOf('\n');
        return newline < 0 ? (normalized.Trim(), "") : (normalized[..newline].Trim(), normalized[(newline + 1)..].TrimStart('\n'));
    }

    public static int[] ExtractIssues(string text) =>
        [.. IssueRef.Matches(text).Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).Distinct().Order()];

    public static Commit ParseCommit(string sha, string subject, string body)
    {
        Match match = Subject.Match(subject);
        int[] issues = ExtractIssues($"{subject}\n{body}");
        return match.Success
            ? new Commit(sha, match.Groups["type"].Value.ToLowerInvariant(), match.Groups["scope"].Success ? match.Groups["scope"].Value : null, match.Groups["summary"].Value, issues)
            : new Commit(sha, "other", null, subject, issues);
    }

    public static string Section(string body, string label)
    {
        Match match = Regex.Match(
            body,
            @"^#{2,3}\s*" + Regex.Escape(label) + @"\s*$(?<content>.*?)(?=^#{1,3}\s|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);
        return match.Success ? match.Groups["content"].Value.Trim() : "";
    }

    public static string GroupCommits(IEnumerable<Commit> commits)
    {
        Dictionary<string, string> byType = Headings.ToDictionary(pair => pair.Type, pair => pair.Heading);
        Dictionary<string, List<Commit>> buckets = Headings.ToDictionary(pair => pair.Heading, _ => new List<Commit>());
        buckets[Other] = [];
        foreach (Commit commit in commits)
        {
            buckets[byType.GetValueOrDefault(commit.Type, Other)].Add(commit);
        }

        List<string> lines = [];
        foreach ((string heading, List<Commit> group) in buckets.Where(pair => pair.Value.Count > 0))
        {
            lines.Add($"#### {heading}");
            lines.AddRange(group.Select(commit => $"- {(commit.Scope is null ? "" : $"**{commit.Scope}:** ")}{commit.Summary} ({commit.Sha})"));
            lines.Add("");
        }

        return string.Join('\n', lines);
    }

    public static string BuildSummary(List<Commit> commits, Dictionary<int, IssueInfo> issues)
    {
        if (issues.Count >= 2)
        {
            List<string> blocks = [.. issues.Values.OrderBy(issue => issue.Number).Select(issue => IssueBlock(issue, commits.Where(commit => commit.Issues.Contains(issue.Number))))];
            List<Commit> orphans = [.. commits.Where(commit => commit.Issues.Length == 0)];
            if (orphans.Count > 0)
            {
                blocks.Add("**Other changes:**\n\n" + GroupCommits(orphans).TrimEnd());
            }

            return string.Join("\n\n", blocks).Trim();
        }

        List<string> parts = [];
        IssueInfo? single = issues.Values.FirstOrDefault();
        if (single is { Objective.Length: > 0 })
        {
            parts.Add(FirstParagraph(single.Objective));
        }

        parts.Add(GroupCommits(commits).TrimEnd());
        return string.Join("\n\n", parts.Where(part => part.Length > 0)).Trim();
    }

    public static string ReplaceMarkers(string body, string summary)
    {
        Regex markers = new(Regex.Escape(Start) + ".*?" + Regex.Escape(End), RegexOptions.Singleline);
        return markers.Replace(body, _ => $"{Start}\n{summary}\n{End}", 1);
    }

    public static List<Commit> FetchCommits(Gh gh, string repo, int pr)
    {
        string raw = gh(["gh", "api", "--paginate", $"repos/{repo}/pulls/{pr}/commits", "--jq", CommitsJq]);
        List<Commit> commits = [];
        foreach (string line in raw.Split('\n').Where(line => line.Trim().Length > 0))
        {
            JsonArray record = JsonNode.Parse(line)!.AsArray();
            (string subject, string body) = SplitMessage((string)record[1]!);
            commits.Add(ParseCommit((string)record[0]!, subject, body));
        }

        return commits;
    }

    public static IssueInfo FetchIssue(Gh gh, string repo, int number)
    {
        JsonNode data = JsonNode.Parse(gh(["gh", "api", $"repos/{repo}/issues/{number}"]))!;
        string body = (string?)data["body"] ?? "";
        return new IssueInfo(number, (string)data["title"]!, Section(body, "Objective"));
    }

    public static string RunGh(string[] argv, string? input = null)
    {
        ProcessStartInfo start = new(argv[0])
        {
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in argv.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"cannot start {argv[0]}");
        if (input is not null)
        {
            process.StandardInput.Write(input);
            process.StandardInput.Close();
        }

        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{string.Join(' ', argv)} exited {process.ExitCode}: {error.Result.Trim()}");
        }

        return output;
    }

    private static string ResolveRepo(Gh gh)
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")?.Trim();
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return fromEnvironment;
        }

        string? name = (string?)JsonNode.Parse(gh(["gh", "repo", "view", "--json", "nameWithOwner"]))?["nameWithOwner"];
        return string.IsNullOrWhiteSpace(name) ? throw new InvalidOperationException("Unable to determine current repo via gh repo view") : name.Trim();
    }

    private static string FirstParagraph(string text) => text.Split("\n\n", 2)[0].Trim();

    // An issue only appears here because a commit references it, so its group is never empty.
    private static string IssueBlock(IssueInfo issue, IEnumerable<Commit> commits)
    {
        List<string> lines = [$"**Issue #{issue.Number} — {issue.Title}:**"];
        if (issue.Objective.Length > 0)
        {
            lines.Add(FirstParagraph(issue.Objective));
        }

        lines.Add("");
        lines.Add(GroupCommits(commits).TrimEnd());
        return string.Join('\n', lines);
    }

    private static string Plural(int count, string word) => $"{count} {word}{(count == 1 ? "" : "s")}";

    public static int SelfTest()
    {
        const string Repo = "owner/repo";
        List<string> failures = [];
        void Check(bool passed, string name)
        {
            if (!passed)
            {
                failures.Add(name);
            }
        }

        // --- split_message ---
        Check(SplitMessage("feat(core): add parser\n\nLong body.\nCloses #5\n") == ("feat(core): add parser", "Long body.\nCloses #5"), "split: subject and body");
        Check(SplitMessage("chore: subject only") == ("chore: subject only", ""), "split: subject only");
        Check(SplitMessage("fix: x\r\nRefs #9\r\n") == ("fix: x", "Refs #9"), "split: CRLF and no blank separator");

        // --- parse_commit ---
        Commit parsed = ParseCommit("aaa1111", "feat(core): parse damage events", "Closes #21");
        Check((parsed.Sha, parsed.Type, parsed.Scope, parsed.Summary) == ("aaa1111", "feat", "core", "parse damage events") && parsed.Issues.SequenceEqual([21]), "parse: conventional with scope");
        parsed = ParseCommit("bbb2222", "fix!: handle empty log", "");
        Check((parsed.Type, parsed.Scope, parsed.Summary, parsed.Issues.Length) == ("fix", null, "handle empty log", 0), "parse: breaking marker and no scope");
        parsed = ParseCommit("ccc3333", "Merge branch tidy-up", "");
        Check((parsed.Type, parsed.Summary) == ("other", "Merge branch tidy-up"), "parse: non-conventional subject is other");

        // --- extract_issues ---
        Check(ExtractIssues("Closes #7\nrefs: #3\nFixes #7\nResolves #12\nRelates to #99 is not a trailer\nsee #4 is not either").SequenceEqual([3, 7, 12]), "issues: every trailer form, deduplicated");

        // --- section ---
        const string IssueForm = "### Type\n\nwork-package\n\n### Objective\n\nFirst paragraph.\n\nSecond paragraph.\n\n### Risk\n\nlow\n";
        Check(Section(IssueForm, "Objective") == "First paragraph.\n\nSecond paragraph.", "section: content");
        Check(Section(IssueForm, "Acceptance criteria") == "", "section: missing is empty");
        Check(Section(IssueForm, "Risk") == "low", "section: last runs to the end");

        // --- group_commits ---
        string grouped = GroupCommits(
        [
            ParseCommit("c1", "chore: tidy", ""), ParseCommit("f1", "feat(core): parse", ""), ParseCommit("x1", "revert(core): undo parse", ""),
            ParseCommit("o1", "Merge something", ""), ParseCommit("f2", "feat: second", ""),
        ]);
        Check(
            grouped.IndexOf("#### Features", StringComparison.Ordinal) < grouped.IndexOf("#### Reverts", StringComparison.Ordinal)
                && grouped.IndexOf("#### Reverts", StringComparison.Ordinal) < grouped.IndexOf("#### Chores", StringComparison.Ordinal)
                && grouped.IndexOf("#### Chores", StringComparison.Ordinal) < grouped.IndexOf("#### Other", StringComparison.Ordinal),
            "group: fixed order");
        Check(grouped.Contains("- **core:** parse (f1)\n- second (f2)\n", StringComparison.Ordinal) && grouped.Contains("- Merge something (o1)", StringComparison.Ordinal), "group: scope and sha");
        Check(GroupCommits([]) == "", "group: empty");

        // --- build_summary ---
        Dictionary<int, IssueInfo> issues = new()
        {
            [21] = new(21, "Damage parsing", "Parse combat damage lines.\n\nMore detail."),
            [22] = new(22, "CLI robustness", ""),
        };
        string summary = BuildSummary(
            [ParseCommit("aaa1111", "feat(core): parse damage events", "Closes #21"), ParseCommit("bbb2222", "fix(cli): handle empty log", "Refs #22"), ParseCommit("ccc3333", "chore: tidy", "")],
            issues);
        Check(
            summary.StartsWith("**Issue #21 — Damage parsing:**\nParse combat damage lines.\n\n#### Features\n", StringComparison.Ordinal)
                && !summary.Contains("More detail.", StringComparison.Ordinal)
                && summary.Contains("**Issue #22 — CLI robustness:**\n\n#### Bug Fixes\n- **cli:** handle empty log (bbb2222)", StringComparison.Ordinal)
                && summary.EndsWith("**Other changes:**\n\n#### Chores\n- tidy (ccc3333)", StringComparison.Ordinal),
            "summary: several issues use the template heading and the first Objective paragraph");
        summary = BuildSummary([ParseCommit("aaa1111", "feat(core): parse damage events", "Closes #21"), ParseCommit("bbb2222", "fix(cli): handle empty log", "Refs #22")], issues);
        Check(!summary.Contains("Other changes", StringComparison.Ordinal) && summary.EndsWith("- **cli:** handle empty log (bbb2222)", StringComparison.Ordinal), "summary: no other block without orphans");
        Check(
            BuildSummary([ParseCommit("aaa1111", "feat(core): parse damage events", "Closes #21")], new() { [21] = issues[21] }) == "Parse combat damage lines.\n\n#### Features\n- **core:** parse damage events (aaa1111)",
            "summary: one issue has no heading");
        Check(BuildSummary([ParseCommit("b1", "fix(cli): x", "Refs #22")], new() { [22] = issues[22] }) == "#### Bug Fixes\n- **cli:** x (b1)", "summary: one issue without Objective is commits only");
        Check(BuildSummary([ParseCommit("c1", "chore: tidy", "")], []) == "#### Chores\n- tidy (c1)", "summary: no issue is commits only");

        // --- replace_markers ---
        Check(ReplaceMarkers($"x\n{Start}\nOLD\n{End}\ny", "NEW") == $"x\n{Start}\nNEW\n{End}\ny", "markers: replaced between the markers only");
        Check(ReplaceMarkers("plain body", "NEW") == "plain body", "markers: body without markers unchanged");

        // --- fetch, through a fake gh that answers a fixed list of calls in order ---
        string[] commitsArgv = ["gh", "api", "--paginate", $"repos/{Repo}/pulls/74/commits", "--jq", CommitsJq];
        static string CommitLines(params (string Sha, string Message)[] records) =>
            string.Join('\n', records.Select(record => new JsonArray(record.Sha, record.Message).ToJsonString())) + "\n\n";

        (Gh Gh, Func<bool> Exhausted, List<string?> Inputs) Fake(params (string[] Argv, string Output)[] calls)
        {
            Queue<(string[] Argv, string Output)> queue = new(calls);
            List<string?> inputs = [];
            string Answer(string[] argv, string? input)
            {
                inputs.Add(input);
                if (queue.Count == 0 || !queue.Peek().Argv.SequenceEqual(argv))
                {
                    failures.Add($"unexpected gh call: {string.Join(' ', argv)}");
                    return "{}";
                }

                return queue.Dequeue().Output;
            }

            return (Answer, () => queue.Count == 0, inputs);
        }

        var fake = Fake((commitsArgv, CommitLines(("aaa1111", "feat(core): x\n\nCloses #21"), ("bbb2222", "chore: y"))));
        List<Commit> fetched = FetchCommits(fake.Gh, Repo, 74);
        Check(fetched.Select(commit => commit.Sha).SequenceEqual(["aaa1111", "bbb2222"]) && fetched[0].Issues.SequenceEqual([21]) && fake.Exhausted(), "fetch: one commit per line");

        const string IssueBody = "### Objective\n\nDo the thing.\n\n### Acceptance criteria\n\n- [ ] done\n";
        fake = Fake((["gh", "api", $"repos/{Repo}/issues/21"], new JsonObject { ["title"] = "T", ["body"] = IssueBody }.ToJsonString()));
        Check(FetchIssue(fake.Gh, Repo, 21) == new IssueInfo(21, "T", "Do the thing."), "fetch: issue title and sections");
        fake = Fake((["gh", "api", $"repos/{Repo}/issues/21"], """{"title": "T", "body": null}"""));
        Check(FetchIssue(fake.Gh, Repo, 21) == new IssueInfo(21, "T", ""), "fetch: issue with null body");

        // --- handle ---
        const string RiskBody = "### Objective\n\nDo the thing.\n\n### Risk\n\nlow\n";
        string pullBody = $"# Pull Request\n\n## Summary\n\n{Start}\n- stale\n{End}\n\n## Linked issues\n\nCloses #21\n";
        (string[], string)[] reads =
        [
            (commitsArgv, CommitLines(("aaa1111", "feat(core): x\n\nCloses #21"))),
            (["gh", "api", $"repos/{Repo}/issues/21"], new JsonObject { ["title"] = "T", ["body"] = RiskBody }.ToJsonString()),
        ];

        StringWriter output = new();
        fake = Fake([.. reads, (["gh", "api", $"repos/{Repo}/pulls/74"], new JsonObject { ["body"] = pullBody }.ToJsonString()), (["gh", "pr", "edit", "74", "--repo", Repo, "--body-file", "-"], "")]);
        int code = Handle(Repo, 74, dryRun: false, fake.Gh, output);
        string? written = fake.Inputs[^1];
        Check(
            code == 0 && fake.Exhausted() && written is not null
                && written.Contains($"{Start}\nDo the thing.\n\n#### Features\n- **core:** x (aaa1111)\n{End}", StringComparison.Ordinal)
                && written.EndsWith("## Linked issues\n\nCloses #21\n", StringComparison.Ordinal)
                && output.ToString().Contains("Updated PR #74 auto-summary (1 commit, 1 issue)", StringComparison.Ordinal),
            "handle: writes the regenerated body through stdin");

        output = new();
        fake = Fake([.. reads, (["gh", "api", $"repos/{Repo}/pulls/74"], new JsonObject { ["body"] = pullBody }.ToJsonString())]);
        Check(Handle(Repo, 74, dryRun: true, fake.Gh, output) == 0 && fake.Exhausted() && output.ToString().Contains("#### Features", StringComparison.Ordinal), "handle: dry run prints and does not edit");

        output = new();
        string current = $"{Start}\nDo the thing.\n\n#### Features\n- **core:** x (aaa1111)\n{End}";
        fake = Fake([.. reads, (["gh", "api", $"repos/{Repo}/pulls/74"], new JsonObject { ["body"] = current }.ToJsonString())]);
        Check(Handle(Repo, 74, dryRun: false, fake.Gh, output) == 0 && fake.Exhausted() && output.ToString().Contains("left untouched", StringComparison.Ordinal), "handle: unchanged body left alone");

        output = new();
        fake = Fake((commitsArgv, CommitLines(("c1", "chore: tidy"))), (["gh", "api", $"repos/{Repo}/pulls/74"], """{"body": null}"""));
        Check(Handle(Repo, 74, dryRun: false, fake.Gh, output) == 0 && fake.Exhausted() && output.ToString().Contains("left untouched", StringComparison.Ordinal), "handle: body without markers left alone");

        StringWriter error = new();
        Check(Run(["--repo", Repo], Fake().Gh, TextWriter.Null, error) == 2 && error.ToString().Contains("--pr", StringComparison.Ordinal), "main: --pr is required");
        Check(Run(["--pr", "74", "--repo", Repo], (_, _) => throw new InvalidOperationException("gh failed"), TextWriter.Null, TextWriter.Null) == 2, "main: a failed gh call exits 2");

        if (failures.Count > 0)
        {
            Console.Error.WriteLine("SELFTEST FAILED: " + string.Join(" | ", failures));
            return 1;
        }

        Console.WriteLine("selftest ok");
        return 0;
    }
}
