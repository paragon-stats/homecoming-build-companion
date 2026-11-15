using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;
using CohEngine.Core.Registry.SlottingRules;
using CohEngine.Core.SetBonuses;

namespace CohEngine.Core.Tests.Registry;

public sealed class SlottingTests
{
    private const int RechargeIo = 43;
    private const int EndModIo = 32;
    private const int DamageIo = 29;
    private const int ResistDamageIo = 44;
    private const int PlusFive = 9;
    private const int RagnarokSet = 122;
    private const int AnnihilationSet = 172;
    private const int PerformanceShifterFirst = 658;
    private const int HealthUniqueCount = 5;

    private static readonly int[] HealthUniques = [342, 330, 801, 1005, 318];

    private static IReadOnlyDictionary<int, EnhancementLegality> EnhLeg => RegistryMake.EnhLeg;

    private static SetBonusDb SetDb => Fixtures.Store.SetBonuses;

    private static IReadOnlyList<Diagnostic> Check(Power[] powers, params (int BuildIndex, SlotRef[] Slots)[] slots)
    {
        return SlottingRunner.Check(powers, RegistryMake.Slots(slots), EnhLeg, SetDb, Fixtures.Store.EnhancementEffects, Fixtures.Store.Maths);
    }

    private static IReadOnlyList<Diagnostic> CheckOne(Power power, params SlotRef[] slots)
    {
        return Check([power], (power.BuildIndex, slots));
    }

    private static Power Hasten()
    {
        return RegistryMake.Power(0, "Speed.Speed.Hasten");
    }

    private static SlotRef[] Copies(int enh, int count, int relativeLevel = 4)
    {
        return [.. Enumerable.Repeat(RegistryMake.Slot(enh) with { RelativeLevel = relativeLevel }, count)];
    }

    private static Diagnostic[] Only(IEnumerable<Diagnostic> found, string ruleId)
    {
        return [.. found.Where(diagnostic => string.Equals(diagnostic.RuleId, ruleId, StringComparison.Ordinal))];
    }

    private static int[] SetNids(int nidSet)
    {
        return [.. SetDb.Sets[nidSet].Enhancements];
    }

    private static int[] SetNidsByUid(string uid)
    {
        return SetNids(SetDb.Sets.Values.Single(set => string.Equals(set.Uid, uid, StringComparison.Ordinal)).Nid);
    }

    private static SlotRef[] Pieces(params int[] nids)
    {
        return [.. nids.Select(nid => RegistryMake.Slot(nid))];
    }

    private static Power ForSetType(int index, string name, int setType)
    {
        return RegistryMake.Power(index, name, setTypes: [setType]);
    }

    [Fact]
    public void Hasten_with_two_plain_recharge_ios_is_clean()
    {
        Assert.Empty(CheckOne(Hasten(), Copies(RechargeIo, 2)));
    }

    [Fact]
    public void Hasten_with_two_boosted_recharge_ios_is_clean()
    {
        Assert.Empty(CheckOne(Hasten(), Copies(RechargeIo, 2, PlusFive)));
    }

    [Fact]
    public void Hasten_with_three_plain_recharge_ios_is_p_ed_002_with_99_08_against_95_90()
    {
        Diagnostic found = Assert.Single(CheckOne(Hasten(), Copies(RechargeIo, 3)));

        Assert.Equal("P-ED-002", found.RuleId);
        Assert.Equal("warning", found.Severity);
        Assert.Contains("slot 3", found.Message, StringComparison.Ordinal);
        Assert.Contains("99.08%", found.Message, StringComparison.Ordinal);
        Assert.Contains("95.90%", found.Message, StringComparison.Ordinal);
        Assert.Contains("the other 2 boosted to +5", found.Message, StringComparison.Ordinal);
        Assert.Equal("remove this slot and boost the remaining 2 IOs to +5 with Enhancement Boosters", found.Fix);
    }

    [Fact]
    public void Hasten_with_four_plain_recharge_ios_is_p_ed_001_on_slot_4()
    {
        Diagnostic found = Assert.Single(CheckOne(Hasten(), Copies(RechargeIo, 4)));

        Assert.Equal("P-ED-001", found.RuleId);
        Assert.Contains("slot 4 adds 6.36% RechargeTime", found.Message, StringComparison.Ordinal);
        Assert.Contains("is 127.20% before Enhancement Diversification, past the 100.00% last threshold", found.Message, StringComparison.Ordinal);
        Assert.Contains("15% of its 42.40% value", found.Message, StringComparison.Ordinal);
        Assert.Equal("remove this slot and place it where it adds more; boosting the remaining IOs to +5 recovers most of the value", found.Fix);
    }

