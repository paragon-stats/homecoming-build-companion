using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Enhancement;

/// <summary>
/// GBPA_Pass1_EnhancePreED then GBPA_Pass2_ApplyED: one aspect's per-slot values summed across a power's slots,
/// then ED applied once on the aggregate.
/// </summary>
internal static class Pipeline
{
    /// <summary><c>Enhancement.GetSchedule(eEnhance, tSub)</c>, keyed on the eEnhance name.</summary>
    /// <param name="aspect">The eEnhance name.</param>
    /// <param name="sub">The sub-aspect, or -1.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleForEnhance(string aspect, int sub = -1)
    {
        return aspect switch
        {
            "Defense" or "Range" or "Resistance" or "ToHit" => Schedule.B,
            "Interrupt" => Schedule.C,
            "Mez" => sub is 4 or 5 ? Schedule.D : Schedule.A,
            _ => Schedule.A,
        };
    }

    /// <summary>
    /// Sums one aspect's per-slot values across <paramref name="slots"/>, then applies ED once. A slot counts only when
    /// filled and placed below the ForceLevel cutoff. <paramref name="preEdAddend"/> is summed after the slots and
    /// before ED, so an incarnate's IgnoreED-false term is co-ED'd with them in one ApplyED.
    /// </summary>
    /// <param name="slots">The power's slots.</param>
    /// <param name="aspect">The eEnhance name.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <param name="forceLevel">The exemplar cutoff.</param>
    /// <param name="tables">The Maths.mhd tables.</param>
    /// <param name="subEnh">The sub-aspect, or -1.</param>
    /// <param name="mag">The sign the per-slot gate reads: 1 for a scalar aspect, the effect's magnitude otherwise.</param>
    /// <param name="preEdAddend">An extra term co-ED'd with the slots.</param>
    /// <param name="skipEd">Whether ED is skipped, for an effect ED never reaches.</param>
    /// <returns>The aggregate.</returns>
    public static double AggregateAndEd(
        IEnumerable<SlotRef> slots,
        string aspect,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        int forceLevel,
        MathTables tables,
        int subEnh = -1,
        double mag = 1.0,
        double preEdAddend = 0.0,
        bool skipEd = false)
    {
        double preEd = 0.0;
        foreach (SlotRef slot in slots)
        {
            if (slot.Enh < 0 || slot.Level >= forceLevel)
            {
                continue;
            }

            preEd = F32.Round(preEd + SlotValue.EnhancementEffect(enhDb[slot.Enh], slot, aspect, subEnh, mag, tables));
        }

        preEd = F32.Round(preEd + preEdAddend);
        return skipEd ? preEd : Ed.ApplyEd(ScheduleForEnhance(aspect, subEnh), preEd, tables);
    }
}
