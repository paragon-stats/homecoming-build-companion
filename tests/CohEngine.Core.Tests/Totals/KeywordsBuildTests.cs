using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Registry;
using CohEngine.Core.Tests.Parity;
using CohEngine.Core.Totals;
using CohEngine.Core.Totals.Phases;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Totals;

/// <summary>
/// The Mids dump under <c>regressions/keywords_endcost_owner_arch</c>: a Blaster whose Lightning_Bolt carries three
/// endurance reductions and whose pool power Arcane_Bolt carries an effect gated on the owner's archetype.
/// </summary>
public sealed class KeywordsBuildTests
{
    private const string Keyword = "power.base>endcost";

    private static readonly string Directory = Path.Join(Fixtures.Store.MidsDirectory, "regressions", "keywords_endcost_owner_arch");

    [Fact]
    public void The_totals_match_the_mids_dump()
    {
        TotalStatistics expected = new BuildFixture(Directory).Totals.Totals;

        TotalStatistics actual = DumpScoring.Build(Directory, DumpScoring.LoadShared(Fixtures.Store.MidsDirectory)).Result.Totals;

        EngineRun.CompareTotals(expected, actual, "keywords_endcost_owner_arch.Totals", EngineRun.NotCompared);
    }

    [Fact]
    public void Effects_that_read_the_endcost_keyword_have_the_magnitude_mids_dumped_only_with_the_slotted_cost()
    {
        BuildFixture fixture = new(Directory);
        BuiltBuild built = DumpScoring.Build(Directory, DumpScoring.LoadShared(Fixtures.Store.MidsDirectory));
        List<Power> included = [.. built.Powers.Where(power => power.StatInclude)];
        int archetypeIndex = Fixtures.Store.Archetypes.NidFromUidClass(built.ClassName);
        Dictionary<int, double> costs = PerPowerEnhancementMathPhase.ComputeBuffedEndCosts(
            included,
            built.Slots,
            built.EnhDb,
            built.Tables,
            built.Data.Config.ForceLevel,
            built.Result.IncarnateAddends!.Scalar,
            built.Result.GlobalEnhance.EndDiscount,
            Fixtures.Store.Archetypes.Classes[archetypeIndex].RechargeCap);
        MagContext baseContext = new()
        {
            Mods = Fixtures.Store.AttribMods,
            Classes = Fixtures.Store.Archetypes,
            ArchetypeIndex = archetypeIndex,
            Config = built.Data.Config,
            EnhanceNames = PerPowerEnhancementMathPhase.EnhanceNames(Fixtures.Store.Enums),
            Enums = Fixtures.Store.Enums,
        };
        MagContext buffed = baseContext with { BuffedEndCost = costs };
        int compared = 0;

        foreach (EnhancedPower want in fixture.EnhancedPowers)
        {
            Power power = included.SingleOrDefault(candidate => candidate.BuildIndex == want.BuildIndex)!;
            foreach (Effect effect in power?.Effects.Where(candidate => candidate.ExprMagnitude.Contains(Keyword, StringComparison.Ordinal)) ?? [])
            {
                double dumped = F32.Round(want.Effects.Single(candidate => candidate.Index == effect.Index).MathMag);

                Assert.Equal(dumped, F32.Round(buffed.Mag(effect, power!)));
                Assert.NotEqual(dumped, F32.Round(baseContext.Mag(effect, power!)));
                compared++;
            }
        }

        Assert.True(compared > 0);
        Assert.Contains(costs, cost => cost.Value < included.Single(power => power.BuildIndex == cost.Key).EndCost);
    }
}
