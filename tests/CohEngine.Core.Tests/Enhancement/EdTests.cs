using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;
using CohEngine.Core.Registry;

namespace CohEngine.Core.Tests.Enhancement;

/// <summary>Port of <c>test_ed.py</c>, <c>test_pass3.py</c> and <c>test_enh_aspects.py</c>.</summary>
public sealed class EdTests
{
    private static readonly GlobalEnhance Global = new(F32.Round(0.425), F32.Round(0.0375), F32.Round(0.1), F32.Round(0.2), F32.Round(0.3));

    private static MathTables Tables => Fixtures.Store.Maths;

    [Theory]
    [InlineData(Schedule.None)]
    [InlineData(Schedule.Multiple)]
    public void None_and_multiple_schedules_return_zero(Schedule schedule)
    {
        Assert.Equal(0.0, Ed.ApplyEd(schedule, 1.0, Tables));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.7)]
    public void Values_at_or_below_the_first_threshold_are_unreduced(double value)
    {
        Assert.Equal(value, Ed.ApplyEd(Schedule.A, value, Tables), 1e-6);
    }

    [Fact]
    public void Second_band_slope_is_0_9()
    {
        Assert.Equal(0.8332, Math.Round(Ed.ApplyEd(Schedule.A, 0.848, Tables), 4));
    }

    [Fact]
    public void Third_band_slope_is_0_7()
    {
        Assert.Equal(0.915, Ed.ApplyEd(Schedule.A, 0.95, Tables), 1e-6);
    }

    [Fact]
    public void Above_the_third_threshold_slope_is_0_15()
    {
        Assert.Equal(0.9908, Math.Round(Ed.ApplyEd(Schedule.A, 1.272, Tables), 4));
    }

    [Fact]
    public void Schedule_b_thresholds()
    {
        Assert.Equal(0.40, Ed.ApplyEd(Schedule.B, 0.40, Tables), 1e-6);
        Assert.Equal(0.445, Ed.ApplyEd(Schedule.B, 0.45, Tables), 1e-6);
    }

    [Fact]
    public void Schedule_d_thresholds()
    {
        Assert.Equal(1.0, Ed.ApplyEd(Schedule.D, 1.0, Tables), 1e-6);
        Assert.Equal(1.2 + (0.3 * 0.9) + (0.3 * 0.7) + (0.2 * 0.15), Ed.ApplyEd(Schedule.D, 2.0, Tables), 1e-6);
    }

    [Theory]
    [InlineData(3, 1.272, 0.9908)]
    [InlineData(2, 0.848, 0.8332)]
    public void Hasten_goldens_from_level_50_recharge_ios(int count, double preEdWant, double want)
    {
        double preEd = count * Tables.MultIo[49][(int)Schedule.A];
        Assert.Equal(preEdWant, preEd, 1e-6);
        Assert.Equal(want, Math.Round(Ed.ApplyEd(Schedule.A, preEd, Tables), 4));
    }

    [Theory]
    [InlineData(Aspect.Defense, -1, Schedule.B)]
    [InlineData(Aspect.Range, -1, Schedule.B)]
    [InlineData(Aspect.Resistance, -1, Schedule.B)]
    [InlineData(Aspect.ToHit, -1, Schedule.B)]
    [InlineData(Aspect.Interrupt, -1, Schedule.C)]
    [InlineData(Aspect.Mez, -1, Schedule.A)]
    [InlineData(Aspect.Mez, 1, Schedule.A)]
    [InlineData(Aspect.Mez, 4, Schedule.D)]
    [InlineData(Aspect.Mez, 5, Schedule.D)]
    [InlineData(Aspect.Accuracy, -1, Schedule.A)]
    [InlineData(Aspect.Damage, -1, Schedule.A)]
    [InlineData(Aspect.EnduranceDiscount, -1, Schedule.A)]
    [InlineData(Aspect.Heal, -1, Schedule.A)]
    [InlineData(Aspect.RechargeTime, -1, Schedule.A)]
    public void Schedule_for_maps_the_curated_aspects(Aspect aspect, int mezSub, Schedule want)
    {
        Assert.Equal(want, Ed.ScheduleFor(aspect, mezSub));
    }

    [Fact]
    public void Fold_is_aggregate_plus_global_plus_one()
    {
        Assert.Equal(F32.Round(F32.Round(0.9908 + 0.0) + 1.0), AspectRouting.FoldDivisor(0.9908, 0.0, "RechargeTime"));
        Assert.Equal(F32.Round(1.425), AspectRouting.FoldDivisor(0.0, 0.425, "RechargeTime"));
    }

    [Fact]
    public void Ignored_aspect_skips_the_fold()
    {
        Assert.Equal(1.0, AspectRouting.FoldDivisor(5.0, 3.0, "RechargeTime", ["RechargeTime"]));
        Assert.Equal(F32.Round(1.425), AspectRouting.FoldDivisor(0.0, 0.425, "RechargeTime", ["Range"]));
    }

    [Fact]
    public void Cap_clamps_the_multiplier()
    {
        Assert.Equal(5.0, AspectRouting.FoldDivisor(9.0, 0.0, "RechargeTime", cap: 5.0));
        Assert.Equal(F32.Round(1.9908), AspectRouting.FoldDivisor(0.9908, 0.0, "RechargeTime", cap: 5.0));
    }

    [Fact]
    public void Ignored_aspect_ignores_the_cap_too()
    {
        Assert.Equal(1.0, AspectRouting.FoldDivisor(9.0, 3.0, "RechargeTime", ["RechargeTime"], 5.0));
    }

    [Fact]
    public void The_handlers_are_the_five_registered_scalar_aspects()
    {
        AspectHandler[] handlers = [AspectRouting.Accuracy, AspectRouting.End, AspectRouting.Interrupt, AspectRouting.Range, AspectRouting.Recharge];
        Assert.Equal([.. RuleRegistry.EnhancementAspects.Select(rule => rule.Id)], handlers.Select(handler => handler.Aspect).ToArray());
        Assert.All(handlers, handler => Assert.Equal(handler.Capped, ReferenceEquals(handler, AspectRouting.Recharge)));
    }

    [Fact]
    public void Global_term_reads_the_handlers_field()
    {
        Assert.Equal(F32.Round(0.425), AspectRouting.GlobalTerm(AspectRouting.Recharge, Global));
        Assert.Equal(F32.Round(0.0375), AspectRouting.GlobalTerm(AspectRouting.End, Global));
        Assert.Equal(F32.Round(0.1), AspectRouting.GlobalTerm(AspectRouting.Accuracy, Global));
        Assert.Equal(F32.Round(0.2), AspectRouting.GlobalTerm(AspectRouting.Interrupt, Global));
        Assert.Equal(F32.Round(0.3), AspectRouting.GlobalTerm(AspectRouting.Range, Global));
    }

    [Fact]
    public void Only_recharge_is_capped()
    {
        Assert.Equal(5.0, AspectRouting.CapFor(AspectRouting.Recharge, 5.0));
        Assert.Null(AspectRouting.CapFor(AspectRouting.End, 5.0));
    }

    [Fact]
    public void Fold_scalar_reproduces_the_pass3_divisor()
    {
        double divisor = AspectRouting.FoldScalar(AspectRouting.End, F32.Round(0.2), F32.Round(0.0375), [], 5.0);
        Assert.Equal(F32.Round(F32.Round(F32.Round(0.2 + 0.0375) + 1.0)), divisor);
    }

    [Fact]
    public void Fold_scalar_clamps_recharge_to_cap()
    {
        Assert.Equal(5.0, AspectRouting.FoldScalar(AspectRouting.Recharge, F32.Round(9.0), 0.0, [], 5.0));
    }

    [Fact]
    public void Fold_scalar_ignored_aspect_returns_one()
    {
        Assert.Equal(1.0, AspectRouting.FoldScalar(AspectRouting.Recharge, F32.Round(9.0), 0.0, ["RechargeTime"], 5.0));
    }

    [Fact]
    public void Fold_scalar_post_ed_extra_adds_after_ed()
    {
        double plain = AspectRouting.FoldScalar(AspectRouting.End, F32.Round(0.2), F32.Round(0.0375), [], 5.0);
        double extra = AspectRouting.FoldScalar(AspectRouting.End, F32.Round(0.2), F32.Round(0.0375), [], 5.0, F32.Round(0.5));
        Assert.Equal(F32.Round(plain + 0.5), extra);
    }
}
