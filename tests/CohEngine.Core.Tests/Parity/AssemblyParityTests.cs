using System.Globalization;

using CohEngine.Core.Assembly;
using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.SetBonuses;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// Port of <c>test_assembly_parity.py</c>: the engine assembles every StatInclude power of the 12 sample builds as
/// Mids does, compared with <c>assembled_powers.json</c> effect by effect on the fields totals read.
/// </summary>
[Trait("Category", "Parity")]
public sealed class AssemblyParityTests
{
    public static TheoryData<string> Builds => [.. AssemblyBuilds()];

    [Theory]
    [MemberData(nameof(Builds))]
    public void Assembly_matches_mids(string name)
    {
        Assert.Empty(Mismatches(name));
    }

    [Fact]
    public void Every_sample_build_is_compared()
    {
        Assert.Equal(12, AssemblyBuilds().Count);
    }

    internal static Dictionary<int, AssembledEffects> AssembleBuild(BuildInputs inputs, IReadOnlyList<Effect> setBonusEffects)
    {
        return CrossPowerPass.AssembleBuild(
            inputs.Fixture.PowerAssembly,
            inputs.Slots,
            inputs.StatInclude,
            Fixtures.Store.EnhancementEffects,
            Fixtures.Store.SetBonuses,
            Fixtures.Store.AssemblyPowers,
            Fixtures.Store.AssemblyEntities,
            inputs.ArchetypeIndex,
            effect => Conditionals.ConditionalsMet(effect, inputs.Expression),
            setBonusEffects);
    }

    private static List<string> AssemblyBuilds()
    {
        return [.. Fixtures.Store.BuildNames().Where(name => Fixtures.Store.Build(name).Has("assembled_powers.json"))];
    }

    private static List<string> Mismatches(string name)
    {
        BuildInputs inputs = new(name);
        Power bonus = Accumulator.BuildSetBonusPower(inputs.Powers, inputs.Slots, Fixtures.Store.EnhancementEffects, Fixtures.Store.SetBonuses, 50, false);
        Dictionary<int, AssembledEffects> got = AssembleBuild(inputs, bonus.Effects);

        // Mids has multiplied a variable power's scale by its variable value; the engine does that when it reads it.
        Dictionary<int, int> factor = inputs.Powers.ToDictionary(power => power.BuildIndex, power => power.VariableEnabled ? power.VariableValue : 1);
        List<string> output = [];
        foreach ((int index, IReadOnlyList<Effect> want) in inputs.Fixture.AssembledPowers)
        {
            string power = inputs.Fixture.PowerAssembly[index].Power.FullName;
            IReadOnlyList<Effect> have = got[index].Effects;
            if (have.Count != want.Count)
            {
                output.Add($"{power}: {have.Count} effects, Mids has {want.Count}");
                continue;
            }

            for (int i = 0; i < have.Count; i++)
            {
                output.AddRange(EffectMismatches(have[i], want[i], factor[index], power));
            }
        }

        return output;
    }

    private static IEnumerable<string> EffectMismatches(Effect have, Effect want, int factor, string power)
    {
        double scale = have.VariableModified && !have.IgnoreScaling ? have.Scale * factor : have.Scale;
        (string Field, object Have, object Want)[] fields =
        [
            ("effect_type", have.EffectType, want.EffectType),
            ("et_modifies", have.EtModifies, want.EtModifies),
            ("damage_type", have.DamageType, want.DamageType),
            ("mez_type", have.MezType, want.MezType),
            ("to_who", have.ToWho, want.ToWho),
            ("effect_class", have.EffectClass, want.EffectClass),
            ("buffable", have.Buffable, want.Buffable),
            ("absorbed_effect", have.AbsorbedEffect, want.AbsorbedEffect),
            ("absorbed_power_type", have.AbsorbedPowerType, want.AbsorbedPowerType),
            ("absorbed_class_n_id", have.AbsorbedClassNId, want.AbsorbedClassNId),
            ("is_enhancement_effect", have.IsEnhancementEffect, want.IsEnhancementEffect),
            ("scale", scale, want.Scale),
            ("n_magnitude", have.NMagnitude, want.NMagnitude),
            ("aspect", have.Aspect, want.Aspect),
            ("modifier_table", have.ModifierTable, want.ModifierTable),
            ("delayed_time", have.DelayedTime, want.DelayedTime),
            ("pv_mode", have.PvMode, want.PvMode),
            ("ignore_scaling", have.IgnoreScaling, want.IgnoreScaling),
            ("conditionals", string.Join("|", have.Conditionals), string.Join("|", want.Conditionals)),
        ];
        foreach ((string field, object got, object expected) in fields)
        {
            if (!got.Equals(expected))
            {
                yield return string.Create(CultureInfo.InvariantCulture, $"{power} effect {want.Index} {field}: {got}, Mids has {expected}");
            }
        }

        if (have.Probability > 0 != want.Probability > 0)
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"{power} effect {want.Index} probability: {have.Probability}, Mids has {want.Probability}");
        }
    }
}
