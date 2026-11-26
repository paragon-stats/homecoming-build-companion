// check-text-files.cs - language-neutral hygiene checks on text files, run by the Husky pre-commit group.
//
//   dotnet run scripts/dev/check-text-files.cs                  staged files, judged as staged (the hook)
//   dotnet run scripts/dev/check-text-files.cs -- --all         every tracked file, from the working tree
//   dotnet run scripts/dev/check-text-files.cs -- --files a b   the named files, from the working tree
//
// Checks: trailing whitespace, final newline, extra blank lines at the end and line endings as .editorconfig
// sets them, JSON syntax (every .json file, filter or not), files over 15 MB, and markdownlint (through npx,
// with the config Super-Linter reads). Apart from JSON syntax and size, files the CI Super-Linter job
// excludes are skipped: the pattern is read from FILTER_REGEX_EXCLUDE in .github/workflows/super-linter.yml,
// so there is no second list. A .patch file is skipped because the whitespace in a unified diff is content.
// YAML syntax is left to the pre-push Super-Linter run.
// shortcut: markdownlint reads the working-tree file, not the staged blob; upgrade if partial staging of
// Markdown matters. A Markdown file name with a shell character is refused, because npx.cmd runs through cmd.
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
bool staged = filesAt < 0 && !args.Contains("--all");
List<string> files;
List<string> ids = [];
if (filesAt >= 0)
{
    files = [.. args.Skip(filesAt + 1)];
}
else
{
    string[] listing = staged
        ? ["diff", "--cached", "--raw", "--no-abbrev", "--no-renames", "--diff-filter=ACM", "-z"]
        : ["ls-files", "-z"];
    (int code, byte[] output, string error) = Git(listing);
    if (code != 0)
    {
        Console.Error.WriteLine($".git:1:1: error: H-TEXT-010: git {string.Join(' ', listing)} failed ({code}): {error}");
        return 1;
    }

    files = [.. Encoding.UTF8.GetString(output).Split('\0', StringSplitOptions.RemoveEmptyEntries)];
    if (staged)
    {
        // Raw records alternate ":<old mode> <new mode> <old id> <new id> <status>" and the path.
        // A symlink (120000) holds a target and a submodule (160000) a commit: neither is text to judge.
        string[] records = [.. files];
        files = [];
        for (int i = 0; i + 1 < records.Length; i += 2)
        {
            string[] meta = records[i].Split(' ');
            if (meta[1] is "100644" or "100755")
            {
                files.Add(records[i + 1]);
                ids.Add(meta[3]);
            }
        }
    }
}

using Process? blobs = staged ? StartBlobReader(ids) : null;
List<string> findings = [];
List<string> markdown = [];
foreach (string file in files)
{
    string path = file.Replace('\\', '/');
    byte[]? bytes = staged ? StagedBytes(path) : File.Exists(path) ? File.ReadAllBytes(path) : null;
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

Console.WriteLine("[OK] text files are clean.");
return 0;

// The next blob from the batch reader, which answers in the order the ids were listed.
byte[]? StagedBytes(string path)
{
    Stream stream = blobs!.StandardOutput.BaseStream;
    StringBuilder header = new();
    for (int next = stream.ReadByte(); next is not ('\n' or -1); next = stream.ReadByte())
    {
        header.Append((char)next);
    }

    // "<id> blob <size>", or "<id> missing" when the object is not in the repository.
    if (header.ToString().Split(' ') is not [_, "blob", string size])
    {
        findings.Add($"{path}:1:1: error: H-TEXT-010: git cannot read the staged content of this file");
        return null;
    }

    byte[] blob = new byte[int.Parse(size, System.Globalization.CultureInfo.InvariantCulture)];
    stream.ReadExactly(blob);
    _ = stream.ReadByte();
    return blob;
}

// One git process serves every staged blob, so a commit of thousands of files starts git once, not per file.
static Process StartBlobReader(List<string> ids)
{
    ProcessStartInfo info = new("git", "cat-file --batch")
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
        StandardInputEncoding = new UTF8Encoding(false),
    };
    Process process = Process.Start(info)!;

    // Written from another thread: git stops reading ids while its answers wait to be read.
    _ = Task.Run(() =>
    {
        foreach (string id in ids)
        {
            process.StandardInput.Write(id + "\n");
        }

        process.StandardInput.Close();
    });
    return process;
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