    [Fact]
    public void Hasten_with_five_plain_recharge_ios_is_p_ed_001_on_slots_4_and_5()
    {
        Diagnostic[] found = [.. CheckOne(Hasten(), Copies(RechargeIo, 5))];

        Assert.Equal(["P-ED-001", "P-ED-001"], found.Select(diagnostic => diagnostic.RuleId), StringComparer.Ordinal);
        Assert.Contains("slot 4", found[0].Message, StringComparison.Ordinal);
        Assert.Contains("slot 5", found[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stamina_with_five_performance_shifter_pieces_and_an_endmod_io_is_p_ed_001_on_the_io()
    {
        Power stamina = RegistryMake.Power(0, "Inherent.Fitness.Stamina", setTypes: [RegistryMake.SetType(PerformanceShifterFirst)]);
        int[] pieces = [.. Enumerable.Range(PerformanceShifterFirst, 5)];

        Diagnostic found = Assert.Single(CheckOne(stamina, [.. Pieces(pieces), RegistryMake.Slot(EndModIo)]));

        Assert.Equal("P-ED-001", found.RuleId);
        Assert.Contains("slot 6 adds", found.Message, StringComparison.Ordinal);
        Assert.Contains("% Endurance:", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stamina_with_six_performance_shifter_pieces_is_clean()
    {
        Power stamina = RegistryMake.Power(0, "Inherent.Fitness.Stamina", setTypes: [RegistryMake.SetType(PerformanceShifterFirst)]);

        Assert.Empty(CheckOne(stamina, Pieces([.. Enumerable.Range(PerformanceShifterFirst, 6)])));
    }

    [Fact]
    public void A_power_six_slotted_with_a_very_rare_set_has_no_slotting_finding()
    {
        Power power = ForSetType(0, "Blast.Ranged.Nuke", SetDb.Sets[RagnarokSet].SetType);

        Assert.Empty(CheckOne(power, Pieces(SetNids(RagnarokSet))));
    }

    [Fact]
    public void A_power_five_slotted_with_ragnarok_has_no_p_ed_finding()
    {
        Power power = ForSetType(0, "Blast.Ranged.Nuke", SetDb.Sets[RagnarokSet].SetType);

        Assert.Empty(Check([power], (0, Pieces([.. SetNids(RagnarokSet).Take(5)]))));
    }

    [Fact]
    public void A_plain_io_slotted_beside_five_set_pieces_is_the_only_slot_flagged()
    {
        Power power = ForSetType(0, "Blast.Ranged.Nuke", SetDb.Sets[RagnarokSet].SetType);

        Diagnostic found = Assert.Single(CheckOne(power, [.. Pieces([.. SetNids(RagnarokSet).Take(5)]), RegistryMake.Slot(DamageIo)]));

        Assert.Contains("slot 6", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_plain_io_with_no_aspect_and_a_slot_missing_from_the_effects_are_skipped()
    {
        EnhancementRecord noAspect = Fixtures.Store.EnhancementEffects[RechargeIo] with { Effects = [] };
        Dictionary<int, EnhancementRecord> effects = new(Fixtures.Store.EnhancementEffects) { [RechargeIo] = noAspect };
        effects.Remove(EndModIo);
        Power power = Hasten();
        SlotRef[] slots = [.. Copies(RechargeIo, 3), .. Copies(EndModIo, 3)];

        IReadOnlyList<Diagnostic> found = SlottingRunner.Check([power], RegistryMake.Slots((0, slots)), EnhLeg, SetDb, effects, Fixtures.Store.Maths);

        Assert.Empty(found);
    }

    private static int GamblerPiece(bool special)
    {
        EnhancementSetDef gambler = SetDb.Sets.Values.Single(set => string.Equals(set.Uid, "Luck_of_the_Gambler", StringComparison.Ordinal));
        return gambler.Enhancements.First(nid => SetSlotting.PieceGrantsSpecial(gambler, nid) == special);
    }

    private static Power Gambler(int index)
    {
        return ForSetType(index, "Defense.Combat_Training_Defensive", RegistryMake.SetType(GamblerPiece(special: true)));
    }

    [Fact]
    public void A_lone_gambler_recharge_piece_is_p_set_002_when_a_slot_is_reclaimable()
    {
        Power hasten = Hasten();
        Power host = Gambler(1);

        IReadOnlyList<Diagnostic> found = Check([hasten, host], (0, Copies(RechargeIo, 4)), (1, Pieces(GamblerPiece(special: true))));

        Diagnostic lone = Assert.Single(Only(found, "P-SET-002"));
        Assert.Equal("warning", lone.Severity);
        Assert.Contains("the build has 1 slots that add little (P-ED-001, P-ED-002)", lone.Message, StringComparison.Ordinal);
        Assert.StartsWith("move a reclaimed slot here and add Luck of the Gambler", lone.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void A_lone_gambler_recharge_piece_is_clean_when_no_slot_is_reclaimable()
    {
        Assert.Empty(CheckOne(Gambler(0), Pieces(GamblerPiece(special: true))));
    }

    [Fact]
    public void A_gambler_recharge_piece_with_a_second_gambler_piece_is_clean()
    {
        Power hasten = Hasten();

        IReadOnlyList<Diagnostic> found = Check([hasten, Gambler(1)], (0, Copies(RechargeIo, 4)), (1, Pieces(GamblerPiece(special: true), GamblerPiece(special: false))));

        Assert.Empty(Only(found, "P-SET-002"));
    }

    private static Power Tough(int index = 0)
    {
        return RegistryMake.Power(index, "Resistance.Tough", setTypes: [8]) with { PowerType = "Toggle" };
    }

    [Fact]
    public void Tough_with_one_resist_damage_io_and_no_resistance_uniques_is_p_io_002()
    {
        Diagnostic found = Assert.Single(CheckOne(Tough(), RegistryMake.Slot(ResistDamageIo)));

        Assert.Equal("P-IO-002", found.RuleId);
        Assert.Contains("no Steadfast Protection: Resistance/+Def 3% or Gladiator's Armor: TP Protection +3% Def (All);", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tough_with_one_resist_damage_io_is_clean_when_both_uniques_are_in_the_build()
    {
        Power mule = RegistryMake.Power(1, "Armor.Other", setTypes: [8]);

        IReadOnlyList<Diagnostic> found = Check([Tough(), mule], (0, [RegistryMake.Slot(ResistDamageIo)]), (1, Pieces(RegistryMake.Nid("Crafted_Steadfast_Protection_B"), RegistryMake.Nid("Crafted_Gladiators_Armor_F"))));

        Assert.Empty(Only(found, "P-IO-002"));
    }

    [Fact]
    public void Tough_with_one_missing_unique_names_only_that_one()
    {
        Power mule = RegistryMake.Power(1, "Armor.Other", setTypes: [8]);

        Diagnostic found = Assert.Single(Check([Tough(), mule], (0, [RegistryMake.Slot(ResistDamageIo)]), (1, Pieces(RegistryMake.Nid("Crafted_Steadfast_Protection_B")))));

        Assert.Contains("has no Gladiator's Armor: TP Protection +3% Def (All);", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tough_with_two_resistance_set_pieces_is_clean()
    {
        int[] pieces = [.. SetNids(46).Take(2)];

        Assert.Empty(CheckOne(Tough(), Pieces(pieces)));
    }

    [Fact]
    public void A_build_with_health_and_no_absorb_proc_is_p_io_003_naming_it()
    {
        Power health = RegistryMake.Power(0, "Inherent.Fitness.Health", setTypes: [RegistryMake.SetType(HealthUniques[0])]);

        Diagnostic found = Assert.Single(CheckOne(health, Pieces(HealthUniques[..(HealthUniqueCount - 2)]).Append(RegistryMake.Slot(HealthUniques[HealthUniqueCount - 1])).ToArray()));

        Assert.Equal(("P-IO-003", "info"), (found.RuleId, found.Severity));
        Assert.Contains("the build has no Preventive Medicine: Chance for +Absorb;", found.Message, StringComparison.Ordinal);
        Assert.StartsWith("slot the missing pieces in Health; take the slots from plain IOs", found.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void A_build_with_all_five_survivability_uniques_is_clean()
    {
        Power health = RegistryMake.Power(0, "Inherent.Fitness.Health");

        Assert.Empty(CheckOne(health, Pieces(HealthUniques)));
    }

    [Fact]
    public void A_build_without_health_is_not_told_about_the_survivability_uniques()
    {
        Assert.Empty(Check([RegistryMake.Power(0, "Inherent.Fitness.Stamina")], (0, [])));
    }

    private static Power Auto(int level, int pickLevel, params int[] classes)
    {
        return RegistryMake.Power(0, "Pool.Auto", enhancements: classes) with { PowerType = "Auto_", Level = level, PickLevel = pickLevel };
    }

    [Fact]
    public void Mental_training_picked_at_41_is_p_power_001()
    {
        Diagnostic found = Assert.Single(CheckOne(Auto(22, 40, 15)));

        Assert.Equal(("P-POWER-001", "warning", 22, 41), (found.RuleId, found.Severity, found.Expected, found.Actual));
        Assert.Contains("opens at level 22 but is picked at level 41; exemplared to levels 17 to 35 the character lacks it", found.Message, StringComparison.Ordinal);
        Assert.Equal("pick this power at level 22, or record the later pick as a trade-off", found.Fix);
    }

    [Fact]
    public void Mental_training_picked_at_22_is_clean()
    {
        Assert.Empty(CheckOne(Auto(22, 21, 15)));
    }

    [Fact]
    public void An_armor_auto_picked_late_is_clean()
    {
        Assert.Empty(CheckOne(Auto(22, 40, 1)));
    }

    [Fact]
    public void The_rarer_sets_include_the_ten_very_rare_sets_and_not_annihilation()
    {
        int[] rarer = [.. RarerSetUnused.RarerSets(SetDb, EnhLeg).Select(set => set.Nid)];

        Assert.All(Enumerable.Range(119, 10), nid => Assert.Contains(nid, rarer));
        Assert.DoesNotContain(AnnihilationSet, rarer);
    }

    [Fact]
    public void The_rarer_sets_leave_out_overwhelming_force_and_attuned_cupids_crush()
    {
        string[] uids = [.. RarerSetUnused.RarerSets(SetDb, EnhLeg).Select(set => set.Uid)];

        Assert.DoesNotContain("Overwhelming_Force", uids, StringComparer.Ordinal);
        Assert.DoesNotContain("Attuned_Cupids_Crush", uids, StringComparer.Ordinal);
    }

    [Fact]
    public void A_movement_auto_that_opens_low_and_is_picked_a_few_levels_late_prints_no_inverted_range()
    {
        Diagnostic found = Assert.Single(CheckOne(Auto(2, 4, 15)));

        Assert.Equal("'Pool.Auto' opens at level 2 but is picked at level 5", found.Message);
        Assert.Equal(("P-POWER-001", 2, 5), (found.RuleId, found.Expected, found.Actual));
    }

    [Fact]
    public void Two_annihilation_pieces_with_ragnarok_unused_is_p_set_003_info()
    {
        Power power = ForSetType(0, "Blast.Ranged.Nuke", SetDb.Sets[AnnihilationSet].SetType);

        Diagnostic found = Assert.Single(CheckOne(power, Pieces([.. SetNids(AnnihilationSet).Take(2)])));

        Assert.Equal(("P-SET-003", "info"), (found.RuleId, found.Severity));
        Assert.StartsWith("'Annihilation' fills 'Blast.Ranged.Nuke' while Ragnarok, Superior_Frozen_Blast, Frozen_Blast of the same category are not slotted anywhere in the build", found.Message, StringComparison.Ordinal);
        Assert.Equal("compare the build's totals with the rarer set in this power before keeping 'Annihilation'", found.Fix);
    }

    [Fact]
    public void One_unused_rarer_set_is_named_with_is()
    {
        Power power = ForSetType(0, "Blast.Ranged.Nuke", SetDb.Sets[AnnihilationSet].SetType);
        Power elsewhere = ForSetType(1, "Blast.Ranged.Other", SetDb.Sets[RagnarokSet].SetType);

        Diagnostic found = Assert.Single(Check([power, elsewhere], (0, Pieces([.. SetNids(AnnihilationSet).Take(2)])), (1, Pieces(SetNids(RagnarokSet)[0], SetNidsByUid("Superior_Frozen_Blast")[0]))));

        Assert.Equal("P-SET-003", found.RuleId);
        Assert.Contains("while Frozen_Blast of the same category is not slotted", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_annihilation_pieces_are_clean_when_ragnarok_is_slotted_elsewhere()
    {
        Power power = ForSetType(0, "Blast.Ranged.Nuke", SetDb.Sets[AnnihilationSet].SetType);
        Power elsewhere = ForSetType(1, "Blast.Ranged.Other", SetDb.Sets[RagnarokSet].SetType);
        int[] rarerPieces = [SetNids(RagnarokSet)[0], SetNidsByUid("Frozen_Blast")[0], SetNidsByUid("Superior_Frozen_Blast")[0]];

        IReadOnlyList<Diagnostic> found = Check([power, elsewhere], (0, Pieces([.. SetNids(AnnihilationSet).Take(2)])), (1, Pieces(rarerPieces)));

        Assert.Empty(found);
    }

    [Fact]
    public void Every_slotting_rule_is_documented()
    {
        string text = File.ReadAllText(Path.Join(Fixtures.Store.RepositoryRoot, "docs", "style-guides", "build-rules", "slotting.md"));

        Assert.All(RuleRegistry.Slotting, rule => Assert.Contains(rule.Id, text, StringComparison.Ordinal));
        Assert.Equal(["P-ED-001", "P-ED-002", "P-SET-002", "P-IO-002", "P-IO-003", "P-POWER-001", "P-SET-003"], RuleRegistry.Slotting.Select(rule => rule.Id), StringComparer.Ordinal);
    }
}
