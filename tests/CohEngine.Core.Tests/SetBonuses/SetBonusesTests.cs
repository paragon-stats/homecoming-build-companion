using CohEngine.Core.Data;
using CohEngine.Core.SetBonuses;

namespace CohEngine.Core.Tests.SetBonuses;

/// <summary>
/// Port of <c>test_set_bonuses.py</c>: the tally, BuildEffects, Rule of Five and legality on synthetic sets, covering
/// the edges one fixture cannot reach.
/// </summary>
public sealed class SetBonusesTests
{
    private const int DefenseSetType = 7;

    // A set with a 2-piece tier (bonus power 100), a PvP-only 2-piece tier (101), and a special on its first member (102).
    private static readonly EnhancementSetDef TestSet = new()
    {
        Nid = 1,
        Uid = "Test_Set",
        SetType = DefenseSetType,
        SetTypeName = "Defense",
        Enhancements = [10, 11, 12],
        Bonus = [Item(2, Accumulator.PvxAny, 100), Item(2, Accumulator.PvxPvp, 101)],
        SpecialBonus = [Item(1, Accumulator.PvxAny, 102), Item(1, Accumulator.PvxAny)],
    };

    [Fact]
    public void Accepts_when_set_type_listed()
    {
        Assert.True(SetSlotting.PowerAcceptsSet(Power(0, setTypes: [7]), TestSet));
    }

    [Fact]
    public void Rejects_when_set_type_absent()
    {
        Assert.False(SetSlotting.PowerAcceptsSet(Power(0, setTypes: []), TestSet));
    }

    [Fact]
    public void Valid_slotting_passes()
    {
        SetSlotting.ValidateSetSlotting([Power(0)], Slots((0, [10])), Enh((10, 1)), Db([Bonus(100), Bonus(102)]));
    }

