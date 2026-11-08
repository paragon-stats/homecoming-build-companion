using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.Expressions;

/// <summary>Port of <c>test_expressions.py</c>: magnitude keywords and conditional-effect evaluation.</summary>
public sealed class ExpressionsTests
{
    private const string PowerName = "Pool.Fighting.Cross_Punch";
    private const string Boxing = "Active:Pool.Fighting.Boxing";
    private const string Kick = "Active:Pool.Fighting.Kick";

    public static TheoryData<double, string> FloatTexts => new()
    {
        { F32.Round(0.1), "0.1" },
        { F32.Round(1.0 / 3), "0.33333334" },
        { 0.1, "0.1" },
        { 5.0, "5" },
        { F32.Round(1339.35), "1339.35" },
    };

    public static TheoryData<string, Func<Power, Power>, double> PowerBaseKeywords => new()
    {
        { "power.base>rechargetime * 2", power => power with { BaseRechargeTime = F32.Round(0.1) }, F32.Round(0.2) },
        { "power.base>areafactor + 1", power => power with { AoeModifier = F32.Round(0.75) }, 1.75 },
        { "power.base>activateperiod * 10", power => power with { ActivatePeriod = F32.Round(0.1) }, 1.0 },
        { "0.7 * power.base>activatetime + 0.4", power => power with { CastTime = F32.Round(2.0), ActivatePeriod = F32.Round(3.0) }, 1.8 },
        { "power.base>areafactor", power => power, 1.0 },
        { "power.base>rechargetime", power => power, 0.0 },
    };

    public static TheoryData<string, double> PowerGroups => new()
    {
        { "powerGroupIn(Pool.Fighting) * 5", 5.0 },
        { "powerGroupIn(Pool.Leaping)", 0.0 },
        { "powerGroupNotIn(Pool.Leaping) * 5", 5.0 },
        { "powerGroupNotIn(Pool.Fighting)", 0.0 },
    };

    public static TheoryData<string, bool> RefusedKeywords
    {
        get
        {
            TheoryData<string, bool> data = [];
            foreach (bool withContext in new[] { false, true })
            {
                foreach (string expr in new[] { "source>cur.kMeter", "powerIs(Pool.Fighting.Cross_Punch)", "@StdResult *" })
                {
                    data.Add(expr, withContext);
                }
            }

            return data;
        }
    }

    public static TheoryData<string, string, bool> SingleConditions => new()
    {
        { "Active:Pool.Fighting.Boxing", "True", true },
        { "Active:Pool.Fighting.Boxing", "False", false },
        { "Active:Pool.Fighting.Kick", "true", false },
        { "Active:Pool.Fighting.Kick", "False", true },
        { "Taken:Pool.Fighting.Boxing", "True", true },
        { "Taken:Pool.Fighting.Kick", "True", false },
        { "Taken:Pool.Fighting.Kick", "False", true },
        { "Stacks:Pool.Fighting.Kick", "= 3", true },
        { "Stacks:Pool.Fighting.Kick", "= 2", false },
        { "Stacks:Pool.Fighting.Kick", "> 2", true },
        { "Stacks:Pool.Fighting.Kick", "> 3", false },
        { "Stacks:Pool.Fighting.Kick", "< 4", true },
        { "Stacks:Pool.Fighting.Kick", "< 3", false },
        { "Stacks:Pool.Fighting.Kick", "~ 3", false },
        { "Stacks:Pool.Fighting.Boxing", "= 0", true },
    };

    public static TheoryData<string[], bool> Chains => new()
    {
        // A and B and C: every condition must hold.
        { [Boxing, "True", "AND " + Kick, "True"], false },
        { [Boxing, "True", "AND " + Kick, "False"], true },
        { [Boxing, "True", Kick, "False"], true },

        // A or B.
        { [Kick, "True", "OR " + Boxing, "True"], true },
        { [Kick, "True", "OR " + Kick, "True"], false },

        // AND binds tighter than OR: kick-and-boxing, or boxing.
        { [Kick, "True", Boxing, "True", "OR " + Boxing, "True"], true },
        { [Kick, "True", "OR " + Boxing, "False", Boxing, "True"], false },
        { [Boxing, "False", "OR " + Kick, "False", Boxing, "True"], true },

        // An OR prefix on the first condition is not an operator.
        { ["OR " + Kick, "False"], true },
    };

