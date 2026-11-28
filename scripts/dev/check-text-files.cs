// check-text-files.cs - language-neutral hygiene checks on text files, run by the Husky pre-commit group.
//
//   dotnet run scripts/dev/check-text-files.cs                  staged files, judged as staged (the hook)
//   dotnet run scripts/dev/check-text-files.cs -- --all         every tracked file, from the working tree
//   dotnet run scripts/dev/check-text-files.cs -- --files a b   the named files, from the working tree
//   dotnet run scripts/dev/check-text-files.cs -- --files-from list   the NUL-separated paths of git diff -z (CI's changed files)
//
// A named file that does not exist is skipped. A listed one is an error (H-TEXT-013): CI lists the change without
// deletions, so a listed path that is not a file means the list did not survive the trip.
//
// Checks: trailing whitespace, final newline, extra blank lines at the end and line endings as .editorconfig
// sets them, JSON and TOML syntax (every .json and .toml file, filter or not; TOML through Tomlyn), files over
// 15 MB, and markdownlint (through npx, with the config Super-Linter reads). Apart from JSON and TOML syntax and size, files the CI Super-Linter job
// excludes are skipped: the pattern is read from FILTER_REGEX_EXCLUDE in .github/workflows/super-linter.yml,
// so there is no second list. A .patch file is skipped because the whitespace in a unified diff is content.
// YAML syntax is left to the pre-push Super-Linter run.
// shortcut: markdownlint reads the working-tree file, not the staged blob; upgrade if partial staging of
// Markdown matters. A Markdown file name with a shell character is refused, because npx.cmd runs through cmd.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false
#:package Tomlyn
#:include staged-files.cs

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
int listAt = Array.IndexOf(args, "--files-from");
bool staged = filesAt < 0 && listAt < 0 && !args.Contains("--all");
List<string> files;
List<string> ids = [];
if (listAt >= 0)
{
    string? list = listAt + 1 < args.Length ? args[listAt + 1] : null;
    if (list is null || !File.Exists(list))
    {
        Console.Error.WriteLine($"{list ?? "--files-from"}:1:1: error: H-TEXT-012: the file list to check was not found; pass --files-from <file of NUL-separated paths, as git diff -z writes>");
        return 1;
    }

    files = [.. File.ReadAllText(list, new UTF8Encoding(false)).Split('\0', StringSplitOptions.RemoveEmptyEntries)];
}
else if (filesAt >= 0)
{
    files = [.. args.Skip(filesAt + 1)];
}
else if (staged)
{
    (List<(string Path, string Id)> entries, string? error) = StagedFiles.List();
    if (error is not null)
    {
        Console.Error.WriteLine($".git:1:1: error: H-TEXT-010: {error}");
        return 1;
    }

    files = [.. entries.Select(entry => entry.Path)];
    ids = [.. entries.Select(entry => entry.Id)];
}
else
{
    (int code, byte[] output, string error) = Git("ls-files", "-z");
    if (code != 0)
    {
        Console.Error.WriteLine($".git:1:1: error: H-TEXT-010: git ls-files -z failed ({code}): {error}");
        return 1;
    }

    files = [.. Encoding.UTF8.GetString(output).Split('\0', StringSplitOptions.RemoveEmptyEntries)];
}

using Process? blobs = staged ? StagedFiles.StartReader(ids) : null;
List<string> findings = [];
List<string> markdown = [];
foreach (string file in files)
{
    string path = file.Replace('\\', '/');
    byte[]? bytes = staged ? StagedBytes(path) : File.Exists(path) ? File.ReadAllBytes(path) : null;
    if (bytes is null && listAt >= 0)
    {
        // The list leaves deletions out, so a path that is not a file means the list and the tree disagree.
        findings.Add($"{path}:1:1: error: H-TEXT-013: listed path is not a file in the working tree; list the change with git diff --name-only -z --diff-filter=d");
        continue;
    }

    if (bytes is null)
    {
        continue;
    }

    if (bytes.LongLength > MaxBytes)
    {
        findings.Add($"{path}:1:1: error: H-TEXT-006: file is over 15 MB; keep it out of git or add it to a data store");
        continue;
    }

    if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0)
    {
        continue;
    }

    string text = new UTF8Encoding(false).GetString(bytes);
    if (path.EndsWith(".json", StringComparison.Ordinal))
    {
        CheckJson(path, text);
    }

    if (path.EndsWith(".toml", StringComparison.Ordinal))
    {
        CheckToml(path, text);
    }

    // Super-Linter sees absolute paths, so test the pattern against a rooted one.
    if (excluded.IsMatch("/" + path) || path.EndsWith(".patch", StringComparison.Ordinal))
    {
        continue;
    }

    CheckText(path, text);
    if (path.EndsWith(".md", StringComparison.Ordinal) && File.Exists(path))
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

