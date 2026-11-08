using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;

using static CohEngine.Core.Tests.Totals.TotalsMake;

namespace CohEngine.Core.Tests.Totals;

/// <summary>Port of <c>test_base_totals.py</c> TestGbdTotals: the totals fold, its scalars and the caps.</summary>
public sealed class ActorTotalsCalculatorTests
{
    private static readonly Effect Base = Make.Effect;

    public static TheoryData<string[], double> ConditionalCases => new()
    {
        { ["Pool.Fighting.Kick"], 0.25 },
        { [], 0.0 },
    };

    private static ServerData Server => Fixtures.Store.ServerData;

    [Fact]
    public void Unknown_class_name_raises()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => Pipeline.ComputeBaseTotals([], "Class_Nope", TinyData()));
        Assert.StartsWith("E09", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Toggle_end_use_and_fly_gating()
    {
        // A toggle with no ActivatePeriod costs its EndCost; (1 + 0.5) * 31.5 fly speed stands because a Fly effect is up.
        Power flyer = Make.Power(Base with { EffectType = "Fly", Scale = 1.0 }, Base with { Index = 1, EffectType = "SpeedFlying", Scale = 0.5 }) with { EndCost = 0.26 };
        BaseTotals result = Compute([flyer]);
        Assert.Equal(F32.Round(0.26), result.Totals.EndUse);
        Assert.Equal(F32.Round(47.25), result.Totals.FlySpd);
    }

    [Fact]
    public void Toggle_with_activate_period_divides_by_it()
    {
        Power toggle = Make.Power() with { EndCost = F32.Round(0.26), ActivatePeriod = F32.Round(0.5) };
        Assert.Equal(F32.Round(F32.Round(0.26) / F32.Round(0.5)), Compute([toggle]).Totals.EndUse);
    }

    [Fact]
    public void Fly_speed_zeroed_without_fly_effect()
    {
        Assert.Equal(0.0, Compute([Make.Power(Base with { EffectType = "SpeedFlying", Scale = 0.5 })]).Totals.FlySpd);
    }

    [Fact]
    public void Speed_buff_floor_and_maxmax_clamp()
    {
        BaseTotals result = Compute([Make.Power(Base with { EffectType = "SpeedRunning", Scale = -5.0 }, Base with { Index = 1, EffectType = "SpeedJumping", Scale = 50.0 })]);

        // Floor at -90%: (1 - 0.9) * 21 = 2.1; jump clamps to the MaxMax speed.
        Assert.Equal(2.1, result.Totals.RunSpd, 1e-5);
        Assert.Equal(Server.MaxMaxJumpSpeed, result.Totals.JumpSpd);
    }

    [Fact]
    public void Defense_index0_folds_into_typed_channels()
    {
        BuffsX buffs = new(Enums);
        buffs.Defense[0] = F32.Round(0.05);
        buffs.Defense[1] = F32.Round(0.10);
        BaseTotals result = Fold(buffs);
        Assert.Equal(F32.Round(0.05), result.Totals.Def[0]);
        Assert.Equal(F32.Round(F32.Round(0.10) + F32.Round(0.05)), result.Totals.Def[1]);
        Assert.Equal(F32.Round(0.05), result.Totals.Def[2]);
    }

    [Theory]
    [InlineData(new[] { 0.1, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 }, 1.0)]
    [InlineData(new[] { 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 1.0 }, 0.1)]
    [InlineData(new[] { -0.5, 0.1, 0.1, 0.1, 0.1, 0.1, 1.0 }, 1.0)]
    public void Buff_dam_picks_from_min_average_max(double[] damage, double want)
    {
        BuffsX buffs = new(Enums);
        for (int i = 0; i < damage.Length; i++)
        {
            buffs.Damage[i + 1] = F32.Round(damage[i]);
        }

        Assert.Equal(F32.Round(want), Fold(buffs).Totals.BuffDam);
    }

    [Fact]
    public void Buff_dam_is_seeded_from_self_enhance_when_the_buffs_hold_none()
    {
        BuffsX selfEnhance = new(Enums);
        selfEnhance.Damage[1] = F32.Round(0.2);
        Assert.Equal(F32.Round(0.2), Fold(new BuffsX(Enums), selfEnhance).Totals.BuffDam);
    }

    [Fact]
    public void Caps_clamp_res_hp_regen_and_perception()
    {
        BuffsX buffs = new(Enums);
        buffs.Resistance[1] = F32.Round(0.9);
        buffs.Effect[Stat("HPMax")] = 900.0;
        buffs.Effect[Stat("HPRegen")] = 25.0;
        buffs.Effect[Stat("EndRec")] = 9.0;
        buffs.Effect[Stat("Haste")] = 9.0;
        buffs.Effect[Stat("Perception")] = 5.0;
        buffs.Effect[Stat("JumpHeight")] = 100.0;
        buffs.Effect[Stat("Absorb")] = 5000.0;
        BaseTotals result = Fold(buffs);
        TotalStatistics capped = result.TotalsCapped;
        Assert.Equal(F32.Round(0.9), result.Totals.Res[1]);
        Assert.Equal(F32.Round(0.75), capped.Res[1]);
        Assert.Equal(F32.Round(1900.0), result.Totals.HpMax);
        Assert.Equal(F32.Round(1500.0), capped.HpMax);
        Assert.Equal(F32.Round(1500.0), capped.Absorb);
        Assert.Equal(F32.Round(19.0), capped.HpRegen);
        Assert.Equal(F32.Round(4.0), capped.EndRec);
        Assert.Equal(F32.Round(4.0), capped.BuffHaste);
        Assert.Equal(F32.Round(1153.0), capped.Perception);
        Assert.Equal(Server.MaxJumpHeight, capped.JumpHeight);

        // The verbatim Assign quirk: the Max speeds are not copied into TotalsCapped.
        Assert.Equal((0.0, 0.0, 0.0), (capped.MaxRunSpd, capped.MaxJumpSpd, capped.MaxFlySpd));
    }

    [Fact]
    public void Hp_cap_zero_skips_hp_clamp()
    {
        BuffsX buffs = new(Enums);
        buffs.Effect[Stat("HPMax")] = 9000.0;
        Archetype uncapped = EffectMagnitudeTests.TinyClasses.Classes[0] with { HpCap = 0.0 };
        Assert.Equal(F32.Round(10000.0), Fold(buffs, archetype: uncapped).TotalsCapped.HpMax);
    }

    [Fact]
    public void Pvp_mode_applies_defense_diminishing_returns()
    {
        BaseTotals result = Compute([Make.Power(Base with { DamageType = "Smashing", Scale = 0.4 })], config: Pvp);
        Assert.Equal(ActorTotalsCalculator.CalculatePvpDr(F32.Round(0.4)), result.Totals.Def[1]);
    }

    [Fact]
    public void Calculate_pvp_dr_formula()
    {
        // The bracket is narrowed before the final float multiply: two single-precision rounding steps.
        double value = F32.Round(0.45);
        double factor = F32.Round(1.0 - (Math.Abs(Math.Atan(F32.Round(1.2) * value)) * (2.0 / Math.PI) * F32.Round(1.0)));
        Assert.Equal(F32.Round(value * factor), ActorTotalsCalculator.CalculatePvpDr(value));
    }

    [Theory]
    [MemberData(nameof(ConditionalCases))]
    public void Conditional_effect_counts_when_its_conditions_hold(string[] active, double want)
    {
        Effect conditional = Base with { EffectType = "Regeneration", Scale = 0.25, ActiveConditionalsCount = 1, Conditionals = [Make.Condition("Active:Pool.Fighting.Kick", "True")] };
        ExpressionContext expr = Expr(0.0, active);
        Assert.Equal(F32.Round(want), Compute([Make.Power(conditional)], new Pipeline.BuildOptions { Expr = expr }).Totals.HpRegen);
    }

    [Fact]
    public void Expression_reads_the_previous_max_hit_points()
    {
        Effect absorb = Base with { EffectType = "Absorb", AttribType = "Expression", ExprMagnitude = "0.15*source>Max.kHitPoints" };
        Assert.Equal(F32.Round(150.0), Compute([Make.Power(absorb)], new Pipeline.BuildOptions { Expr = Expr(1000.0, []) }).Totals.Absorb);
    }

    [Fact]
    public void Stat_exclude_powers_contribute_nothing()
    {
        BaseTotals result = Compute([Make.Power(Base with { Scale = 1.0 }) with { StatInclude = false, ToggleCost = 0.5 }]);
        Assert.Equal(0.0, result.Totals.EndUse);
        Assert.All(result.Totals.Def, value => Assert.Equal(0.0, value));
    }

    [Fact]
    public void Enum_maps_size()
    {
        Assert.Equal(16, Enums.Size("eDamage"));
        Assert.Equal(20, Enums.Size("eMez"));
    }

    private static ExpressionContext Expr(double prevHpMax, string[] active)
    {
        return new ExpressionContext
        {
            PrevHpMax = prevHpMax,
            BuildPowerNames = active.ToHashSet(StringComparer.Ordinal),
            ActivePowerNames = active.ToHashSet(StringComparer.Ordinal),
            VariableValues = new Dictionary<string, int>(StringComparer.Ordinal),
        };
    }

    private static BaseTotals Fold(BuffsX selfBuffs, BuffsX? selfEnhance = null, Archetype? archetype = null)
    {
        return ActorTotalsCalculator.Calculate(new TotalsRequest
        {
            Included = [],
            SelfBuffs = selfBuffs,
            SelfEnhance = selfEnhance ?? new BuffsX(Enums),
            Archetype = archetype ?? EffectMagnitudeTests.TinyClasses.Classes[0],
            Context = Context(),
            Server = Server,
            ToggleEndAgg = new Dictionary<int, double>(),
            IncarnateScalar = new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>(),
        });
    }
}
