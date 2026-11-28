using System.Globalization;

using CohEngine.Core.BuildFile;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Registry;

public sealed class HardLimitTests
{
    private const int MaxSlots = 67;

    private static LevelSchedule Schedule => Fixtures.Store.Levels;

    public static TheoryData<string> SampleBuilds()
    {
        return [.. BuildFileFixtures.SampleMbd().Select(BuildFileFixtures.SamplePath)];
    }

    private static Power Pick(
        int buildIndex,
        string fullName,
        int level = 1,
        int pickLevel = 0,
        string[][]? requires = null,
        string[][]? requiresNot = null,
        bool chosen = true)
    {
        return Make.Power() with
        {
            BuildIndex = buildIndex,
            NidPower = buildIndex,
            FullName = fullName,
            StaticIndex = buildIndex,
            PowerType = "Click",
            Level = level,
            PickLevel = pickLevel,
            RequiresPowers = requires?.Cast<IReadOnlyList<string>>().ToArray() ?? [],
            RequiresPowersNot = requiresNot?.Cast<IReadOnlyList<string>>().ToArray() ?? [],
            Chosen = chosen,
        };
    }

    private static HashSet<string> Ids(IEnumerable<Diagnostic> diagnostics)
    {
        return [.. diagnostics.Select(diagnostic => diagnostic.RuleId)];
    }

    private static IReadOnlyList<Diagnostic> Check(IReadOnlyList<Power> powers, IReadOnlyDictionary<int, IReadOnlyList<SlotRef>>? slots = null, MbdFormat? format = null)
    {
        return HardLimitRunner.Check(powers, slots ?? RegistryMake.Slots(), Schedule, MaxSlots, format);
    }

    private static Power[] Numbered(int count, string prefix, int pickLevel = 0)
    {
        return [.. Enumerable.Range(0, count).Select(index => Pick(index, string.Create(CultureInfo.InvariantCulture, $"{prefix}.P{index}"), pickLevel: pickLevel))];
    }

    private static SlotRef[] AddedAt(int count)
    {
        return [RegistryMake.Slot(0, 0, inherent: true), .. Schedule.SlotGrantLevels.Order().Take(count).Select(level => RegistryMake.Slot(1, level))];
    }

