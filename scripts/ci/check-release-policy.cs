// check-release-policy.cs - fail when the release bump policy in pyproject.toml drifts from commit-types.txt.
//
//   dotnet run scripts/ci/check-release-policy.cs -- <commit-types.txt> <pyproject.toml>
//   dotnet run scripts/ci/check-release-policy.cs -- --selftest
//
// commit-types.txt (vendored from paragon-stats/github-actions into scripts/git) names every Conventional
// Commit type and the bump it triggers; the commit-message hook and the commitlint workflow read it directly.
// pyproject.toml restates it in [tool.semantic_release.commit_parser_options] for the release tooling, so
// allowed_tags, minor_tags and patch_tags must each equal the fact-set's list.
// Exit codes: 0 match, 1 drift (one H-REL-001 line each), 2 unreadable fact-set (E80), policy missing a
// list (E81) or wrong arguments.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Text.RegularExpressions;

if (args.Contains("--selftest"))
{
    return SelfTest();
}

return Check(args, Console.Out, Console.Error);

static int Check(string[] args, TextWriter output, TextWriter error)
{
    if (args.Length != 2)
    {
        error.WriteLine("usage: <commit-types.txt> <pyproject.toml> | --selftest");
        return 2;
    }

    (string factSet, string pyproject) = (args[0], args[1]);
    Dictionary<string, string> facts;
    try
    {
        facts = LoadFactSet(factSet);
    }
    catch (FormatException exception)
    {
        error.WriteLine($"{factSet}:1:1: error: E80: fact-set unreadable: {exception.Message}; fix the row in commit-types.txt");
        return 2;
    }

    Dictionary<string, string[]> wanted = new()
    {
        ["allowed_tags"] = [.. facts.Keys],
        ["minor_tags"] = [.. facts.Where(fact => fact.Value == "minor").Select(fact => fact.Key)],
        ["patch_tags"] = [.. facts.Where(fact => fact.Value == "patch").Select(fact => fact.Key)],
    };
    string section = ParserOptions(File.ReadAllText(pyproject));
    List<string> errors = [];
    foreach ((string key, string[] want) in wanted)
    {
        Match list = Regex.Match(section, $@"(?m)^\s*{key}\s*=\s*\[(?<items>[^\]]*)\]");
        if (!list.Success)
        {
            error.WriteLine($"{pyproject}:1:1: error: E81: release policy incomplete: no {key}; declare allowed_tags, minor_tags and patch_tags under [tool.semantic_release.commit_parser_options]");
            return 2;
        }

        string[] got = [.. Regex.Matches(list.Groups["items"].Value, "\"([^\"]*)\"").Select(item => item.Groups[1].Value)];
        string[] missing = [.. want.Except(got).Order(StringComparer.Ordinal)];
        string[] extra = [.. got.Except(want).Order(StringComparer.Ordinal)];
        if (missing.Length + extra.Length > 0)
        {
            string detail = string.Join("; ", new[] { missing.Length > 0 ? $"missing [{string.Join(", ", missing)}]" : "", extra.Length > 0 ? $"extra [{string.Join(", ", extra)}]" : "" }.Where(part => part.Length > 0));
            errors.Add($"{pyproject}:1:1: error: H-REL-001: {key} drifted from commit-types.txt ({detail}); copy the fact-set's list");
        }
    }

    foreach (string line in errors)
    {
        error.WriteLine(line);
    }

    if (errors.Count > 0)
    {
        return 1;
    }

    output.WriteLine($"release policy matches {Path.GetFileName(factSet)} ({facts.Count} types)");
    return 0;
}

