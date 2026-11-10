using CohEngine.Core.Data;

namespace CohEngine.Core.SetBonuses;

/// <summary>
/// <c>Build.GenerateSetBonusData</c>: the build's slotted set pieces tallied per set and power (<c>I9SetData.Add</c>),
/// each tally's activated bonus ids resolved (<c>I9SetData.BuildEffects</c>), then folded through the Rule of Five into
/// one virtual power. Order is load-bearing: build-power order, then set first-encounter order, then tier before
/// special, then effect-array order, because the totals passes sum each bucket in float32.
/// </summary>
internal static class Accumulator
{
    /// <summary>The ePvX ordinal that matches every mode.</summary>
    public const int PvxAny = 0;

    /// <summary>The ePvX ordinal for PvE.</summary>
    public const int PvxPve = 1;

    /// <summary>The ePvX ordinal for PvP.</summary>
    public const int PvxPvp = 2;

    /// <summary>The virtual power's build index, distinct from any real build power.</summary>
    public const int SetBonusBuildIndex = -1;

    /// <summary>
    /// The set-bonus virtual power for a build: the surviving bonus effects, empty when the build slots no set piece.
    /// It is StatInclude and neither Toggle nor GlobalBoost, so the totals passes fold it like any ordinary source.
    /// </summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="slots">The build's slots by build index.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <param name="setDb">The set records.</param>
    /// <param name="forceLevel">The exemplar level.</param>
    /// <param name="disablePve">Whether the build is computed in PvP mode.</param>
    /// <returns>The virtual power.</returns>
    public static Power BuildSetBonusPower(
        IReadOnlyList<Power> powers,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        SetBonusDb setDb,
        int forceLevel,
        bool disablePve)
    {
        int pvMode = disablePve ? PvxPvp : PvxPve;
        List<SetInfo> ordered = TallyBuild(powers, slots, enhDb, setDb, forceLevel, pvMode);
        List<Effect> effects = RuleOfFive.Fold(ordered, setDb);
        return new Power
        {
            BuildIndex = SetBonusBuildIndex,
            NidPower = -1,
            FullName = "Inherent.Inherent.Special_Set_Bonuses",
            StaticIndex = -1,
            PowerType = string.Empty,
            ForcedClass = string.Empty,
            ClickBuff = false,
            Level = 0,
            EndCost = 0.0,
            ActivatePeriod = 0.0,
            ToggleCost = 0.0,
            VariableEnabled = false,
            StatInclude = true,
            VariableValue = 0,
            Effects = effects,
            SetTypes = [],
            Enhancements = [],
            PickLevel = 0,
            RechargeTime = 0.0,
            CastTime = 0.0,
            InterruptTime = 0.0,
            IgnoreEnh = [],
        };
    }

    /// <summary><c>I9SetData.Add</c> over a power's slots: set pieces tallied per set, in first-seen order.</summary>
    /// <param name="powerSlots">The power's slots.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <returns>The tallies.</returns>
    internal static List<SetInfo> TallyPower(IReadOnlyList<SlotRef> powerSlots, IReadOnlyDictionary<int, EnhancementRecord> enhDb)
    {
        List<SetInfo> infos = [];
        Dictionary<int, SetInfo> bySet = [];
        foreach (SlotRef slot in powerSlots)
        {
            // An enhancement absent from the records is skipped like a non-set piece, as validation does.
            if (slot.Enh < 0 || !enhDb.TryGetValue(slot.Enh, out EnhancementRecord? record) || !string.Equals(record.TypeName, "SetO", StringComparison.Ordinal))
            {
                continue;
            }

            if (!bySet.TryGetValue(record.NidSet, out SetInfo? info))
            {
                info = new SetInfo(record.NidSet);
                bySet[record.NidSet] = info;
                infos.Add(info);
            }

            info.SlottedCount++;
            info.EnhIndexes.Add(slot.Enh);
        }

        return infos;
    }

    /// <summary>
    /// <c>I9SetData.BuildEffects</c> for one set: tier bonuses need two pieces and a matching or Any PvMode; each
    /// special needs its own member slotted. Duplicates stay, as each advances the Rule-of-Five counter.
    /// </summary>
    /// <param name="setDef">The set.</param>
    /// <param name="info">The set's tally in one power.</param>
    /// <param name="pvMode">The build's ePvX ordinal.</param>
    /// <returns>The activated set_bonus power ids.</returns>
    internal static List<int> ResolveBonusPowers(EnhancementSetDef setDef, SetInfo info, int pvMode)
    {
        List<int> output = [];
        if (info.SlottedCount > 1)
        {
            foreach (BonusItem bonus in setDef.Bonus.Where(bonus => bonus.Slotted <= info.SlottedCount && (bonus.PvMode == pvMode || bonus.PvMode == PvxAny)))
            {
                output.AddRange(bonus.Index);
            }
        }

        // A tally exists only once a piece was added, so the specials' one-piece guard always holds.
        for (int i = 0; i < setDef.Enhancements.Count; i++)
        {
            if (i >= setDef.SpecialBonus.Count)
            {
                continue;
            }

            foreach (int enh in info.EnhIndexes.Where(enh => enh == setDef.Enhancements[i]))
            {
                output.AddRange(setDef.SpecialBonus[i].Index);
            }
        }

        return output;
    }

    /// <summary>Tallies over the whole build, with resolved bonus ids, in fold order; only powers inside the exemplar gate count.</summary>
    private static List<SetInfo> TallyBuild(
        IReadOnlyList<Power> powers,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        SetBonusDb setDb,
        int forceLevel,
        int pvMode)
    {
        List<SetInfo> ordered = [];
        foreach (Power power in powers.OrderBy(power => power.BuildIndex))
        {
            if (!ExemplarGate.Passes(power, forceLevel))
            {
                continue;
            }

            List<SetInfo> infos = TallyPower(slots.GetValueOrDefault(power.BuildIndex, []), enhDb);
            foreach (SetInfo info in infos)
            {
                info.PowerIds = ResolveBonusPowers(setDb.Sets[info.SetIdx], info, pvMode);
            }

            ordered.AddRange(infos);
        }

        return ordered;
    }
}
