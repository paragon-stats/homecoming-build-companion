using CohEngine.Core.Chain;

namespace CohEngine.TestSupport;

/// <summary>
/// The Python <c>chain</c> captures under <c>tests/fixtures/cli/chain</c>: one set of files per capture name, named
/// <c>&lt;build&gt;[.pets].&lt;txt|json&gt;</c> with <c>.exit</c> and, when the run wrote to stderr, <c>.err</c> beside it.
/// </summary>
public static class ChainCaptures
{
    /// <summary>Lists the captures in ordinal order, each as <c>&lt;build&gt;[.pets].&lt;txt|json&gt;</c>.</summary>
    /// <param name="store">The fixture store.</param>
    /// <returns>The capture names.</returns>
    public static IReadOnlyList<string> Names(FixtureStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return
        [
            .. Directory.GetFiles(Path.Join(store.Root, "cli", "chain"), "*.exit")
                .Select(file => Path.GetFileName(file)[..^".exit".Length])
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>The Python run as it was captured: exit code, standard output, and standard error when it had any.</summary>
    /// <param name="store">The fixture store.</param>
    /// <param name="name">The capture name.</param>
    /// <returns>The captured run as one comparable text.</returns>
    public static string Expected(FixtureStore store, string name)
    {
        ArgumentNullException.ThrowIfNull(store);
        string stem = Path.Join(store.Root, "cli", "chain", name);
        string error = File.Exists(stem + ".err") ? File.ReadAllText(stem + ".err") : string.Empty;
        return Compose(int.Parse(File.ReadAllText(stem + ".exit").Trim(), System.Globalization.CultureInfo.InvariantCulture), File.ReadAllText(stem + ".out"), error);
    }

    /// <summary>Runs the C# command the way the capture was made, from the repository root with the relative build path.</summary>
    /// <param name="store">The fixture store.</param>
    /// <param name="name">The capture name.</param>
    /// <returns>The run as one comparable text.</returns>
    public static string Actual(FixtureStore store, string name)
    {
        ArgumentNullException.ThrowIfNull(store);
        bool json = name.EndsWith(".json", StringComparison.Ordinal);
        string stem = name[..name.LastIndexOf('.')];
        bool pets = stem.EndsWith(".pets", StringComparison.Ordinal);
        string build = pets ? stem[..^".pets".Length] : stem;
        List<string> args = [$"tests/fixtures/mids/builds/{build}", "--format", json ? "json" : "text"];
        if (pets)
        {
            args.AddRange(["--mids-db", "tests/fixtures/attribmod"]);
        }

        using StringWriter output = new();
        using StringWriter error = new();
        int code = ChainCommand.Run(args, output, error, store.RepositoryRoot);
        return Compose(code, output.ToString(), File.Exists(Path.Join(store.Root, "cli", "chain", name + ".err")) ? error.ToString() : string.Empty);
    }

    private static string Compose(int code, string output, string error)
    {
        string text = $"exit {code}\n{output}\n--- stderr ---\n{error}";
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace(@"tests\\fixtures\\mids\\builds\\", "tests/fixtures/mids/builds/", StringComparison.Ordinal)
            .Replace(@"tests\fixtures\mids\builds\", "tests/fixtures/mids/builds/", StringComparison.Ordinal);
    }
}
