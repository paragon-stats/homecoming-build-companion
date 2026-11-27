using CohEngine.Core.BuildFile;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>
/// The parsing half of <c>test_sample_builds.py</c> plus the writer round trip: every sample and fixture <c>.mbd</c> reads,
/// writes and reads back to an equal model that holds the format invariants. The writer's exact bytes are pinned in
/// <see cref="MbdWriterTests"/>.
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

    public static TheoryData<string> Builds()
    {
        return [.. BuildFileFixtures.AllMbd()];
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
    public void The_rewritten_build_holds_the_format_invariants(string path)
    {
        MbdDocument document = Mbd.DumpMbd(Mbd.LoadMbd(path));

        Mbd.CheckMbdInvariants(document, BuildFileFixtures.Database);

        Assert.NotEmpty(document.PowerEntries);
    }

    [Fact]
    public void There_is_one_curated_sample_per_available_archetype()
    {
        IReadOnlyList<string> curated = BuildFileFixtures.CuratedMbd();
        IEnumerable<string> classes = curated.Select(path => Mbd.LoadMbd(path).ClassName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

        Assert.Equal(ExpectedClasses, classes, StringComparer.Ordinal);
        Assert.Equal(ExpectedClasses.Length, curated.Count);
    }
}
