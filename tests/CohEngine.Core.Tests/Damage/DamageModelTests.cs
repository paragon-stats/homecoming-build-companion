using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Maths;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.DamageLayers;

public sealed class DamageModelTests
{
    private static readonly SourceRegistration Fixed = new(_ => new SourceResult { Source = "a", Dps = 10.0, TimelineShare = 0.25, EndPerSec = 1.0 }, FillsRemainder: false);
    private static readonly SourceRegistration Absent = new(_ => null, FillsRemainder: false);
    private static readonly SourceRegistration Remainder = new(context => new SourceResult { Source = "r", Dps = 20.0, EndPerSec = context.OtherDrainPerSec }, FillsRemainder: true);
    private static readonly SourceRegistration AbsentRemainder = new(_ => null, FillsRemainder: true);

    private static readonly Func<DamageContext, ModifierResult?>[] Modifiers =
    [
        _ => new ModifierResult { Modifier = "m1", Multiplier = 1.5, Additive = true },
        _ => new ModifierResult { Modifier = "m2", Multiplier = 2.0, AppliesTo = new HashSet<string>(StringComparer.Ordinal) { "a" } },
        _ => null,
    ];

    public static TheoryData<double, double> NetCases => new()
    {
        { 0.0, 3.0 },
        { 5.0, -2.0 },
    };

    public static TheoryData<int, int, Shape, int> TargetCases => new()
    {
        { 5, 0, Shape.SingleTarget, 1 },
        { 5, 0, Shape.Cone, 5 },
        { 5, 3, Shape.Cone, 3 },
        { 2, 8, Shape.Pbaoe, 2 },
        { 0, 0, Shape.Location, 1 },
    };

    [Theory]
    [MemberData(nameof(NetCases))]
    public void Net_rate_is_recovery_less_drain(double extra, double expected)
    {
        Assert.Equal(expected, new EnduranceBudget(100.0, 4.0, 1.0).NetPerSec(extra));
    }

    [Fact]
    public void Time_to_empty_is_infinite_when_sustainable()
    {
        Assert.True(double.IsPositiveInfinity(new EnduranceBudget(100.0, 4.0, 1.0).TimeToEmpty()));
    }

    [Fact]
    public void Time_to_empty_divides_the_pool_by_the_net_loss()
    {
        Assert.Equal(50.0, new EnduranceBudget(100.0, 4.0, 1.0).TimeToEmpty(5.0));
    }

    [Theory]
    [MemberData(nameof(TargetCases))]
    public void Area_powers_hit_up_to_their_cap(int targets, int cap, Shape shape, int expected)
    {
        Assert.Equal(expected, new Scenario(targets).TargetsHit(ChainFactory.Profile(shape: shape, maxTargets: cap)));
    }

    [Theory]
    [InlineData(20.0, 2.0)]
    [InlineData(0.0, double.PositiveInfinity)]
    public void Switching_stance_crosses_the_standoff_once(double speed, double expected)
    {
        Assert.Equal(expected, new Movement(speed, 40.0).SwitchTime);
    }

    [Theory]
    [InlineData(false, 1.5)]
    [InlineData(true, 1.25)]
    public void A_modifier_scales_by_uptime_and_an_additive_one_by_baseline(bool additive, double expected)
    {
        Assert.Equal(expected, new ModifierResult { Modifier = "m", Multiplier = 2.0, Uptime = 0.5, Additive = additive }.Scale(2.0 - (additive ? 0.0 : 1.0)));
    }

    [Fact]
    public void Role_and_attack_helpers_read_the_context()
    {
        DamageContext context = ChainFactory.Context([ChainFactory.Attack("A"), ChainFactory.Attack("P", Role.Patch)], assumptions: new Dictionary<string, double>(StringComparer.Ordinal) { ["k"] = 3.0 });

        Assert.Equal("P 3 9", string.Join(",", context.ByRole(Role.Patch).Select(attack => attack.FullName)) + " " + context.Assume("k", 9.0) + " " + context.Assume("none", 9.0));
    }

