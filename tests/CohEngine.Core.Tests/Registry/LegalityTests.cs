using System.Text.Json;

using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;
using CohEngine.Core.Registry.LegalityRules;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Registry;

public sealed class LegalityTests
{
    private static readonly IReadOnlyDictionary<int, EnhancementLegality> EnhLeg = RegistryMake.EnhLeg;

    private static SetBonusDb SetDb => Fixtures.Store.SetBonuses;

    public static TheoryData<int> ProbeIndexes()
    {
        return [.. Enumerable.Range(0, RegistryMake.Probe().Count)];
    }

    private static IReadOnlyList<Diagnostic> One(Power power, params SlotRef[] slots)
    {
        return RegistryMake.Legal([power], RegistryMake.Slots((0, slots)));
    }

    private static IReadOnlyList<Diagnostic> Two(Power first, int firstEnh, Power second, int secondEnh)
    {
        return RegistryMake.Legal([first, second], RegistryMake.Slots((0, [RegistryMake.Slot(firstEnh)]), (1, [RegistryMake.Slot(secondEnh)])));
    }

    private static Power ForSet(int index, int enh)
    {
        return RegistryMake.Power(index, "p" + index, setTypes: [RegistryMake.SetType(enh)]);
    }

    private static int[] Ints(JsonElement element)
    {
        return [.. element.EnumerateArray().Select(item => item.GetInt32())];
    }

    [Fact]
    public void The_loader_reads_the_class_and_uniqueness_fields()
    {
        EnhancementLegality recharge = EnhLeg[43];

        Assert.Equal(("Crafted_Recharge", "InventO", -1, false), (recharge.Uid, recharge.TypeName, recharge.NidSet, recharge.Unique));
        Assert.Equal([18], recharge.ClassIds);
    }

    [Theory]
    [MemberData(nameof(ProbeIndexes))]
    public void Placement_matches_the_mids_probe(int index)
    {
        JsonElement pair = RegistryMake.Probe()[index];
        Power power = RegistryMake.Power(0, pair.GetProperty("PowerFullName").GetString()!, Ints(pair.GetProperty("SetTypes")), Ints(pair.GetProperty("PowerEnhancements")));

        Assert.Equal(pair.GetProperty("IsValid").GetBoolean(), Placement.IsEnhancementValid(power, EnhLeg[pair.GetProperty("EnhNid").GetInt32()], SetDb));
    }

    [Fact]
    public void An_attuned_set_io_validates_by_its_set_type()
    {
        EnhancementLegality attuned = EnhLeg[892];
        Power accepts = RegistryMake.Power(0, "acc", setTypes: [SetDb.Sets[attuned.NidSet].SetType]);
        Power rejects = RegistryMake.Power(1, "rej");

        Assert.Equal((true, false), (Placement.IsEnhancementValid(accepts, attuned, SetDb), Placement.IsEnhancementValid(rejects, attuned, SetDb)));
    }

