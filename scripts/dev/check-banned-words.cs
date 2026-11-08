// check-banned-words.cs - fail on banned words and the operator's name in tracked text files.
//
// Run by the Husky pre-commit hook and CI:  dotnet run scripts/dev/check-banned-words.cs
// Self-test on one file:                    dotnet run scripts/dev/check-banned-words.cs -- --seed tests/fixtures/banned-words-seed.txt
//
// The rules are in docs/style-guides/naming-and-words.md. Names come only from the
// COH_BANNED_NAMES environment variable (comma-separated) so a name never appears in the
// repository; when it is unset the name check is skipped and the script says so.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Text.RegularExpressions;

string[] bannedWords = ["oracle"];

string[] skipPrefixes = ["data/", "tests/fixtures/", "samples/"];
// The script itself and the guide that defines the list must name the words.
string[] skipFiles = ["scripts/dev/check-banned-words.cs", "docs/style-guides/naming-and-words.md"];
string[] binaryExtensions =
[
    ".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".gz", ".7z", ".dll", ".exe", ".pdb",
    ".mbd", ".bin", ".woff", ".woff2", ".ttf", ".otf", ".mp4", ".webp", ".snk",
];

string[] names = (Environment.GetEnvironmentVariable("COH_BANNED_NAMES") ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (names.Length == 0)
{
    Console.WriteLine("[SKIP] COH_BANNED_NAMES is unset: the operator-name check was not run.");
}

(Regex Pattern, string Label)[] rules =
[
    .. bannedWords.Select(w => (new Regex($@"\b{Regex.Escape(w)}\b", RegexOptions.IgnoreCase), $"banned word '{w}'")),
    .. names.Select(n => (new Regex($@"\b{Regex.Escape(n)}\b", RegexOptions.IgnoreCase), "operator name (COH_BANNED_NAMES)")),
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
    files = TrackedFiles().Where(p =>
        !skipPrefixes.Any(p.StartsWith) &&
        !skipFiles.Contains(p) &&
        !binaryExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()));
}

int hits = 0;
foreach (string path in files)
{
    if (!File.Exists(path))
    {
        continue; // tracked but deleted in the working tree
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

static IEnumerable<string> TrackedFiles()
{
    using Process git = Process.Start(new ProcessStartInfo("git", "ls-files")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    })!;
    string output = git.StandardOutput.ReadToEnd();
    git.WaitForExit();
    return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
