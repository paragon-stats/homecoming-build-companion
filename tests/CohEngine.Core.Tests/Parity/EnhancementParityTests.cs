using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;
using CohEngine.Core.Totals.Phases;

using EnhancementPipeline = CohEngine.Core.Enhancement.Pipeline;
using TotalsPipeline = CohEngine.Core.Totals.Pipeline;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// Port of <c>test_enhancement_parity.py</c>: enhanced per-power values and slotted totals. The generic IOs the
/// fixtures slot carry no set bonus, so the enhancement flows through the per-effect multiplier path only.
/// </summary>
[Trait("Category", "Parity")]
public sealed class EnhancementParityTests
{
    private const string Slotted = "shield_scrapper_slotted";
    private const string Hasten2Rech = "hasten_2rech";
    private const int DeflectionIndex = 2;
    private const int HastenIndex = 6;

    public static TheoryData<string> SlottedBuilds => [Slotted, Hasten2Rech];

    public static TheoryData<Effect, string?> Aspects => new()
    {
        { Make.Effect with { EffectType = "Defense", Buffable = true }, "Defense" },
        { Make.Effect with { EffectType = "Resistance", Buffable = true }, "Resistance" },
        { Make.Effect with { EffectType = "Recovery", Buffable = true }, "Recovery" },
        { Make.Effect with { EffectType = "Enhancement", EtModifies = "Accuracy", Buffable = true }, "Accuracy" },
        { Make.Effect with { EffectType = "ResEffect", EtModifies = "Defense", Buffable = true }, "Defense" },
        { Make.Effect with { EffectType = "ResEffect", EtModifies = "Regeneration", Buffable = true }, "Heal" },
        { Make.Effect with { EffectType = "Defense", Buffable = false }, null },
        { Make.Effect with { EffectType = "ResEffect", EtModifies = "Recovery", Buffable = true }, null },
        { Make.Effect with { EffectType = "Damage", DamageType = "Special", Buffable = true }, null },
        { Make.Effect with { EffectType = "Mez", AttribType = "Duration", Buffable = true }, null },
        { Make.Effect with { EffectType = "ResEffect", EtModifies = "Regeneration", MezType = "Held", Buffable = true }, null },
    };

    [Theory]
    [MemberData(nameof(SlottedBuilds))]
    public void Slotted_totals_match_mids(string name)
    {
        BuildInputs inputs = new(name);
        BaseTotals result = EngineRun.Totals(inputs, EngineRun.Slotted(inputs));
        EngineRun.CompareTotals(inputs.Fixture.Totals.Totals, result.Totals, $"{name}.Totals");
        EngineRun.CompareTotals(inputs.Fixture.Totals.TotalsCapped, result.TotalsCapped, $"{name}.TotalsCapped");
    }

    [Fact]
    public void Deflection_per_effect_math_mag()
    {
        // Effects are matched by structural identity, not position: assembly can reorder the buffed effect array, and
        // same-identity effects carry the same enhanced magnitude, so they pair in order.
        BuildInputs inputs = new(Slotted);
        MagContext context = new()
        {
            Mods = Fixtures.Store.AttribMods,
            Classes = Fixtures.Store.Archetypes,
            ArchetypeIndex = inputs.ArchetypeIndex,
            Config = Fixtures.Store.Config,
            EnhanceNames = PerPowerEnhancementMathPhase.EnhanceNames(Fixtures.Store.Enums),
            Enums = Fixtures.Store.Enums,
        };
        List<Power> included = [.. inputs.Powers.Where(power => power.StatInclude)];
        Dictionary<(int BuildIndex, int EffectIndex), double> enhMult = PerPowerEnhancementMathPhase.ComputeEnhMultipliers(included, inputs.Slots, Fixtures.Store.EnhancementEffects, Fixtures.Store.Maths, 50, context);

        Power deflection = inputs.Powers.Single(power => power.BuildIndex == DeflectionIndex);
        EnhancedPower dumped = inputs.Fixture.EnhancedPowers.Single(power => power.BuildIndex == DeflectionIndex);
        Dictionary<(string, string, string, string), Queue<double>> byKey = [];
        foreach (EnhancedEffect effect in dumped.Effects)
        {
            (string, string, string, string) key = (effect.EffectType, effect.DamageType, effect.MezType, effect.EtModifies);
            byKey.TryAdd(key, new Queue<double>());
            byKey[key].Enqueue(effect.MathMag);
        }

        foreach (Effect effect in deflection.Effects)
        {
            double mag = F32.Round(context.Mag(effect, deflection) * enhMult.GetValueOrDefault((DeflectionIndex, effect.Index), 1.0));
            ParityAssert.Equal(byKey[(effect.EffectType, effect.DamageType, effect.MezType, effect.EtModifies)].Dequeue(), mag, $"Deflection.{effect.EffectType}.{effect.DamageType}");
        }

        Assert.All(byKey.Values, queue => Assert.Empty(queue));
    }

    [Theory]
    [InlineData(Slotted, 0.9908)]
    [InlineData(Hasten2Rech, 0.8332)]
    public void Hasten_buffed_recharge_matches(string name, double rounded)
    {
        IReadOnlyList<SlotRef> slots = Fixtures.Store.Build(name).Slots.Single(entry => entry.BuildIndex == HastenIndex).Slots;
        double aggregate = EnhancementPipeline.AggregateAndEd(slots, "RechargeTime", Fixtures.Store.EnhancementEffects, 50, Fixtures.Store.Maths);
        Assert.Equal(rounded, Math.Round(aggregate, 4));

        EnhancedPower dumped = Fixtures.Store.Build(name).EnhancedPowers.Single(power => power.BuildIndex == HastenIndex);
        double buffed = F32.Round(dumped.Base.RechargeTime / F32.Round(1.0 + aggregate));
        ParityAssert.Equal(dumped.RechargeTime, buffed, $"{name}.Hasten.RechargeTime");
    }

    [Fact]
    public void Empty_slots_are_a_no_op()
    {
        BuildInputs inputs = new("shield_scrapper_noslots");
        BaseTotals without = EngineRun.Totals(inputs);
        BaseTotals withData = EngineRun.Totals(inputs, EngineRun.Slotted(inputs));
        BaseTotalsParityTests.AssertSame(without.Totals, withData.Totals);
        BaseTotalsParityTests.AssertSame(without.TotalsCapped, withData.TotalsCapped);
    }

    [Theory]
    [MemberData(nameof(Aspects))]
    public void Enhance_aspect_routes_by_name_identity_and_special_cases(Effect effect, string? want)
    {
        Assert.Equal(want, PerPowerEnhancementMathPhase.EnhanceAspect(effect, PerPowerEnhancementMathPhase.EnhanceNames(Fixtures.Store.Enums)));
    }

    [Fact]
    public void Partial_enhancement_inputs_are_refused()
    {
        BuildInputs inputs = new(Slotted);
        ArgumentException error = Assert.Throws<ArgumentException>(() => EngineRun.Totals(inputs, new TotalsPipeline.BuildOptions { Slots = inputs.Slots }));
        Assert.StartsWith("E11", error.Message, StringComparison.Ordinal);
    }
}
