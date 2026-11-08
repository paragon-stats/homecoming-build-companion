using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Tests.Totals;

/// <summary>Port of the magnitude tests of <c>test_effect.py</c> and the GetModifier tests of <c>test_attribmod.py</c>.</summary>
public sealed class EffectMagnitudeTests
{
    /// <summary>Gets two 50x2 tables: Ones (all 1) and Twos (2 in column 0, 3 in column 1).</summary>
    internal static AttribMods TinyMods { get; } = new(
    [
        new ModifierTable { Id = "Ones", BaseIndex = 0, Table = [.. Enumerable.Repeat<IReadOnlyList<double>>([1.0, 1.0], 50)] },
        new ModifierTable { Id = "Twos", BaseIndex = 100, Table = [.. Enumerable.Repeat<IReadOnlyList<double>>([2.0, 3.0], 50)] },
    ]);

    /// <summary>Gets two class records with distinct AttribMod columns and small caps.</summary>
    internal static ArchetypeDb TinyClasses { get; } = new([Class(0, "Class_Test", 0), Class(1, "Class_Pet", 1)]);

    public static TheoryData<string, double> Expressions => new()
    {
        // Fury: the effect's Scale times the power's own variable value.
        { "@StdResult*Inherent.Inherent.Rage_Buff>variableVal", F32.Round(0.1) * 7 },

        // Super Reflexes' scaling resistance at the default full health is zero.
        { "0.003 * minmax(60 - cfg>player>hp, 0, 100)", 0.0 },

        // A set bonus that scales with missing health, at full health.
        { "-0.001 * cfg>player>hp + 0.129", 0.029 },
        { "modifier>current * (lt(3, 10) + lte(10, 10) + gt(1, 2) + gte(2, 2))", 6.0 },
        { "eq(1, 1) + ne(1, 2) + and(1, 0) + or(0, 1) + 4 / 2 - +1", 4.0 },
        { "minmax(500, 100, 0)", 100.0 },
    };

    [Fact]
    public void Magnitude_is_scale_times_nmag_times_modifier()
    {
        Effect effect = Make.Effect with { Scale = 0.5, NMagnitude = 3.0, ModifierTable = "Twos", NModifierTable = 1 };
        Assert.Equal(F32.Round(3.0), Mag(effect));
    }

    [Fact]
    public void Damage_effect_reports_negative_mag()
    {
        Assert.Equal(F32.Round(-2.0), Mag(Make.Effect with { EffectType = "Damage", Scale = 2.0 }));
    }

    [Fact]
    public void Duration_attrib_type_returns_nmagnitude()
    {
        Assert.Equal(F32.Round(4.0), Mag(Make.Effect with { AttribType = "Duration", Scale = 99.0, NMagnitude = 4.0 }));
    }

    [Fact]
    public void Blank_expression_falls_back_to_scale_times_nmag()
    {
        Assert.Equal(F32.Round(6.0), Mag(Make.Effect with { AttribType = "Expression", Scale = 2.0, NMagnitude = 3.0, ExprMagnitude = "  " }));
    }

    [Theory]
    [MemberData(nameof(Expressions))]
    public void Magnitude_expression_is_evaluated(string expr, double want)
    {
        Effect effect = Make.Effect with { AttribType = "Expression", ExprMagnitude = expr, Scale = F32.Round(0.1), ModifierTable = "Twos", NModifierTable = 1 };
        Power power = Make.Power(effect) with { FullName = "Inherent.Inherent.Rage_Buff", VariableValue = 7 };
        Assert.Equal(F32.Round(want), EffectMagnitude.Mag(effect, power, TinyMods, TinyClasses, 0));
    }

