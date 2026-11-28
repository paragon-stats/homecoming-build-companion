using CohEngine.Core.Registry.HardLimitRules;

namespace CohEngine.Core.Tests.Registry;

public sealed class LevelOnePairTests
{
    private const string Flares = "Blaster_Ranged.Fire_Blast.Flares";
    private const string Blast = "Blaster_Ranged.Fire_Blast.Fire_Blast";
    private const string Ring = "Blaster_Support.Fire_Manipulation.Ring_of_Fire";
    private const string Sword = "Blaster_Support.Fire_Manipulation.Fire_Sword";

    private static readonly string[] Sets = ["Blaster_Ranged.Fire_Blast", "Blaster_Support.Fire_Manipulation", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty];

    [Theory]
    [InlineData(Flares, Ring, "InOrder", null)]
    [InlineData(Ring, Flares, "Swapped", "swap 'Blaster_Support.Fire_Manipulation.Ring_of_Fire' and 'Blaster_Ranged.Fire_Blast.Flares' so the primary pick comes first")]
    [InlineData(Flares, Blast, "SecondNotSecondary", "replace 'Blaster_Ranged.Fire_Blast.Fire_Blast' with a power from 'Blaster_Support.Fire_Manipulation'")]
    [InlineData(Ring, Sword, "FirstNotPrimary", "replace 'Blaster_Support.Fire_Manipulation.Ring_of_Fire' with a power from 'Blaster_Ranged.Fire_Blast'")]
    [InlineData("Pool.Speed.Hasten", Ring, "FirstNotPrimary", "replace 'Pool.Speed.Hasten' with a power from 'Blaster_Ranged.Fire_Blast'")]
    public void Each_pair_gets_one_verdict_and_its_action(string first, string second, string verdict, string? action)
    {
        LevelOnePairVerdict found = LevelOnePair.Classify(Sets, [first, second]);

        Assert.Equal((verdict, action), (found.ToString(), LevelOnePair.Action(found, Sets, [first, second])));
    }

    [Fact]
    public void Two_sets_in_one_category_count_alike()
    {
        string[] bane = ["Arachnos_Soldiers.Bane_Spider_Soldier", "Training_Gadgets.Bane_Spider_Training"];

        Assert.Equal(LevelOnePairVerdict.InOrder, LevelOnePair.Classify(bane, ["Arachnos_Soldiers.Arachnos_Soldier.Single_Shot", "Training_Gadgets.Training_and_Gadgets.Wolf_Spider_Armor"]));
    }

    [Theory]
    [InlineData(new[] { "Blaster_Ranged.Fire_Blast" }, 2)]
    [InlineData(new[] { "", "Blaster_Support.Fire_Manipulation" }, 2)]
    [InlineData(new[] { "Blaster_Ranged.Fire_Blast", "" }, 2)]
    [InlineData(new[] { "Blaster_Ranged.Fire_Blast", null }, 2)]
    [InlineData(new[] { "Blaster_Ranged.Fire_Blast", "Blaster_Support.Fire_Manipulation" }, 1)]
    public void Missing_power_sets_or_a_lone_pick_cannot_be_told(string?[] powerSets, int picks)
    {
        Assert.Equal(LevelOnePairVerdict.CannotTell, LevelOnePair.Classify(powerSets!, [.. new[] { Ring, Ring }.Take(picks)]));
    }
}
