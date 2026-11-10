using CohEngine.Core.BuildFile;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>
/// A full build spec for the writer tests: a Willpower/Super Strength Tanker with Leadership. Its written file is
/// <c>tests/fixtures/buildfile/builds/wpss_tanker_leadership.mbd</c> and MidsReborn's dump of that file is
/// <c>tests/fixtures/mids/builds/wpss_tanker_leadership/</c>.
/// </summary>
internal static class TankerSpec
{
    private const string Crafted = "Crafted_";
    private const string Might = "Superior_Attuned_Superior_Might_of_the_Tanker_";
    private const string Cold = "Superior_Attuned_Blistering_Cold_";
    private const string Bite = "Superior_Attuned_Winters_Bite_";

    public static BuildSpec Spec { get; } = new()
    {
        ClassName = "Class_Tanker",
        Origin = "Science",
        Alignment = "Hero",
        Name = "WP-SS Tanker - Hard Mode (Leadership)",
        Comment = "Willpower / Super Strength / Fire Mastery.",
        PowerSets =
        [
            "Tanker_Defense.Willpower",
            "Tanker_Melee.Super_Strength",
            string.Empty,
            "Pool.Leaping",
            "Pool.Fighting",
            "Pool.Leadership",
            "Pool.Speed",
            "Epic.Fire_Mastery",
        ],
        Picks = Picks(),
        Inherents = Inherents(),
        Incarnates = ["Incarnate.Alpha.Cardiac_Radial_Paragon"],
    };

    public static PickSpec Pick(string power, int level, IReadOnlyList<string?> enhancements, bool statInclude = false, bool essential = false)
    {
        return new PickSpec { Power = power, Level = level, Enhancements = enhancements, StatInclude = statInclude, Essential = essential };
    }

    private static string[] Set(string prefix, string letters)
    {
        return [.. letters.Select(letter => prefix + letter)];
    }

    private static List<PickSpec> Picks()
    {
        return
        [
            Pick("Tanker_Defense.Willpower.High_Pain_Tolerance", 1, [.. Set(Crafted + "Unbreakable_Guard_", "ABDEF"), Crafted + "Steadfast_Protection_B"], true),
            Pick("Tanker_Melee.Super_Strength.Punch", 1, Set(Might, "ACBDEF"), essential: true),
            Pick("Tanker_Defense.Willpower.Mind_Over_Body", 2, Set(Crafted + "Unbreakable_Guard_", "ABDE"), true, true),
            Pick("Tanker_Melee.Super_Strength.Haymaker", 4, Set(Crafted + "Makos_Bite_", "AEBCDF"), essential: true),
            Pick("Tanker_Defense.Willpower.Indomitable_Will", 6, Set(Crafted + "Red_Fortune_", "ABDECF"), true, true),
            Pick("Tanker_Defense.Willpower.Rise_to_the_Challenge", 8, Set(Crafted + "Preventive_Medicine_", "ABCDE"), true, true),
            Pick("Pool.Leaping.Combat_Jumping", 10, [Crafted + "Luck_of_the_Gambler_F", Crafted + "Shield_Wall_F", Crafted + "Luck_of_the_Gambler_A"], true, true),
            Pick("Pool.Leaping.Long_Jump", 12, [Crafted + "Blessing_of_the_Zephyr_C"]),
            Pick("Pool.Fighting.Boxing", 14, [Crafted + "Accuracy"]),
            Pick("Pool.Fighting.Tough", 16, Set(Crafted + "Gladiators_Armor_", "ABDECF"), true),
            Pick("Tanker_Defense.Willpower.Heightened_Senses", 18, Set(Crafted + "Luck_of_the_Gambler_", "BDEF"), true),
            Pick("Tanker_Melee.Super_Strength.Knockout_Blow", 20, Set(Cold, "ACBDEF")),
            Pick("Tanker_Defense.Willpower.Fast_Healing", 22, [Crafted + "Numinas_Convalesence_A"], true),
            Pick("Tanker_Melee.Super_Strength.Rage", 24, [Crafted + "Recharge", Crafted + "Recharge"], true),
            Pick("Pool.Leadership.Defense", 26, [Crafted + "Luck_of_the_Gambler_F", Crafted + "Red_Fortune_A", Crafted + "Red_Fortune_E"], true),
            Pick("Tanker_Defense.Willpower.Quick_Recovery", 28, [Crafted + "Recovery", Crafted + "Recovery"], true),
            Pick("Tanker_Melee.Super_Strength.Foot_Stomp", 30, Set(Crafted + "Eradication_", "DEACBF")),
            Pick("Tanker_Defense.Willpower.Strength_of_Will", 32, [Crafted + "Unbreakable_Guard_A", Crafted + "Unbreakable_Guard_E"], true),
            Pick("Pool.Leadership.Tactics", 35, [Crafted + "Endurance_Discount"], true),
            Pick("Epic.Fire_Mastery.Fire_Blast", 38, Set(Bite, "ACBDEF")),
            Pick("Epic.Fire_Mastery.Fire_Shield", 41, Set(Crafted + "Aegis_", "ABED"), true),
            Pick("Pool.Leadership.Vengeance", 44, [Crafted + "Luck_of_the_Gambler_F"]),
            Pick("Tanker_Melee.Super_Strength.Taunt", 47, Set(Crafted + "Mocking_Beratement_", "ABCD")),
            Pick("Pool.Speed.Hasten", 49, [Crafted + "Recharge", Crafted + "Recharge"], true),
        ];
    }

    private static List<PickSpec> Inherents()
    {
        return
        [
            Pick("Inherent.Inherent.Sprint", 1, [Crafted + "Celerity_C"]),
            Pick("Inherent.Fitness.Swift", 1, [Crafted + "Run"], true),
            Pick("Inherent.Fitness.Health", 1, [Crafted + "Numinas_Convalesence_F", Crafted + "Miracle_F", Crafted + "Panacea_F"], true),
            Pick("Inherent.Fitness.Hurdle", 1, [Crafted + "Jump"], true),
            Pick("Inherent.Fitness.Stamina", 1, [Crafted + "Performance_Shifter_F", Crafted + "Performance_Shifter_A"], true),
        ];
    }
}
