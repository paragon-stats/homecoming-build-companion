// run-super-linter.cs - run the CI Super-Linter image locally for lint parity.
//
//   dotnet run scripts/dev/run-super-linter.cs                  lint the changes (the pre-push hook, make super-linter)
//   dotnet run scripts/dev/run-super-linter.cs -- --print-plan  print the docker command and run nothing
//
// Reads the image version from the pinned action's comment and FILTER_REGEX_EXCLUDE from
// .github/workflows/super-linter.yml, and the VALIDATE_* flags from .github/super-linter.env, so local and CI
// lint cannot drift. Works from a linked git worktree. Docker is required; when it is unavailable the run is
// skipped, not failed - CI still lints.
//
// Environment: SUPER_LINTER_DOCKER_CMD (default "docker", for example "sudo -n docker"), SUPER_LINTER_IMAGE,
// SUPER_LINTER_PULL_POLICY ("always" checks the registry digest, "missing" pulls only when absent),
// SUPER_LINTER_DEFAULT_BRANCH, SUPER_LINTER_VALIDATE_ALL_CODEBASE, FILTER_REGEX_EXCLUDE.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

const string WorkflowFile = ".github/workflows/super-linter.yml";
const string EnvFile = ".github/super-linter.env";
const string Workspace = "/workspace";
const string GitCommon = "/git-common";

string root = Directory.GetCurrentDirectory();
if (!File.Exists(WorkflowFile))
{
    Console.Error.WriteLine($"[FAIL] {WorkflowFile} not found; run from the repository root.");
    return 1;
}

string workflow = File.ReadAllText(WorkflowFile);
Match action = Regex.Match(workflow, @"uses: super-linter/super-linter@[0-9a-f]{40}\s+#\s*(v[0-9][0-9.]*)");
Match filter = Regex.Match(workflow, @"^\s+FILTER_REGEX_EXCLUDE: '([^']*)'\s*$", RegexOptions.Multiline);
if (!action.Success || !filter.Success)
{
    Console.Error.WriteLine($"[FAIL] {WorkflowFile}: the pinned super-linter action or FILTER_REGEX_EXCLUDE was not found.");
    return 1;
}

string defaultImage = $"ghcr.io/super-linter/super-linter:{action.Groups[1].Value}";
string image = Env("SUPER_LINTER_IMAGE");
if (image.Length == 0 || image.Contains("super-linter:slim-v8", StringComparison.Ordinal))
{
    image = defaultImage;
}

string pullPolicy = Env("SUPER_LINTER_PULL_POLICY").ToLowerInvariant();
if (pullPolicy.Length == 0)
{
    pullPolicy = "always";
}

if (pullPolicy is not ("always" or "missing"))
{
    Console.Error.WriteLine("[FAIL] SUPER_LINTER_PULL_POLICY must be 'always' or 'missing'.");
    return 2;
}

string defaultBranch = Env("SUPER_LINTER_DEFAULT_BRANCH");
if (defaultBranch.Length == 0)
{
    (int code, string head) = Git("symbolic-ref refs/remotes/origin/HEAD");
    defaultBranch = code == 0 && head.StartsWith("refs/remotes/", StringComparison.Ordinal) ? head["refs/remotes/".Length..] : "main";
}

string validateAll = Env("SUPER_LINTER_VALIDATE_ALL_CODEBASE").ToLowerInvariant();
if (validateAll.Length == 0)
{
    List<string> refs = [];
    if (defaultBranch.StartsWith("refs/", StringComparison.Ordinal))
    {
        refs.Add(defaultBranch);
    }
    else
    {
        if (defaultBranch.Contains('/'))
        {
            refs.Add($"refs/remotes/{defaultBranch}");
        }

        refs.Add($"refs/heads/{defaultBranch}");
    }

    bool found = refs.Any(r => Git($"show-ref --verify --quiet {r}").Code == 0);
    if (!found)
    {
        Console.Error.WriteLine("DEFAULT_BRANCH ref not found locally; running Super-Linter against all files.");
    }

    validateAll = found ? "false" : "true";
}

List<string> envPairs =
[
    "RUN_LOCAL=true",
    "GITHUB_ACTIONS=true",
    $"GITHUB_WORKSPACE={Workspace}",
    $"DEFAULT_BRANCH={defaultBranch}",
    $"VALIDATE_ALL_CODEBASE={validateAll}",
];

