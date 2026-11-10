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

    /// <summary>Lists every sample <c>.mbd</c>.</summary>
    /// <returns>The full paths.</returns>
    public static IReadOnlyList<string> SampleMbd()
    {
        return Files(Samples, "*.mbd");
    }

    /// <summary>Lists every sample and fixture <c>.mbd</c> build.</summary>
    /// <returns>The full paths.</returns>
    public static IReadOnlyList<string> AllMbd()
    {
        return [.. SampleMbd(), .. FixtureMbd()];
    }

    /// <summary>Gets the path of the Python writer's output for a build file.</summary>
    /// <param name="path">The build file.</param>
    /// <returns>The reference file under <c>roundtrip</c>.</returns>
    public static string RoundTripPath(string path)
    {
        return Path.Join(Root, "roundtrip", Path.GetFileName(path));
    }

    /// <summary>Lists the fixture <c>.mbd</c> builds the writer tests keep.</summary>
    /// <returns>The full paths.</returns>
    public static IReadOnlyList<string> FixtureMbd()
    {
        return Files(Path.Join(Root, "builds"), "*.mbd");
    }
}
