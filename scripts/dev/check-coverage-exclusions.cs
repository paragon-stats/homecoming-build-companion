// check-coverage-exclusions.cs - allow [ExcludeFromCodeCoverage] only on source-generated JSON contexts.
//
//   dotnet run scripts/dev/check-coverage-exclusions.cs                  CI: scans the working tree
//   dotnet run scripts/dev/check-coverage-exclusions.cs -- --staged      pre-commit: scans the staged content
//   dotnet run scripts/dev/check-coverage-exclusions.cs -- --seed <file> [--as <path.cs>]
//                                                                        self-test on one file, named <path.cs> for the rule
//
// Hand-written code carries no coverage exclusion. The one allowed place is a file named
// *JsonContext.cs that declares a type deriving from JsonSerializerContext (a textual match on the
// base list, not a semantic one: it cannot tell which type in the file carries the attribute).
// Comments and string literals are ignored (a simple scan: raw string literals are not understood).
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Text.RegularExpressions;

int seedAt = Array.IndexOf(args, "--seed");
int asAt = Array.IndexOf(args, "--as");
if ((seedAt >= 0 && seedAt + 1 >= args.Length) || (asAt >= 0 && asAt + 1 >= args.Length))
{
    Console.Error.WriteLine("[FAIL] --seed and --as each need a value.");
    return 2;
}

Regex nonCode = new(
    @"'(?:\\.|[^'\\])'|//[^\n]*|/\*.*?\*/|@""(?:[^""]|"""")*""|""(?:\\.|[^""\\\n])*""",
    RegexOptions.Singleline);
Regex derivesFromContext = new(@":[^{;]*\bJsonSerializerContext\b");
Regex attribute = new(@"\bExcludeFromCodeCoverage(Attribute)?\b");

List<(string Path, string Text)> files = [];
if (seedAt >= 0)
{
    files.Add((asAt >= 0 ? args[asAt + 1] : args[seedAt + 1], File.ReadAllText(args[seedAt + 1])));
}
else if (args.Contains("--staged"))
{
    foreach (string path in Git("diff --cached --name-only --diff-filter=ACMR -z -- *.cs"))
    {
        files.Add((path, string.Concat(Git($"show :{path}", split: false))));
    }
}
else
{
    foreach (string path in Git("ls-files -z -- *.cs").Where(File.Exists))
    {
        files.Add((path, File.ReadAllText(path)));
    }
}

int hits = 0;
foreach ((string path, string text) in files)
{
    // Blank comments and strings but keep the newlines, so reported line numbers stay right.
    string code = nonCode.Replace(text, m => string.Concat(m.Value.Where(c => c == '\n')));
    if (path.EndsWith("JsonContext.cs", StringComparison.Ordinal) && derivesFromContext.IsMatch(code))
    {
        continue;
    }

    string[] lines = code.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        if (attribute.IsMatch(lines[i]))
        {
            Console.Error.WriteLine($"{path.Replace('\\', '/')}:{i + 1}: [ExcludeFromCodeCoverage] outside a JsonSerializerContext file named *JsonContext.cs");
            hits++;
        }
    }
}

if (hits > 0)
{
    Console.Error.WriteLine($"[FAIL] {hits} coverage exclusion(s) on hand-written code. Cover the code with a test instead.");
    return 1;
}

Console.WriteLine("[OK] no coverage exclusion outside the JSON contexts.");
return 0;

static string[] Git(string arguments, bool split = true)
{
    using Process git = Process.Start(new ProcessStartInfo("git", arguments)
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    }) ?? throw new InvalidOperationException("could not start git.");
    string output = git.StandardOutput.ReadToEnd();
    git.WaitForExit();
    if (git.ExitCode != 0)
    {
        throw new InvalidOperationException($"git {arguments} exited {git.ExitCode}; the file list is unknown.");
    }

    return split ? output.Split('\0', StringSplitOptions.RemoveEmptyEntries) : [output];
}