// Dev-only folders exist on a developer machine and never in CI; the rest is CI's own pattern.
string operatorFilter = Env("FILTER_REGEX_EXCLUDE");
if (operatorFilter.Length > 0)
{
    envPairs.Add($"FILTER_REGEX_EXCLUDE={operatorFilter}");
}
else if (image == defaultImage)
{
    envPairs.Add($@"FILTER_REGEX_EXCLUDE=(^|/)(\.venv|\.tox|\.pytest_cache|\.mypy_cache|\.ruff_cache|htmlcov)/|{filter.Groups[1].Value}");
}

if (image == defaultImage
    && Env("JSCPD_LINTER_RULES").Length == 0
    && Env("JSCPD_CONFIG_FILE").Length == 0
    && File.Exists(".github/linters/.jscpd.json"))
{
    envPairs.Add($"JSCPD_LINTER_RULES={Workspace}/.github/linters/.jscpd.json");
}

string dockerOverride = Env("SUPER_LINTER_DOCKER_CMD");
string[] dockerCommand = SplitCommand(dockerOverride.Length > 0 ? dockerOverride : "docker");
if (dockerCommand.Length == 0)
{
    Console.Error.WriteLine("[FAIL] SUPER_LINTER_DOCKER_CMD names no program.");
    return 1;
}

(string Path, string Content)? gitFile = null;

List<string> run = ["run", "--rm", "--pull", pullPolicy == "always" ? "never" : pullPolicy];
if (File.Exists(EnvFile))
{
    run.AddRange(["--env-file", Path.GetFullPath(EnvFile)]);
}

foreach (string pair in envPairs)
{
    run.AddRange(["-e", pair]);
}

run.AddRange(["-v", $"{root}:{Workspace}"]);
run.AddRange(WorktreeArguments());
run.AddRange(["-w", Workspace, image]);

if (args.Contains("--print-plan"))
{
    Console.WriteLine(string.Join(' ', dockerCommand));
    run.ForEach(Console.WriteLine);
    return 0;
}

int probe = Docker(capture: true, "version");
if (probe == 127 && dockerOverride.Length == 0)
{
    Console.Error.WriteLine(
        "[SKIP] Docker is not installed - skipping the local Super-Linter pass; CI will still lint. " +
        "Install Docker for local parity.");
    return 0;
}

if (probe == 127)
{
    Console.Error.WriteLine($"[FAIL] SUPER_LINTER_DOCKER_CMD names a program that was not found: {dockerCommand[0]}");
    return 1;
}

if (probe != 0)
{
    Console.Error.WriteLine(
        $"[FAIL] Docker was found but is not usable ('{string.Join(' ', dockerCommand)} version' exited {probe}). " +
        "To fix it, start Docker (the Docker Desktop app or the docker service), check your user may use it, then run this again.");
    return 1;
}

// One run at a time per repository keeps the output readable and the machine responsive.
using FileStream lockFile = AcquireLock();
if (gitFile is { } linked)
{
    File.WriteAllText(linked.Path, linked.Content);
}

int fresh = pullPolicy == "always" ? EnsureImageFresh() : 0;
if (fresh != 0)
{
    return fresh;
}

Console.Error.WriteLine("[INFO] running Super-Linter locally (RUN_LOCAL) ...");
int result = Docker(capture: false, [.. run]);
if (result != 0)
{
    Console.Error.WriteLine("[FAIL] Super-Linter reported findings - fix them before pushing.");
}

return result;

