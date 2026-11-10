using CohEngine.Core;
using CohEngine.Core.Registry;

namespace CohEngine.TestSupport;

/// <summary>
/// The Python engine's captured CLI output under <c>tests/fixtures/cli</c>: one text file per command, build and format,
/// and the exit code beside it. The files are LF with no BOM, and the C# output is written with LF, so the two compare byte for byte.
/// </summary>
public sealed class CliCapture
{
    private readonly string directory;

    /// <summary>Initializes a new instance of the <see cref="CliCapture"/> class.</summary>
    /// <param name="store">The fixture store that locates <c>tests/fixtures</c>.</param>
    public CliCapture(FixtureStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Store = store;
        directory = Path.Join(store.Root, "cli");
    }

    /// <summary>Gets the fixture store.</summary>
    public FixtureStore Store { get; }

    /// <summary>Runs the CLI in process and returns its stdout and exit code.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The stdout with LF line endings and the exit code.</returns>
    public static (string Output, int ExitCode) Run(params string[] args)
    {
        return Run(null, args);
    }

    /// <summary>Runs the CLI in process with a <c>.mbd</c> source and returns its stdout and exit code.</summary>
    /// <param name="source">What resolves a <c>.mbd</c> file; null leaves the <c>.mbd</c> form unavailable.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The stdout with LF line endings and the exit code.</returns>
    public static (string Output, int ExitCode) Run(IMbdBuildSource? source, params string[] args)
    {
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new();
        int code = CliRunner.Run(args, output, error, source);
        return (output.ToString(), code);
    }

    /// <summary>Finds the <c>.mbd</c> build a capture was made from.</summary>
    /// <param name="name">The build's file name without <c>.mbd</c>.</param>
    /// <returns>The full path under <c>samples/builds</c> or <c>tests/fixtures/buildfile/builds</c>.</returns>
    public static string MbdPath(string name)
    {
        return BuildFileFixtures.AllMbd().Single(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.Ordinal));
    }

    /// <summary>Lists the builds a command has a capture for, in ordinal order.</summary>
    /// <param name="command">The command folder, for example <c>validate</c>.</param>
    /// <returns>The build names.</returns>
    public IReadOnlyList<string> Names(string command)
    {
        return [.. Directory.GetFiles(Path.Join(directory, command), "*.exit").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.Ordinal)];
    }

    /// <summary>Reads a captured output file with LF line endings.</summary>
    /// <param name="command">The command folder.</param>
    /// <param name="name">The build name, or the command name for <c>rules</c>.</param>
    /// <param name="extension">The extension: <c>txt</c> or <c>json</c>.</param>
    /// <returns>The captured stdout.</returns>
    public string Output(string command, string name, string extension)
    {
        return System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Join(directory, command, name + "." + extension)));
    }

    /// <summary>Reads the captured exit code.</summary>
    /// <param name="command">The command folder.</param>
    /// <param name="name">The build name, or the command name for <c>rules</c>.</param>
    /// <returns>The exit code.</returns>
    public int ExitCode(string command, string name)
    {
        return int.Parse(File.ReadAllText(Path.Join(directory, command, name + ".exit")).Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Gets a build's dump directory.</summary>
    /// <param name="name">The build name.</param>
    /// <returns>The directory.</returns>
    public string BuildDirectory(string name)
    {
        return Path.Join(Store.MidsDirectory, "builds", name);
    }
}
