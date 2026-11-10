using System.Text.Json;

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
    public MbdValidationInput Load(string path, string midsDirectory)
    {
        MbdDocument document = Mbd.ReadDocument(path);
        MbdBuild build = Mbd.ParseMbd(document);
        MbdDb database = Mbd.LoadMbdDb(midsDirectory);
        return new MbdValidationInput(
            Mbd.PowersFromBuild(build, database),
            Mbd.SlotsFromBuild(build, database),
            new MbdFormat(build.PowerSets, RawLevel(path), Mbd.IoBoundsByNid(database)));
    }

    // The converter that reads a Level number as text has already accepted the file, so the token is a string or an integer.
    private static object RawLevel(string path)
    {
        using JsonDocument raw = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement level = raw.RootElement.GetProperty("Level");
        return level.ValueKind == JsonValueKind.Number ? level.GetInt64() : level.GetString()!;
    }
}
