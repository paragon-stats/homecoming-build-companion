using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Enhancement;

/// <summary>
/// The <c>I9Slot</c> value path: one slotted enhancement's contribution to one aspect, before aggregation and ED
/// (<c>GetScheduleMult</c>, <c>GetRelativeLevelMultiplier</c>, <c>GetEnhancementEffect</c>).
/// </summary>
internal static class SlotValue
{
    private const int GradeNone = 0;
    private const int GradeTrainingO = 1;
    private const int GradeDualO = 2;
    private const int GradeSingleO = 3;
    private const int RelNone = 0;
    private const int RelEven = 4;
    private const int RelPlusFive = 9;

    // The double literals of GetRelativeLevelMultiplier, kept so the narrowing matches Mids.
    private const double RelStepUp = 0.0500000007450581;
    private const double RelStepDown = 0.100000001490116;

    // GetEnhancementEffect applies the per-effect multiplier only above this magnitude.
    private const double MultiplierEpsilon = 0.01;

    private static readonly double SuperiorMult = F32.Round(1.25);

    /// <summary>
    /// <c>I9Slot.GetScheduleMult</c>: one effect's pre-ED value. Grade, relative level and IO level are clamped to
    /// their ranges, as Mids clamps rather than rejects, then the schedule cell is read and the relative-level and
    /// Superior multipliers applied.
    /// </summary>
    /// <param name="typeName">The eType name.</param>
    /// <param name="superior">Whether the enhancement is Superior.</param>
    /// <param name="ioLevel">The 0-based IO level.</param>
    /// <param name="grade">The eEnhGrade ordinal.</param>
    /// <param name="relativeLevel">The eEnhRelative ordinal.</param>
    /// <param name="schedule">The eSchedule ordinal.</param>
    /// <param name="tables">The Maths.mhd tables.</param>
    /// <returns>The value.</returns>
    public static double ScheduleMult(string typeName, bool superior, int ioLevel, int grade, int relativeLevel, int schedule, MathTables tables)
    {
        grade = Math.Min(Math.Max(grade, GradeNone), GradeSingleO);
        relativeLevel = Math.Min(Math.Max(relativeLevel, RelNone), RelPlusFive);
        ioLevel = Math.Min(Math.Max(ioLevel, 0), tables.MultIo.Count - 1);
        if (schedule is (int)Schedule.None or (int)Schedule.Multiple)
        {
            return 0.0;
        }

        double num1 = BaseTableValue(typeName, grade, ioLevel, schedule, tables);
        double num2 = F32.Round(num1 * RelativeLevelMult(relativeLevel));
        if (superior)
        {
            num2 = F32.Round(num2 * SuperiorMult);
        }

        return num2;
    }

    /// <summary>
    /// <c>I9Slot.GetEnhancementEffect</c>: the sum of the schedule multipliers of every Enhancement-mode effect that
    /// matches the aspect, and the sub-aspect for Mez. <paramref name="mag"/>'s sign gates buff-only and debuff-only
    /// effects and never scales the result.
    /// </summary>
    /// <param name="record">The enhancement.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="aspect">The eEnhance name.</param>
    /// <param name="subEnh">The sub-aspect, or -1.</param>
    /// <param name="mag">The magnitude whose sign gates the effects.</param>
    /// <param name="tables">The Maths.mhd tables.</param>
    /// <returns>The slot's value for the aspect.</returns>
    public static double EnhancementEffect(EnhancementRecord record, SlotRef slot, string aspect, int subEnh, double mag, MathTables tables)
    {
        if (slot.Enh < 0)
        {
            return 0.0;
        }

        double total = 0.0;
        foreach (EnhEffect effect in record.Effects)
        {
            if ((string.Equals(effect.BuffMode, "DeBuffOnly", StringComparison.Ordinal) && !(mag <= 0.0))
                || (string.Equals(effect.BuffMode, "BuffOnly", StringComparison.Ordinal) && !(mag >= 0.0))
                || effect.Schedule == (int)Schedule.None
                || !string.Equals(effect.Enhance, aspect, StringComparison.Ordinal)
                || (subEnh >= 0 && subEnh != effect.EnhanceSubId))
            {
                continue;
            }

            double value = ScheduleMult(record.TypeName, record.Superior, slot.IoLevel, slot.Grade, slot.RelativeLevel, effect.Schedule, tables);
            if (Math.Abs(effect.Multiplier) > MultiplierEpsilon)
            {
                value = F32.Round(value * effect.Multiplier);
            }

            total = F32.Round(total + value);
        }

        return total;
    }

    /// <summary><c>GetRelativeLevelMultiplier</c>: None kills the slot; each level above Even adds 5%, each below takes 10%.</summary>
    /// <param name="relativeLevel">The eEnhRelative ordinal.</param>
    /// <returns>The multiplier.</returns>
    internal static double RelativeLevelMult(int relativeLevel)
    {
        if (relativeLevel == RelNone)
        {
            return 0.0;
        }

        int offset = relativeLevel - RelEven;
        return offset >= 0 ? F32.Round((offset * RelStepUp) + 1.0) : F32.Round(1.0 + (offset * RelStepDown));
    }

    /// <summary>The schedule-table cell the enhancement type selects.</summary>
    private static double BaseTableValue(string typeName, int grade, int ioLevel, int schedule, MathTables tables)
    {
        switch (typeName)
        {
            case "Normal":
                return grade switch
                {
                    GradeTrainingO => tables.MultTo[schedule],
                    GradeDualO => tables.MultDo[schedule],
                    GradeSingleO => tables.MultSo[schedule],
                    _ => 0.0,
                };
            case "InventO" or "SetO":
                return tables.MultIo[ioLevel][schedule];
            case "SpecialO":
                return tables.MultSo[schedule];
            default:
                return 0.0;
        }
    }
}
