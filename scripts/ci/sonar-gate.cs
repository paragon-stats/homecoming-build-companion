// sonar-gate.cs - fail the sonar job while a gated rule still has open findings.
//
//   dotnet run scripts/ci/sonar-gate.cs -- --rules-dir DIR --sarif-root DIR [--pull-request N] [--base REF] [--repo-root DIR]
//   dotnet run scripts/ci/sonar-gate.cs -- --selftest
//
// SonarQube Cloud's pull-request analysis reports only issues a pull request introduces, so a pull request
// that leaves an old finding in place still shows zero. This gate reads the rules in
// scripts/ci/sonar_gate_rules/*.txt (one "<rule-key> <path-prefix>" per line, '#' starts a comment) and
// checks them against:
//   - the analyzer results the scanner writes to .sonarqube/out/*/Issues.json during the build
//     (csharpsquid rules; every analysed file, old or new, so this is a full proof);
//   - on a pull request, the findings open on main (each must sit in a file, or for most rules on a line,
//     the pull request changes) and, for rules outside C#, the pull request's own analysis;
//   - on a push to main, the findings open on main.
// --base defaults to origin/main; --repo-root defaults to the working directory.
// Exit codes: 0 pass, 1 findings, missing scanner output or a failed API or git call, 2 bad arguments or a
// malformed rules file.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false
// The gate runs after the scanner's end step in the same workspace; excluded, this build adds no project
// the gate would then expect results for.
#:property SonarQubeExclude=true

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

if (args.Contains("--selftest"))
{
    return SelfTest();
}

using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(60) };
return Gate(args, Gates.HttpFetch(client), Gates.GitDiff, Console.Out, Console.Error);

