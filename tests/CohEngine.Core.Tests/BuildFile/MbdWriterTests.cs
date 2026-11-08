using System.Text;

using CohEngine.Core.BuildFile;
using CohEngine.Core.Data;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>Port of <c>test_mbd_writer.py</c>: a build written as the game grants it, one level-by-level walk.</summary>
public sealed class MbdWriterTests
{
    private static readonly int[] PickLevels = [1, 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 35, 38, 41, 44, 47, 49];

    private static readonly MbdDb Database = BuildFileFixtures.Database;

    private static readonly LevelSchedule Schedule = Fixtures.Store.Levels;

    private static BuildSpec Spec(IReadOnlyList<PickSpec> picks, IReadOnlyList<PickSpec>? inherents = null)
    {
        return new BuildSpec
        {
            ClassName = "Class_Tanker",
            Origin = "Science",
            Alignment = "Hero",
            Name = "test",
            Comment = string.Empty,
            PowerSets = ["Tanker_Defense.Willpower", "Tanker_Melee.Super_Strength", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty],
            Picks = picks,
            Inherents = inherents ?? [],
        };
    }

    // 24 picks on the schedule's own levels; `added` maps a pick position to its added slots, `overrides` replaces a pick.
    private static List<PickSpec> Full(Dictionary<int, int>? added = null, params (int Index, PickSpec Pick)[] overrides)
    {
        List<PickSpec> picks =
        [
            .. PickLevels.Select((level, index) => TankerSpec.Pick(
                $"Tanker_Melee.Super_Strength.P{index}",
                level,
                [.. Enumerable.Repeat("Crafted_Accuracy", 1 + (added?.GetValueOrDefault(index) ?? 0))])),
        ];
        foreach ((int index, PickSpec pick) in overrides)
        {
            picks[index] = pick;
        }

        return picks;
    }

    private static MbdBuild Written(BuildSpec spec)
    {
        return Mbd.ParseMbd(Mbd.WriteMbd(spec, Schedule, Database));
    }

    private static string Message(BuildSpec spec)
    {
        return Assert.Throws<InvalidDataException>(() => Mbd.WriteMbd(spec, Schedule, Database)).Message;
    }

    [Fact]
    public void Picks_are_written_in_schedule_order_whatever_order_the_spec_lists_them()
    {
        List<PickSpec> picks = Full(new Dictionary<int, int> { [0] = 2, [3] = 1 });

        MbdBuild build = Written(Spec([.. picks.AsEnumerable().Reverse()]));

        Assert.Equal(PickLevels, build.PowerEntries.Take(24).Select(entry => entry.Level + 1));
        Assert.Equal([picks[1].Power, picks[0].Power, .. picks.Skip(2).Select(pick => pick.Power)], build.PowerEntries.Take(24).Select(entry => entry.PowerName), StringComparer.Ordinal);
        Assert.Equal([1, 3, 3], build.PowerEntries[1].SlotEntries.Select(slot => slot.Level + 1));
        Assert.Equal([4, 5], build.PowerEntries[3].SlotEntries.Select(slot => slot.Level + 1));
        Assert.Equal((24, "2026.1.1242"), (build.LastPower, build.Meta.DatabaseVersion));
    }

    [Fact]
    public void A_pick_off_its_schedule_level_is_E46()
    {
        List<PickSpec> picks = Full(null, (22, TankerSpec.Pick("Tanker_Melee.Super_Strength.Taunt", 48, ["Crafted_Accuracy"])));

        Assert.Matches("E46: pick 23 of 24 .*Taunt.* is at level 48; the schedule grants it at level 47", Message(Spec(picks)));
        Assert.Equal("E46: 25 picks; the schedule grants 24", Message(Spec([.. Full(), TankerSpec.Pick("Pool.Speed.Hasten", 50, [null])])));
    }

    [Fact]
    public void A_slot_before_its_power_is_E47()
    {
        PickSpec early = TankerSpec.Pick("Tanker_Melee.Super_Strength.Haymaker", 4, ["Crafted_Accuracy", "Crafted_Accuracy"]) with { SlotLevels = [3] };

        Assert.Matches("E47: .*Haymaker.* places a slot at level 3, before it is picked at level 4", Message(Spec(Full(null, (3, early)))));
    }

    [Fact]
    public void A_slot_off_a_grant_level_is_E48()
    {
        PickSpec off = TankerSpec.Pick("Tanker_Melee.Super_Strength.Haymaker", 4, ["Crafted_Accuracy", "Crafted_Accuracy"]) with { SlotLevels = [6] };

        Assert.Matches("E48: .*Haymaker.* places a slot at level 6, which grants no slot", Message(Spec(Full(null, (3, off)))));
    }

