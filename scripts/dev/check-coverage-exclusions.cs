// check-coverage-exclusions.cs - allow [ExcludeFromCodeCoverage] only on source-generated JSON contexts.
//
// Run by the Husky pre-commit hook:  dotnet run scripts/dev/check-coverage-exclusions.cs
// Self-test on one file:             dotnet run scripts/dev/check-coverage-exclusions.cs -- --seed tests/fixtures/coverage-exclusion-seed.txt
//
// Hand-written code carries no coverage exclusion. The one allowed place is a file named
// *JsonContext.cs, where the JSON source generator emits the code.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;

int seedAt = Array.IndexOf(args, "--seed");
if (seedAt >= 0 && seedAt + 1 >= args.Length)
{
    Console.Error.WriteLine("[FAIL] --seed needs a file path.");
    return 2;
}

string[] files = seedAt >= 0 ? [args[seedAt + 1]] : TrackedCsFiles();
int hits = 0;
foreach (string path in files)
{
    string name = Path.GetFileName(path);
    bool allowed = seedAt < 0
        && (name.EndsWith("JsonContext.cs", StringComparison.Ordinal) || name == "check-coverage-exclusions.cs");
    if (allowed || !File.Exists(path))
    {
        continue;
    }

    string[] lines = File.ReadAllLines(path);
    for (int i = 0; i < lines.Length; i++)
    {
        if (lines[i].Contains("ExcludeFromCodeCoverage", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"{path.Replace('\\', '/')}:{i + 1}: [ExcludeFromCodeCoverage] outside a *JsonContext.cs file");
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

static string[] TrackedCsFiles()
{
    using Process git = Process.Start(new ProcessStartInfo("git", "ls-files -z -- *.cs")
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
