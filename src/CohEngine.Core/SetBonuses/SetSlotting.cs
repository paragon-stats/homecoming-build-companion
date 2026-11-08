using CohEngine.Core.Data;

namespace CohEngine.Core.SetBonuses;

/// <summary>Set-piece legality: which sets a power accepts, and which pieces carry a special of their own.</summary>
internal static class SetSlotting
{
    /// <summary>Whether a power can slot the set's pieces: the set's eSetType ordinal is in the power's list.</summary>
    /// <param name="power">The power.</param>
    /// <param name="setDef">The set.</param>
    /// <returns>True when the power accepts the set.</returns>
    public static bool PowerAcceptsSet(Power power, EnhancementSetDef setDef)
    {
        return power.SetTypes.Contains(setDef.SetType);
    }

    /// <summary>
    /// Refuses a set piece slotted in a power that rejects its set type. Mids strips such slots on load, so a build
    /// that went through Mids never trips this; one assembled directly can, and is refused before any total runs.
    /// </summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="slots">The build's slots by build index.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <param name="setDb">The set records.</param>
    /// <exception cref="InvalidDataException">H-ENH-001 for a set the power does not accept; H-ENH-002 for a piece whose set the records lack.</exception>
    public static void ValidateSetSlotting(IReadOnlyList<Power> powers, IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots, IReadOnlyDictionary<int, EnhancementRecord> enhDb, SetBonusDb setDb)
    {
        foreach (Power power in powers)
        {
            foreach (SlotRef slot in slots.GetValueOrDefault(power.BuildIndex, []))
            {
                if (slot.Enh < 0 || !enhDb.TryGetValue(slot.Enh, out EnhancementRecord? record) || !string.Equals(record.TypeName, "SetO", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!setDb.Sets.TryGetValue(record.NidSet, out EnhancementSetDef? setDef))
                {
                    throw new InvalidDataException(
                        $"H-ENH-002: enhancement nID {slot.Enh} in '{power.FullName}' is a set IO whose set "
                        + $"(nIDSet {record.NidSet}) is not in the set database; remove the piece or re-dump the "
                        + "set database so it includes this set");
                }

                if (!PowerAcceptsSet(power, setDef))
                {
                    throw new InvalidDataException(
                        $"H-ENH-001: set '{setDef.Uid}' (set type {setDef.SetType}) is not slottable in "
                        + $"'{power.FullName}' (accepts set types [{string.Join(", ", power.SetTypes.Order())}]); remove the piece or "
                        + "move it to a power that accepts this set");
                }
            }
        }
    }

    /// <summary>
    /// Whether a member piece carries a special of its own, which activates from one piece; every other member only
    /// counts through the tier bonuses, which need two pieces of the set in the same power.
    /// </summary>
    /// <param name="setDef">The set.</param>
    /// <param name="enhNid">The piece.</param>
    /// <returns>True when the piece grants a special.</returns>
    public static bool PieceGrantsSpecial(EnhancementSetDef setDef, int enhNid)
    {
        for (int i = 0; i < setDef.Enhancements.Count; i++)
        {
            if (setDef.Enhancements[i] == enhNid && i < setDef.SpecialBonus.Count && setDef.SpecialBonus[i].Index.Count != 0)
            {
                return true;
            }
        }

        return false;
    }
}
