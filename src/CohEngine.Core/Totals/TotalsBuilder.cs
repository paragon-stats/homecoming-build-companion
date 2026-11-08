using CohEngine.Core.Data;

namespace CohEngine.Core.Totals;

/// <summary><c>Character.TotalStatistics</c> while GBD_Totals fills it; <see cref="Build"/> freezes it into the record.</summary>
internal sealed class TotalsBuilder
{
    /// <summary>Initializes a new instance of the <see cref="TotalsBuilder"/> class, the vectors sized by their enums.</summary>
    /// <param name="enums">The enum maps.</param>
    public TotalsBuilder(EnumMaps enums)
    {
        Def = new double[enums.Size("eDamage")];
        Res = new double[enums.Size("eDamage")];
        Mez = new double[enums.Size("eMez")];
        MezRes = new double[enums.Size("eMez")];
        DebuffRes = new double[enums.Size("eEffectType")];
        Elusivity = new double[enums.Size("eDamage")];
    }

    private TotalsBuilder(TotalsBuilder source)
    {
        Def = [.. source.Def];
        Res = [.. source.Res];
        Mez = [.. source.Mez];
        MezRes = [.. source.MezRes];
        DebuffRes = [.. source.DebuffRes];
        Elusivity = [.. source.Elusivity];
    }

    public double[] Def { get; }

    public double[] Res { get; }

    public double[] Mez { get; }

    public double[] MezRes { get; }

    public double[] DebuffRes { get; }

    public double[] Elusivity { get; }

    public double HpRegen { get; set; }

    public double HpMax { get; set; }

    public double Absorb { get; set; }

    public double EndRec { get; set; }

    public double EndUse { get; set; }

    public double EndMax { get; set; }

    public double RunSpd { get; set; }

    public double MaxRunSpd { get; set; }

    public double JumpSpd { get; set; }

    public double MaxJumpSpd { get; set; }

    public double FlySpd { get; set; }

    public double MaxFlySpd { get; set; }

    public double JumpHeight { get; set; }

    public double StealthPve { get; set; }

    public double StealthPvp { get; set; }

    public double ThreatLevel { get; set; }

    public double Perception { get; set; }

    public double BuffHaste { get; set; }

    public double BuffAcc { get; set; }

    public double BuffToHit { get; set; }

    public double BuffDam { get; set; }

    public double BuffEndRdx { get; set; }

    public double BuffRange { get; set; }

    public double BuffHeal { get; set; }

    /// <summary>
    /// <c>TotalStatistics.Assign</c>: a value copy, quirk included: MaxRunSpd, MaxJumpSpd and MaxFlySpd are not copied,
    /// so the capped copy keeps them at 0 and the speed caps read them from the uncapped totals.
    /// </summary>
    /// <returns>The copy.</returns>
    public TotalsBuilder Assign()
    {
        return new TotalsBuilder(this)
        {
            HpRegen = HpRegen,
            HpMax = HpMax,
            Absorb = Absorb,
            EndRec = EndRec,
            EndUse = EndUse,
            EndMax = EndMax,
            RunSpd = RunSpd,
            JumpSpd = JumpSpd,
            FlySpd = FlySpd,
            JumpHeight = JumpHeight,
            StealthPve = StealthPve,
            StealthPvp = StealthPvp,
            ThreatLevel = ThreatLevel,
            Perception = Perception,
            BuffHaste = BuffHaste,
            BuffAcc = BuffAcc,
            BuffToHit = BuffToHit,
            BuffDam = BuffDam,
            BuffEndRdx = BuffEndRdx,
            BuffRange = BuffRange,
            BuffHeal = BuffHeal,
        };
    }

    /// <summary>Freezes the totals.</summary>
    /// <returns>The record.</returns>
    public TotalStatistics Build()
    {
        return new TotalStatistics
        {
            Def = [.. Def],
            Res = [.. Res],
            Mez = [.. Mez],
            MezRes = [.. MezRes],
            DebuffRes = [.. DebuffRes],
            Elusivity = [.. Elusivity],
            HpRegen = HpRegen,
            HpMax = HpMax,
            Absorb = Absorb,
            EndRec = EndRec,
            EndUse = EndUse,
            EndMax = EndMax,
            RunSpd = RunSpd,
            MaxRunSpd = MaxRunSpd,
            JumpSpd = JumpSpd,
            MaxJumpSpd = MaxJumpSpd,
            FlySpd = FlySpd,
            MaxFlySpd = MaxFlySpd,
            JumpHeight = JumpHeight,
            StealthPve = StealthPve,
            StealthPvp = StealthPvp,
            ThreatLevel = ThreatLevel,
            Perception = Perception,
            BuffHaste = BuffHaste,
            BuffAcc = BuffAcc,
            BuffToHit = BuffToHit,
            BuffDam = BuffDam,
            BuffEndRdx = BuffEndRdx,
            BuffRange = BuffRange,
            BuffHeal = BuffHeal,
        };
    }
}
