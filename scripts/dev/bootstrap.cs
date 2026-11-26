// bootstrap.cs - set up and verify the local dev environment (cross-platform).
//
// Run:  dotnet run scripts/dev/bootstrap.cs              (set up)
//       dotnet run scripts/dev/bootstrap.cs -- --verify  (check only, no changes; make hooks-check)
//
// Restores the .NET local tools and installs the Husky.Net git hooks, then verifies the toolchain
// (git, signed-commit config; signing is REQUIRED and blocks). Idempotent: `dotnet tool restore` and
// `dotnet husky install` are safe to re-run.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;

const string InstallCommand = "dotnet tool restore && dotnet husky install";

bool verify = args.Contains("--verify");

static int Run(string file, string fileArgs, bool quiet = false)
{
    try
    {
        using var proc = Process.Start(new ProcessStartInfo(file, fileArgs)
        {
            UseShellExecute = false,
            RedirectStandardOutput = quiet,
            RedirectStandardError = quiet,
        })!;
        if (quiet)
        {
            _ = proc.StandardOutput.ReadToEnd();
            _ = proc.StandardError.ReadToEnd();
        }

        proc.WaitForExit();
        return proc.ExitCode;
    }
    catch
    {
        return 127;
    }
}

static string Capture(string file, string fileArgs)
{
    try
    {
        using var proc = Process.Start(new ProcessStartInfo(file, fileArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;
        string output = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit();
        return output;
    }
    catch
    {
        return string.Empty;
    }
}

static bool SigningConfigured() =>
    Capture("git", "config --get commit.gpgsign").Equals("true", StringComparison.OrdinalIgnoreCase);

// The commit-msg hook still runs Python (scripts/dev/hook-python.sh) until the commit-message check is ported.
// Same search as the hook: this checkout's venv, the main checkout's venv, then python on PATH.
static string? PythonProblem()
{
    string common = Capture("git", "rev-parse --path-format=absolute --git-common-dir");
    string main = Path.GetDirectoryName(common.TrimEnd('/', '\\')) ?? string.Empty;
    string[] candidates =
    [
        ".venv/Scripts/python.exe", ".venv/bin/python",
        Path.Join(main, ".venv/Scripts/python.exe"), Path.Join(main, ".venv/bin/python"),
        "python", "python3",
    ];
    if (candidates.Any(python => Run(python, "-m pre_commit --version", quiet: true) == 0))
    {
        return null;
    }

    return "the commit-msg hook still needs a Python with pre-commit installed (until its check is ported); create it:" + Environment.NewLine +
        "      python -m venv .venv" + Environment.NewLine +
        "      .venv/Scripts/python.exe -m pip install -e \".[dev]\"   (Linux/macOS: .venv/bin/python -m pip install -e \".[dev]\")";
}

// Git runs a hook only when core.hooksPath resolves to this checkout's .husky and the hook file exists.
static string? HooksProblem()
{
    string root = Capture("git", "rev-parse --show-toplevel");
    string hooksPath = Capture("git", "config --get core.hooksPath");
    string husky = Path.GetFullPath(".husky", root);
    if (hooksPath.Length == 0 || Path.GetFullPath(hooksPath, root) != husky)
    {
        return $"Husky.Net hooks not installed - run: {InstallCommand}";
    }

    return new[] { "pre-commit", "commit-msg", "pre-push" }.Any(name => !HookRuns(Path.Join(husky, name)))
        ? "this checkout predates the Husky hooks: merge or rebase onto main"
        : null;
}

// git runs a hook only when the file exists and, off Windows, is executable.
static bool HookRuns(string path) =>
    File.Exists(path) && (OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0);

if (string.IsNullOrWhiteSpace(Capture("git", "--version")))
{
    Console.Error.WriteLine("[FAIL] git not found on PATH.");
    return 1;
}

if (verify)
{
    List<string> problems = [];
    if (HooksProblem() is { } hooks)
    {
        problems.Add(hooks);
    }

    if (!SigningConfigured())
    {
        problems.Add("commit.gpgsign != true - signed commits are required (CONTRIBUTING.md, \"Sign your commits\").");
    }

    if (PythonProblem() is { } python)
    {
        problems.Add(python);
    }

    if (problems.Count > 0)
    {
        Console.Error.WriteLine("[FAIL] dev environment is not ready:");
        foreach (string problem in problems)
        {
            Console.Error.WriteLine($"  - {problem}");
        }

        return 1;
    }

    if (string.IsNullOrWhiteSpace(Capture("docker", "--version")))
    {
        Console.WriteLine(
            "[NOTE] Docker not found - the pre-push Super-Linter pass will be skipped. " +
            "Install Docker for local lint parity with CI.");
    }

    if (string.IsNullOrWhiteSpace(Capture("node", "--version")))
    {
        Console.WriteLine("[NOTE] Node.js not found - the pre-commit markdownlint check cannot run. Install Node.js.");
    }

    Console.WriteLine("[OK] dev environment is correct and up to date.");
    return 0;
}

Console.WriteLine("[INFO] restoring .NET local tools ...");
if (Run("dotnet", "tool restore") != 0)
{
    return 1;
}

Console.WriteLine("[INFO] installing Husky.Net git hooks ...");
if (Run("dotnet", "husky install") != 0)
{
    return 1;
}

// Signed commits are required by the repo ruleset - block until configured.
if (!SigningConfigured())
{
    Console.Error.WriteLine(
        "[FAIL] commit.gpgsign != true - signed commits are required. Configure signing (CONTRIBUTING.md, \"Sign your commits\"), then re-run.");
    return 1;
}

if (PythonProblem() is { } missingPython)
{
    Console.Error.WriteLine($"[FAIL] {missingPython}");
    return 1;
}

Console.WriteLine("[OK] dev environment ready.");
return 0;
