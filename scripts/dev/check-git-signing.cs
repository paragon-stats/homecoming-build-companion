// check-git-signing.cs - fail when git commit signing is not set up, so an unsigned commit never starts.
//
// Run by the Husky pre-commit group:  dotnet run scripts/dev/check-git-signing.cs
//
// Needs commit.gpgsign=true, gpg.format=ssh and a user.signingkey that exists. When gpg.ssh.program is set
// (for example the 1Password signer) the program must exist; otherwise a real ssh-keygen signature proves
// the key signs. Setup steps: CONTRIBUTING.md, "Sign your commits".
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;

string? problem = Problem();
if (problem is null)
{
    Console.WriteLine("[OK] git commit signing is configured.");
    return 0;
}

Console.Error.WriteLine($".git/config:1:1: error: H-SIGN-001: git commit signing is not configured - {problem}");
Console.Error.WriteLine("This repository requires every commit to be signed. Set it up as CONTRIBUTING.md, \"Sign your commits\", describes:");
Console.Error.WriteLine("  git config --global gpg.format ssh");
Console.Error.WriteLine("  git config --global user.signingkey ~/.ssh/id_ed25519_signing.pub");
Console.Error.WriteLine("  git config --global commit.gpgsign true");
return 1;

static string? Problem()
{
    if (Config("commit.gpgsign") != "true")
    {
        return "commit.gpgsign is not set to 'true'";
    }

    string format = Config("gpg.format");
    if (format != "ssh")
    {
        return $"gpg.format is '{format}' (expected 'ssh')";
    }

    string key = Config("user.signingkey");
    if (key.Length == 0)
    {
        return "user.signingkey is not set";
    }

    string keyPath = key.StartsWith('~') ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), key[1..].TrimStart('/', '\\')) : key;
    if (!File.Exists(keyPath))
    {
        return $"signing key file not found: {key}";
    }

    string program = Config("gpg.ssh.program");
    if (program.Length > 0)
    {
        return File.Exists(program) || OnPath(program) ? null : $"gpg.ssh.program not found: {program}";
    }

    return SignTestFile(keyPath);
}

static string? SignTestFile(string keyPath)
{
    string file = Path.GetTempFileName();
    File.WriteAllText(file, "signing-test\n");
    try
    {
        ProcessStartInfo info = new("ssh-keygen") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "-Y", "sign", "-f", keyPath, "-n", "git", file })
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            process.Kill();
            return "signing timed out (stale SSH_AUTH_SOCK?); unset SSH_AUTH_SOCK and retry";
        }

        return process.ExitCode == 0 ? null : $"signing check failed: {error.GetAwaiter().GetResult().Trim()}";
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return "ssh-keygen not found; cannot verify signing";
    }
    finally
    {
        File.Delete(file);
        File.Delete(file + ".sig");
    }
}

static string Config(string key)
{
    using Process process = Process.Start(new ProcessStartInfo("git", $"config --get {key}") { RedirectStandardOutput = true, UseShellExecute = false })!;
    string value = process.StandardOutput.ReadToEnd().Trim();
    process.WaitForExit();
    return value;
}

static bool OnPath(string program) =>
    (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator)
        .Any(dir => File.Exists(Path.Join(dir, program)) || File.Exists(Path.Join(dir, program + ".exe")));