    [Fact]
    public void A_real_build_has_no_errors()
    {
        BuildFixture build = Fixtures.Store.Build("shield_scrapper_set_bonuses");
        Dictionary<int, IReadOnlyList<SlotRef>> slots = build.Slots.ToDictionary(entry => entry.BuildIndex, entry => entry.Slots);

        IReadOnlyList<Diagnostic> found = RegistryMake.Legal(build.Powers, slots);

        Assert.DoesNotContain(found, diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.Ordinal));
    }

    [Fact]
    public void A_class_illegal_standard_io_is_h_enh_003()
    {
        JsonElement pair = RegistryMake.Probe().First(item => item.GetProperty("PowerFullName").GetString()!.Contains("One_with_the_Shield", StringComparison.Ordinal));
        Power power = RegistryMake.Power(0, pair.GetProperty("PowerFullName").GetString()!, enhancements: Ints(pair.GetProperty("PowerEnhancements")));

        Diagnostic found = One(power, RegistryMake.Slot(pair.GetProperty("EnhNid").GetInt32())).Single(diagnostic => string.Equals(diagnostic.RuleId, "H-ENH-003", StringComparison.Ordinal));

        Assert.Equal(power.Enhancements.Order().ToArray(), (int[])found.Expected!);
        Assert.Contains("its enhancement class [", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_set_type_illegal_set_io_is_h_enh_001()
    {
        JsonElement pair = RegistryMake.Probe().First(item => item.GetProperty("PowerFullName").GetString()!.Contains("Soul_Drain", StringComparison.Ordinal));
        Power power = RegistryMake.Power(0, pair.GetProperty("PowerFullName").GetString()!, setTypes: Ints(pair.GetProperty("SetTypes")));

        Diagnostic found = One(power, RegistryMake.Slot(pair.GetProperty("EnhNid").GetInt32())).Single(diagnostic => string.Equals(diagnostic.RuleId, "H-ENH-001", StringComparison.Ordinal));

        Assert.Contains("is not slottable in 'Scrapper_Melee.Dark_Melee.Soul_Drain' (accepts set types [4, 25, 41, 44])", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_enhancement_absent_from_the_database_is_h_enh_007()
    {
        Assert.True(RegistryMake.Has(One(RegistryMake.Power(0, "p", enhancements: [18]), RegistryMake.Slot(9_999_999)), "H-ENH-007"));
    }

    [Fact]
    public void A_unique_io_slotted_twice_is_h_enh_004()
    {
        const int Kismet = 253;

        Assert.True(RegistryMake.Has(Two(ForSet(0, Kismet), Kismet, ForSet(1, Kismet), Kismet), "H-ENH-004"));
    }

    [Fact]
    public void A_unique_io_slotted_once_is_legal()
    {
        const int Kismet = 253;

        Assert.False(RegistryMake.Has(One(ForSet(0, Kismet), RegistryMake.Slot(Kismet)), "H-ENH-004"));
    }

    [Fact]
    public void The_same_set_piece_twice_in_a_power_is_h_enh_006()
    {
        const int ForceFeedback = 640;

        Assert.True(RegistryMake.Has(One(ForSet(0, ForceFeedback), RegistryMake.Slot(ForceFeedback), RegistryMake.Slot(ForceFeedback)), "H-ENH-006"));
    }

    [Fact]
    public void Two_stealth_procs_are_mutually_exclusive()
    {
        Assert.True(RegistryMake.Has(Two(ForSet(0, 521), 521, ForSet(1, 509), 509), "H-ENH-005"));
    }

    [Fact]
    public void Regular_and_superior_versions_of_one_piece_are_mutually_exclusive()
    {
        Assert.True(RegistryMake.Has(Two(ForSet(0, 892), 892, ForSet(1, 970), 970), "H-ENH-005"));
    }

    [Fact]
    public void The_superior_flag_alone_does_not_mutex_two_purples()
    {
        Assert.False(RegistryMake.Has(Two(ForSet(0, 550), 550, ForSet(1, 556), 556), "H-ENH-005"));
    }

    [Fact]
    public void Empty_slots_and_no_slots_are_legal()
    {
        Power power = RegistryMake.Power(0, "p", enhancements: [18]);

        Assert.Empty(One(power, RegistryMake.Slot(-1)));
        Assert.Empty(RegistryMake.Legal([power], RegistryMake.Slots()));
    }

    [Fact]
    public void A_set_io_whose_set_is_unknown_is_h_enh_002()
    {
        EnhancementLegality orphan = new()
        {
            Nid = 7,
            Uid = "Orphan_A",
            LongName = "Orphan Set: Piece A",
            TypeName = "SetO",
            ClassIds = [],
            NidSet = 999_999,
            Unique = false,
            MutexName = "None",
            Superior = false,
        };

        IReadOnlyList<Diagnostic> found = LegalityRunner.Check([RegistryMake.Power(0, "p", setTypes: [1, 2, 3])], RegistryMake.Slots((0, [RegistryMake.Slot(7)])), new Dictionary<int, EnhancementLegality> { [7] = orphan }, SetDb);

        Assert.Equal("H-ENH-002", Assert.Single(found).RuleId);
    }

    [Fact]
    public void A_valid_standard_io_has_no_diagnostics()
    {
        Assert.Empty(One(RegistryMake.Power(0, "Pool.Speed.Hasten", enhancements: [18]), RegistryMake.Slot(43)));
    }

    [Fact]
    public void A_lone_plain_piece_is_a_p_set_001_warning()
    {
        Diagnostic found = One(ForSet(0, 640), RegistryMake.Slot(640)).Single(diagnostic => string.Equals(diagnostic.RuleId, "P-SET-001", StringComparison.Ordinal));

        Assert.Equal(("warning", (object)2, (object)1), (found.Severity, found.Expected, found.Actual));
    }

    [Fact]
    public void Two_pieces_of_a_set_in_one_power_are_clean()
    {
        Assert.False(RegistryMake.Has(One(ForSet(0, 640), RegistryMake.Slot(640), RegistryMake.Slot(641)), "P-SET-001"));
    }

    [Fact]
    public void One_set_split_across_powers_warns_for_each()
    {
        Assert.Equal(2, Two(ForSet(0, 640), 640, ForSet(1, 640), 641).Count(diagnostic => string.Equals(diagnostic.RuleId, "P-SET-001", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("Crafted_Luck_of_the_Gambler_F")]
    [InlineData("Crafted_Kismet_E")]
    public void A_lone_special_or_unique_piece_is_exempt(string uid)
    {
        int nid = RegistryMake.Nid(uid);

        Assert.False(RegistryMake.Has(One(ForSet(0, nid), RegistryMake.Slot(nid)), "P-SET-001"));
    }

    [Fact]
    public void A_lone_pure_proc_is_exempt_only_with_the_effects_dump()
    {
        int proc = RegistryMake.Nid("Crafted_Force_Feedback_F");
        IReadOnlyDictionary<int, EnhancementRecord> effects = Fixtures.Store.EnhancementEffects;
        Power power = ForSet(0, proc);
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = RegistryMake.Slots((0, [RegistryMake.Slot(proc)]));

        Assert.Equal(
            (false, true),
            (RegistryMake.Has(RegistryMake.Legal([power], slots, effects), "P-SET-001"), RegistryMake.Has(RegistryMake.Legal([power], slots), "P-SET-001")));
    }

    [Fact]
    public void A_piece_that_enhances_numerically_still_warns_with_the_effects_dump()
    {
        int plain = RegistryMake.Nid("Crafted_Force_Feedback_A");
        Power power = ForSet(0, plain);

        Assert.True(RegistryMake.Has(RegistryMake.Legal([power], RegistryMake.Slots((0, [RegistryMake.Slot(plain)])), Fixtures.Store.EnhancementEffects), "P-SET-001"));
    }

    [Fact]
    public void A_piece_missing_from_the_effects_dump_still_warns()
    {
        int plain = RegistryMake.Nid("Crafted_Force_Feedback_A");
        Power power = ForSet(0, plain);

        Assert.True(RegistryMake.Has(RegistryMake.Legal([power], RegistryMake.Slots((0, [RegistryMake.Slot(plain)])), new Dictionary<int, EnhancementRecord>()), "P-SET-001"));
    }

    [Fact]
    public void The_registry_lists_the_five_rules_in_registration_order()
    {
        string[] expected = ["_rule_placement", "_rule_global_unique", "_rule_mutex", "_rule_lone_set_piece", "_rule_per_power_set_dup"];

        Assert.True(expected.SequenceEqual(RuleRegistry.Legality.Select(rule => rule.Id), StringComparer.Ordinal));
    }

    [Fact]
    public void The_runner_refuses_null_inputs()
    {
        Dictionary<int, EnhancementLegality> none = [];

        Assert.Throws<ArgumentNullException>(() => LegalityRunner.Check(null!, RegistryMake.Slots(), none, SetDb));
        Assert.Throws<ArgumentNullException>(() => LegalityRunner.Check([], null!, none, SetDb));
        Assert.Throws<ArgumentNullException>(() => LegalityRunner.Check([], RegistryMake.Slots(), null!, SetDb));
        Assert.Throws<ArgumentNullException>(() => LegalityRunner.Check([], RegistryMake.Slots(), none, null!));
    }
}
