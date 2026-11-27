using CohEngine.Core.BuildFile;
using CohEngine.Core.Data;

namespace CohEngine.TestSupport;

/// <summary>The build files under <c>samples/builds</c> and <c>tests/fixtures/buildfile</c>, listed and indexed once.</summary>
public static class BuildFileFixtures
{
    private static readonly FixtureStore Store = FixtureStore.Locate(AppContext.BaseDirectory);
    private static readonly Lazy<IReadOnlyDictionary<int, string>> PowerIndexValue = new(() => DbIndex.LoadPowerIndex(Path.Join(Store.MidsDirectory, "power_static_index.json")));
    private static readonly Lazy<IReadOnlyDictionary<int, EnhIndexEntry>> EnhIndexValue = new(() => DbIndex.LoadEnhancementIndex(Path.Join(Store.MidsDirectory, "enhancements.json")));
    private static readonly Lazy<MbdDb> DatabaseValue = new(() => Mbd.LoadMbdDb(Store.MidsDirectory));

    // One sample per archetype that has one, each with its Mids dump; the other sample builds are not curated.
    private static readonly string[] CuratedPaths =
    [
        "characters/account1/laser-accretion/main.mbd",
        "characters/account1/laser-alt-f4/main.mbd",
        "characters/account1/laser-fallout/main.mbd",
        "characters/account1/laser-hazmat/pve.mbd",
        "characters/account1/laser-nuke/main.mbd",
        "characters/account1/laser-prime/main.mbd",
        "characters/account1/laser-quantum/main.mbd",
        "characters/account2/hollowmind/main.mbd",
        "community/arachnos-widow/general/unknown-melee.mbd",
        "community/mastermind/general/unknown-petless.mbd",
        "community/stalker/general/unknown-fiery-invulnerability.mbd",
        "reference/arachnos-soldier/bane-mace/bane-leviathan.mbd",
    ];

    /// <summary>Gets the reference tables a <c>.mbd</c> is checked and resolved against.</summary>
    public static MbdDb Database => DatabaseValue.Value;

    /// <summary>Gets <c>tests/fixtures/buildfile</c>.</summary>
    public static string Root { get; } = Path.Join(Store.Root, "buildfile");

    /// <summary>Gets <c>samples/builds</c>.</summary>
    public static string Samples { get; } = Path.Join(Store.RepositoryRoot, "samples", "builds");

    /// <summary>Gets the StaticIndex to power full name map.</summary>
    public static IReadOnlyDictionary<int, string> PowerIndex => PowerIndexValue.Value;

    /// <summary>Gets the StaticIndex to enhancement map.</summary>
    public static IReadOnlyDictionary<int, EnhIndexEntry> EnhIndex => EnhIndexValue.Value;

    /// <summary>Gets the class table.</summary>
    public static ArchetypeDb Archetypes => Store.Archetypes;

    /// <summary>Lists the files under a directory, recursively, in ordinal order.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="pattern">The file pattern.</param>
    /// <returns>The full paths.</returns>
    public static IReadOnlyList<string> Files(string directory, string pattern)
    {
        return [.. Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
    }

    /// <summary>Lists the sample <c>.mbd</c> builds git tracks, so untracked working builds never reach a test.</summary>
    /// <param name="folder">A folder below <c>samples/builds</c>, with forward slashes, or empty for all of it.</param>
    /// <returns>The full paths in ordinal order.</returns>
    public static IReadOnlyList<string> SampleMbd(string folder = "")
    {
        return [.. GitFiles.Tracked(Store.RepositoryRoot, "samples/builds/" + folder).Where(path => path.EndsWith(".mbd", StringComparison.Ordinal))];
    }

    /// <summary>Lists the curated sample builds, one per archetype that has one.</summary>
    /// <returns>The full paths, in ordinal order of their paths under <c>samples/builds</c>.</returns>
    public static IReadOnlyList<string> CuratedMbd()
    {
        return [.. CuratedPaths.Select(Sample)];
    }

    /// <summary>Lists every file git tracks under <c>samples/builds</c>.</summary>
    /// <returns>The paths relative to <c>samples/builds</c>, with forward slashes, in ordinal order.</returns>
    public static IReadOnlyList<string> SampleFiles()
    {
        return [.. GitFiles.Tracked(Store.RepositoryRoot, "samples/builds").Select(SamplePath)];
    }

    /// <summary>Lists every sample and fixture <c>.mbd</c> build.</summary>
    /// <returns>The full paths.</returns>
    public static IReadOnlyList<string> AllMbd()
    {
        return [.. SampleMbd(), .. FixtureMbd()];
    }

    /// <summary>Gets a sample build by its path under <c>samples/builds</c>.</summary>
    /// <param name="relativePath">The path relative to <c>samples/builds</c>, with forward slashes.</param>
    /// <returns>The full path.</returns>
    public static string Sample(string relativePath)
    {
        return Path.Join(Samples, relativePath);
    }

    /// <summary>Gets a sample build's path relative to <c>samples/builds</c>.</summary>
    /// <param name="path">The full path.</param>
    /// <returns>The relative path with forward slashes.</returns>
    public static string SamplePath(string path)
    {
        return Path.GetRelativePath(Samples, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Gets the name a build's CLI captures and Mids dumps are kept under: for a sample build its path under
    /// <c>samples/builds</c> without the extension, each <c>/</c> written <c>__</c>; for a fixture build its file name without the extension.
    /// </summary>
    /// <param name="path">The build file.</param>
    /// <returns>The fixture name.</returns>
    public static string FixtureName(string path)
    {
        return path.StartsWith(Samples, StringComparison.Ordinal)
            ? Path.ChangeExtension(SamplePath(path), extension: null).Replace("/", "__", StringComparison.Ordinal)
            : Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>Lists the fixture <c>.mbd</c> builds the writer tests keep.</summary>
    /// <returns>The full paths.</returns>
    public static IReadOnlyList<string> FixtureMbd()
    {
        return Files(Path.Join(Root, "builds"), "*.mbd");
    }
}