Console.WriteLine(listAt >= 0 ? $"[OK] text files are clean ({files.Count} listed file{(files.Count == 1 ? "" : "s")} checked)." : "[OK] text files are clean.");
return 0;

// The next blob from the batch reader, which answers in the order the ids were listed.
byte[]? StagedBytes(string path)
{
    byte[]? blob = StagedFiles.Next(blobs!);
    if (blob is null)
    {
        findings.Add($"{path}:1:1: error: H-TEXT-010: git cannot read the staged content of this file");
    }

    return blob;
}

void CheckJson(string path, string text)
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

void CheckToml(string path, string text)
{
    Tomlyn.Syntax.DocumentSyntax document = Tomlyn.Parsing.SyntaxParser.Parse(text, path, validate: true);
    foreach (Tomlyn.Syntax.DiagnosticMessage message in document.Diagnostics.Where(message => message.Kind == Tomlyn.Syntax.DiagnosticMessageKind.Error))
    {
        findings.Add($"{path}:{message.Span.Start.Line + 1}:{message.Span.Start.Column + 1}: error: H-TEXT-014: invalid TOML: {message.Message}");
    }
}

void CheckText(string path, string text)
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

    if (rules.GetValueOrDefault("insert_final_newline") == "true" && text.Length > 0)
    {
        if (!text.EndsWith('\n'))
        {
            findings.Add($"{path}:1:1: error: H-TEXT-002: missing final newline; end the file with one");
        }
        else if (Regex.IsMatch(text, @"\n\r?\n$"))
        {
            findings.Add($"{path}:{text.Split('\n').Length - 1}:1: error: H-TEXT-009: blank line at the end of the file; keep exactly one newline");
        }
    }

    if (rules.GetValueOrDefault("end_of_line") == "lf" && text.Contains('\r'))
    {
        findings.Add($"{path}:{text.Split('\n').TakeWhile(l => !l.Contains('\r')).Count() + 1}:1: error: H-TEXT-003: CR line ending; .editorconfig expects lf");
    }
}

void RunMarkdownlint(List<string> paths)
{
    // npx is npx.cmd on Windows, which only cmd can start without a shell, so a name cmd would read as code is refused.
    List<string> safe = [];
    foreach (string path in paths)
    {
        if (path.IndexOfAny(['&', '|', '^', '%', '<', '>', '"', '!']) >= 0)
        {
            findings.Add($"{path}:1:1: error: H-TEXT-008: file name has a shell character (& | ^ % < > \" !); rename the file");
        }
        else
        {
            safe.Add(path);
        }
    }

    if (safe.Count == 0)
    {
        return;
    }

    if (Resolve("npx") is null)
    {
        findings.Add("package.json:1:1: error: H-TEXT-007: npx not found; install Node.js so markdownlint can run");
        return;
    }

    // The bare name: a quoted full path ahead of a quoted file name makes cmd strip the outer quotes.
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

    foreach (string argument in new[] { "--yes", MarkdownlintPackage, "--config", ".github/linters/.markdownlint.yml" }.Concat(safe))
    {
        info.ArgumentList.Add(argument);
    }

    using Process process = Process.Start(info)!;
    Task<string> err = process.StandardError.ReadToEndAsync();
    string output = process.StandardOutput.ReadToEnd() + err.GetAwaiter().GetResult();
    process.WaitForExit();
    if (process.ExitCode == 0)
    {
        return;
    }

    string[] lines = [.. Lines(output)];
    if (lines.Length == 0)
    {
        findings.Add($"{safe[0]}:1:1: error: H-TEXT-011: markdownlint exited {process.ExitCode} and printed nothing; run it by hand: npx {MarkdownlintPackage} {safe[0]}");
    }

    findings.AddRange(lines.Select(line => $"{line} [markdownlint]"));
}

// A program on PATH, trying the extensions Windows adds (PATHEXT: .exe, .cmd, .bat).
static string? Resolve(string program)
{
    string[] extensions = OperatingSystem.IsWindows()
        ? [.. (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)]
        : [string.Empty];
    return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .SelectMany(folder => extensions.Select(extension => Path.Join(folder, program + extension)))
        .FirstOrDefault(File.Exists);
}

static (int Code, byte[] Output, string Error) Git(params string[] arguments)
{
    ProcessStartInfo info = new("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (string argument in arguments)
    {
        info.ArgumentList.Add(argument);
    }

    try
    {
        using Process process = Process.Start(info)!;
        using MemoryStream buffer = new();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.StandardOutput.BaseStream.CopyTo(buffer);
        process.WaitForExit();
        return (process.ExitCode, buffer.ToArray(), error.GetAwaiter().GetResult().Trim());
    }
    catch (System.ComponentModel.Win32Exception exception)
    {
        return (127, [], exception.Message);
    }
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
    string g = glob.TrimStart('/');
    StringBuilder pattern = new(glob.Contains('/') ? "^" : "(^|/)");
    int braces = 0;
    for (int i = 0; i < g.Length; i++)
    {
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
