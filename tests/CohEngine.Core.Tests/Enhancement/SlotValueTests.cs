using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.Enhancement;

/// <summary>Port of the value-path tests of <c>test_enhancement.py</c>: GetScheduleMult and GetEnhancementEffect.</summary>
public sealed class SlotValueTests
{
    private const int RelNone = 0;
    private const int RelMinusThree = 1;
    private const int RelEven = 4;
    private const int RelPlusFive = 9;
    private const int GradeNone = 0;
    private const int GradeTrainingO = 1;
    private const int GradeDualO = 2;
    private const int GradeSingleO = 3;
    private const int CraftedRecharge = 43;
    private const int CraftedDefense = 30;
    private const int SchA = (int)Schedule.A;

    private static MathTables Tables => Fixtures.Store.Maths;

    private static IReadOnlyDictionary<int, EnhancementRecord> EnhDb => Fixtures.Store.EnhancementEffects;

    [Theory]
    [InlineData("InventO", false, 49, GradeNone, RelEven, SchA, 0.424)]
    [InlineData("InventO", false, 49, GradeNone, RelEven, (int)Schedule.B, 0.255)]
    [InlineData("InventO", false, 49, GradeNone, RelPlusFive, SchA, 0.530)]
    [InlineData("InventO", false, 49, GradeNone, RelMinusThree, SchA, 0.2968)]
    [InlineData("InventO", true, 49, GradeNone, RelEven, SchA, 0.530)]
    [InlineData("Normal", false, 0, GradeSingleO, RelEven, SchA, 0.333)]
    [InlineData("Normal", false, 0, GradeTrainingO, RelEven, SchA, 0.083)]
    [InlineData("Normal", false, 0, GradeDualO, RelEven, SchA, 0.167)]
    [InlineData("SpecialO", false, 49, GradeNone, RelEven, SchA, 0.333)]
    [InlineData("InventO", false, 999, GradeNone, RelEven, SchA, 0.435)]
    public void Schedule_mult_reads_the_table_for_the_type(string type, bool superior, int ioLevel, int grade, int relative, int schedule, double want)
    {
        Assert.Equal(want, SlotValue.ScheduleMult(type, superior, ioLevel, grade, relative, schedule, Tables), 1e-3);
    }

    [Theory]
    [InlineData("InventO", 49, GradeNone, RelEven, (int)Schedule.None)]
    [InlineData("InventO", 49, GradeNone, RelEven, (int)Schedule.Multiple)]
    [InlineData("InventO", 49, GradeNone, RelNone, SchA)]
    [InlineData("Normal", 0, GradeNone, RelEven, SchA)]
    [InlineData("None", 49, GradeNone, RelEven, SchA)]
    public void Schedule_mult_is_zero(string type, int ioLevel, int grade, int relative, int schedule)
    {
        Assert.Equal(0.0, SlotValue.ScheduleMult(type, false, ioLevel, grade, relative, schedule, Tables));
    }

    [Fact]
    public void Recharge_matches_aspect()
    {
        Assert.Equal(0.424, SlotValue.EnhancementEffect(EnhDb[CraftedRecharge], Slot(), "RechargeTime", -1, 1.0, Tables), 1e-3);
    }

    [Fact]
    public void Wrong_aspect_returns_zero()
    {
        Assert.Equal(0.0, SlotValue.EnhancementEffect(EnhDb[CraftedRecharge], Slot(), "Defense", -1, 1.0, Tables));
    }

    [Fact]
    public void Empty_slot_returns_zero()
    {
        Assert.Equal(0.0, SlotValue.EnhancementEffect(EnhDb[CraftedRecharge], Slot(-1), "RechargeTime", -1, 1.0, Tables));
    }

    [Fact]
    public void Buff_only_skipped_for_debuff_mag()
    {
        SlotRef slot = Slot(CraftedDefense);
        Assert.Equal(0.0, SlotValue.EnhancementEffect(EnhDb[CraftedDefense], slot, "Defense", -1, -1.0, Tables));
        Assert.True(SlotValue.EnhancementEffect(EnhDb[CraftedDefense], slot, "Defense", -1, 1.0, Tables) > 0.0);
    }

    [Fact]
    public void Debuff_only_skipped_for_buff_mag()
    {
        EnhancementRecord record = Synth(buffMode: "DeBuffOnly");
        Assert.Equal(0.0, SlotValue.EnhancementEffect(record, Slot(9999), "RechargeTime", -1, 1.0, Tables));
        Assert.True(SlotValue.EnhancementEffect(record, Slot(9999), "RechargeTime", -1, -1.0, Tables) > 0.0);
    }

    [Fact]
    public void Schedule_none_effect_skipped()
    {
        Assert.Equal(0.0, SlotValue.EnhancementEffect(Synth(schedule: (int)Schedule.None), Slot(9999), "RechargeTime", -1, 1.0, Tables));
    }

    [Fact]
    public void Sub_enh_mismatch_skipped()
    {
        EnhancementRecord record = Synth(enhance: "Mez", subId: 2);
        Assert.Equal(0.0, SlotValue.EnhancementEffect(record, Slot(9999), "Mez", 3, 1.0, Tables));
        Assert.True(SlotValue.EnhancementEffect(record, Slot(9999), "Mez", 2, 1.0, Tables) > 0.0);
    }

    [Fact]
    public void Multiplier_below_epsilon_ignored()
    {
        Assert.Equal(0.424, SlotValue.EnhancementEffect(Synth(multiplier: 0.0), Slot(9999), "RechargeTime", -1, 1.0, Tables), 1e-3);
    }

    [Fact]
    public void Multiplier_above_epsilon_scales()
    {
        double plain = SlotValue.EnhancementEffect(Synth(), Slot(9999), "RechargeTime", -1, 1.0, Tables);
        Assert.Equal(F32.Round(plain * F32.Round(0.5)), SlotValue.EnhancementEffect(Synth(multiplier: F32.Round(0.5)), Slot(9999), "RechargeTime", -1, 1.0, Tables));
    }

    [Fact]
    public void Single_recharge_io_contributes_0_424()
    {
        double perSlot = SlotValue.EnhancementEffect(EnhDb[CraftedRecharge], Slot(), "RechargeTime", -1, 1.0, Tables);
        Assert.Equal(1.272, Math.Round(3 * perSlot, 3));
        Assert.Equal(0.848, Math.Round(2 * perSlot, 3));
    }

    internal static SlotRef Slot(int enh = CraftedRecharge, int level = 9)
    {
        return new SlotRef { Level = level, IsInherent = false, Enh = enh, Grade = GradeNone, IoLevel = 49, RelativeLevel = RelEven };
    }

    private static EnhancementRecord Synth(string enhance = "RechargeTime", int subId = -1, int schedule = SchA, double multiplier = 1.0, string buffMode = "Any")
    {
        return new EnhancementRecord
        {
            Nid = 9999,
            StaticIndex = 9999,
            TypeName = "InventO",
            Superior = false,
            Effects = [new EnhEffect { Enhance = enhance, EnhanceSubId = subId, Schedule = schedule, Multiplier = multiplier, BuffMode = buffMode }],
            NidSet = -1,
            IsProc = false,
            PowerNid = -1,
        };
    }
}