    [Theory]
    [InlineData(1, 3, "names 1 slot level for 2 added slots")]
    [InlineData(2, 2, "names 2 slot levels for 1 added slots")]
    public void A_short_or_long_pin_list_is_E48(int pins, int enhancements, string expected)
    {
        PickSpec pinned = TankerSpec.Pick("Tanker_Melee.Super_Strength.Haymaker", 4, [.. Enumerable.Repeat("Crafted_Accuracy", enhancements)]) with { SlotLevels = [.. Enumerable.Repeat(5, pins)] };

        Assert.Contains(expected, Message(Spec(Full(null, (3, pinned)))), StringComparison.Ordinal);
    }

    [Fact]
    public void Pinned_slots_are_kept_and_the_rest_placed_around_them()
    {
        PickSpec fixedSlots = TankerSpec.Pick("Tanker_Melee.Super_Strength.Haymaker", 4, [.. Enumerable.Repeat("Crafted_Accuracy", 3)]) with { SlotLevels = [50, 50] };

        MbdBuild build = Written(Spec(Full(new Dictionary<int, int> { [0] = 1 }, (3, fixedSlots))));

        Assert.Equal([4, 50, 50], build.PowerEntries[3].SlotEntries.Select(slot => slot.Level + 1));
        Assert.Equal([1, 3], build.PowerEntries[0].SlotEntries.Select(slot => slot.Level + 1));
    }

    [Fact]
    public void A_pin_list_on_a_power_with_no_added_slots_is_ignored()
    {
        PickSpec single = TankerSpec.Pick("Tanker_Melee.Super_Strength.Haymaker", 4, ["Crafted_Accuracy"]) with { SlotLevels = [5] };

        Assert.Single(Written(Spec(Full(null, (3, single)))).PowerEntries[3].SlotEntries);
    }

    [Fact]
    public void Demand_the_schedule_cannot_place_for_one_late_pick_is_E49()
    {
        PickSpec late = TankerSpec.Pick("Pool.Speed.Hasten", 49, [.. Enumerable.Repeat("Crafted_Recharge", 5)]);

        Assert.Matches("E49: .*Hasten.* picked at level 49 needs 4 added slots; 3 can be placed from level 49", Message(Spec(Full(null, (23, late)))));
    }

    [Fact]
    public void More_added_slots_than_the_schedule_grants_is_E49()
    {
        Dictionary<int, int> greedy = Enumerable.Range(0, 14).ToDictionary(index => index, _ => 5);

        Assert.Equal("E49: 70 added slots; the schedule grants 67", Message(Spec(Full(greedy))));
    }

    [Fact]
    public void Two_late_picks_that_fit_alone_but_not_together_are_E49()
    {
        PickSpec taunt = TankerSpec.Pick("Tanker_Melee.Super_Strength.Taunt", 47, [.. Enumerable.Repeat("Crafted_Accuracy", 5)]);
        PickSpec hasten = TankerSpec.Pick("Pool.Speed.Hasten", 49, [.. Enumerable.Repeat("Crafted_Recharge", 4)]);

        Assert.Contains("E49: the powers picked late need more added slots", Message(Spec(Full(null, (22, taunt), (23, hasten)))), StringComparison.Ordinal);
    }

    [Fact]
    public void The_fill_never_gives_a_grant_that_strands_a_later_pick()
    {
        List<PickSpec> powers = [TankerSpec.Pick("A", 1, [null, null]), TankerSpec.Pick("B", 49, [null, null])];
        Dictionary<int, List<int>> placed = new() { [0] = [], [1] = [] };

        Mbd.FillSlots(powers, new Dictionary<int, int> { [0] = 1, [1] = 1 }, [50], placed);

        Assert.Equal([[], []], placed.Values);
    }

    [Fact]
    public void Essential_powers_get_two_added_slots_by_level_15()
    {
        Dictionary<int, int> hungry = Enumerable.Range(1, 5).ToDictionary(index => index, _ => 5);
        PickSpec toggle = TankerSpec.Pick("Tanker_Defense.Willpower.Indomitable_Will", 6, [.. Enumerable.Repeat("Crafted_Accuracy", 3)], true, true);
        PickSpec anchor = TankerSpec.Pick("Tanker_Melee.Super_Strength.Punch", 1, [.. Enumerable.Repeat("Crafted_Accuracy", 3)], true, true);

        MbdBuild build = Written(Spec(Full(hungry, (0, anchor), (4, toggle))));

        Assert.All(new[] { build.PowerEntries[0], build.PowerEntries[4] }, entry => Assert.True(entry.SlotEntries.Skip(1).Count(slot => slot.Level + 1 <= 15) >= 2, entry.PowerName));
    }