static int Gate(string[] args, Func<string, JsonNode> fetch, Func<string, string> gitDiff, TextWriter output, TextWriter error)
{
    Options? options = Options.Parse(args, Directory.GetCurrentDirectory().Replace('\\', '/'), error);
    if (options is null)
    {
        return 2;
    }

    List<Entry> entries;
    try
    {
        entries = Gates.ParseRules(options.RulesDir);
    }
    catch (RuleFileException exception)
    {
        error.WriteLine(exception.Message);
        return 2;
    }

    Scan scan;
    try
    {
        scan = Gates.ReadSarif(options.SarifRoot, options.RepoRoot);
    }
    catch (ScannerOutputException exception)
    {
        output.WriteLine($"FAIL {exception.Message}");
        return 1;
    }

    output.WriteLine($"read {scan.Files} Issues.json files for {scan.Projects} expected projects: {scan.Total} results, {scan.Findings.Count} csharpsquid");
    Dictionary<Entry, List<string>> summary;
    List<string> failures;
    try
    {
        (summary, failures) = Gates.Evaluate(options, entries, scan.Findings, fetch, gitDiff);
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
    {
        output.WriteLine($"FAIL {exception.Message}");
        return 1;
    }

    foreach ((Entry entry, List<string> counts) in summary)
    {
        output.WriteLine($"{entry.Rule} {entry.Prefix} {string.Join(' ', counts)}");
    }

    foreach (string failure in failures)
    {
        output.WriteLine($"FAIL {failure}");
    }

    return failures.Count > 0 ? 1 : 0;
}

static int SelfTest()
{
    const string Root = "D:/a/repo/repo";
    const string Rule = "csharpsquid:S3776";
    const string Other = "python:S1192";
    const string Diff = """
        diff --git a/src/A.cs b/src/A.cs
        index 1..2 100644
        --- a/src/A.cs
        +++ b/src/A.cs
        @@ -10,3 +10,2 @@ class A
        --- removed line that looks like a header
        @@ -20 +19 @@
        @@ -30,0 +30,4 @@
        diff --git a/src/New.cs b/src/New.cs
        new file mode 100644
        --- /dev/null
        +++ b/src/New.cs
        @@ -0,0 +1,5 @@
        diff --git a/src/Gone.cs b/src/Gone.cs
        deleted file mode 100644
        --- a/src/Gone.cs
        +++ /dev/null
        @@ -1,2 +0,0 @@

        """;

    List<string> failures = [];
    void Check(bool passed, string name)
    {
        if (!passed)
        {
            failures.Add(name);
        }
    }

    void Throws<T>(Action action, string fragment, string name)
        where T : Exception
    {
        try
        {
            action();
            failures.Add($"{name} (no exception)");
        }
        catch (T exception) when (exception.Message.Contains(fragment, StringComparison.Ordinal))
        {
        }
        catch (Exception exception)
        {
            failures.Add($"{name} ({exception.GetType().Name}: {exception.Message})");
        }
    }

    string temp = Path.Join(Path.GetTempPath(), $"sonar-gate-{Guid.NewGuid():N}");
    int scratch = 0;
    string Fresh() => Directory.CreateDirectory(Path.Join(temp, (scratch++).ToString(System.Globalization.CultureInfo.InvariantCulture))).FullName;

    static JsonObject V1(string rule, string path, int line = 1) => new()
    {
        ["ruleId"] = rule,
        ["locations"] = new JsonArray(new JsonObject { ["resultFile"] = new JsonObject { ["uri"] = $"file:///D:/a/repo/repo/{path}", ["region"] = new JsonObject { ["startLine"] = line } } }),
    };

    static JsonObject V2(string rule, string uri, int line = 1) => new()
    {
        ["ruleId"] = rule,
        ["locations"] = new JsonArray(new JsonObject { ["physicalLocation"] = new JsonObject { ["artifactLocation"] = new JsonObject { ["uri"] = uri }, ["region"] = new JsonObject { ["startLine"] = line } } }),
    };

    static void Issues(string directory, string name, params JsonObject[] results)
    {
        string target = Directory.CreateDirectory(Path.Join(directory, name)).FullName;
        File.WriteAllText(Path.Join(target, "ProjectInfo.xml"), "<ProjectInfo><IsExcluded>false</IsExcluded></ProjectInfo>");
        JsonObject document = new() { ["runs"] = new JsonArray(new JsonObject { ["results"] = new JsonArray([.. results]) }) };
        File.WriteAllText(Path.Join(target, "Issues.json"), document.ToJsonString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    static string[] Arguments(string directory, string rules, params string[] extra)
    {
        string rulesDir = Directory.CreateDirectory(Path.Join(directory, "rules")).FullName;
        File.WriteAllText(Path.Join(rulesDir, "a.txt"), rules);
        return ["--rules-dir", rulesDir, "--sarif-root", Path.Join(directory, "out"), "--repo-root", Root, .. extra];
    }

    static JsonObject Issue(string rule, string path, int line = 0)
    {
        JsonObject issue = new() { ["rule"] = rule, ["component"] = $"{Gates.ProjectKey}:{path}" };
        if (line != 0)
        {
            issue["line"] = line;
        }

        return issue;
    }

    static (Func<string, JsonNode> Fetch, List<string> Calls) Api(JsonObject[] mainIssues, JsonObject[]? pullIssues = null, int pageSize = 500)
    {
        List<string> calls = [];
        JsonNode Fetch(string url)
        {
            calls.Add(url);
            JsonObject[] issues = url.Contains("pullRequest=", StringComparison.Ordinal) && pullIssues is not null ? pullIssues : mainIssues;
            int page = int.Parse(url.Split("&p=")[1].Split('&')[0], System.Globalization.CultureInfo.InvariantCulture);
            return new JsonObject
            {
                ["total"] = issues.Length,
                ["issues"] = new JsonArray([.. issues.Skip((page - 1) * pageSize).Take(pageSize).Select(issue => issue.DeepClone())]),
            };
        }

        return (Fetch, calls);
    }

    static string NoDiff(string baseRef) => throw new NotSupportedException("git diff must not run");

    static (int Code, string Output, string Error) Run(string[] arguments, Func<string, JsonNode> fetch, Func<string, string> gitDiff)
    {
        StringWriter output = new();
        StringWriter error = new();
        int code = Gate(arguments, fetch, gitDiff, output, error);
        return (code, output.ToString(), error.ToString());
    }

    try
    {
        // --- rules parser ---
        string dir = Fresh();
        File.WriteAllText(Path.Join(dir, "b.txt"), "# note\n\ncsharpsquid:S1 src/A/  # trailing\n  python:S2   scripts/x.py\n");
        File.WriteAllText(Path.Join(dir, "ignored.md"), "not a rule\n");
        Check(Gates.ParseRules(dir).SequenceEqual([new Entry("csharpsquid:S1", "src/A/"), new Entry("python:S2", "scripts/x.py")]), "rules: entries, comments and blanks");

        foreach (string line in new[] { "csharpsquid:S1", "csharpsquid:S1 a b", "S1 src/" })
        {
            dir = Fresh();
            File.WriteAllText(Path.Join(dir, "bad.txt"), $"# ok\n{line}\n");
            Throws<RuleFileException>(() => Gates.ParseRules(dir), "bad.txt:2", $"rules: malformed '{line}' names file and line");
        }

        // --- scanner output ---
        dir = Fresh();
        Issues(dir, "1", V1("S3776", "src/A.cs", 7), V1("S3776", "src/A.cs", 7), V1("MA0003", "src/A.cs"), new JsonObject { ["ruleId"] = "S107" });
        Issues(dir, "2", V2("S1192", "src/B%20x.cs", 3), V2("S1192", "file:///E:/elsewhere/C.cs"));
        Scan scan = Gates.ReadSarif(dir, Root);
        Check((scan.Files, scan.Projects, scan.Total) == (2, 2, 6), "sarif: counts");
        Check(
            scan.Findings.SetEquals([new Finding(Rule, "src/A.cs", 7), new Finding("csharpsquid:S107", "", 0), new Finding("csharpsquid:S1192", "src/B x.cs", 3), new Finding("csharpsquid:S1192", "E:/elsewhere/C.cs", 1)]),
            "sarif: 1.0 and 2.1 results map to repository-relative Sonar rules");

        dir = Fresh();
        Throws<ScannerOutputException>(() => Gates.ReadSarif(dir, Root), "missing", "sarif: no Issues.json fails");

        foreach (string content in new[] { "", "{", "[]", """{"runs": []}""" })
        {
            dir = Fresh();
            Issues(dir, "1");
            File.WriteAllText(Path.Join(dir, "1", "Issues.json"), content);
            Throws<ScannerOutputException>(() => Gates.ReadSarif(dir, Root), "", $"sarif: malformed '{content}' fails");
        }

        dir = Fresh();
        Issues(dir, "1");
        Issues(dir, "2");
        scan = Gates.ReadSarif(dir, Root);
        Check((scan.Files, scan.Projects, scan.Total, scan.Findings.Count) == (2, 2, 0, 0), "sarif: every file present with no results passes");

        dir = Fresh();
        Issues(dir, "1", V1("S1", "src/A.cs"));
        Directory.CreateDirectory(Path.Join(dir, "2"));
        File.WriteAllText(Path.Join(dir, "2", "ProjectInfo.xml"), "<ProjectInfo/>");
        Throws<ScannerOutputException>(() => Gates.ReadSarif(dir, Root), "missing for 1 of 2 projects: ['2']", "sarif: a project without its Issues.json fails");

        dir = Fresh();
        Issues(dir, "1");
        Directory.CreateDirectory(Path.Join(dir, "2"));
        File.WriteAllText(Path.Join(dir, "2", "ProjectInfo.xml"), "<IsExcluded>true</IsExcluded>");
        Check(Gates.ReadSarif(dir, Root).Projects == 1, "sarif: an excluded project needs no Issues.json");

        dir = Fresh();
        Directory.CreateDirectory(Path.Join(dir, "1"));
        File.WriteAllText(Path.Join(dir, "1", "Issues.json"), "{}");
        Throws<ScannerOutputException>(() => Gates.ReadSarif(dir, Root), "ProjectInfo", "sarif: Issues.json without any ProjectInfo.xml fails");

        dir = Fresh();
        Issues(dir, "1");
        Directory.CreateDirectory(Path.Join(dir, "1", "sub", "Issues.json"));
        Throws<ScannerOutputException>(() => Gates.ReadSarif(dir, Root), "unreadable", "sarif: an Issues.json that cannot be read fails");

        // --- diff parser ---
        DiffInfo diff = Gates.ParseDiff(Diff);
        Check(diff.Files.SetEquals(["src/A.cs", "src/New.cs", "src/Gone.cs"]), "diff: files");
        Check(diff.OldLines["src/A.cs"].SetEquals([10, 11, 12, 20]), "diff: multi-line, single-line and empty hunks");
        Check(diff.OldLines["src/Gone.cs"].SetEquals([1, 2]), "diff: deleted file");
        Check(!diff.OldLines.ContainsKey("src/New.cs"), "diff: added file has no old lines");

        // --- API paging ---
        (Func<string, JsonNode> fetch, List<string> calls) = Api([.. Enumerable.Range(0, 5).Select(n => Issue("python:S1", $"f{n}.py", n + 1))], pageSize: 2);
        List<Finding> found = Gates.FetchIssues(fetch, ["python:S1", "python:S2"], [new("branch", "main")]);
        Check(found.SequenceEqual(Enumerable.Range(0, 5).Select(n => new Finding("python:S1", $"f{n}.py", n + 1))), "fetch: every issue read");
        Check(calls.Count == 3, "fetch: three pages");
        Check(calls[0].Contains("rules=python%3AS1%2Cpython%3AS2", StringComparison.Ordinal) && calls[0].Contains("branch=main", StringComparison.Ordinal), "fetch: rules and branch in the query");
        Check(Gates.FetchIssues(_ => JsonNode.Parse("""{"total": 9, "issues": []}""")!, ["python:S1"], []).Count == 0, "fetch: stops on an empty page");

        // --- the gate ---
        dir = Fresh();
        Issues(Path.Join(dir, "out"), "1", V1("S1", "src/A.cs"));
        (int code, string output, string error) = Run(Arguments(dir, "# nothing\n"), Api([]).Fetch, NoDiff);
        Check(code == 0 && output.Contains("read 1 Issues.json files for 1 expected projects: 1 results, 1 csharpsquid", StringComparison.Ordinal), "gate: no entries passes after reading the scanner output");

        dir = Fresh();
        (code, _, error) = Run(Arguments(dir, "oops\n"), Api([]).Fetch, NoDiff);
        Check(code == 2 && error.Contains("a.txt:1", StringComparison.Ordinal), "gate: a malformed rules file exits 2");

        dir = Fresh();
        (code, output, _) = Run(Arguments(dir, "# nothing\n"), Api([]).Fetch, NoDiff);
        Check(code == 1 && output.Contains("FAIL scanner output missing", StringComparison.Ordinal), "gate: missing scanner output fails closed");

        dir = Fresh();
        Issues(Path.Join(dir, "out"), "1", V1("S3776", "src/A/x.cs", 9), V1("S3776", "src/B/y.cs", 2));
        (code, output, _) = Run(Arguments(dir, $"{Rule} src/A/\n"), Api([]).Fetch, NoDiff);
        Check(
            code == 1 && output.Contains("FAIL scanner: csharpsquid:S3776 src/A/x.cs:9", StringComparison.Ordinal) && !output.Contains("y.cs", StringComparison.Ordinal)
                && output.Contains($"{Rule} src/A/ scanner=1 main=0", StringComparison.Ordinal),
            "gate: a gated C# result fails and is listed");

        dir = Fresh();
        Issues(Path.Join(dir, "out"), "1", V1("S3776", "src/B/y.cs"));
        Check(Run(Arguments(dir, $"{Rule} src/A/\n"), Api([]).Fetch, NoDiff).Code == 0, "gate: a clean C# rule passes on main");

        dir = Fresh();
        Issues(Path.Join(dir, "out"), "1", V1("S1", "src/A.cs"));
        (code, output, _) = Run(Arguments(dir, $"{Other} scripts/a.py\n"), Api([Issue(Other, "scripts/a.py", 4), Issue(Other, "scripts/other/b.py", 1)]).Fetch, NoDiff);
        Check(code == 1 && output.Contains("FAIL main: python:S1192 scripts/a.py:4", StringComparison.Ordinal), "gate: main findings under a prefix fail a push to main");

        dir = Fresh();
        Issues(Path.Join(dir, "out"), "1", V1("S1", "src/A.cs"));
        (fetch, calls) = Api([Issue(Other, "src/A.cs", 20), Issue(Other, "src/A.cs", 11), Issue("python:S9", "src/Gone.cs"), Issue(Other, "x", 5)], []);
        (code, _, _) = Run(Arguments(dir, $"{Other} src/A.cs\npython:S9 src/Gone.cs\n", "--pull-request", "7"), fetch, _ => Diff);
        Check(code == 0 && calls.Exists(url => url.Contains("pullRequest=7", StringComparison.Ordinal)), "gate: a pull request passes with findings on changed lines and a clean analysis");

        dir = Fresh();
        Issues(Path.Join(dir, "out"), "1", V1("S1", "src/A.cs"));
        JsonObject[] mainIssues =
        [
            Issue(Other, "src/A.cs", 99), Issue(Other, "src/Untouched.cs", 1), Issue("python:S3776", "src/A.cs", 99), Issue("python:S3776", "src/Untouched.cs", 1),
            Issue(Other, "src/A.cs"), Issue(Other, "src/Untouched.cs"), Issue(Rule, "src/A.cs", 99), Issue(Rule, "src/Untouched.cs", 1),
        ];
        (code, output, _) = Run(Arguments(dir, $"{Other} src/\npython:S3776 src/\n{Rule} src/\n", "--pull-request", "7"), Api(mainIssues, [Issue(Other, "src/A.cs", 5)]).Fetch, _ => Diff);
        string[] expected =
        [
            "FAIL baseline: python:S1192 src/A.cs:99", "FAIL baseline: python:S1192 src/Untouched.cs:1", "FAIL baseline: python:S1192 src/Untouched.cs:0",
            "FAIL baseline: python:S3776 src/Untouched.cs:1", "FAIL baseline: csharpsquid:S3776 src/Untouched.cs:1", "FAIL pull-request: python:S1192 src/A.cs:5",
        ];
        string[] absent = ["baseline: python:S3776 src/A.cs", "baseline: csharpsquid:S3776 src/A.cs", "FAIL baseline: python:S1192 src/A.cs:0"];
        Check(
            code == 1 && expected.All(text => output.Contains(text, StringComparison.Ordinal)) && !absent.Any(text => output.Contains(text, StringComparison.Ordinal)),
            "gate: a pull request fails on untouched findings and on its own analysis");

        Check(Gates.GitDiffArguments("origin/main").SequenceEqual(["diff", "-U0", "--no-renames", "origin/main...HEAD"]), "git: three-dot diff against the base");

        StubHandler handler = new("""{"issues": []}""");
        using (HttpClient client = new(handler))
        {
            JsonNode body = Gates.HttpFetch(client)("https://example.test/x");
            Check(JsonNode.DeepEquals(body, JsonNode.Parse("""{"issues": []}""")) && handler.Urls.SequenceEqual(["https://example.test/x"]), "http: reads JSON from the URL");
        }

        foreach (string baseRef in new[] { "--output=x", "é/x", "origin/é" })
        {
            dir = Fresh();
            Check(Run(Arguments(dir, "# x\n", "--base", baseRef), Api([]).Fetch, NoDiff).Code == 2, $"arguments: base '{baseRef}' refused");
        }
    }
    finally
    {
        Directory.Delete(temp, recursive: true);
    }

    if (failures.Count > 0)
    {
        Console.Error.WriteLine("SELFTEST FAILED: " + string.Join(" | ", failures));
        return 1;
    }

    Console.WriteLine("selftest ok");
    return 0;
}

internal sealed record Entry(string Rule, string Prefix);

internal sealed record Finding(string Rule, string Path, int Line);

internal sealed record Scan(int Files, int Projects, int Total, HashSet<Finding> Findings);

internal sealed record DiffInfo(HashSet<string> Files, Dictionary<string, HashSet<int>> OldLines);

internal sealed class RuleFileException(string message) : Exception(message);

internal sealed class ScannerOutputException(string message, Exception? inner = null) : Exception(message, inner);

internal sealed record Options(string RulesDir, string SarifRoot, int? PullRequest, string Base, string RepoRoot)
{
    private static readonly Regex Ref = new(@"^[A-Za-z0-9_][A-Za-z0-9_./-]*$");

    public static Options? Parse(string[] args, string defaultRoot, TextWriter error)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal) { ["--base"] = "origin/main", ["--repo-root"] = defaultRoot };
        string[] known = ["--rules-dir", "--sarif-root", "--pull-request", "--base", "--repo-root"];
        for (int i = 0; i < args.Length; i++)
        {
            if (!known.Contains(args[i]) || i + 1 >= args.Length)
            {
                error.WriteLine($"usage: --rules-dir DIR --sarif-root DIR [--pull-request N] [--base REF] [--repo-root DIR]; bad argument '{args[i]}'");
                return null;
            }

            values[args[i]] = args[++i];
        }

        if (!values.ContainsKey("--rules-dir") || !values.ContainsKey("--sarif-root"))
        {
            error.WriteLine("--rules-dir and --sarif-root are required");
            return null;
        }

        int? pullRequest = null;
        if (values.TryGetValue("--pull-request", out string? number))
        {
            if (!int.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsed))
            {
                error.WriteLine($"--pull-request must be a number, got '{number}'");
                return null;
            }

            pullRequest = parsed;
        }

        if (!Ref.IsMatch(values["--base"]))
        {
            error.WriteLine($"--base must be a plain git ref, got '{values["--base"]}'");
            return null;
        }

        return new Options(values["--rules-dir"], values["--sarif-root"], pullRequest, values["--base"], values["--repo-root"]);
    }
}