    public static TheoryData<string[], bool?, bool> InclusionChains => new()
    {
        // ValidateConditional chains an OR group; CanInclude needs every condition and reads no OR lead.
        { [Kick, "True", "OR " + Boxing, "True"], true, false },
        { [Boxing, "True", "AND " + Boxing, "True"], true, false },
        { [Boxing, "True", "Team:Any", "= 1"], null, false },
        { [Boxing, "True", Kick, "False"], true, true },
    };

    [Theory]
    [MemberData(nameof(FloatTexts))]
    public void Float_text_is_the_shortest_round_trip(double value, string text)
    {
        Assert.Equal(text, Evaluator.FloatText(value));
    }

    [Theory]
    [MemberData(nameof(PowerBaseKeywords))]
    public void Power_base_keywords_read_the_owning_power(string expr, Func<Power, Power> power, double want)
    {
        Assert.Equal(F32.Round(want), Evaluate(expr, configure: power));
    }

    [Fact]
    public void Hit_point_keyword_reads_the_previous_maximum()
    {
        double got = Evaluate("source>Max.kHitPoints * @StdResult", Context(), scale: F32.Round(0.03));
        Assert.Equal(F32.Round(1339.35 * F32.Round(0.03)), got);
    }

    [Fact]
    public void Hit_point_keyword_needs_the_context()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Evaluate("source>Max.kHitPoints"));
        Assert.Matches("E08.*source>Max.kHitPoints", error.Message);
    }

    [Fact]
    public void Constants_are_replaced_before_functions()
    {
        const string expr = "minmax(source>Max.kHitPoints, 0, 1000) / power.base>areafactor + modifier>current";
        Assert.Equal(252.0, Evaluate(expr, Context(), modifier: 2.0, configure: power => power with { AoeModifier = 4.0 }));
    }

    [Theory]
    [MemberData(nameof(PowerGroups))]
    public void Power_group_functions_test_the_name_prefix(string expr, double want)
    {
        Assert.Equal(want, Evaluate(expr));
    }

    [Fact]
    public void Player_hit_points_come_from_the_context()
    {
        const string expr = "minmax(60 - cfg>player>hp, 0, 100)";
        Assert.Equal(35.0, Evaluate(expr, Context() with { PlayerHpPercent = 25 }));
        Assert.Equal(0.0, Evaluate(expr));
    }

    [Fact]
    public void Another_powers_variable_value_comes_from_the_context()
    {
        ExpressionContext context = Context() with { VariableValues = new Dictionary<string, int>(StringComparer.Ordinal) { ["Other.Power.Name"] = 4 } };
        Assert.Equal(8.0, Evaluate("Other.Power.Name>variableVal * 2", context));
    }

    [Fact]
    public void A_power_missing_from_the_build_has_variable_value_zero()
    {
        Assert.Equal(3.0, Evaluate("Other.Power.Name>variableVal + 3", Context()));
    }

    [Fact]
    public void Own_variable_value_is_the_powers_unless_the_context_has_one()
    {
        const string expr = PowerName + ">variableVal";
        ExpressionContext context = Context() with { VariableValues = new Dictionary<string, int>(StringComparer.Ordinal) { [PowerName] = 9 } };
        Assert.Equal(7.0, Evaluate(expr, Context(), configure: power => power with { VariableValue = 7 }));
        Assert.Equal(9.0, Evaluate(expr, context, configure: power => power with { VariableValue = 7 }));
    }

    [Fact]
    public void Another_powers_variable_value_needs_the_context()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Evaluate("Other.Power.Name>variableVal"));
        Assert.Matches("E08.*Other.Power.Name", error.Message);
    }

    [Theory]
    [MemberData(nameof(RefusedKeywords))]
    public void Other_keywords_are_still_refused(string expr, bool withContext)
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Evaluate(expr, withContext ? Context() : null));
        Assert.StartsWith("E08", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2 ** 3")]
    [InlineData("7 % 2")]
    [InlineData("1 < 2")]
    [InlineData("~1")]
    [InlineData("eq(1)")]
    [InlineData("Pool.Fighting(1)")]
    [InlineData("(1")]
    [InlineData("1 $ 2")]
    [InlineData("1 +")]
    [InlineData("2 - ")]
    [InlineData("2 // ")]
    [InlineData("2 ** ")]
    [InlineData("- ")]
    [InlineData("eq(1 2)")]
    [InlineData("eq(1,")]
    [InlineData("x.")]
    [InlineData("1 < ")]
    [InlineData("")]
    [InlineData("1.2.3")]
    [InlineData(".5e")]
    public void Terms_outside_the_ported_grammar_are_refused(string expr)
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Evaluate(expr));
        Assert.StartsWith("E08", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1e-2 * 100", 1.0)]
    [InlineData("2E+1", 20.0)]
    [InlineData(".5 + +1", 1.5)]
    [InlineData("(1 + 2) * 3", 9.0)]
    [InlineData("eq(1, 1) + ne(1, 2) + gt(2, 1) + gte(1, 1) + lt(1, 2) + lte(1, 1)", 6.0)]
    [InlineData("eq(1, 2) + ne(1, 1) + gt(1, 2) + gte(1, 2) + lt(2, 1) + lte(2, 1)", 0.0)]
    [InlineData("and(1, 1) + or (0, 1) + and(0, 1) + or(0, 0)", 2.0)]
    [InlineData("minmax(5, 10, 0) + minmax(-5, 0, 10) + minmax(50, 0, 10)", 15.0)]
    public void Functions_and_literals_follow_the_python_semantics(string expr, double want)
    {
        Assert.Equal(want, Evaluate(expr));
    }

    [Theory]
    [InlineData("1 / 0", double.PositiveInfinity)]
    [InlineData("-1 / 0", double.NegativeInfinity)]
    [InlineData("0 / 0", double.NaN)]
    public void Division_by_zero_follows_double_arithmetic(string expr, double want)
    {
        Assert.Equal(want, Evaluate(expr));
    }

    [Theory]
    [MemberData(nameof(SingleConditions))]
    public void Single_conditions(string key, string value, bool want)
    {
        ExpressionContext context = Context() with { VariableValues = new Dictionary<string, int>(StringComparer.Ordinal) { ["Pool.Fighting.Kick"] = 3 } };
        Assert.Equal(want, Conditionals.ConditionalsMet(Conditional(key, value), context));
    }

    [Fact]
    public void No_conditions_are_met()
    {
        Assert.True(Conditionals.ConditionalsMet(Make.Effect, Context()));
    }

    [Theory]
    [MemberData(nameof(Chains))]
    public void Conditions_chain_with_and_before_or(string[] conditions, bool want)
    {
        Assert.Equal(want, Conditionals.ConditionalsMet(Conditional(conditions), Context()));
    }

    [Theory]
    [InlineData("Team:Pool.Fighting.Boxing", "= 1")]
    [InlineData("Config:cfg.player.hp", "> 5")]
    public void Other_condition_types_are_refused(string key, string value)
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Conditionals.ConditionalsMet(Conditional(key, value), Context()));
        Assert.Matches("E92.*Active, Taken or Stacks", error.Message);
    }

    [Fact]
    public void A_key_with_the_type_after_the_power_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Conditionals.ConditionalsMet(Conditional("Pool.Fighting.Boxing:Active", "True"), Context()));
        Assert.Matches("E92.*'Pool.Fighting.Boxing'", error.Message);
    }

    [Theory]
    [InlineData("Active:X", "yes")]
    [InlineData("Taken:X", "")]
    public void A_condition_value_that_is_not_a_boolean_is_refused(string key, string value)
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Conditionals.ConditionalsMet(Conditional(key, value), Context()));
        Assert.Matches("E93.*'True' or 'False'", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("=")]
    [InlineData("= two")]
    public void A_stacks_value_without_an_integer_is_refused(string value)
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Conditionals.ConditionalsMet(Conditional("Stacks:X", value), Context()));
        Assert.Matches("E93.*Stacks", error.Message);
    }

    [Fact]
    public void Unconditional_effect_is_included_with_or_without_a_context()
    {
        Assert.True(Conditionals.EffectIncluded(Make.Effect, null));
        Assert.True(Conditionals.EffectIncluded(Make.Effect, Context()));
    }

    [Fact]
    public void Special_case_effect_is_excluded()
    {
        Effect hidden = Make.Effect with { SpecialCase = "Hidden" };
        Assert.False(Conditionals.EffectIncluded(hidden, null));
        Assert.False(Conditionals.EffectIncluded(hidden, Context()));
        Assert.False(Conditionals.EffectIncluded(Conditional(Boxing, "True") with { SpecialCase = "Hidden" }, Context()));
    }

    [Fact]
    public void Conditional_effect_needs_a_context_and_met_conditions()
    {
        Effect effect = Conditional(Boxing, "True");
        Assert.False(Conditionals.EffectIncluded(effect, null));
        Assert.True(Conditionals.EffectIncluded(effect, Context()));
        Assert.False(Conditionals.EffectIncluded(Conditional(Kick, "True"), Context()));
    }

    [Theory]
    [MemberData(nameof(InclusionChains))]
    public void Inclusion_needs_every_condition_where_validation_chains_them(string[] conditions, bool? met, bool included)
    {
        Effect effect = Conditional(conditions);
        Assert.Equal(included, Conditionals.EffectIncluded(effect, Context()));
        if (met is { } expected)
        {
            Assert.Equal(expected, Conditionals.ConditionalsMet(effect, Context()));
        }
    }

    [Fact]
    public void Conditions_known_only_by_count_are_not_evaluated()
    {
        Effect effect = Make.Effect with { ActiveConditionalsCount = 2 };
        Assert.False(Conditionals.EffectIncluded(effect, null));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Conditionals.EffectIncluded(effect, Context()));
        Assert.Matches("E94.*counts 2 conditions", error.Message);
    }

    [Fact]
    public void Conditions_without_a_count_still_exclude_without_a_context()
    {
        Assert.False(Conditionals.EffectIncluded(Make.Effect with { Conditionals = [Make.Condition(Boxing, "True")] }, null));
    }

    private static ExpressionContext Context()
    {
        return new ExpressionContext
        {
            PrevHpMax = F32.Round(1339.35),
            BuildPowerNames = new HashSet<string>(StringComparer.Ordinal) { "Pool.Fighting.Boxing" },
            ActivePowerNames = new HashSet<string>(StringComparer.Ordinal) { "Pool.Fighting.Boxing" },
            VariableValues = new Dictionary<string, int>(StringComparer.Ordinal),
        };
    }

    private static double Evaluate(string expr, ExpressionContext? context = null, double scale = 1.0, double modifier = 1.0, Func<Power, Power>? configure = null)
    {
        Effect effect = Make.Effect with { AttribType = "Expression", ExprMagnitude = expr };
        Power power = Make.Power(effect) with { FullName = PowerName };
        return Evaluator.EvaluateMagnitude(effect, configure is null ? power : configure(power), context, scale, modifier);
    }

    private static Effect Conditional(params string[] keyValues)
    {
        KeyValuePair<string, string>[] conditions = [.. keyValues.Chunk(2).Select(pair => Make.Condition(pair[0], pair[1]))];
        return Make.Effect with { Conditionals = conditions, ActiveConditionalsCount = conditions.Length };
    }
}