    [Fact]
    public void The_writer_reproduces_the_committed_tanker_build()
    {
        byte[] written = Mbd.Serialize(Mbd.WriteMbd(TankerSpec.Spec, Schedule, Database));

        Assert.Equal(File.ReadAllBytes(Path.Combine(BuildFileFixtures.Root, "builds", "wpss_tanker_leadership.mbd")), written);
    }

    [Fact]
    public void Midsreborn_loads_every_pick_and_slot_where_the_writer_put_them()
    {
        BuildFixture dump = Fixtures.Store.Build("wpss_tanker_leadership");
        MbdBuild build = Mbd.LoadMbd(Path.Combine(BuildFileFixtures.Root, "builds", "wpss_tanker_leadership.mbd"));
        Dictionary<int, string> uidOf = BuildFileFixtures.Database.Enhancements.ToDictionary(pair => pair.Value.Nid, pair => pair.Key);
        Dictionary<string, Power> loaded = dump.Powers.ToDictionary(power => power.FullName, StringComparer.Ordinal);
        Dictionary<int, IReadOnlyList<SlotRef>> slots = dump.Slots.ToDictionary(entry => entry.BuildIndex, entry => entry.Slots);
        List<string> differing = [];
        for (int position = 0; position < build.PowerEntries.Count; position++)
        {
            BuildPowerEntry entry = build.PowerEntries[position];
            Power mids = loaded[entry.PowerName];
            bool pickPlaced = position >= 24 || (mids.BuildIndex == position && mids.PickLevel == entry.Level);
            IEnumerable<(int, string?)> midsSlots = slots[mids.BuildIndex].Select(slot => (slot.Level, uidOf.GetValueOrDefault(slot.Enh)));
            IEnumerable<(int, string?)> ours = entry.SlotEntries.Select(slot => (slot.Level, slot.Enhancement?.Uid));
            if (!pickPlaced || !midsSlots.SequenceEqual(ours))
            {
                differing.Add(entry.PowerName);
            }
        }

        Assert.Empty(differing);
    }

    [Fact]
    public void Every_early_defensive_toggle_holds_two_added_slots_by_level_15()
    {
        Dictionary<string, int> toggles = EarlySlots("Toggle", ["Defense", "Resistance"], 15);

        Assert.True(toggles.Count >= 3);
        Assert.DoesNotContain(toggles, pair => pair.Value < 2);
    }

    [Fact]
    public void Two_early_attacks_hold_two_added_slots_by_level_14()
    {
        Assert.True(EarlySlots("Click", ["Damage"], 14).Values.Count(count => count >= 2) >= 2);
    }

    [Fact]
    public void Inherents_and_incarnates_follow_the_picks()
    {
        PickSpec health = TankerSpec.Pick("Inherent.Fitness.Health", 1, ["Crafted_Numinas_Convalesence_F", "Crafted_Miracle_F"], true);
        BuildSpec spec = Spec(Full(), [TankerSpec.Pick("Inherent.Inherent.Brawl", 1, [null]), TankerSpec.Pick("Inherent.Inherent.Gauntlet", 1, []), health])
            with { Incarnates = ["Incarnate.Alpha.Cardiac_Radial_Paragon"] };

        MbdBuild build = Written(spec);

        Assert.Equal(
            [
                "Inherent.Inherent.Brawl",
                "Inherent.Inherent.Gauntlet",
                "Inherent.Fitness.Health",
                "Inherent.Inherent.Sprint",
                "Inherent.Inherent.Rest",
                "Inherent.Fitness.Swift",
                "Inherent.Fitness.Hurdle",
                "Inherent.Fitness.Stamina",
                "Incarnate.Alpha.Cardiac_Radial_Paragon",
            ],
            build.PowerEntries.Skip(24).Select(entry => entry.PowerName),
            StringComparer.Ordinal);
        Assert.Null(build.PowerEntries[24].SlotEntries[0].Enhancement);
        Assert.Empty(build.PowerEntries[25].SlotEntries);
        Assert.Equal(2, build.PowerEntries[26].SlotEntries.Count);
        Assert.Equal(50, build.PowerEntries[^1].Level + 1);
    }

