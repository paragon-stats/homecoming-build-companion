using CohEngine.Core.Chain;
using CohEngine.Core.Damage;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Damage;

public sealed class AtModifiersTests
{
    public static TheoryData<string, double, bool> Defaults => new()
    {
        { "Class_Brute", F32.Round(1.0 + (0.02 * 75.0)), true },
        { "Class_Controller", F32.Round(1.0 + 0.5), false },
        { "Class_Corruptor", F32.Round(1.0 + 0.10), false },
        { "Class_Blaster", F32.Round(1.0 + 0.30), true },
        { "Class_Defender", F32.Round(1.0 + 0.3), true },
        { "Class_Scrapper", F32.Round(1.0 + (0.10 * (2.0 - 1.0))), false },
        { "Class_Stalker", F32.Round(1.0 + ((0.10 + (0.03 * 1.0)) * (2.0 - 1.0))), false },
        { "Class_Sentinel", F32.Round(1.0 + (0.05 * 1.0)), true },
        { "Class_Mastermind", F32.Round(1.0 + (0.25 * 1.0)), true },
        { "Class_Peacebringer", 1.0, true },
        { "Class_Warshade", 1.0, true },
        { "Class_Dominator", 1.0, false },
        { "Class_Tanker", 1.0, false },
        { "Class_Arachnos_Soldier", 1.0, false },
        { "Class_Arachnos_Widow", 1.0, false },
    };

    private static ModifierResult? Run(string className, IReadOnlyDictionary<string, double>? assumptions = null)
    {
        DamageContext context = ChainFactory.Context([], archetypeClass: className, assumptions: assumptions);
        return AtModifiers.Functions.Select(rule => rule(context)).SingleOrDefault(result => result is not null);
    }

    [Theory]
    [MemberData(nameof(Defaults))]
    public void Each_playable_class_has_one_rule_with_its_default_multiplier(string className, double multiplier, bool additive)
    {
        ModifierResult result = Run(className)!;

        Assert.Equal($"{multiplier} {additive}", $"{result.Multiplier} {result.Additive}");
    }

    [Fact]
    public void Every_class_in_the_table_has_a_rule_function()
    {
        Assert.Equal(AtModifiers.RuleByClass.Count, AtModifiers.Functions.Count);
    }

    [Fact]
    public void An_unknown_class_has_no_inherent_modifier()
    {
        Assert.Null(Run("Class_Nobody"));
    }

    [Fact]
    public void Only_supremacy_scales_the_pets()
    {
        Assert.Equal("pet", string.Join(',', Run("Class_Mastermind")!.AppliesTo!));
    }

    [Fact]
    public void A_parameter_outside_its_range_is_e50()
    {
        IReadOnlyDictionary<string, double> assumptions = new Dictionary<string, double>(StringComparer.Ordinal) { ["Class_Brute.fury_level"] = 101.0 };

        Assert.Equal("E50: assumption Class_Brute.fury_level=101.0 is outside [0.0, 100.0]; set it within that range", Assert.Throws<EngineException>(() => Run("Class_Brute", assumptions)).Message);
    }

    [Fact]
    public void Vigilance_shrinks_with_team_size()
    {
        IReadOnlyDictionary<string, double> assumptions = new Dictionary<string, double>(StringComparer.Ordinal) { ["Class_Defender.team_size"] = 8.0 };

        Assert.Equal(F32.Round(1.0 + 0.0), Run("Class_Defender", assumptions)!.Multiplier);
    }

    [Fact]
    public void A_neutral_rule_says_why_it_changes_nothing()
    {
        Assert.StartsWith("Class_Tanker: neutral, Gauntlet only grants/revokes the taunt power", Run("Class_Tanker")!.Assumption, StringComparison.Ordinal);
    }
}