internal sealed class StubHandler(string body) : HttpMessageHandler
{
    public List<string> Urls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Urls.Add(request.RequestUri!.ToString());
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}

internal static class Gates
{
    public const string ProjectKey = "paragon-stats_homecoming-build-companion";
    private const string ApiUrl = "https://sonarcloud.io/api/issues/search";
    private const int PageSize = 500;
    private const string CSharp = "csharpsquid:";
    private static readonly Regex Hunk = new(@"^@@ -(\d+)(?:,(\d+))? \+\d+(?:,\d+)? @@");
    private static readonly Regex SonarCSharpId = new(@"^S\d+$");
    private static readonly Regex Drive = new("^/[A-Za-z]:");
    private static readonly Regex SchemeAndHost = new("^[A-Za-z][A-Za-z0-9+.-]*://[^/]*");

    public static List<Entry> ParseRules(string rulesDir)
    {
        List<Entry> entries = [];
        IEnumerable<string> files = Directory.GetFiles(rulesDir, "*.txt")
            .Where(file => file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal);
        foreach (string file in files)
        {
            string[] lines = File.ReadAllText(file).Split('\n');
            for (int number = 1; number <= lines.Length; number++)
            {
                string raw = lines[number - 1].TrimEnd('\r');
                string text = raw.Split('#', 2)[0].Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || !parts[0].Contains(':', StringComparison.Ordinal))
                {
                    throw new RuleFileException($"{file}:{number}: expected '<rule-key> <path-prefix>', got '{raw}'");
                }

                entries.Add(new Entry(parts[0], parts[1]));
            }
        }