    [Fact]
    public void Inherents_the_spec_omits_are_added_as_mids_adds_them()
    {
        Dictionary<string, BuildPowerEntry> added = Written(Spec(Full())).PowerEntries.Skip(24).ToDictionary(entry => entry.PowerName, StringComparer.Ordinal);

        string[] expected = ["Inherent.Inherent.Brawl", "Inherent.Inherent.Gauntlet", "Inherent.Inherent.Sprint", "Inherent.Inherent.Rest", "Inherent.Fitness.Swift", "Inherent.Fitness.Health", "Inherent.Fitness.Hurdle", "Inherent.Fitness.Stamina"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), added.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(2, added["Inherent.Inherent.Rest"].Level + 1);
        Assert.Single(added["Inherent.Inherent.Brawl"].SlotEntries);
        Assert.Empty(added["Inherent.Inherent.Gauntlet"].SlotEntries);
        Assert.True(added["Inherent.Fitness.Swift"].StatInclude);
        Assert.False(added["Inherent.Inherent.Brawl"].StatInclude);
    }

    [Theory]
    [InlineData("Crafted_Achilles_Heel_A", 19)]
    [InlineData("Crafted_Kismet_A", 29)]
    [InlineData("Crafted_Steadfast_Protection_A", 29)]
    [InlineData("Crafted_Miracle_A", 39)]
    [InlineData("Crafted_Touch_of_Death_A", 39)]
    [InlineData("Crafted_Shield_Wall_A", 49)]
    [InlineData("Crafted_Panacea_A", 49)]
    [InlineData("Crafted_Accuracy", 49)]
    public void Io_level_follows_the_set_ceiling(string uid, int ceiling)
    {
        MbdDocument written = Mbd.WriteMbd(Spec(Full(null, (0, TankerSpec.Pick("Tanker_Melee.Super_Strength.P0", 1, [uid])))), Schedule, Database);

        Assert.Equal(
            [ceiling],
            written.PowerEntries.SelectMany(entry => entry.SlotEntries).Where(slot => string.Equals(slot.Enhancement?.Uid, uid, StringComparison.Ordinal)).Select(slot => slot.Enhancement!.IoLevel).Distinct());
    }

    [Fact]
    public void The_character_level_is_written_zero_based()
    {
        Assert.Equal("49", Mbd.WriteMbd(Spec(Full()), Schedule, Database).Level);
        Assert.Equal("29", Mbd.WriteMbd(Spec(Full()) with { Level = 30 }, Schedule, Database).Level);
        Assert.Matches("E75: .*'50'.*0 to 49", Message(Spec(Full()) with { Level = 51 }));
    }

