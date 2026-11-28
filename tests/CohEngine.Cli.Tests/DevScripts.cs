using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

/// <summary>Runs the file-based scripts in <c>scripts/dev</c> and the Husky wiring that calls them, as a developer's machine does.</summary>
internal static class DevScripts
{
    /// <summary>
    /// The test collection of every class that runs a script with <see cref="RunScript"/>. Each script build also builds
    /// src/CohEngine.Analyzers into that project's own obj and bin, so two script builds at once can collide on its
    /// output (CS2012); classes in one collection never run at the same time.
    /// </summary>
    internal const string Collection = "dev scripts";

    internal static readonly string RepositoryRoot = FixtureStore.Locate(AppContext.BaseDirectory).RepositoryRoot;

    /// <summary>Runs a program and returns its exit code and both output streams joined.</summary>
    /// <param name="workingDirectory">Where the program runs.</param>
    /// <param name="program">The executable.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The exit code and the combined output.</returns>
    internal static (int Exit, string Output) Run(string workingDirectory, string program, params string[] args)
    {
        return RunWithEnvironment(workingDirectory, program, new Dictionary<string, string?>(StringComparer.Ordinal), args);
    }

    /// <summary>Runs a program with extra environment variables; a null value removes the variable.</summary>
    /// <param name="workingDirectory">Where the program runs.</param>
    /// <param name="program">The executable.</param>
    /// <param name="environment">The variables to set or remove.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The exit code and the combined output.</returns>
    internal static (int Exit, string Output) RunWithEnvironment(string workingDirectory, string program, IReadOnlyDictionary<string, string?> environment, params string[] args)
    {
        (int exit, string output, _, _) = RunSeparately(workingDirectory, program, environment, input: null, args);
        return (exit, output);
    }

    /// <summary>Runs a program and returns its exit code, both streams joined, and each stream on its own.</summary>
    /// <param name="workingDirectory">Where the program runs.</param>
    /// <param name="program">The executable.</param>
    /// <param name="environment">The variables to set or remove.</param>
    /// <param name="input">What the program reads on standard input, or null to leave standard input alone.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The exit code, the combined output, standard output and standard error.</returns>
    internal static (int Exit, string Output, string StandardOutput, string StandardError) RunSeparately(string workingDirectory, string program, IReadOnlyDictionary<string, string?> environment, string? input, params string[] args)
    {
        // Without the checkout's global.json a scratch tree runs dotnet under the newest SDK installed, not the one the hooks get.
        string pin = Path.Join(workingDirectory, "global.json");
        if (Directory.Exists(workingDirectory) && !File.Exists(pin))
        {
            File.Copy(Path.Join(RepositoryRoot, "global.json"), pin);
        }

        ProcessStartInfo start = new(program)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            CreateNoWindow = true,
        };
        foreach (string argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        SetEnvironment(start, environment);
        using Process process = new() { StartInfo = start };
        return Collect(process, program, input);
    }

    private static void SetEnvironment(ProcessStartInfo start, IReadOnlyDictionary<string, string?> environment)
    {
        // A hook that runs the tests sets these to its own repository; the working directory must decide.
        foreach (string variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE" })
        {
            start.Environment.Remove(variable);
        }

        // The developer's own overrides must not change what a script does; GIT_CONFIG_COUNT and its pairs outrank the repository's config.
        foreach (string variable in start.Environment.Keys.Where(key => key.StartsWith("SUPER_LINTER_", StringComparison.Ordinal) || key.StartsWith("GIT_CONFIG_", StringComparison.Ordinal) || key is "FILTER_REGEX_EXCLUDE" or "JSCPD_LINTER_RULES" or "JSCPD_CONFIG_FILE").ToList())
        {
            start.Environment.Remove(variable);
        }

        foreach ((string name, string? value) in environment)
        {
            start.Environment[name] = value;
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = Path.Join(start.WorkingDirectory, ".test-global-gitconfig");
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";

        // The coverage collector profiles child .NET processes too; the scripts and the Husky tool are not the code under test.
        start.Environment["CORECLR_ENABLE_PROFILING"] = "0";
    }

    private static (int Exit, string Output, string StandardOutput, string StandardError) Collect(Process process, string program, string? input)
    {
        StringBuilder output = new();
        StringBuilder standardOutput = new();
        StringBuilder standardError = new();
        object gate = new();
        process.OutputDataReceived += (_, received) => Append(output, standardOutput, gate, received.Data);
        process.ErrorDataReceived += (_, received) => Append(output, standardError, gate, received.Data);
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"cannot start {program}: it is not on PATH", exception);
        }

        if (input is not null)
        {
            process.StandardInput.Write(input);
            process.StandardInput.Close();
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return (process.ExitCode, output.ToString(), standardOutput.ToString(), standardError.ToString());
    }

    /// <summary>Runs a script from the repository against a scratch directory.</summary>
    /// <param name="script">The script's file name under <c>scripts/dev</c>.</param>
    /// <param name="workingDirectory">The directory the script treats as the repository.</param>
    /// <param name="args">The arguments after <c>--</c>.</param>
    /// <returns>The exit code and the combined output.</returns>
    internal static (int Exit, string Output) RunScript(string script, string workingDirectory, params string[] args)
    {
        return RunScriptWithEnvironment(script, workingDirectory, new Dictionary<string, string?>(StringComparer.Ordinal), args);
    }

    /// <summary>Runs a script from the repository with extra environment variables.</summary>
    /// <param name="script">The script's file name under <c>scripts/dev</c>.</param>
    /// <param name="workingDirectory">The directory the script treats as the repository.</param>
    /// <param name="environment">The variables to set or remove.</param>
    /// <param name="args">The arguments after <c>--</c>.</param>
    /// <returns>The exit code and the combined output.</returns>
    internal static (int Exit, string Output) RunScriptWithEnvironment(string script, string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] args)
    {
        string path = Path.Join(RepositoryRoot, "scripts", "dev", script);
        (int exit, string output) = RunWithEnvironment(workingDirectory, "dotnet", environment, ["run", path, "--", .. args]);

        // A script that did not compile printed compiler errors and exited 1, which a test must not read as the check's verdict.
        Assert.False(output.Contains("The build failed. Fix the build errors and run again.", StringComparison.Ordinal), $"{script} did not build, so it gave no result: {output}");
        return (exit, output);
    }

