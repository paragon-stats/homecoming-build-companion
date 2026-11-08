using CohEngine.Core.Maths;

namespace CohEngine.Core.Enhancement;

/// <summary>
/// Enhancement Diversification and the aspect-to-schedule map (<c>Enhancement.ApplyED</c>, <c>Enhancement.GetSchedule</c>).
/// The slopes are the exact doubles the C# source uses, and the float casts land where its casts do.
/// </summary>
internal static class Ed
{
    private const double Slope2 = 0.899999976158142;
    private const double Slope3 = 0.699999988079071;
    private const double Slope4 = 0.150000005960464;

    /// <summary>Maps a curated aspect to its ED schedule.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="mezSub">The eMez sub-type, or -1.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleFor(Aspect aspect, int mezSub = -1)
    {
        return aspect switch
        {
            Aspect.Defense or Aspect.Range or Aspect.Resistance or Aspect.ToHit => Schedule.B,
            Aspect.Interrupt => Schedule.C,
            Aspect.Mez when mezSub is 4 or 5 => Schedule.D,
            _ => Schedule.A,
        };
    }

    /// <summary>
    /// Applies ED once to a per-aspect pre-ED aggregate: piecewise linear, slopes 1, 0.9, 0.7 and 0.15 over the
    /// schedule's three thresholds. It runs on the sum across all slots, never per slot.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="value">The pre-ED aggregate.</param>
    /// <param name="tables">The Maths.mhd tables.</param>
    /// <returns>The post-ED value.</returns>
    public static double ApplyEd(Schedule schedule, double value, MathTables tables)
    {
        if (schedule is Schedule.None or Schedule.Multiple)
        {
            return 0.0;
        }

        IReadOnlyList<double> thresholds = tables.MultEd[(int)schedule];
        value = F32.Round(value);
        if (value <= thresholds[0])
        {
            return value;
        }

        double edm1 = F32.Round(thresholds[0] + F32.Round((thresholds[1] - thresholds[0]) * Slope2));
        double edm2 = F32.Round(thresholds[0] + ((thresholds[1] - thresholds[0]) * Slope2) + ((thresholds[2] - thresholds[1]) * Slope3));
        if (value > thresholds[2])
        {
            return F32.Round(edm2 + F32.Round((value - thresholds[2]) * Slope4));
        }

        if (value > thresholds[1])
        {
            return F32.Round(edm1 + F32.Round((value - thresholds[1]) * Slope3));
        }

        return F32.Round(thresholds[0] + F32.Round((value - thresholds[0]) * Slope2));
    }
}