    [Fact]
    public void The_registry_lists_the_fifteen_rules_without_duplicates()
    {
        string[] names = [.. RuleRegistry.HardLimits.Select(rule => rule.Id)];

        Assert.Equal(15, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("_rule_mbd_level_text", names, StringComparer.Ordinal);
        Assert.Contains("_rule_pool_count", names, StringComparer.Ordinal);
        Assert.Contains("_rule_level_one_pick_order", names, StringComparer.Ordinal);
    }

    [Fact]
    public void A_small_legal_build_is_clean()
    {
        Power[] powers = [Pick(0, "Scrapper_Melee.Dark_Melee.Smite"), Pick(3, "Pool.Speed.Hasten", level: 4, pickLevel: 3)];
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = RegistryMake.Slots(
            (0, [RegistryMake.Slot(1, 0, inherent: true), RegistryMake.Slot(1, 2), RegistryMake.Slot(1, 4)]),
            (3, [RegistryMake.Slot(1, 3, inherent: true)]));

        Assert.Empty(Check(powers, slots));
    }

    [Fact]
    public void More_than_the_cap_of_added_slots_is_h_slot_001()
    {
        Power[] powers = Numbered(14, "Scrapper_Melee.Dark_Melee");
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = powers.ToDictionary(power => power.BuildIndex, power => (IReadOnlyList<SlotRef>)AddedAt(5));

        Diagnostic found = Check(powers, slots).First(diagnostic => string.Equals(diagnostic.RuleId, "H-SLOT-001", StringComparison.Ordinal));

        Assert.Equal(("total added slots (70) exceeds the cap (67)", "remove 3 slot(s) from one or more powers"), (found.Message, found.Fix));
    }

    [Fact]
    public void A_sixth_added_slot_is_h_slot_002()
    {
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = RegistryMake.Slots((0, AddedAt(6)));

        Assert.Contains("H-SLOT-002", Ids(Check([Pick(0, "Scrapper_Melee.Dark_Melee.Smite")], slots)));
    }

    [Fact]
    public void Five_added_slots_is_the_legal_maximum()
    {
        Assert.Empty(Check([Pick(0, "Scrapper_Melee.Dark_Melee.Smite")], RegistryMake.Slots((0, AddedAt(5)))));
    }

    [Fact]
    public void A_slot_before_the_pick_level_is_h_slot_003()
    {
        Power power = Pick(0, "Scrapper_Melee.Dark_Melee.Shadow_Maul", level: 2, pickLevel: 14);

        Assert.Contains("H-SLOT-003", Ids(Check([power], RegistryMake.Slots((0, [RegistryMake.Slot(1, 14, inherent: true), RegistryMake.Slot(1, 2)])))));
    }

    [Fact]
    public void A_slot_off_a_grant_level_is_h_slot_004()
    {
        Assert.Contains("H-SLOT-004", Ids(Check([Pick(0, "Scrapper_Melee.Dark_Melee.Smite")], RegistryMake.Slots((0, [RegistryMake.Slot(1, 0, inherent: true), RegistryMake.Slot(1, 1)])))));
    }

    [Fact]
    public void Slots_placed_before_they_are_earned_are_h_slot_005()
    {
        Power[] powers = Numbered(3, "Scrapper_Melee.Dark_Melee");
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = powers.ToDictionary<Power, int, IReadOnlyList<SlotRef>>(power => power.BuildIndex, _ => [RegistryMake.Slot(1, 0), RegistryMake.Slot(1, 2)]);

        Diagnostic found = Check(powers, slots).Single(diagnostic => string.Equals(diagnostic.RuleId, "H-SLOT-005", StringComparison.Ordinal));

        Assert.Equal(("by level 3, 3 added slots are placed but only 2 have been granted (a slot cannot be placed before it is earned)", (object)2, (object)3), (found.Message, found.Expected, found.Actual));
    }

    [Fact]
    public void Deferred_slotting_is_legal()
    {
        SlotRef[] added = [RegistryMake.Slot(1, 0), RegistryMake.Slot(1, 2), RegistryMake.Slot(1, 4), RegistryMake.Slot(1, 4), RegistryMake.Slot(1, 4)];

        Assert.DoesNotContain("H-SLOT-005", Ids(Check([Pick(0, "Scrapper_Melee.Dark_Melee.Smite")], RegistryMake.Slots((0, added)))));
    }

    [Fact]
    public void Twenty_five_picks_is_h_power_001()
    {
        Assert.Contains("H-POWER-001", Ids(Check(Numbered(25, "Scrapper_Melee.Dark_Melee"))));
    }

    [Fact]
    public void Inherents_do_not_count_toward_picks()
    {
        Power[] powers = [.. Numbered(24, "Scrapper_Melee.Dark_Melee"), Pick(24, "Inherent.Fitness.Stamina")];

        Assert.DoesNotContain("H-POWER-001", Ids(Check(powers)));
    }

    [Theory]
    [InlineData("Incarnate.Alpha.Agility_Core_Paragon", 50, 49)]
    [InlineData("Temporary_Powers.Accolades.Task_Force_Commander", 1, 0)]
    public void Incarnates_and_accolades_are_not_picks(string fullName, int level, int pickLevel)
    {
        Power[] powers = [.. Schedule.PowerPickLevels.Select((pick, index) => Pick(index, string.Create(CultureInfo.InvariantCulture, $"Dominator_Control.Arsenal_Control.P{index}"), pickLevel: pick)), Pick(24, fullName, level: level, pickLevel: pickLevel)];

        Assert.DoesNotContain(Ids(Check(powers)), ruleId => ruleId.StartsWith("H-POWER-00", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unchosen_sub_power_is_not_a_pick()
    {
        Power[] powers = [.. Numbered(24, "Blaster_Ranged.Electrical_Blast"), Pick(24, "Pool.Sorcery.Translocation", pickLevel: 5, chosen: false)];

        Assert.DoesNotContain("H-POWER-001", Ids(Check(powers)));
    }

    [Fact]
    public void Picking_below_the_database_level_is_h_power_002()
    {
        Assert.Contains("H-POWER-002", Ids(Check([Pick(0, "Scrapper_Defense.Shield_Defense.One_with_the_Shield", level: 32, pickLevel: 19)])));
    }

    [Fact]
    public void An_ancillary_power_at_its_level_is_available()
    {
        Assert.DoesNotContain("H-POWER-002", Ids(Check([Pick(0, "Epic.Stalker_Soul_Mastery.Moonbeam", level: 35, pickLevel: 34)])));
    }

    [Fact]
    public void A_pick_at_a_non_pick_level_is_h_power_003()
    {
        Assert.Contains("H-POWER-003", Ids(Check([Pick(0, "Scrapper_Melee.Dark_Melee.Smite", pickLevel: 2)])));
    }

    [Fact]
    public void A_pick_out_of_schedule_order_is_h_power_003_at_each_swapped_position()
    {
        int[] levels = [.. Schedule.PowerPickLevels];
        (levels[22], levels[23]) = (levels[23], levels[22]);
        Power[] powers = [.. levels.Select((level, index) => Pick(index, string.Create(CultureInfo.InvariantCulture, $"Tanker_Melee.Super_Strength.P{index}"), pickLevel: level))];

        Diagnostic[] found = [.. Check(powers).Where(diagnostic => string.Equals(diagnostic.RuleId, "H-POWER-003", StringComparison.Ordinal))];

        Assert.Equal([(47, 49), (49, 47)], found.Select(diagnostic => ((int)diagnostic.Expected!, (int)diagnostic.Actual!)));
        Assert.Contains("pick 23 of 24", found[0].Message, StringComparison.Ordinal);
        Assert.Contains("'Tanker_Melee.Super_Strength.P22'", found[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Picks_in_schedule_order_are_legal()
    {
        Power[] powers = [.. Schedule.PowerPickLevels.Select((level, index) => Pick(index, string.Create(CultureInfo.InvariantCulture, $"Tanker_Melee.Super_Strength.P{index}"), pickLevel: level))];

        Assert.DoesNotContain("H-POWER-003", Ids(Check(powers)));
    }

    private static MbdFormat PowerSetsFormat(string primary, string secondary)
    {
        return new MbdFormat([primary, secondary, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty], "49", new Dictionary<int, (int Min, int Max)>());
    }

    private static Diagnostic[] LevelOneOrder(MbdFormat? format, params string[] levelOnePicks)
    {
        return LevelOneOrder([.. levelOnePicks.Select((name, index) => Pick(index, name))], format);
    }

    private static Diagnostic[] LevelOneOrder(IReadOnlyList<Power> powers, MbdFormat? format)
    {
        return [.. Check(powers, slots: null, format).Where(diagnostic => string.Equals(diagnostic.RuleId, "H-POWER-006", StringComparison.Ordinal))];
    }

    [Fact]
    public void A_level_one_secondary_pick_before_the_primary_is_h_power_006_naming_both()
    {
        MbdFormat format = PowerSetsFormat("Blaster_Ranged.Fire_Blast", "Blaster_Support.Fire_Manipulation");

        Diagnostic found = Assert.Single(LevelOneOrder(format, "Blaster_Support.Fire_Manipulation.Ring_of_Fire", "Blaster_Ranged.Fire_Blast.Flares"));

        Assert.Equal(
            ("'Blaster_Support.Fire_Manipulation.Ring_of_Fire', the secondary pick, comes before 'Blaster_Ranged.Fire_Blast.Flares', the primary pick, at level 1; "
                + "the level-up view offers the primary pick first",
            "swap 'Blaster_Support.Fire_Manipulation.Ring_of_Fire' and 'Blaster_Ranged.Fire_Blast.Flares' so the primary pick comes first", "error"),
            (found.Message, found.Fix, found.Severity));
    }

    [Fact]
    public void Level_one_picks_with_the_primary_first_are_legal()
    {
        MbdFormat format = PowerSetsFormat("Blaster_Ranged.Fire_Blast", "Blaster_Support.Fire_Manipulation");

        Assert.Empty(LevelOneOrder(format, "Blaster_Ranged.Fire_Blast.Flares", "Blaster_Support.Fire_Manipulation.Ring_of_Fire"));
    }

    [Theory]
    [InlineData("Blaster_Ranged.Fire_Blast.Flares", "Blaster_Ranged.Fire_Blast.Fire_Blast", "replace 'Blaster_Ranged.Fire_Blast.Fire_Blast' with a power from 'Blaster_Support.Fire_Manipulation'")]
    [InlineData("Blaster_Support.Fire_Manipulation.Ring_of_Fire", "Blaster_Support.Fire_Manipulation.Fire_Sword", "replace 'Blaster_Support.Fire_Manipulation.Ring_of_Fire' with a power from 'Blaster_Ranged.Fire_Blast'")]
    public void Two_level_one_picks_from_one_set_are_h_power_006_naming_both(string first, string second, string fix)
    {
        MbdFormat format = PowerSetsFormat("Blaster_Ranged.Fire_Blast", "Blaster_Support.Fire_Manipulation");

        Diagnostic found = Assert.Single(LevelOneOrder(format, first, second));

        Assert.Equal(
            ($"the level 1 picks are '{first}' and '{second}'; the level-up view offers a primary pick and then a secondary pick at level 1", fix, "error"),
            (found.Message, found.Fix, found.Severity));
    }

    [Fact]
    public void The_level_one_order_is_not_checked_without_the_power_sets()
    {
        Assert.Empty(LevelOneOrder(format: null, "Blaster_Support.Fire_Manipulation.Ring_of_Fire", "Blaster_Ranged.Fire_Blast.Flares"));
        Assert.Empty(LevelOneOrder(Format(1), "Blaster_Support.Fire_Manipulation.Ring_of_Fire", "Blaster_Ranged.Fire_Blast.Flares"));
        Assert.Empty(LevelOneOrder(PowerSetsFormat(string.Empty, "Blaster_Support.Fire_Manipulation"), "Blaster_Support.Fire_Manipulation.Ring_of_Fire", "Blaster_Ranged.Fire_Blast.Flares"));
        Assert.Empty(LevelOneOrder(PowerSetsFormat("Blaster_Ranged.Fire_Blast", string.Empty), "Blaster_Support.Fire_Manipulation.Ring_of_Fire", "Blaster_Ranged.Fire_Blast.Flares"));
    }

    [Fact]
    public void A_single_level_one_pick_is_left_to_h_power_003()
    {
        Power[] powers = [Pick(0, "Blaster_Support.Fire_Manipulation.Ring_of_Fire"), Pick(1, "Blaster_Ranged.Fire_Blast.Flares", pickLevel: 1)];

        IReadOnlyList<Diagnostic> found = Check(powers, slots: null, PowerSetsFormat("Blaster_Ranged.Fire_Blast", "Blaster_Support.Fire_Manipulation"));

        Assert.Equal((false, true), (Ids(found).Contains("H-POWER-006"), Ids(found).Contains("H-POWER-003")));
    }

    [Fact]
    public void An_arachnos_soldiers_base_sets_are_its_primary_and_secondary_at_level_one()
    {
        // A Bane Spider build lists the branch sets; the level 1 picks come from the base sets, which share their categories.
        MbdFormat format = PowerSetsFormat("Arachnos_Soldiers.Bane_Spider_Soldier", "Training_Gadgets.Bane_Spider_Training");
        const string Armor = "Training_Gadgets.Training_and_Gadgets.Wolf_Spider_Armor";
        const string Shot = "Arachnos_Soldiers.Arachnos_Soldier.Single_Shot";

        Assert.Equal($"swap '{Armor}' and '{Shot}' so the primary pick comes first", Assert.Single(LevelOneOrder(format, Armor, Shot)).Fix);
        Assert.Empty(LevelOneOrder(format, Shot, Armor));
    }

    [Theory]
    [MemberData(nameof(SampleBuilds))]
    public void Every_committed_sample_build_passes_the_level_one_pick_order(string sample)
    {
        MbdBuild build = Mbd.LoadMbd(BuildFileFixtures.Sample(sample));
        MbdBuild swapped = build with { PowerEntries = [build.PowerEntries[1], build.PowerEntries[0], .. build.PowerEntries.Skip(2)] };
        MbdFormat format = new(build.PowerSets, "49", new Dictionary<int, (int Min, int Max)>());

        // The swapped copy shows the rule reads this sample's level 1 picks, so the pass is not vacuous.
        Assert.Empty(LevelOneOrder(Mbd.PowersFromBuild(build, BuildFileFixtures.Database), format));
        string fix = Assert.Single(LevelOneOrder(Mbd.PowersFromBuild(swapped, BuildFileFixtures.Database), format)).Fix;
        Assert.Contains($"'{build.PowerEntries[0].PowerName}'", fix, StringComparison.Ordinal);
        Assert.Contains($"'{build.PowerEntries[1].PowerName}'", fix, StringComparison.Ordinal);
    }

    [Fact]
    public void Five_pools_is_h_pool_001_and_four_is_legal()
    {
        Power[] five = [.. "Speed,Leaping,Fighting,Leadership,Flight".Split(',').Select((name, index) => Pick(index, $"Pool.{name}.T1"))];
        Power[] four = [.. five.Take(4), Pick(9, "Epic.Stalker_Soul_Mastery.Moonbeam", level: 35, pickLevel: 34)];

        Assert.Equal((true, false), (Ids(Check(five)).Contains("H-POOL-001"), Ids(Check(four)).Contains("H-POOL-001")));
    }

    [Fact]
    public void A_missing_prerequisite_is_h_power_004()
    {
        Power power = Pick(0, "Epic.Stalker_Soul_Mastery.Dark_Blast", level: 41, pickLevel: 40, requires: [["Epic.Stalker_Soul_Mastery.Gloom"]]);

        Diagnostic found = Check([power]).Single(diagnostic => string.Equals(diagnostic.RuleId, "H-POWER-004", StringComparison.Ordinal));

        Assert.Contains("requires a prerequisite power (Epic.Stalker_Soul_Mastery.Gloom) picked at or before it", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_prerequisite_picked_earlier_satisfies_and_one_picked_later_does_not()
    {
        Power dependent = Pick(1, "Epic.Stalker_Soul_Mastery.Dark_Blast", level: 41, pickLevel: 40, requires: [["Epic.Stalker_Soul_Mastery.Gloom"]]);
        Power early = Pick(0, "Epic.Stalker_Soul_Mastery.Gloom", level: 35, pickLevel: 34);
        Power late = Pick(0, "Epic.Stalker_Soul_Mastery.Gloom", level: 35, pickLevel: 43);

        Assert.Equal((false, true), (Ids(Check([early, dependent])).Contains("H-POWER-004"), Ids(Check([late, dependent])).Contains("H-POWER-004")));
    }

    [Fact]
    public void Two_of_three_as_an_or_of_and_pairs_is_satisfied_by_any_pair()
    {
        string[] names = ["Pool.Leadership.Maneuvers", "Pool.Leadership.Assault", "Pool.Leadership.Tactics"];
        Power[] powers =
        [
            Pick(0, names[0], level: 4, pickLevel: 3),
            Pick(2, names[2], level: 4, pickLevel: 7),
            Pick(3, "Pool.Leadership.Vengeance", level: 14, pickLevel: 13, requires: [[names[0], names[1]], [names[0], names[2]], [names[1], names[2]]]),
        ];

        Assert.DoesNotContain("H-POWER-004", Ids(Check(powers)));
    }

    [Fact]
    public void A_forbidden_power_in_the_build_is_h_power_005_and_absent_is_legal()
    {
        Power forbidding = Pick(1, "Pool.Speed.Burnout", level: 14, pickLevel: 15, requiresNot: [["Pool.Speed.Whirlwind"]]);
        Power forbidden = Pick(0, "Pool.Speed.Whirlwind", level: 14, pickLevel: 13);

        Assert.Equal((true, false), (Ids(Check([forbidden, forbidding])).Contains("H-POWER-005"), Ids(Check([forbidding])).Contains("H-POWER-005")));
    }

    [Fact]
    public void Two_ancillary_pools_is_h_pool_002_and_one_is_legal()
    {
        Power moonbeam = Pick(0, "Epic.Stalker_Soul_Mastery.Moonbeam", level: 35, pickLevel: 34);
        Power meld = Pick(1, "Epic.Stalker_Soul_Mastery.Shadow_Meld", level: 38, pickLevel: 37);
        Power lightning = Pick(1, "Epic.Mu_Mastery.Mu_Lightning", level: 35, pickLevel: 37);

        Assert.Equal((true, false), (Ids(Check([moonbeam, lightning])).Contains("H-POOL-002"), Ids(Check([moonbeam, meld])).Contains("H-POOL-002")));
    }

    [Fact]
    public void The_virtual_power_is_not_a_pick()
    {
        Power[] powers = [Pick(-1, string.Empty), Pick(0, "Scrapper_Melee.Dark_Melee.Smite")];

        Assert.Equal("Scrapper_Melee.Dark_Melee.Smite", Assert.Single(HardLimitRunner.BuildContext(powers, RegistryMake.Slots(), Schedule, MaxSlots, format: null).Picks).FullName);
    }

    private static MbdFormat Format(int powerSets = 8)
    {
        return FormatWith(powerSets, "49");
    }

    private static MbdFormat FormatWith(int powerSets, object? levelText)
    {
        return new MbdFormat([.. new string[powerSets].Select(_ => "a")], levelText, new Dictionary<int, (int Min, int Max)> { [1] = (9, 29) });
    }

    private static HashSet<string> FormatIds(MbdFormat? format, int ioLevel = 29)
    {
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = RegistryMake.Slots((0, [RegistryMake.Slot(1, 0, inherent: true, ioLevel: ioLevel)]));
        return Ids(Check([Pick(0, "Scrapper_Melee.Dark_Melee.Smite")], slots, format));
    }

    [Fact]
    public void The_format_rules_are_silent_without_a_format()
    {
        Assert.DoesNotContain(FormatIds(format: null, ioLevel: 99), ruleId => ruleId.StartsWith("H-FMT", StringComparison.Ordinal));
    }

    [Fact]
    public void A_well_formed_mbd_has_no_format_violations()
    {
        Assert.Empty(FormatIds(Format()));
    }

    [Fact]
    public void Seven_powersets_is_h_fmt_001()
    {
        Diagnostic found = Check([Pick(0, "Scrapper_Melee.Dark_Melee.Smite")], slots: null, Format(7)).Single(diagnostic => string.Equals(diagnostic.RuleId, "H-FMT-001", StringComparison.Ordinal));

        Assert.Equal(((object)8, (object)7, true), (found.Expected, found.Actual, found.Fix.Contains("pad", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_io_level_outside_its_bounds_is_h_fmt_002()
    {
        Assert.Equal((true, false), (FormatIds(Format(), ioLevel: 49).Contains("H-FMT-002"), FormatIds(Format(), ioLevel: 29).Contains("H-FMT-002")));
    }

    [Fact]
    public void An_enhancement_with_no_bounds_is_not_checked()
    {
        MbdFormat format = Format() with { IoBoundsByNid = new Dictionary<int, (int Min, int Max)> { [2] = (9, 29) } };

        Assert.DoesNotContain("H-FMT-002", FormatIds(format, ioLevel: 49));
    }

    [Theory]
    [InlineData("50")]
    [InlineData("-1")]
    [InlineData("x")]
    [InlineData("--1")]
    [InlineData("")]
    [InlineData(49)]
    [InlineData(null)]
    public void A_bad_level_text_is_h_fmt_003(object? levelText)
    {
        Assert.Contains("H-FMT-003", FormatIds(FormatWith(8, levelText)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("49")]
    [InlineData("-0")]
    public void A_good_level_text_passes(string levelText)
    {
        Assert.Null(MbdFormat.LevelTextProblem(levelText));
    }

    [Fact]
    public void The_level_text_problem_names_the_value()
    {
        Assert.Equal("'50' is not a string of an integer from 0 to 49", MbdFormat.LevelTextProblem("50"));
        Assert.Equal("49 is not a string of an integer from 0 to 49", MbdFormat.LevelTextProblem(49));
    }

    [Fact]
    public void The_runner_refuses_null_inputs()
    {
        Assert.Throws<ArgumentNullException>(() => HardLimitRunner.Check(null!, RegistryMake.Slots(), Schedule, MaxSlots));
        Assert.Throws<ArgumentNullException>(() => HardLimitRunner.Check([], null!, Schedule, MaxSlots));
        Assert.Throws<ArgumentNullException>(() => HardLimitRunner.Check([], RegistryMake.Slots(), null!, MaxSlots));
    }
}
