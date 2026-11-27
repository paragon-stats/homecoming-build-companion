// check-banned-words.cs - fail on banned words and the operator's name in tracked text files.
//
// Run by CI and the pre-push hook:          dotnet run scripts/dev/check-banned-words.cs              (tracked files)
// Run by the pre-commit hook:               dotnet run scripts/dev/check-banned-words.cs -- --staged  (staged content)
// Self-test on one file:                    dotnet run scripts/dev/check-banned-words.cs -- --seed tests/fixtures/banned-words-seed.txt
//
// The rules are in docs/style-guides/naming-and-words.md. Names come only from the
// COH_BANNED_NAMES environment variable (comma-separated) so a name never appears in the
// repository. Unset locally, the script uses the full name and the first name from the git user
// name and skips the name check, saying so, only when git has none; unset in CI
// (GITHUB_ACTIONS set), the script fails, so the gate cannot pass without the secret.
// A banned word matches at the start of a word, so "oracles" is caught too.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false
#:include staged-files.cs

using System.Diagnostics;
using System.Text.RegularExpressions;

string[] bannedWords = ["oracle"];

string[] skipPrefixes = ["data/", "tests/fixtures/", "samples/"];
// The script, the guide that defines the list and manifest.json (it catalogs data/ paths, which
// include the game powerset "oracle_earth") must keep the word.
string[] skipFiles = ["scripts/dev/check-banned-words.cs", "docs/style-guides/naming-and-words.md", "manifest.json"];
string[] binaryExtensions =
[
    ".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".gz", ".7z", ".dll", ".exe", ".pdb",
    ".mbd", ".bin", ".woff", ".woff2", ".ttf", ".otf", ".mp4", ".webp", ".snk",
];

string[] names = (Environment.GetEnvironmentVariable("COH_BANNED_NAMES") ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
string nameSource = "COH_BANNED_NAMES";
string nameCheck = string.Empty;
if (names.Length == 0 && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is null)
{
    names = OperatorNames();
    nameSource = "names taken from git config user.name";
}

if (names.Length == 0)
{
    if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null)
    {
        Console.Error.WriteLine("[FAIL] COH_BANNED_NAMES is unset in CI: add the repository secret and pass it to this step.");
        return 1;
    }

    nameCheck = "; the operator-name check was not run: COH_BANNED_NAMES is unset and git config user.name is empty";
}

(Regex Pattern, string Label)[] rules =
[
    .. bannedWords.Select(w => (new Regex($@"\b{Regex.Escape(w)}", RegexOptions.IgnoreCase), $"banned word '{w}'")),
    .. names.Select(n => (new Regex($@"\b{Regex.Escape(n)}\b", RegexOptions.IgnoreCase), $"operator name ({nameSource})")),
];

IEnumerable<string> files;
List<string> ids = [];
bool staged = args.Contains("--staged");
int seedAt = Array.IndexOf(args, "--seed");
if (seedAt >= 0)
{
    if (seedAt + 1 >= args.Length)
    {
        Console.Error.WriteLine("[FAIL] --seed needs a file path.");
        return 2;
    }

    files = [args[seedAt + 1]];
    staged = false;
}
else
{
    List<string> listed;
    if (staged)
    {
        (List<(string Path, string Id)> entries, string? error) = StagedFiles.List();
        if (error is not null)
        {
            Console.Error.WriteLine($"[FAIL] {error}; the file list is unknown.");
            return 2;
        }

        listed = [.. entries.Select(entry => entry.Path)];
        ids = [.. entries.Select(entry => entry.Id)];
    }
    else
    {
        try
        {
            listed = [.. TrackedFiles()];
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"[FAIL] {ex.Message}");
            return 2;
        }
    }

    // The staged blobs are read in listing order, so a skipped file drops its id too.
    List<string> kept = [];
    List<string> keptIds = [];
    for (int i = 0; i < listed.Count; i++)
    {
        string p = listed[i];
        if (!skipPrefixes.Any(s => p.StartsWith(s, StringComparison.Ordinal)) &&
            !skipFiles.Contains(p, StringComparer.Ordinal) &&
            !binaryExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
        {
            kept.Add(p);
            if (staged)
            {
                keptIds.Add(ids[i]);
            }
        }
    }

    files = kept;
    ids = keptIds;
}

using Process? blobs = staged ? StagedFiles.StartReader(ids) : null;
int hits = 0;
foreach (string path in files)
{
    byte[]? bytes = staged ? StagedFiles.Next(blobs!) : File.Exists(path) ? File.ReadAllBytes(path) : null;
    if (bytes is null && staged)
    {
        // A missing blob, or git stopping partway: what is staged here was not checked.
        Console.Error.WriteLine($"{path}:1: git cannot read the staged content of this file");
        hits++;
        continue;
    }

    if (bytes is null)
    {
        continue; // tracked but deleted in the working tree
    }

    // A NUL byte in the first 8 KB marks a binary file.
    if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8192)) >= 0)
    {
        continue;
    }

    string[] lines = new System.Text.UTF8Encoding(false).GetString(bytes).ReplaceLineEndings("\n").Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        foreach ((Regex pattern, string label) in rules)
        {
            if (pattern.IsMatch(lines[i]))
            {
                Console.Error.WriteLine($"{path.Replace('\\', '/')}:{i + 1}: {label}");
                hits++;
            }
        }
    }
}

if (hits > 0)
{
    Console.Error.WriteLine($"[FAIL] {hits} banned-word hit(s){nameCheck}. See docs/style-guides/naming-and-words.md.");
    return 1;
}

Console.WriteLine($"[OK] no banned words found{nameCheck}.");
return 0;

static string[] OperatorNames()
{
    using Process git = Process.Start(new ProcessStartInfo("git", "config user.name")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    }) ?? throw new InvalidOperationException("could not start git.");
    string full = git.StandardOutput.ReadToEnd().Trim();
    git.WaitForExit();
    if (git.ExitCode != 0 || full.Length == 0)
    {
        return [];
    }

    return [.. new[] { full, full.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0] }.Distinct(StringComparer.OrdinalIgnoreCase)];
}

static string[] TrackedFiles()
{
    using Process git = Process.Start(new ProcessStartInfo("git", "ls-files -z")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    }) ?? throw new InvalidOperationException("could not start git ls-files.");
    string output = git.StandardOutput.ReadToEnd();
    git.WaitForExit();
    if (git.ExitCode != 0)
    {
        throw new InvalidOperationException($"git ls-files exited {git.ExitCode}; the file list is unknown.");
    }

    return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
}
