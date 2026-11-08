using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.Enhancement;

/// <summary>
/// Port of <c>test_enh_pipeline.py</c>: aggregate per aspect, then ED once. The Hasten aggregates are the spec's
/// worked goldens; ED per slot then summed would give 1.272, never 0.9908.
/// </summary>
public sealed class PipelineTests
{
    private const int ForceLevel = 50;
    private const int HastenIndex = 6;
    private const int DeflectionIndex = 2;

    private static MathTables Tables => Fixtures.Store.Maths;

    [Theory]
    [InlineData("shield_scrapper_slotted", 0.9908)]
    [InlineData("hasten_2rech", 0.8332)]
    public void Hasten_recharge_aggregates(string build, double want)
    {
        double result = Aggregate(SlotsOf(build, HastenIndex), "RechargeTime");
        Assert.Equal(want, Math.Round(result, 4));
        Assert.True(result < 1.0);
    }

    [Fact]
    public void Deflection_defense_uses_schedule_b()
    {
        double result = Aggregate(SlotsOf("shield_scrapper_slotted", DeflectionIndex), "Defense");
        double preEd = 3 * Tables.MultIo[49][(int)Schedule.B];
        Assert.Equal(Ed.ApplyEd(Schedule.B, preEd, Tables), result, 1e-6);
        Assert.NotEqual(Ed.ApplyEd(Schedule.A, preEd, Tables), result, 1e-6);
    }

    [Fact]
    public void Slot_at_or_above_force_level_excluded()
    {
        double one = Aggregate([SlotValueTests.Slot(level: 9), SlotValueTests.Slot(level: 60)], "RechargeTime");
        Assert.Equal(0.424, one, 1e-3);
    }

    [Fact]
    public void Slot_exactly_at_force_level_excluded()
    {
        Assert.Equal(0.0, Aggregate([SlotValueTests.Slot(level: ForceLevel)], "RechargeTime"));
    }

    [Fact]
    public void No_matching_slots_returns_zero()
    {
        Assert.Equal(0.0, Aggregate([SlotValueTests.Slot(enh: -1)], "RechargeTime"));
    }

    [Fact]
    public void Pre_ed_addend_is_co_eded_and_skip_ed_returns_the_sum()
    {
        IReadOnlyList<SlotRef> slots = SlotsOf("shield_scrapper_slotted", HastenIndex);
        double slotted = Pipeline.AggregateAndEd(slots, "RechargeTime", Fixtures.Store.EnhancementEffects, ForceLevel, Tables, skipEd: true);
        double coEd = Pipeline.AggregateAndEd(slots, "RechargeTime", Fixtures.Store.EnhancementEffects, ForceLevel, Tables, preEdAddend: 0.25);
        Assert.Equal(Ed.ApplyEd(Schedule.A, F32.Round(slotted + 0.25), Tables), coEd);
        Assert.Equal(F32.Round(3 * F32.Round(Tables.MultIo[49][0])), slotted, 1e-6);
    }

    [Theory]
    [InlineData("Defense", -1, Schedule.B)]
    [InlineData("Range", -1, Schedule.B)]
    [InlineData("Resistance", -1, Schedule.B)]
    [InlineData("ToHit", -1, Schedule.B)]
    [InlineData("Interrupt", -1, Schedule.C)]
    [InlineData("RechargeTime", -1, Schedule.A)]
    [InlineData("Accuracy", -1, Schedule.A)]
    [InlineData("Damage", -1, Schedule.A)]
    [InlineData("Heal", -1, Schedule.A)]
    [InlineData("EnduranceDiscount", -1, Schedule.A)]
    [InlineData("Mez", -1, Schedule.A)]
    [InlineData("Mez", 1, Schedule.A)]
    [InlineData("Mez", 4, Schedule.D)]
    [InlineData("Mez", 5, Schedule.D)]
    public void Schedule_for_enhance_keys_on_the_eenhance_name(string aspect, int sub, Schedule want)
    {
        Assert.Equal(want, Pipeline.ScheduleForEnhance(aspect, sub));
    }

    private static IReadOnlyList<SlotRef> SlotsOf(string build, int index)
    {
        return Fixtures.Store.Build(build).Slots.Single(entry => entry.BuildIndex == index).Slots;
    }

    private static double Aggregate(IEnumerable<SlotRef> slots, string aspect)
    {
        return Pipeline.AggregateAndEd(slots, aspect, Fixtures.Store.EnhancementEffects, ForceLevel, Tables);
    }
}
