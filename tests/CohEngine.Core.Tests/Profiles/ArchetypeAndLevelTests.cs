using CohEngine.Core.Data;

namespace CohEngine.Core.Tests.Profiles;

public sealed class ArchetypeAndLevelTests
{
    private static ArchetypeDb Archetypes => Fixtures.Store.Archetypes;

    private static LevelSchedule Levels => Fixtures.Store.Levels;

    private static Archetype Named(string className)
    {
        return Archetypes.Classes[Archetypes.NidFromUidClass(className)];
    }

    [Fact]
    public void The_blaster_record_matches_the_dump()
    {
        Archetype blaster = Archetypes.Classes[0];

        Assert.Equal(("Class_Blaster", "Blaster", 0, true, 1205, 0.75, 5.0, 20.0), (blaster.ClassName, blaster.DisplayName, blaster.Column, blaster.Playable, blaster.Hitpoints, blaster.ResCap, blaster.DamageCap, blaster.RegenCap));
    }

    [Fact]
    public void The_class_lookup_is_case_insensitive_and_misses_return_minus_one()
    {
        Assert.Equal((0, -1), (Archetypes.NidFromUidClass("class_blaster"), Archetypes.NidFromUidClass("Class_Nonexistent")));
    }

    [Fact]
    public void A_missing_archetype_file_raises()
    {
        Assert.Throws<FileNotFoundException>(() => ArchetypeData.LoadArchetypes(Path.Join(Path.GetTempPath(), "coh-missing-archetypes.json")));
    }

    [Theory]
    [InlineData("Class_Arachnos_Soldier")]
    [InlineData("Class_Arachnos_Widow")]
    public void The_arachnos_classes_list_the_four_patron_pools(string className)
    {
        string[] pools = ["Epic.VEAT_Leviathan_Mastery", "Epic.VEAT_Mace_Mastery", "Epic.VEAT_Mu_Mastery", "Epic.VEAT_Soul_Mastery"];

        Assert.True(pools.SequenceEqual(Named(className).Ancillary, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_playable_class_but_the_kheldians_has_an_ancillary_pool()
    {
        string[] without = [.. Archetypes.Classes.Where(archetype => archetype.Playable && archetype.Ancillary.Count == 0).Select(archetype => archetype.ClassName).Order(StringComparer.Ordinal)];

        Assert.True(new[] { "Class_Peacebringer", "Class_Warshade" }.SequenceEqual(without, StringComparer.Ordinal));
    }

    [Fact]
    public void The_scrapper_lists_the_inherent_powers_mids_adds()
    {
        Dictionary<string, int> levels = Named("Class_Scrapper").InherentPowers.ToDictionary(power => power.FullName, power => power.Level, StringComparer.Ordinal);

        Assert.Equal(9, levels.Count);
        Assert.Equal(2, levels["Inherent.Inherent.Rest"]);
        Assert.Contains("Inherent.Fitness.Stamina", levels.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void The_dump_carries_every_class_record_playable_or_not()
    {
        Assert.Equal(61, Archetypes.Classes.Count);
    }

    [Fact]
    public void The_schedule_grants_67_slots()
    {
        Assert.Equal(67, Levels.TotalSlotsGranted);
    }

    [Fact]
    public void The_pick_levels_stay_in_order_as_zero_based_indexes()
    {
        int[] gameLevels = [1, 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 35, 38, 41, 44, 47, 49];

        Assert.Equal(gameLevels.Select(level => level - 1), Levels.PowerPickLevels);
    }

    [Fact]
    public void Early_levels_grant_two_slots_and_late_levels_three()
    {
        Assert.Equal((2, 3), (Levels.SlotsPerLevel[2], Levels.SlotsPerLevel[49]));
    }

    [Fact]
    public void The_slot_grant_levels_are_the_key_set()
    {
        Assert.True(Levels.SlotGrantLevels.SetEquals(Levels.SlotsPerLevel.Keys));
        Assert.DoesNotContain(0, Levels.SlotGrantLevels);
    }
}