    [Fact]
    public void Expression_context_reaches_the_evaluator()
    {
        Effect effect = Make.Effect with { AttribType = "Expression", ExprMagnitude = "source>Max.kHitPoints / 100" };
        ExpressionContext context = new()
        {
            PrevHpMax = F32.Round(1500.0),
            BuildPowerNames = new HashSet<string>(StringComparer.Ordinal),
            ActivePowerNames = new HashSet<string>(StringComparer.Ordinal),
            VariableValues = new Dictionary<string, int>(StringComparer.Ordinal),
        };
        Assert.Equal(F32.Round(15.0), EffectMagnitude.Mag(effect, Make.Power(effect), TinyMods, TinyClasses, 0, context));
        Assert.StartsWith("E08", Assert.Throws<NotSupportedException>(() => Mag(effect)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Damage_expression_is_negated()
    {
        Assert.Equal(F32.Round(-3.0), Mag(Make.Effect with { EffectType = "Damage", AttribType = "Expression", ExprMagnitude = "@StdResult * 2", Scale = 1.5 }));
    }

    [Theory]
    [InlineData("@StdResult * source>cur.kToHit")]
    [InlineData("Other.Power.Name>variableVal * 2")]
    [InlineData("max(1, 2)")]
    [InlineData("2 ** 3")]
    [InlineData("@StdResult *")]
    public void Unported_expression_is_refused(string expr)
    {
        Effect effect = Make.Effect with { AttribType = "Expression", ExprMagnitude = expr };
        Assert.StartsWith("E08", Assert.Throws<NotSupportedException>(() => Mag(effect)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_attrib_type_yields_zero()
    {
        Assert.Equal(0.0, Mag(Make.Effect with { AttribType = "Unknown" }));
    }

    [Fact]
    public void Forced_class_overrides_archetype_column()
    {
        Effect effect = Make.Effect with { ModifierTable = "Twos", NModifierTable = 1 };
        Power power = Make.Power(effect) with { ForcedClass = "Class_Pet" };
        Assert.Equal(F32.Round(3.0), EffectMagnitude.ModifierForEffect(effect, power, TinyMods, TinyClasses, 0));
    }

    [Fact]
    public void Absorbed_class_beats_archetype_but_not_forced_class()
    {
        Effect effect = Make.Effect with { ModifierTable = "Twos", NModifierTable = 1, AbsorbedClassNId = 1 };
        Assert.Equal(F32.Round(3.0), EffectMagnitude.ModifierForEffect(effect, Make.Power(effect), TinyMods, TinyClasses, 0));
        Power forced = Make.Power(effect) with { ForcedClass = "Class_Test" };
        Assert.Equal(F32.Round(2.0), EffectMagnitude.ModifierForEffect(effect, forced, TinyMods, TinyClasses, 0));
    }

    [Theory]
    [InlineData(0.3, true, false, 0, 0.0)]
    [InlineData(0.25, true, false, 4, 1.0)]
    [InlineData(0.5, true, true, 0, 0.5)]
    [InlineData(0.5, false, false, 0, 0.5)]
    public void Variable_scaling(double scale, bool enabled, bool ignoreScaling, int value, double want)
    {
        Effect effect = Make.Effect with { Scale = scale, VariableModified = true, IgnoreScaling = ignoreScaling };
        Power power = Make.Power(effect) with { VariableEnabled = enabled, VariableValue = value };
        Assert.Equal(F32.Round(want), EffectMagnitude.Mag(effect, power, TinyMods, TinyClasses, 0));
    }

    [Fact]
    public void Get_modifier_resolves_column_indirection()
    {
        AttribMods mods = Fixtures.Store.AttribMods;
        IReadOnlyList<Archetype> classes = Fixtures.Store.Archetypes.Classes;
        Assert.Equal(F32.Round(-55.6102), EffectMagnitude.Modifier(mods, classes, 0, 0, 49));
        Assert.Equal(mods.Tables[0].Table[49][classes[3].Column], EffectMagnitude.Modifier(mods, classes, 3, 0, 49));
    }

    [Theory]
    [InlineData(-1, 0, 49)]
    [InlineData(0, -1, 49)]
    [InlineData(0, 0, -1)]
    [InlineData(999, 0, 49)]
    [InlineData(0, 999, 49)]
    [InlineData(0, 0, 999)]
    public void Get_modifier_out_of_bounds_returns_zero(int classIndex, int table, int level)
    {
        Assert.Equal(0.0, EffectMagnitude.Modifier(Fixtures.Store.AttribMods, Fixtures.Store.Archetypes.Classes, classIndex, table, level));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void Get_modifier_bad_column_returns_zero(int column)
    {
        Assert.Equal(0.0, EffectMagnitude.Modifier(Fixtures.Store.AttribMods, [Class(0, "X", column)], 0, 0, 49));
    }

    internal static Archetype Class(int index, string name, int column)
    {
        return new Archetype
        {
            Index = index,
            ClassName = name,
            DisplayName = name,
            ClassType = "Hero",
            Column = column,
            Playable = true,
            Hitpoints = 1000,
            HpCap = 1500.0,
            ResCap = 0.75,
            DamageCap = 4.0,
            RechargeCap = 5.0,
            RecoveryCap = 5.0,
            RegenCap = 20.0,
            PerceptionCap = 1153.0,
            BaseRecovery = 1.67,
            BaseRegen = 1.0,
            BaseThreat = 1.0,
            Ancillary = [],
            InherentPowers = [],
        };
    }

    private static double Mag(Effect effect)
    {
        return EffectMagnitude.Mag(effect, Make.Power(effect), TinyMods, TinyClasses, 0);
    }
}
