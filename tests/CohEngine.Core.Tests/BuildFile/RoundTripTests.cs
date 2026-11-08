using CohEngine.Core.BuildFile;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>
/// The parsing half of <c>test_sample_builds.py</c> plus the writer round trip: every sample and fixture <c>.mbd</c> reads,
/// writes and reads back to an equal model, and its written bytes equal the Python writer's output kept under
/// <c>tests/fixtures/buildfile/roundtrip</c>.
/// </summary>
public sealed class RoundTripTests
{
    // The classes with a committed sample; Defender, Peacebringer and Warshade have only .mxd files to take.
    private static readonly string[] ExpectedClasses =
    [
        "Class_Arachnos_Soldier",
        "Class_Arachnos_Widow",
        "Class_Blaster",
        "Class_Brute",
        "Class_Controller",
        "Class_Corruptor",
        "Class_Dominator",
        "Class_Mastermind",
        "Class_Scrapper",
        "Class_Sentinel",
        "Class_Stalker",
        "Class_Tanker",
    ];

    private static readonly string[] CuratedFolders = ["operator-builds", "community/Mids Archives"];

    public static TheoryData<string> Builds()
    {
        return [.. BuildFileFixtures.AllMbd()];
    }

    private static IEnumerable<string> Curated()
    {
        return CuratedFolders.SelectMany(folder => BuildFileFixtures.Files(Path.Combine(BuildFileFixtures.Samples, folder), "*.mbd"));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Reading_writing_and_reading_gives_an_equal_model(string path)
    {
        MbdBuild build = Mbd.LoadMbd(path);

        Assert.Equal(build, Mbd.ParseMbd(Mbd.DumpMbd(build)));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void The_written_bytes_equal_the_python_writers(string path)
    {
        byte[] written = Mbd.Serialize(Mbd.DumpMbd(Mbd.LoadMbd(path)));

        Assert.Equal(File.ReadAllBytes(BuildFileFixtures.RoundTripPath(path)), written);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void The_rewritten_build_holds_the_format_invariants(string path)
    {
        MbdDocument document = Mbd.DumpMbd(Mbd.LoadMbd(path));

        Mbd.CheckMbdInvariants(document, BuildFileFixtures.Database);

        Assert.NotEmpty(document.PowerEntries);
    }

    [Fact]
    public void There_is_one_curated_sample_per_available_archetype()
    {
        string[] curated = [.. Curated()];
        IEnumerable<string> classes = curated.Select(path => Mbd.LoadMbd(path).ClassName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

        Assert.Equal(ExpectedClasses, classes, StringComparer.Ordinal);
        Assert.Equal(ExpectedClasses.Length, curated.Length);
    }

    [Fact]
    public void Every_build_has_a_python_reference_file()
    {
        Assert.Equal(
            BuildFileFixtures.AllMbd().Select(Path.GetFileName).Order(StringComparer.Ordinal),
            BuildFileFixtures.Files(Path.Combine(BuildFileFixtures.Root, "roundtrip"), "*.mbd").Select(Path.GetFileName).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