// Split a command line on spaces, keeping a double-quoted part (a path with spaces) whole.
static string[] SplitCommand(string command) =>
    [.. System.Text.RegularExpressions.Regex.Matches(command, @"""([^""]*)""|(\S+)").Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)];

static string Env(string name) => (Environment.GetEnvironmentVariable(name) ?? string.Empty).Trim();

static (int Code, string Output) Git(string arguments)
{
    using Process process = Process.Start(new ProcessStartInfo("git", arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    })!;
    string output = process.StandardOutput.ReadToEnd().Trim();
    _ = process.StandardError.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, output);
}

// Run docker. When capturing, drain BOTH streams before WaitForExit so a child that fills the
// stderr pipe buffer cannot deadlock; otherwise inherit stdio for live output.
int Docker(bool capture, params string[] dockerArgs) => DockerOutput(capture, out _, dockerArgs);

int DockerOutput(bool capture, out byte[] stdout, params string[] dockerArgs)
{
    stdout = [];
    try
    {
        ProcessStartInfo info = new(dockerCommand[0])
        {
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
            UseShellExecute = false,
        };
        foreach (string a in dockerCommand.Skip(1).Concat(dockerArgs))
        {
            info.ArgumentList.Add(a);
        }

        using Process process = Process.Start(info)!;
        if (capture)
        {
            using MemoryStream buffer = new();
            Task<string> err = process.StandardError.ReadToEndAsync();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            _ = err.GetAwaiter().GetResult();
            stdout = buffer.ToArray();
        }

        process.WaitForExit();
        return process.ExitCode;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return 127;
    }
}

string Text(params string[] dockerArgs)
{
    int code = DockerOutput(capture: true, out byte[] bytes, dockerArgs);
    return code == 0 ? System.Text.Encoding.UTF8.GetString(bytes).Trim() : string.Empty;
}

// Pull when the image is missing or the registry's digest differs; keep the cached image when the
// registry cannot be reached. Returns 0, or the pull's exit code when there is no image to run.
int EnsureImageFresh()
{
    string local = Text("image", "inspect", "--format", "{{index .RepoDigests 0}}", image);
    string localDigest = local.Contains('@') ? local.Split('@', 2)[1] : string.Empty;
    if (localDigest.Length == 0)
    {
        Console.Error.WriteLine($"Super-Linter image not found locally. Pulling {image}...");
        int pulled = Docker(capture: false, "pull", image);
        if (pulled != 0)
        {
            Console.Error.WriteLine($"Failed to pull {image} (exit {pulled}).");
        }

        return pulled;
    }

    if (DockerOutput(capture: true, out byte[] manifest, "buildx", "imagetools", "inspect", "--raw", image) != 0)
    {
        Console.Error.WriteLine("Cannot check registry digest. Using cached image.");
        return 0;
    }

    if (localDigest == "sha256:" + Convert.ToHexString(SHA256.HashData(manifest)).ToLowerInvariant())
    {
        return 0;
    }

    string oldId = Text("image", "inspect", "--format", "{{.Id}}", image);
    Console.Error.WriteLine($"Super-Linter image is stale. Pulling {image}...");
    if (Docker(capture: false, "pull", image) != 0)
    {
        Console.Error.WriteLine($"Failed to pull {image}. Using cached image.");
        return 0;
    }

    if (oldId.Length > 0)
    {
        _ = Docker(capture: true, "rmi", oldId);
    }

    return 0;
}

FileStream AcquireLock()
{
    string lockPath = Path.Join(CommonGitDirectory(), "super-linter.lock");
    bool told = false;
    while (true)
    {
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            if (!told)
            {
                Console.Error.WriteLine($"[INFO] waiting for another Super-Linter run on this repository to finish (lock file {lockPath}) ...");
                told = true;
            }

            Thread.Sleep(1000);
        }
    }
}

// The repository's shared git directory, for a normal checkout or a linked worktree.
string CommonGitDirectory()
{
    string dotGit = Path.Join(root, ".git");
    if (Directory.Exists(dotGit))
    {
        return dotGit;
    }

    string gitDir = LinkedGitDirectory(dotGit);
    return Path.GetFullPath(File.ReadAllText(Path.Join(gitDir, "commondir")).Trim(), gitDir);
}

string LinkedGitDirectory(string dotGit) =>
    Path.GetFullPath(File.ReadAllText(dotGit).Split("gitdir:", 2)[1].Trim(), root);

// A linked worktree's .git is a file naming a host path the container cannot see. Mount the shared git
// directory, and mount over /workspace/.git a file that names its container path.
List<string> WorktreeArguments()
{
    string dotGit = Path.Join(root, ".git");
    if (!File.Exists(dotGit))
    {
        return [];
    }

    string gitDir = LinkedGitDirectory(dotGit);
    string common = CommonGitDirectory();
    string containerGitDir = $"{GitCommon}/{Path.GetRelativePath(common, gitDir).Replace('\\', '/')}";
    string gitFilePath = Path.Join(gitDir, "super-linter.gitfile");

    // Written just before the run, with no trailing newline: Super-Linter keeps it as part of the path it reads.
    gitFile = (gitFilePath, $"gitdir: {containerGitDir}");
    return
    [
        "-v", $"{common}:{GitCommon}",
        "-v", $"{gitFilePath}:{Workspace}/.git:ro",
        "-e", "GIT_CONFIG_COUNT=1",
        "-e", "GIT_CONFIG_KEY_0=safe.directory",
        "-e", "GIT_CONFIG_VALUE_0=*",
    ];
}