// {type: bump}; '#' starts a comment and blank lines are skipped. A malformed row is reported against the fact-set, never as drift.
static Dictionary<string, string> LoadFactSet(string path)
{
    Dictionary<string, string> facts = new(StringComparer.Ordinal);
    foreach (string line in File.ReadLines(path))
    {
        string[] cols = line.Split('#', 2)[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (cols.Length == 0)
        {
            continue;
        }

        if (cols.Length != 2)
        {
            throw new FormatException($"row '{cols[0]}' must be '<type> <bump>'");
        }

        if (cols[1] is not ("minor" or "patch" or "none"))
        {
            throw new FormatException($"type '{cols[0]}' has bump '{cols[1]}', expected minor, none or patch");
        }

        if (!facts.TryAdd(cols[0], cols[1]))
        {
            throw new FormatException($"type '{cols[0]}' is listed twice");
        }
    }

    return facts;
}

// The body of [tool.semantic_release.commit_parser_options], comments dropped, up to the next table header.
// shortcut: a line-based read, not a TOML parser; the tag lists hold plain quoted words and no '#' or ']'.
static string ParserOptions(string toml)
{
    IEnumerable<string> lines = toml.ReplaceLineEndings("\n").Split('\n')
        .SkipWhile(line => line.Trim() != "[tool.semantic_release.commit_parser_options]")
        .Skip(1)
        .TakeWhile(line => !line.TrimStart().StartsWith('['))
        .Select(line => line.Split('#', 2)[0]);
    return string.Join('\n', lines);
}

static int SelfTest()
{
    List<string> failures = [];
    void Expect(bool condition, string name)
    {
        if (!condition)
        {
            failures.Add(name);
        }
    }

    const string Types = "# comment\n\nfeat minor\nfix patch  # trailing comment\nsecurity patch\ndocs none\n";
    const string Matching = "[tool.semantic_release]\nx = 1\n\n[tool.semantic_release.commit_parser_options]\n# a comment\nallowed_tags = [\n    \"feat\", \"fix\",\n    \"security\", \"docs\",\n]\nminor_tags = [\"feat\"]\npatch_tags = [\"fix\", \"security\"]\n\n[tool.other]\nminor_tags = [\"ignored\"]\n";

    string root = Directory.CreateTempSubdirectory("check-release-policy-").FullName;
    (int Code, string Out, string Err) Run(string types, string pyproject)
    {
        string typesPath = Path.Join(root, "commit-types.txt");
        string pyprojectPath = Path.Join(root, "pyproject.toml");
        File.WriteAllText(typesPath, types);
        File.WriteAllText(pyprojectPath, pyproject);
        StringWriter output = new();
        StringWriter error = new();
        int code = Check([typesPath, pyprojectPath], output, error);
        return (code, output.ToString(), error.ToString());
    }

    try
    {
        var match = Run(Types, Matching);
        Expect(match.Code == 0 && match.Out.Contains("matches commit-types.txt (4 types)"), "matching policy: exit 0");

        // allowed_tags lost security; minor gained fix; patch lost fix.
        var drift = Run(Types, Matching.Replace("\"security\", \"docs\"", "\"docs\"").Replace("minor_tags = [\"feat\"]", "minor_tags = [\"feat\", \"fix\"]").Replace("patch_tags = [\"fix\", \"security\"]", "patch_tags = [\"security\"]"));
        Expect(drift.Code == 1, "drift: exit 1");
        Expect(drift.Err.Split('\n').Count(line => line.Contains("H-REL-001")) == 3, "drift: one line per list");
        Expect(drift.Err.Contains("allowed_tags drifted from commit-types.txt (missing [security])"), "drift: allowed_tags named");
        Expect(drift.Err.Contains("minor_tags drifted from commit-types.txt (extra [fix])"), "drift: minor_tags named");
        Expect(drift.Err.Contains("patch_tags drifted from commit-types.txt (missing [fix])"), "drift: patch_tags named");

        var order = Run(Types, Matching.Replace("patch_tags = [\"fix\", \"security\"]", "patch_tags = [\"security\", \"fix\"]"));
        Expect(order.Code == 0, "order does not matter");

        foreach ((string types, string name) in new[] { ("feat\n", "row without a bump"), ("feat major\n", "unknown bump"), ("feat minor\nfeat patch\n", "repeated type") })
        {
            var bad = Run(types, Matching);
            Expect(bad.Code == 2 && bad.Err.Contains("E80"), $"fact-set {name}: E80");
        }

        var noSection = Run(Types, "[tool.semantic_release]\nx = 1\n");
        Expect(noSection.Code == 2 && noSection.Err.Contains("E81"), "no section: E81");
        var noKey = Run(Types, Matching.Replace("patch_tags = [\"fix\", \"security\"]", ""));
        Expect(noKey.Code == 2 && noKey.Err.Contains("E81") && noKey.Err.Contains("patch_tags"), "missing key: E81 names it");

        Expect(Check(["only-one"], TextWriter.Null, TextWriter.Null) == 2, "one argument: exit 2");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }

    if (failures.Count > 0)
    {
        Console.Error.WriteLine("SELFTEST FAILED: " + string.Join(" | ", failures));
        return 1;
    }

    Console.WriteLine("selftest ok");
    return 0;
}