    [Fact]
    public void Illegal_set_in_power_raises()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => SetSlotting.ValidateSetSlotting([Power(0, setTypes: [])], Slots((0, [10])), Enh((10, 1)), Db([Bonus(100)])));
        Assert.StartsWith("H-ENH-001", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_set_raises()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => SetSlotting.ValidateSetSlotting([Power(0)], Slots((0, [10])), Enh((10, 99)), Db([Bonus(100)])));
        Assert.StartsWith("H-ENH-002", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_and_non_set_slots_skipped()
    {
        Dictionary<int, EnhancementRecord> enhDb = Enh((10, 1));
        enhDb[20] = Invento(20);

        // An empty slot, a generic InventO slot and a power with no slots all skip the set-type check.
        SetSlotting.ValidateSetSlotting([Power(0), Power(1)], Slots((0, [-1, 20, 30])), enhDb, Db([Bonus(100)]));
    }

    [Fact]
    public void Tier_and_special_activate()
    {
        Power virtualPower = Build([Power(0)], Slots((0, [10, 11])), Enh((10, 1), (11, 1)), Db([Bonus(100), Bonus(102)]));

        // Two pieces fire tier 100; member 10 slotted fires special 102. Each bonus power adds its one effect.
        Assert.Equal(2, virtualPower.Effects.Count);
        Assert.Equal((Accumulator.SetBonusBuildIndex, "Inherent.Inherent.Special_Set_Bonuses", true), (virtualPower.BuildIndex, virtualPower.FullName, virtualPower.StatInclude));
    }

    [Fact]
    public void Single_piece_skips_tier_keeps_special()
    {
        Assert.Single(Build([Power(0)], Slots((0, [10])), Enh((10, 1)), Db([Bonus(100), Bonus(102)])).Effects);
    }

    [Fact]
    public void Pvp_tier_gated_out_in_pve()
    {
        // Members 12 and 13 are not member 10, so no special; only tiers can fire.
        Assert.Single(Build([Power(0)], Slots((0, [12, 13])), Enh((12, 1), (13, 1)), Db([Bonus(100), Bonus(101)])).Effects);
    }

    [Fact]
    public void Pvp_mode_includes_pvp_tier()
    {
        Power virtualPower = Build([Power(0)], Slots((0, [12, 13])), Enh((12, 1), (13, 1)), Db([Bonus(100), Bonus(101)]), disablePve: true);
        Assert.Equal(2, virtualPower.Effects.Count);
    }

    [Fact]
    public void Exemplar_gate_excludes_high_pick_level()
    {
        Assert.Empty(Build([Power(0, level: 51)], Slots((0, [10, 11])), Enh((10, 1), (11, 1)), Db([Bonus(100), Bonus(102)])).Effects);
    }

    [Fact]
    public void Non_set_io_does_not_tally()
    {
        Dictionary<int, EnhancementRecord> enhDb = new() { [20] = Invento(20) };
        Assert.Empty(Build([Power(0)], Slots((0, [20, -1, 30])), enhDb, Db([Bonus(100)])).Effects);
    }

    [Fact]
    public void Rule_of_five_caps_at_five()
    {
        // Six powers each slot member 10: six copies of special 102 fold as five.
        Power[] powers = [.. Enumerable.Range(0, 6).Select(i => Power(i))];
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = Enumerable.Range(0, 6).ToDictionary(i => i, _ => (IReadOnlyList<SlotRef>)[Slot(10)]);
        Assert.Equal(5, Build(powers, slots, Enh((10, 1)), Db([Bonus(102)])).Effects.Count);
    }

    [Fact]
    public void My_pet_bonus_skipped_but_counts()
    {
        Assert.Empty(Build([Power(0), Power(1)], Slots((0, [10]), (1, [10])), Enh((10, 1)), Db([Bonus(102, myPet: true)])).Effects);
    }

    [Fact]
    public void Missing_bonus_power_counts_without_effects()
    {
        Assert.Empty(Build([Power(0)], Slots((0, [10])), Enh((10, 1)), Db([])).Effects);
    }

    [Fact]
    public void Negative_bonus_index_skipped()
    {
        EnhancementSetDef negative = TestSet with { Uid = "Neg", Enhancements = [10], Bonus = [Item(2, Accumulator.PvxAny, -1)], SpecialBonus = [Item(1, Accumulator.PvxAny, -1)] };
        SetBonusDb setDb = new(new Dictionary<int, EnhancementSetDef> { [1] = negative }, new Dictionary<int, SetBonusPower>());
        Assert.Empty(Build([Power(0)], Slots((0, [10, 11])), Enh((10, 1), (11, 1)), setDb).Effects);
    }

    [Fact]
    public void A_member_past_the_specials_adds_none()
    {
        EnhancementSetDef shortSpecials = TestSet with { SpecialBonus = [Item(1, Accumulator.PvxAny, 102)] };
        SetBonusDb setDb = new(new Dictionary<int, EnhancementSetDef> { [1] = shortSpecials }, Db([Bonus(102)]).Powers);
        Assert.Empty(Build([Power(0)], Slots((0, [12])), Enh((12, 1)), setDb).Effects);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(12, false)]
    [InlineData(99, false)]
    public void A_piece_grants_a_special_only_with_one_of_its_own(int piece, bool want)
    {
        Assert.Equal(want, SetSlotting.PieceGrantsSpecial(TestSet, piece));
    }

    [Fact]
    public void Slow_set_bonus_effects_are_debuffs()
    {
        Dictionary<string, HashSet<string>> modes = Fixtures.Store.SetBonuses.Powers.Values
            .Where(power => power.Effects.Count != 0)
            .ToDictionary(power => power.FullName, power => power.Effects.Select(effect => effect.BuffMode).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["Debuff"], modes["Set_Bonus.Set_Bonus.Improved_Slow_2"]);
        Assert.Equal(["Normal"], modes["Set_Bonus.PVP_Set_Bonus.Energy_Neg_Mez_Res_2"]);
    }

    private static BonusItem Item(int slotted, int pvMode, params int[] index)
    {
        return new BonusItem { Slotted = slotted, PvMode = pvMode, Index = index };
    }

    private static Power Power(int buildIndex, int level = 1, int[]? setTypes = null)
    {
        return Make.Power() with
        {
            BuildIndex = buildIndex,
            NidPower = buildIndex,
            FullName = $"Test.Set.Power{buildIndex}",
            StaticIndex = buildIndex,
            Level = level,
            PickLevel = level,
            SetTypes = setTypes ?? [DefenseSetType],
        };
    }

    private static SlotRef Slot(int enh)
    {
        return new SlotRef { Level = 1, IsInherent = false, Enh = enh, Grade = 0, IoLevel = 49, RelativeLevel = 4 };
    }

    private static Dictionary<int, IReadOnlyList<SlotRef>> Slots(params (int Power, int[] Enhs)[] entries)
    {
        return entries.ToDictionary(entry => entry.Power, entry => (IReadOnlyList<SlotRef>)[.. entry.Enhs.Select(Slot)]);
    }

    private static Dictionary<int, EnhancementRecord> Enh(params (int Nid, int NidSet)[] pieces)
    {
        return pieces.ToDictionary(
            piece => piece.Nid,
            piece => new EnhancementRecord { Nid = piece.Nid, StaticIndex = piece.Nid, TypeName = "SetO", Superior = false, Effects = [], NidSet = piece.NidSet, IsProc = false, PowerNid = -1 });
    }

    private static EnhancementRecord Invento(int nid)
    {
        return new EnhancementRecord { Nid = nid, StaticIndex = nid, TypeName = "InventO", Superior = false, Effects = [], NidSet = -1, IsProc = false, PowerNid = -1 };
    }

    private static SetBonusPower Bonus(int pid, bool myPet = false)
    {
        return new SetBonusPower
        {
            PowerId = pid,
            FullName = $"Set_Bonus.Set_Bonus.Bonus_{pid}",
            PowerType = "Auto_",
            MyPetTarget = myPet,
            MyPetEntities = myPet,
            Effects = [Make.Effect with { EffectType = "Resistance", EtModifies = "None", DamageType = "Fire", Scale = 0.05 }],
        };
    }

    private static SetBonusDb Db(SetBonusPower[] powers)
    {
        return new SetBonusDb(new Dictionary<int, EnhancementSetDef> { [TestSet.Nid] = TestSet }, powers.ToDictionary(power => power.PowerId));
    }

    private static Power Build(Power[] powers, IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots, Dictionary<int, EnhancementRecord> enhDb, SetBonusDb setDb, bool disablePve = false)
    {
        return Accumulator.BuildSetBonusPower(powers, slots, enhDb, setDb, 50, disablePve);
    }
}