    [Fact]
    public void Enum_values_are_the_python_spellings()
    {
        Assert.Equal(
            "melee ranged hybrid chain aura patch pet_summon buff_click support persistent_debuff melee ranged",
            string.Join(' ', Stance.Melee.Value(), Stance.Ranged.Value(), Stance.Hybrid.Value(), Role.Chain.Value(), Role.Aura.Value(), Role.Patch.Value(), Role.PetSummon.Value(), Role.BuffClick.Value(), Role.Support.Value(), Role.PersistentDebuff.Value(), Delivery.Melee.Value(), Delivery.Ranged.Value()));
    }

    [Theory]
    [InlineData("ranged", true)]
    [InlineData("all", false)]
    public void A_stance_parses_from_its_value(string text, bool expected)
    {
        Assert.Equal(expected, EnumText.TryParseStance(text, out _));
    }

    [Fact]
    public void Non_chain_sources_claiming_more_than_the_timeline_raise_e30()
    {
        SourceRegistration heavy = new(_ => new SourceResult { Source = "b", Dps = 1.0, TimelineShare = 0.75 }, FillsRemainder: false);
        SourceRegistration other = new(_ => new SourceResult { Source = "c", Dps = 1.0, TimelineShare = 0.75 }, FillsRemainder: false);

        ChainException error = Assert.Throws<ChainException>(() => DamageModel.Evaluate(ChainFactory.Context([]), [heavy, other], []));

        Assert.Equal(
            "E30: non-chain damage sources claim 1.500 of the timeline (> 1.0): [('b', 0.75), ('c', 0.75)]; a summon, patch or click buff reports a cast share above its recharge cadence",
            error.Message);
    }

    [Fact]
    public void Evaluate_scales_every_source_by_its_modifiers_and_totals_them()
    {
        DamageReport report = DamageModel.Evaluate(ChainFactory.Context([]), [Fixed, Absent, Remainder, AbsentRemainder], Modifiers);

        Assert.Equal("a=30,r=30 60", string.Join(',', report.ScaledDps.Select(pair => $"{pair.Key}={pair.Value}")) + " " + report.TotalDps);
    }

    [Fact]
    public void The_remainder_sources_see_the_timeline_and_drain_the_others_left()
    {
        DamageReport report = DamageModel.Evaluate(ChainFactory.Context([]), [Fixed, Remainder], []);

        Assert.Equal("0.75 1 1", report.RemainderContext.TimelineAvailable + " " + report.RemainderContext.OtherDrainPerSec + " " + report.Sources[1].EndPerSec);
    }

    [Fact]
    public void A_source_with_its_own_baseline_divides_additive_buffs_by_it()
    {
        SourceRegistration own = new(_ => new SourceResult { Source = "a", Dps = 10.0, DamageBuffBaseline = 2.0 }, FillsRemainder: false);

        DamageReport report = DamageModel.Evaluate(ChainFactory.Context([]), [own], [Modifiers[0]]);

        Assert.Equal(F32.Round(10.0 * 1.25), report.TotalDps);
    }

    [Fact]
    public void The_default_registries_are_used_when_none_are_given()
    {
        DamageReport report = DamageModel.Evaluate(ChainFactory.Context([ChainFactory.Attack("Hit")]));

        Assert.Equal("pet,chain", string.Join(',', report.Sources.Select(source => source.Source)));
    }

    [Fact]
    public void Time_to_empty_comes_from_the_total_drain()
    {
        SourceRegistration drain = new(_ => new SourceResult { Source = "a", Dps = 1.0, EndPerSec = 5.0 }, FillsRemainder: false);

        Assert.Equal(50.0, DamageModel.Evaluate(ChainFactory.Context([]), [drain], []).TimeToEmpty);
    }
}