        return entries;
    }

    public static Scan ReadSarif(string sarifRoot, string repoRoot)
    {
        string[] files = Directory.Exists(sarifRoot)
            ? [.. Directory.EnumerateFileSystemEntries(sarifRoot, "Issues.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal)]
            : [];
        if (files.Length == 0)
        {
            throw new ScannerOutputException($"scanner output missing: no Issues.json under {sarifRoot}");
        }

        List<string> projects =
        [
            .. Directory.EnumerateFiles(sarifRoot, "ProjectInfo.xml", SearchOption.AllDirectories)
                .Where(info => !ReadText(info).Contains("<IsExcluded>true</IsExcluded>", StringComparison.Ordinal))
                .Select(info => Path.GetDirectoryName(info)!)
                .Order(StringComparer.Ordinal),
        ];
        if (projects.Count == 0)
        {
            throw new ScannerOutputException($"no ProjectInfo.xml under {sarifRoot}: cannot tell which projects were analysed");
        }

        List<string> absent = [.. projects.Where(project => !File.Exists(Path.Join(project, "Issues.json"))).Select(project => Path.GetFileName(project))];
        if (absent.Count > 0)
        {
            throw new ScannerOutputException($"Issues.json missing for {absent.Count} of {projects.Count} projects: [{string.Join(", ", absent.Select(name => $"'{name}'"))}]");
        }

        int total = 0;
        HashSet<Finding> findings = [];
        foreach (string file in files)
        {
            foreach (JsonNode result in Results(file))
            {
                total++;
                string rule = (string?)result["ruleId"] ?? "";
                if (SonarCSharpId.IsMatch(rule))
                {
                    (string uri, int line) = Location(result);
                    findings.Add(new Finding(CSharp + rule, RepoPath(uri, repoRoot), line));
                }
            }
        }

        return new Scan(files.Length, projects.Count, total, findings);
    }

    public static List<Finding> FetchIssues(Func<string, JsonNode> fetch, IReadOnlyList<string> rules, IReadOnlyList<KeyValuePair<string, string>> scope)
    {
        List<Finding> found = [];
        for (int page = 1; ; page++)
        {
            List<KeyValuePair<string, string>> query =
            [
                new("componentKeys", ProjectKey), new("resolved", "false"), new("ps", PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("p", page.ToString(System.Globalization.CultureInfo.InvariantCulture)), new("rules", string.Join(',', rules)), .. scope,
            ];
            JsonNode data = fetch($"{ApiUrl}?{string.Join('&', query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))}");
            JsonArray issues = data["issues"]!.AsArray();
            found.AddRange(issues.Select(issue => new Finding((string)issue!["rule"]!, ((string)issue["component"]!).Split(':', 2)[1], (int?)issue["line"] ?? 0)));
            if (issues.Count == 0 || found.Count >= ((int?)data["total"] ?? 0))
            {
                return found;
            }
        }
    }

    public static DiffInfo ParseDiff(string text)
    {
        HashSet<string> files = [];
        Dictionary<string, HashSet<int>> oldLines = [];
        string current = "";
        bool inHeader = false;
        foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
        {
            Match hunk = Hunk.Match(line);
            if (line.StartsWith("diff --git", StringComparison.Ordinal))
            {
                (inHeader, current) = (true, "");
            }
            else if (inHeader && line.StartsWith("--- ", StringComparison.Ordinal))
            {
                current = DiffName(line);
                files.Add(current);
            }
            else if (inHeader && line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                files.Add(DiffName(line));
            }
            else if (hunk.Success)
            {
                inHeader = false;
                int start = int.Parse(hunk.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                int count = hunk.Groups[2].Success ? int.Parse(hunk.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
                if (!oldLines.TryGetValue(current, out HashSet<int>? lines))
                {
                    oldLines[current] = lines = [];
                }

                lines.UnionWith(Enumerable.Range(start, count));
            }
        }

        files.Remove("");
        return new DiffInfo(files, oldLines);
    }

    public static (Dictionary<Entry, List<string>> Summary, List<string> Failures) Evaluate(
        Options options, List<Entry> entries, HashSet<Finding> csharp, Func<string, JsonNode> fetch, Func<string, string> gitDiff)
    {
        bool pull = options.PullRequest is not null;
        List<string> rules = [.. entries.Select(entry => entry.Rule).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        List<Finding> onMain = entries.Count > 0 ? FetchIssues(fetch, rules, [new("branch", "main")]) : [];
        List<string> other = [.. rules.Where(rule => !rule.StartsWith(CSharp, StringComparison.Ordinal))];
        List<Finding> onPull = pull && other.Count > 0
            ? FetchIssues(fetch, other, [new("pullRequest", options.PullRequest!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))])
            : [];
        DiffInfo diff = pull && entries.Count > 0 ? ParseDiff(gitDiff(options.Base)) : new DiffInfo([], []);
        List<Finding> scanned = [.. csharp.OrderBy(finding => finding.Rule, StringComparer.Ordinal).ThenBy(finding => finding.Path, StringComparer.Ordinal).ThenBy(finding => finding.Line)];

        Dictionary<Entry, List<string>> summary = [];
        foreach (Entry entry in entries)
        {
            summary.TryAdd(entry, []);
        }

        List<string> failures = [];
        void Record(Entry entry, string check, List<Finding> bad)
        {
            summary[entry].Add($"{check}={bad.Count}");
            failures.AddRange(bad.Select(finding => $"{check}: {finding.Rule} {finding.Path}:{finding.Line}"));
        }

        foreach (Entry entry in entries)
        {
            bool inCSharp = entry.Rule.StartsWith(CSharp, StringComparison.Ordinal);
            if (inCSharp)
            {
                Record(entry, "scanner", Hits(entry, scanned));
            }

            if (!pull)
            {
                Record(entry, "main", Hits(entry, onMain));
                continue;
            }

            Record(entry, "baseline", Untouched(Hits(entry, onMain), diff));
            if (!inCSharp)
            {
                Record(entry, "pull-request", Hits(entry, onPull));
            }
        }

        return (summary, failures);
    }

    public static string[] GitDiffArguments(string baseRef) => ["diff", "-U0", "--no-renames", $"{baseRef}...HEAD"];

    public static string GitDiff(string baseRef)
    {
        ProcessStartInfo start = new("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in GitDiffArguments(baseRef))
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("cannot start git");
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', GitDiffArguments(baseRef))} exited {process.ExitCode}: {error.Result.Trim()}");
        }

        return output;
    }

    public static Func<string, JsonNode> HttpFetch(HttpClient client) =>
        url => JsonNode.Parse(client.GetStringAsync(new Uri(url)).GetAwaiter().GetResult())!;

    private static string ReadText(string path) => File.ReadAllText(path, Encoding.UTF8);

    private static IEnumerable<JsonNode> Results(string file)
    {
        JsonNode? document;
        try
        {
            document = JsonNode.Parse(ReadText(file));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new ScannerOutputException($"{file}: unreadable ({exception.Message})", exception);
        }

        if (document is not JsonObject || document["runs"] is not JsonArray { Count: > 0 } runs)
        {
            throw new ScannerOutputException($"{file}: not SARIF (no runs)");
        }

        return runs.SelectMany(run => run?["results"]?.AsArray() ?? []).Select(result => result!);
    }

    private static (string Uri, int Line) Location(JsonNode result)
    {
        JsonNode? first = result["locations"] is JsonArray { Count: > 0 } locations ? locations[0] : null;
        // SARIF 1.0 (Roslyn's default) nests the file under resultFile; 2.1 under physicalLocation.
        JsonNode? place = first?["resultFile"] ?? first?["physicalLocation"];
        string uri = (string?)place?["uri"] ?? (string?)place?["artifactLocation"]?["uri"] ?? "";
        return (uri, (int?)place?["region"]?["startLine"] ?? 0);
    }

    private static string RepoPath(string uri, string root)
    {
        string path = SchemeAndHost.Replace(uri, "");
        int end = path.IndexOfAny(['?', '#']);
        path = Uri.UnescapeDataString(end < 0 ? path : path[..end]);
        if (Drive.IsMatch(path))
        {
            path = path[1..];
        }

        return path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase) ? path[(root.Length + 1)..] : path;
    }

    private static string DiffName(string line)
    {
        string name = line[4..];
        return name == "/dev/null" ? "" : name[2..];
    }

    private static List<Finding> Hits(Entry entry, IEnumerable<Finding> findings) =>
        [.. findings.Where(finding => finding.Rule == entry.Rule && finding.Path.StartsWith(entry.Prefix, StringComparison.Ordinal))];

    private static List<Finding> Untouched(List<Finding> findings, DiffInfo diff) =>
    [
        .. findings.Where(finding =>
        {
            bool fileLevel = finding.Rule.StartsWith(CSharp, StringComparison.Ordinal) || finding.Rule.EndsWith(":S3776", StringComparison.Ordinal) || finding.Line == 0;
            bool touched = diff.Files.Contains(finding.Path) && (fileLevel || (diff.OldLines.TryGetValue(finding.Path, out HashSet<int>? lines) && lines.Contains(finding.Line)));
            return !touched;
        }),
    ];
}
