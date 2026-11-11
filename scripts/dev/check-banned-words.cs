// check-banned-words.cs - fail on banned words and the operator's name in tracked text files.
//
// Run by the Husky pre-commit hook and CI:  dotnet run scripts/dev/check-banned-words.cs
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

    Console.WriteLine("[SKIP] COH_BANNED_NAMES is unset and git config user.name is empty: the operator-name check was not run.");
}

(Regex Pattern, string Label)[] rules =
[
    .. bannedWords.Select(w => (new Regex($@"\b{Regex.Escape(w)}", RegexOptions.IgnoreCase), $"banned word '{w}'")),
    .. names.Select(n => (new Regex($@"\b{Regex.Escape(n)}\b", RegexOptions.IgnoreCase), $"operator name ({nameSource})")),
];

IEnumerable<string> files;
int seedAt = Array.IndexOf(args, "--seed");
if (seedAt >= 0)
{
    if (seedAt + 1 >= args.Length)
    {
        Console.Error.WriteLine("[FAIL] --seed needs a file path.");
        return 2;
    }

    files = [args[seedAt + 1]];
}
else
{
    string[] tracked;
    try
    {
        tracked = TrackedFiles();
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"[FAIL] {ex.Message}");
        return 2;
    }

    files = tracked.Where(p =>
        !skipPrefixes.Any(s => p.StartsWith(s, StringComparison.Ordinal)) &&
        !skipFiles.Contains(p, StringComparer.Ordinal) &&
        !binaryExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));
}

int hits = 0;
foreach (string path in files)
{
    if (!File.Exists(path))
    {
        continue; // tracked but deleted in the working tree
    }

    if (LooksBinary(path))
    {
        continue;
    }

    string[] lines = File.ReadAllLines(path);
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
    Console.Error.WriteLine($"[FAIL] {hits} banned-word hit(s). See docs/style-guides/naming-and-words.md.");
    return 1;
}

Console.WriteLine("[OK] no banned words found.");
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

// A NUL byte in the first 8 KB marks a binary file.
static bool LooksBinary(string path)
{
    using FileStream stream = File.OpenRead(path);
    byte[] head = new byte[8192];
    int read = stream.Read(head, 0, head.Length);
    return Array.IndexOf(head, (byte)0, 0, read) >= 0;
}
