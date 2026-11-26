using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

/// <summary>Runs the file-based scripts in <c>scripts/dev</c> and the Husky wiring that calls them, as a developer's machine does.</summary>
internal static class DevScripts
{
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
        ProcessStartInfo start = new(program)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        // A hook that runs the tests sets these to its own repository; the working directory must decide.
        foreach (string variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE" })
        {
            start.Environment.Remove(variable);
        }

        // The developer's own overrides must not change what a script does.
        foreach (string variable in start.Environment.Keys.Where(key => key.StartsWith("SUPER_LINTER_", StringComparison.Ordinal) || key is "FILTER_REGEX_EXCLUDE" or "JSCPD_LINTER_RULES" or "JSCPD_CONFIG_FILE").ToList())
        {
            start.Environment.Remove(variable);
        }

        foreach ((string name, string? value) in environment)
        {
            start.Environment[name] = value;
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = Path.Join(workingDirectory, ".test-global-gitconfig");
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";

        // The coverage collector profiles child .NET processes too; the scripts and the Husky tool are not the code under test.
        start.Environment["CORECLR_ENABLE_PROFILING"] = "0";

        using Process process = new() { StartInfo = start };
        StringBuilder output = new();
        object gate = new();
        process.OutputDataReceived += (_, received) => Append(output, gate, received.Data);
        process.ErrorDataReceived += (_, received) => Append(output, gate, received.Data);
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"cannot start {program}: it is not on PATH", exception);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return (process.ExitCode, output.ToString());
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
        return RunWithEnvironment(workingDirectory, "dotnet", environment, ["run", path, "--", .. args]);
    }

    /// <summary>Builds a PATH that holds only the folders of the named programs, so a missing tool is missing.</summary>
    /// <param name="programs">The programs that must stay findable.</param>
    /// <returns>The PATH value.</returns>
    internal static string PathHolding(params string[] programs)
    {
        List<string> folders = [.. programs.Select(program => Path.GetDirectoryName(Locate(program))!)];
        if (OperatingSystem.IsWindows())
        {
            folders.Add(Path.Join(Environment.GetEnvironmentVariable("SystemRoot"), "System32"));
        }

        return string.Join(Path.PathSeparator, folders.Distinct(StringComparer.OrdinalIgnoreCase));
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
        foreach (string file in new[] { ".editorconfig", ".markdownlint.yaml", ".github/linters/.markdownlint.yml", ".github/workflows/super-linter.yml", ".github/super-linter.env", ".config/dotnet-tools.json", ".husky/task-runner.json", "global.json", "scripts/dev/check-text-files.cs", "scripts/dev/check-git-signing.cs" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tree.FullPath(file))!);
            File.Copy(Path.Join(RepositoryRoot, file), tree.FullPath(file));
        }
    }

    private static void Append(StringBuilder output, object gate, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (gate)
        {
            output.AppendLine(line);
        }
    }
}
