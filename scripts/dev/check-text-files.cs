// check-text-files.cs - language-neutral hygiene checks on text files, run by the Husky pre-commit group.
//
//   dotnet run scripts/dev/check-text-files.cs                  staged files (the hook)
//   dotnet run scripts/dev/check-text-files.cs -- --all         every tracked file
//   dotnet run scripts/dev/check-text-files.cs -- --files a b   the named files
//
// Checks: trailing whitespace, final newline and line endings as .editorconfig sets them, JSON
// syntax, files over 15 MB, and markdownlint (through npx, with the config Super-Linter reads).
// Files the CI Super-Linter job excludes are skipped: the pattern is read from FILTER_REGEX_EXCLUDE in
// .github/workflows/super-linter.yml, so there is no second list. A .patch file is skipped
// because the whitespace in a unified diff is content. YAML syntax is left to the pre-push
// Super-Linter run.
// shortcut: reads the working-tree file, not the staged blob; upgrade if partial staging matters.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const long MaxBytes = 15L * 1024 * 1024;
const string MarkdownlintPackage = "markdownlint-cli@0.50.0";
const string Workflow = ".github/workflows/super-linter.yml";

Match filterLine = Regex.Match(File.ReadAllText(Workflow), @"^\s+FILTER_REGEX_EXCLUDE: '([^']*)'\s*$", RegexOptions.Multiline);
if (!filterLine.Success)
{
    Console.Error.WriteLine($"{Workflow}:1:1: error: H-TEXT-005: FILTER_REGEX_EXCLUDE not found; the exclusion list is read from this file");
    return 1;
}

Regex excluded = new(filterLine.Groups[1].Value);
List<(Regex Glob, string Key, string Value)> editorRules = ReadEditorConfig(".editorconfig");

int filesAt = Array.IndexOf(args, "--files");
List<string> files = filesAt >= 0
    ? [.. args.Skip(filesAt + 1)]
    : [.. Lines(Git(args.Contains("--all") ? "ls-files" : "diff --cached --name-only --diff-filter=ACMR"))];

List<string> findings = [];
List<string> markdown = [];
foreach (string file in files.Where(File.Exists))
{
    string path = file.Replace('\\', '/');
    if (new FileInfo(path).Length > MaxBytes)
    {
        findings.Add($"{path}:1:1: error: H-TEXT-006: file is over 15 MB; keep it out of git or add it to a data store");
        continue;
    }

    // Super-Linter sees absolute paths, so test the pattern against a rooted one.
    if (excluded.IsMatch("/" + path) || path.EndsWith(".patch", StringComparison.Ordinal))
    {
        continue;
    }

    byte[] bytes = File.ReadAllBytes(path);
    if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0)
    {
        continue;
    }

    string text = new UTF8Encoding(false).GetString(bytes);
    CheckFile(path, text);
    if (path.EndsWith(".md", StringComparison.Ordinal))
    {
        markdown.Add(path);
    }
}

if (markdown.Count > 0)
{
    RunMarkdownlint(markdown);
}

foreach (string finding in findings)
{
    Console.Error.WriteLine(finding);
}

if (findings.Count > 0)
{
    Console.Error.WriteLine($"[FAIL] {findings.Count} text-file finding(s); fix them and stage the files again.");
    return 1;
}

Console.WriteLine("[OK] text files are clean.");
return 0;

void CheckFile(string path, string text)
{
    Dictionary<string, string> rules = [];
    foreach ((Regex glob, string key, string value) in editorRules)
    {
        if (glob.IsMatch(path))
        {
            rules[key] = value;
        }
    }

    if (rules.GetValueOrDefault("trim_trailing_whitespace") == "true")
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Match trailing = Regex.Match(lines[i].TrimEnd('\r'), @"[ \t]+$");
            if (trailing.Success)
            {
                findings.Add($"{path}:{i + 1}:{trailing.Index + 1}: error: H-TEXT-001: trailing whitespace; remove it");
            }
        }
    }

    if (rules.GetValueOrDefault("insert_final_newline") == "true" && text.Length > 0 && !text.EndsWith('\n'))
    {
        findings.Add($"{path}:1:1: error: H-TEXT-002: missing final newline; end the file with one");
    }

    if (rules.GetValueOrDefault("end_of_line") == "lf" && text.Contains('\r'))
    {
        findings.Add($"{path}:{text.Split('\n').TakeWhile(l => !l.Contains('\r')).Count() + 1}:1: error: H-TEXT-003: CR line ending; .editorconfig expects lf");
    }

    if (path.EndsWith(".json", StringComparison.Ordinal))
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            findings.Add($"{path}:{(exception.LineNumber ?? 0) + 1}:1: error: H-TEXT-004: invalid JSON: {exception.Message}");
        }
    }
}

void RunMarkdownlint(List<string> paths)
{
    // npx is npx.cmd on Windows, which only cmd can start without a shell.
    List<string> npx = ["--yes", MarkdownlintPackage, "--config", ".github/linters/.markdownlint.yml", .. paths];
    ProcessStartInfo info = new(OperatingSystem.IsWindows() ? "cmd" : "npx")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    if (OperatingSystem.IsWindows())
    {
        info.ArgumentList.Add("/c");
        info.ArgumentList.Add("npx");
    }

    npx.ForEach(info.ArgumentList.Add);
    try
    {
        using Process process = Process.Start(info)!;
        Task<string> err = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd() + err.GetAwaiter().GetResult();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            findings.AddRange(Lines(output).Select(line => $"{line} [markdownlint]"));
        }
    }
    catch (System.ComponentModel.Win32Exception)
    {
        findings.Add("package.json:1:1: error: H-TEXT-007: npx not found; install Node.js so markdownlint can run");
    }
}

static string Git(string arguments)
{
    using Process process = Process.Start(new ProcessStartInfo("git", arguments) { RedirectStandardOutput = true, UseShellExecute = false })!;
    string output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    return output;
}

static IEnumerable<string> Lines(string text) =>
    text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static List<(Regex Glob, string Key, string Value)> ReadEditorConfig(string file)
{
    List<(Regex, string, string)> rules = [];
    Regex? current = null;
    foreach (string raw in File.ReadAllLines(file))
    {
        string line = raw.Trim();
        if (line.Length == 0 || line[0] is '#' or ';')
        {
            continue;
        }

        if (line[0] == '[' && line[^1] == ']')
        {
            current = GlobToRegex(line[1..^1]);
        }
        else if (current is not null && line.Split('=', 2) is [var key, var value])
        {
            rules.Add((current, key.Trim().ToLowerInvariant(), value.Trim().ToLowerInvariant()));
        }
    }

    return rules;
}

static Regex GlobToRegex(string glob)
{
    bool anchored = glob.Contains('/');
    StringBuilder pattern = new(anchored ? "^" : "(^|/)");
    int braces = 0;
    for (int i = 0; i < glob.TrimStart('/').Length; i++)
    {
        string g = glob.TrimStart('/');
        char c = g[i];
        switch (c)
        {
            case '*' when i + 1 < g.Length && g[i + 1] == '*':
                pattern.Append(".*");
                i++;
                break;
            case '*':
                pattern.Append("[^/]*");
                break;
            case '?':
                pattern.Append("[^/]");
                break;
            case '{':
                braces++;
                pattern.Append("(?:");
                break;
            case '}' when braces > 0:
                braces--;
                pattern.Append(')');
                break;
            case ',' when braces > 0:
                pattern.Append('|');
                break;
            default:
                pattern.Append(Regex.Escape(c.ToString()));
                break;
        }
    }

    return new Regex(pattern.Append('$').ToString());
}