    /// <summary>Builds a PATH that holds only the folders of the named programs, so a missing tool is missing.</summary>
    /// <param name="programs">The programs that must stay findable.</param>
    /// <returns>The PATH value.</returns>
    internal static string PathHolding(params string[] programs)
    {
        return string.Join(Path.PathSeparator, programs.Select(program => Path.GetDirectoryName(Locate(program))!).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Finds a program on the current PATH.</summary>
    /// <param name="program">The program name without extension.</param>
    /// <returns>The full path of the executable.</returns>
    internal static string Locate(string program)
    {
        string name = OperatingSystem.IsWindows() ? program + ".exe" : program;
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Join(folder, name))
            .First(File.Exists);
    }

    /// <summary>Finds the bash git itself runs hooks with; on Windows that is Git for Windows' bash, never the WSL launcher.</summary>
    /// <returns>The full path of bash.</returns>
    internal static string Bash() => Shell("bash");

    /// <summary>The POSIX sh that runs a <c>#!/bin/sh</c> hook: Git for Windows' own on Windows.</summary>
    internal static string Sh() => Shell("sh");

    private static string Shell(string name)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Locate(name);
        }

        // git --exec-path is <Git>/<mingw64 or ucrt64>/libexec/git-core.
        string execPath = Git(RepositoryRoot, "--exec-path").Trim();
        return Path.GetFullPath(Path.Join(execPath, "..", "..", "..", "usr", "bin", $"{name}.exe"));
    }

    /// <summary>Runs git in a directory and fails the test when git fails.</summary>
    /// <param name="workingDirectory">The repository.</param>
    /// <param name="args">The git arguments.</param>
    /// <returns>The combined output.</returns>
    internal static string Git(string workingDirectory, params string[] args)
    {
        (int exit, string output) = Run(workingDirectory, "git", args);
        Assert.True(exit == 0, string.Create(CultureInfo.InvariantCulture, $"git {string.Join(' ', args)} exited {exit}: {output}"));
        return output;
    }

    /// <summary>Makes a scratch git repository holding the repository's real hook, lint and editor config.</summary>
    /// <param name="tree">The scratch directory.</param>
    internal static void SeedRepository(TempTree tree)
    {
        Git(tree.Root, "init", "--quiet");
        Git(tree.Root, "config", "core.hooksPath", ".husky");
        foreach (string file in new[] { ".editorconfig", "Directory.Packages.props", ".markdownlint.yaml", ".github/linters/.markdownlint.yml", ".github/workflows/super-linter.yml", ".github/super-linter.env", ".config/dotnet-tools.json", ".husky/task-runner.json", "scripts/dev/check-text-files.cs", "scripts/dev/check-git-signing.cs", "scripts/dev/git-signing.cs", "scripts/dev/staged-files.cs", "scripts/git/check-commit-message.sh", "scripts/git/commit-types.txt" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tree.FullPath(file))!);
            File.Copy(Path.Join(RepositoryRoot, file), tree.FullPath(file));
        }
    }

    private static void Append(StringBuilder output, StringBuilder stream, object gate, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (gate)
        {
            output.AppendLine(line);
            stream.AppendLine(line);
        }
    }
}
