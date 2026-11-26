using System.Globalization;

using CohEngine.Core.Data;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Totals;

public sealed class SampleBuildCapsTests
{
    private const double Tolerance = 1e-6;

    private static readonly string Mids = FixtureStore.Locate(AppContext.BaseDirectory).MidsDirectory;

    public static TheoryData<string> Samples()
    {
        TheoryData<string> samples = [];
        foreach (string path in BuildFileFixtures.CuratedMbd())
        {
            samples.Add(BuildFileFixtures.SamplePath(path));
        }

        return samples;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void No_capped_total_of_a_curated_sample_is_above_its_archetype_cap(string sample)
    {
        ComputedBuild build = DumpScoring.Compute(Path.Join(Mids, "builds", BuildFileFixtures.FixtureName(BuildFileFixtures.Sample(sample))), Mids);
        ArchetypeDb classes = ArchetypeData.LoadArchetypes(Path.Join(Mids, "archetypes.json"));
        Archetype archetype = classes.Classes[classes.NidFromUidClass(build.ClassName)];
        TotalStatistics capped = build.TotalsCapped;
        (string Label, double Value, double Cap)[] limits =
        [
            ("BuffDam", capped.BuffDam, archetype.DamageCap - 1),
            ("BuffHaste", capped.BuffHaste, archetype.RechargeCap - 1),
            ("HPRegen", capped.HpRegen, archetype.RegenCap - 1),
            ("EndRec", capped.EndRec, archetype.RecoveryCap - 1),
            .. capped.Res.Select((value, index) => (string.Create(CultureInfo.InvariantCulture, $"Res[{index}]"), value, archetype.ResCap)),
            ("HPMax", capped.HpMax, archetype.HpCap),
        ];

        Assert.Empty(limits.Where(limit => limit.Value > limit.Cap + Tolerance).Select(limit => string.Create(CultureInfo.InvariantCulture, $"{limit.Label} {limit.Value} above the cap {limit.Cap}")));
    }
}