    [Fact]
    public void Seven_power_sets_are_E73()
    {
        BuildSpec spec = Spec(Full()) with { PowerSets = [.. Enumerable.Repeat("Tanker_Defense.Willpower", 7)] };

        Assert.Contains("E73: PowerSets has 7 entries; MidsReborn needs 8", Message(spec), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_enhancement_is_E78_and_an_unknown_inherent_is_E77()
    {
        BuildSpec spec = Spec(Full(null, (0, TankerSpec.Pick("Tanker_Melee.Super_Strength.P0", 1, ["Crafted_No_Such_Thing"]))));
        MbdDb odd = Database with { Powers = Database.Powers.Where(pair => !string.Equals(pair.Key, "Inherent.Inherent.Rest", StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) };

        Assert.Matches("E78: .*Crafted_No_Such_Thing.*enhancements.json", Message(spec));
        Assert.Matches("E77: .*Inherent.Inherent.Rest.*powers.json", Assert.Throws<InvalidDataException>(() => Mbd.WriteMbd(Spec(Full()), Schedule, odd)).Message);
    }

    [Fact]
    public void An_io_level_above_its_set_is_E74()
    {
        MbdDocument document = TankerDocument();
        MbdSlotDoc first = document.PowerEntries[0].SlotEntries[0];
        first.Enhancement = new MbdEnhancementDoc { Uid = "Crafted_Kismet_A", IoLevel = 50 };

        Assert.Matches("E74: Tanker_Melee.Super_Strength.P0 slot .*Crafted_Kismet_A.*50.*9 to 29", CheckMessage(document));

        first.FlippedEnhancement = first.Enhancement;
        first.Enhancement = null;
        Assert.StartsWith("E74: ", CheckMessage(document), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("50")]
    [InlineData("-1")]
    [InlineData("--5")]
    [InlineData("high")]
    [InlineData("")]
    [InlineData(null)]
    public void A_header_level_that_is_not_zero_to_49_text_is_E75(string? bad)
    {
        MbdDocument document = TankerDocument();
        document.Level = bad;

        Assert.StartsWith("E75: Level ", CheckMessage(document), StringComparison.Ordinal);
    }

    [Fact]
    public void Level_text_0_and_49_load()
    {
        Assert.Null(Mbd.LevelTextProblem("0"));
        Assert.Null(Mbd.LevelTextProblem("49"));
        Assert.Equal("'-' is not a string of an integer from 0 to 49", Mbd.LevelTextProblem("-"));
        Assert.Equal("null is not a string of an integer from 0 to 49", Mbd.LevelTextProblem(null));
    }

    [Fact]
    public void A_missing_inherent_is_E76()
    {
        MbdDocument document = TankerDocument();
        document.PowerEntries = [.. document.PowerEntries.Where(entry => !string.Equals(entry.PowerName, "Inherent.Inherent.Rest", StringComparison.Ordinal))];
        string[] names = [.. document.PowerEntries.Select(entry => entry.PowerName ?? string.Empty)];

        Assert.Matches("E76: .*missing .*Inherent.Inherent.Rest", CheckMessage(document));
        Assert.Equal(["Inherent.Inherent.Rest"], Mbd.MissingInherents(names, "Class_Tanker", 50, Database), StringComparer.Ordinal);
        Assert.DoesNotContain("Inherent.Inherent.Special_Set_Bonuses", Mbd.MissingInherents([], "Class_Tanker", 50, Database), StringComparer.Ordinal);
        Assert.DoesNotContain("Inherent.Inherent.Combat_Flight", Mbd.MissingInherents([], "Class_Peacebringer", 5, Database), StringComparer.Ordinal);
        Assert.Contains("Inherent.Inherent.Combat_Flight", Mbd.MissingInherents([], "Class_Peacebringer", 10, Database), StringComparer.Ordinal);
        Assert.StartsWith("E76: class Class_Nope is not in", Assert.Throws<InvalidDataException>(() => Mbd.MissingInherents([], "Class_Nope", 50, Database)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_invariants_accept_an_unknown_uid_and_a_null_slot()
    {
        MbdDocument document = TankerDocument();
        document.PowerEntries[0].SlotEntries[0].Enhancement = new MbdEnhancementDoc { Enhancement = "Legacy_Gone", IoLevel = 50 };
        document.PowerEntries[0].SlotEntries[1].Enhancement = null;

        Mbd.CheckMbdInvariants(document, Database);

        Assert.Equal(8, Mbd.PowersetsArity(document.PowerSets.ToArray()));
    }

    private static MbdDocument TankerDocument()
    {
        return Mbd.WriteMbd(Spec(Full(new Dictionary<int, int> { [0] = 1 })), Schedule, Database);
    }

    private static string CheckMessage(MbdDocument document)
    {
        return Assert.Throws<InvalidDataException>(() => Mbd.CheckMbdInvariants(document, Database)).Message;
    }

    // Added slots placed by `lastLevel` in each early pick Mids loaded as `kind` with one of `effects`.
    private static Dictionary<string, int> EarlySlots(string kind, string[] effects, int lastLevel)
    {
        BuildFixture dump = Fixtures.Store.Build("wpss_tanker_leadership");
        Dictionary<int, IReadOnlyList<SlotRef>> slots = dump.Slots.ToDictionary(entry => entry.BuildIndex, entry => entry.Slots);
        return dump.Powers
            .Take(24)
            .Where(power => power.PickLevel + 1 <= lastLevel && string.Equals(power.PowerType, kind, StringComparison.Ordinal) && power.Effects.Any(effect => effects.Contains(effect.EffectType, StringComparer.Ordinal)))
            .ToDictionary(power => power.FullName, power => slots[power.BuildIndex].Skip(1).Count(slot => slot.Level + 1 <= lastLevel), StringComparer.Ordinal);
    }

    [Fact]
    public void Serialize_writes_two_space_indent_lf_and_a_final_newline_without_a_bom()
    {
        MbdDocument document = TankerDocument();

        byte[] bytes = Mbd.Serialize(document);

        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.StartsWith("{\n  \"BuiltWith\": {\n    \"App\": \"Mids Reborn\",", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_escapes_as_python_json_dumps_does()
    {
        MbdDocument document = TankerDocument() with { Name = "é\n\t\b\f\r\"\\\u007f\u0001😀<>&'+\\u00AB" };

        string text = Encoding.UTF8.GetString(Mbd.Serialize(document));

        const string Escaped = "\"Name\": \"\\u00e9\\n\\t\\b\\f\\r\\\"\\\\\\u007f\\u0001\\ud83d\\ude00<>&'+\\\\u00AB\",";
        Assert.Contains(Escaped, text, StringComparison.Ordinal);
    }
}
