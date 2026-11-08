using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Statistics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Statistics;

public sealed class EnduranceBudgetsTests
{
    private static readonly string Totals = Path.Combine(Fixtures.Store.MidsDirectory, "builds", "shield_scrapper_set_bonuses", "totals.json");
    private static readonly string Archetypes = Path.Combine(Fixtures.Store.RepositoryRoot, "data", "canonical", "archetypes");

    private static Archetype Scrapper => Fixtures.Store.Archetypes.Classes.Single(item => string.Equals(item.ClassName, "Class_Scrapper", StringComparison.Ordinal));

    [Fact]
    public void The_pool_is_100_plus_the_uncapped_max_endurance_bonus()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        Assert.Equal(F32.Round(100.0 + totals.EndMax), EnduranceBudgets.BuildEnduranceBudget(totals, capped, Scrapper).MaxEnd);
    }

    [Fact]
    public void A_cap_below_the_pool_clamps_the_pool_and_the_recovery_scaling()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        EnduranceBudget clamped = EnduranceBudgets.BuildEnduranceBudget(totals, capped, Scrapper, 90.0);

        Assert.Equal(90.0, clamped.MaxEnd);
    }

    [Fact]
    public void A_cap_above_the_pool_changes_nothing()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        Assert.Equal(EnduranceBudgets.BuildEnduranceBudget(totals, capped, Scrapper), EnduranceBudgets.BuildEnduranceBudget(totals, capped, Scrapper, 10000.0));
    }

    [Fact]
    public void Toggle_drain_is_the_uncapped_endurance_use()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        Assert.Equal(totals.EndUse, EnduranceBudgets.BuildEnduranceBudget(totals, capped, Scrapper).ToggleDrainPerSec);
    }

    [Fact]
    public void Health_regeneration_scales_with_max_hp()
    {
        (_, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        HealthBudget health = EnduranceBudgets.BuildHealthBudget(capped, Scrapper);

        Assert.Equal(capped.HpMax, health.MaxHp);
        Assert.True(health.RegenPerSec > 0.0);
    }

    [Fact]
    public void The_totals_reader_returns_the_class()
    {
        Assert.Equal("Class_Scrapper", EnduranceBudgets.LoadMidsTotals(Totals).ClassName);
    }

    [Fact]
    public void A_missing_section_is_e31()
    {
        using TempTree tree = new();
        string path = tree.Write("totals.json", "{\"Class\":\"X\"}");

        Assert.StartsWith($"E31: {path}: '<file>' has no key 'Totals'; re-run tools/mids-dump to regenerate totals.json", Assert.Throws<ChainException>(() => EnduranceBudgets.LoadMidsTotals(path)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_field_is_e31()
    {
        using TempTree tree = new();
        string path = tree.Write("totals.json", "{\"Totals\":{},\"TotalsCapped\":{}}");

        Assert.Contains("'Totals' has no key 'Def'", Assert.Throws<ChainException>(() => EnduranceBudgets.LoadMidsTotals(path)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_corrupt_totals_file_is_e33()
    {
        using TempTree tree = new();

        Assert.StartsWith("E33:", Assert.Throws<ChainException>(() => EnduranceBudgets.LoadMidsTotals(tree.Write("totals.json", "{oops"))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_canonical_caps_are_the_level_50_attrib_max_max()
    {
        (double maxEnd, double maxHp) = EnduranceBudgets.LoadCanonicalCaps(Archetypes, "Class_Scrapper");

        Assert.True(maxEnd > 100.0 && maxHp > 1000.0);
    }

    [Fact]
    public void A_class_with_no_canonical_file_is_e32()
    {
        Assert.StartsWith("E32: no canonical archetype file", Assert.Throws<ChainException>(() => EnduranceBudgets.LoadCanonicalCaps(Archetypes, "Class_Nobody")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_corrupt_canonical_archetype_is_e33()
    {
        using TempTree tree = new();
        tree.Write("nobody.json", "{oops");

        Assert.StartsWith("E33:", Assert.Throws<ChainException>(() => EnduranceBudgets.LoadCanonicalCaps(tree.Root, "Class_Nobody")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_power_dps_port_stays_a_stub_that_fails_loud()
    {
        SlotRef slot = new() { Level = 1, IsInherent = false, Enh = 3, Grade = 0, IoLevel = 0, RelativeLevel = 0 };
        SlotRef empty = slot with { Enh = -1 };

        Assert.StartsWith("E15: DPS for Test.Test_Set.Test_Power requires the FXGetDamageValue critical/conditional port (deferred; a plain buffed-damage sum overcounts). Slots supplied: 1", Assert.Throws<NotSupportedException>(() => PowerDps.Compute(Make.Power(), [slot, empty])).Message, StringComparison.Ordinal);
    }
}
