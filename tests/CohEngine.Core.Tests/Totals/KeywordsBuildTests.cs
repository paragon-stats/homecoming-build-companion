using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;
using CohEngine.Core.Registry;
using CohEngine.Core.Tests.Parity;
using CohEngine.Core.Totals;
using CohEngine.Core.Totals.Phases;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Totals;

/// <summary>
/// The Mids dump under <c>regressions/keywords_endcost_owner_arch</c>: a Blaster whose Lightning_Bolt carries three
/// endurance reductions and whose pool power Arcane_Bolt carries effects gated on the owner's archetype.
/// </summary>
public sealed class KeywordsBuildTests
{
    private const string EndCost = "power.base>endcost";
    private const string OwnerArchetype = "source.owner>arch(";

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
        (BuildFixture fixture, List<Power> included, MagContext baseContext, MagContext buffed) = Contexts();
        int compared = 0;

        foreach ((EnhancedEffect dumped, Effect effect, Power power) in Effects(fixture, included, EndCost))
        {
            Assert.Equal(F32.Round(dumped.MathMag), F32.Round(buffed.Mag(effect, power)));
            Assert.NotEqual(F32.Round(dumped.MathMag), F32.Round(baseContext.Mag(effect, power)));
            compared++;
        }

        Assert.True(compared > 0);
        Assert.Contains(buffed.BuffedEndCost, cost => cost.Value < included.Single(power => power.BuildIndex == cost.Key).EndCost);
    }

    [Fact]
    public void Effects_that_test_the_owner_archetype_have_the_magnitude_mids_dumped()
    {
        (BuildFixture fixture, List<Power> included, _, MagContext buffed) = Contexts();
        List<(EnhancedEffect Dumped, Effect Effect, Power Power)> effects = Effects(fixture, included, OwnerArchetype);

        // Every such effect names another archetype than the build's Blaster, so Mids dumped zero for it.
        Assert.NotEmpty(effects);
        Assert.All(effects, item =>
        {
            Assert.Equal(0.0, F32.Round(item.Dumped.MathMag));
            Assert.Equal(0.0, F32.Round(buffed.Mag(item.Effect, item.Power)));
        });
        Assert.DoesNotContain(effects, item => item.Effect.ExprMagnitude.Contains("arch(Blaster)", StringComparison.Ordinal));
    }

    private static List<(EnhancedEffect Dumped, Effect Effect, Power Power)> Effects(BuildFixture fixture, List<Power> included, string keyword)
    {
        List<(EnhancedEffect Dumped, Effect Effect, Power Power)> found = [];
        foreach (EnhancedPower want in fixture.EnhancedPowers)
        {
            Power? power = included.SingleOrDefault(candidate => candidate.BuildIndex == want.BuildIndex);
            foreach (Effect effect in power?.Effects.Where(candidate => candidate.ExprMagnitude.Contains(keyword, StringComparison.Ordinal)) ?? [])
            {
                found.Add((want.Effects.Single(candidate => candidate.Index == effect.Index), effect, power!));
            }
        }

        return found;
    }

    private static (BuildFixture Fixture, List<Power> Included, MagContext Base, MagContext Buffed) Contexts()
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
            new PerPowerEnhancementMathPhase.EndFold(built.Result.GlobalEnhance.EndDiscount, Fixtures.Store.Archetypes.Classes[archetypeIndex].RechargeCap));
        MagContext baseContext = new()
        {
            Mods = Fixtures.Store.AttribMods,
            Classes = Fixtures.Store.Archetypes,
            ArchetypeIndex = archetypeIndex,
            Config = built.Data.Config,
            EnhanceNames = PerPowerEnhancementMathPhase.EnhanceNames(Fixtures.Store.Enums),
            Enums = Fixtures.Store.Enums,
            Expr = new ExpressionContext
            {
                PrevHpMax = 0.0,
                ArchetypeDisplayName = Fixtures.Store.Archetypes.Classes[archetypeIndex].DisplayName,
                BuildPowerNames = built.Powers.Select(power => power.FullName).ToHashSet(StringComparer.Ordinal),
                ActivePowerNames = included.Select(power => power.FullName).ToHashSet(StringComparer.Ordinal),
                VariableValues = built.Powers.ToLastWinsDictionary(power => power.FullName, power => power.VariableValue),
            },
        };
        return (fixture, included, baseContext, baseContext with { BuffedEndCost = costs });
    }
}
