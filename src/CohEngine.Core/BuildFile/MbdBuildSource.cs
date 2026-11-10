using System.Globalization;
using System.Numerics;
using System.Text.Json;

using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;

namespace CohEngine.Core.BuildFile;

/// <summary>
/// Resolves a bare <c>.mbd</c> file for <c>validate</c> through the build-file reader. The header <c>Level</c> and
/// <c>PowerSets</c> are passed as written, because the <c>H-FMT</c> rules judge them as written. A <c>.mbd</c> has no
/// <c>Chosen</c> flag, so a granted pool sub-power counts as a pick (see <see cref="Mbd"/>).
/// </summary>
/// <param name="defaultMidsDirectory">The shared-dump directory used when <c>--mids-dir</c> is not given.</param>
public sealed class MbdBuildSource(string defaultMidsDirectory) : IMbdBuildSource
{
    private const string SolutionFile = "CohEngine.slnx";

    /// <inheritdoc/>
    public string DefaultMidsDirectory { get; } = defaultMidsDirectory;

    /// <summary>Finds the repository's <c>tests/fixtures/mids</c>, the only dump it ships, from a directory inside it.</summary>
    /// <param name="startDirectory">The directory to search upward from.</param>
    /// <returns>The directory beside the solution file, or under <paramref name="startDirectory"/> when no solution file is found above it.</returns>
    public static string LocateDefaultMidsDirectory(string startDirectory)
    {
        ArgumentNullException.ThrowIfNull(startDirectory);
        for (DirectoryInfo? directory = new(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Join(directory.FullName, SolutionFile)))
            {
                return Path.Join(directory.FullName, "tests", "fixtures", "mids");
            }
        }

        return Path.Join(startDirectory, "tests", "fixtures", "mids");
    }

    /// <inheritdoc/>
    /// <exception cref="EngineException">E72: the default shared-dump directory holds no <c>powers.json</c>.</exception>
    public MbdValidationInput Load(string path, string midsDirectory)
    {
        if (string.Equals(midsDirectory, DefaultMidsDirectory, StringComparison.Ordinal) && !File.Exists(Path.Join(midsDirectory, "powers.json")))
        {
            throw new EngineException($"{ErrorCodes.E72}: cannot read the default Mids dump directory {midsDirectory}, which holds no powers.json; pass --mids-dir <dir> with the shared dumps");
        }

        MbdDocument document = Mbd.ReadDocument(path);
        object? level = RawLevel(path);

        // A missing or null Level fails in ParseMbd (E118). Otherwise the header Level is judged as written, so the parsed integer is unused here; a number or bool token has no integer form to parse.
        MbdBuild build = Mbd.ParseMbd(level is null or string ? document : document with { Level = "0" });
        MbdDb database = Mbd.LoadMbdDb(midsDirectory);
        return new MbdValidationInput(
            Mbd.PowersFromBuild(build, database),
            Mbd.SlotsFromBuild(build, database),
            new MbdFormat(build.PowerSets, level, Mbd.IoBoundsByNid(database)));
    }

    // Python's json gives a str, a bool, an int of any size or a float; the rule prints its repr.
    private static object? RawLevel(string path)
    {
        using JsonDocument raw = JsonDocument.Parse(File.ReadAllBytes(path));
        if (!raw.RootElement.TryGetProperty("Level", out JsonElement level))
        {
            return null;
        }

        string token = level.GetRawText();
        return level.ValueKind switch
        {
            JsonValueKind.Number when token.AsSpan().IndexOfAny('.', 'e', 'E') >= 0 => double.Parse(token, CultureInfo.InvariantCulture),
            JsonValueKind.Number => BigInteger.Parse(token, CultureInfo.InvariantCulture),
            JsonValueKind.True or JsonValueKind.False => level.GetBoolean(),
            _ => level.GetString(),
        };
    }
}
