// git-signing.cs - what stops git from signing a commit, shared by check-git-signing.cs (the pre-commit task)
// and bootstrap.cs --verify, which include it with #:include; it does not run alone.
//
// Needs commit.gpgsign=true, gpg.format=ssh and a user.signingkey that exists. When gpg.ssh.program is set
// (for example the 1Password signer) the program must exist; otherwise a real ssh-keygen signature proves
// the key signs. Setup steps: CONTRIBUTING.md, "Sign your commits".

using System.Diagnostics;

static class GitSigning
{
    // Null when signing works, or the first thing that stops it.
    public static string? Problem()
    {
        // --type=bool prints yes, on, 1 and any case of true as "true", as git itself reads them.
        if (Config("--type=bool commit.gpgsign") != "true")
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

        // 1Password and ssh-agent setups write the public key itself, plain or after "key::".
        bool literal = key.StartsWith("key::", StringComparison.Ordinal) || key.StartsWith("ssh-", StringComparison.Ordinal);
        string keyPath = literal ? string.Empty : key.StartsWith('~') ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), key[1..].TrimStart('/', '\\')) : key;
        if (!literal && !File.Exists(keyPath))
        {
            return $"signing key file not found: {key}";
        }

        string program = Config("gpg.ssh.program");
        if (program.Length > 0)
        {
            return Resolves(program) ? null : $"gpg.ssh.program not found: {program}";
        }

        return SignTestFile(literal ? key.Replace("key::", string.Empty, StringComparison.Ordinal) : keyPath, literal);
    }

    private static string? SignTestFile(string keyPath, bool literal)
    {
        // A literal key is a public key: sign through the agent, which holds the private half.
        string keyFile = keyPath;
        if (literal)
        {
            keyFile = Path.GetTempFileName();
            File.WriteAllText(keyFile, keyPath + "\n");
        }

        string file = Path.GetTempFileName();
        File.WriteAllText(file, "signing-test\n");
        try
        {
            ProcessStartInfo info = new("ssh-keygen") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string argument in (literal ? new[] { "-Y", "sign", "-U", "-f", keyFile, "-n", "git", file } : new[] { "-Y", "sign", "-f", keyFile, "-n", "git", file }))
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
            if (literal)
            {
                File.Delete(keyFile);
            }
        }
    }

    private static string Config(string key)
    {
        using Process process = Process.Start(new ProcessStartInfo("git", $"config --get {key}") { RedirectStandardOutput = true, UseShellExecute = false })!;
        string value = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return value;
    }

    // A program is a path, or a name found on PATH with the extensions Windows tries (PATHEXT: .exe, .cmd, .bat).
    private static bool Resolves(string program)
    {
        string[] extensions = OperatingSystem.IsWindows()
            ? [string.Empty, .. (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)]
            : [string.Empty];
        IEnumerable<string> folders = Path.IsPathRooted(program) || program.Contains('/') || program.Contains('\\')
            ? [string.Empty]
            : (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator);
        return folders.Any(folder => extensions.Any(extension => File.Exists(Path.Join(folder, program + extension))));
    }
}
