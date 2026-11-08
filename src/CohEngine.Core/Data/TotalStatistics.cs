using CohEngine.Core.Maths;

namespace CohEngine.Core.Data;

/// <summary>
/// Mirror of <c>Character.TotalStatistics</c> (the Python port's <c>base_totals.TotalStatistics</c>).
/// Values are as the dump wrote them; parity tests pass both sides through <c>F32.Round</c>.
/// </summary>
public sealed record TotalStatistics
{
    /// <summary>Gets defense per eDamage.</summary>
    public required IReadOnlyList<double> Def { get; init; }

    /// <summary>Gets resistance per eDamage.</summary>
    public required IReadOnlyList<double> Res { get; init; }

    /// <summary>Gets mez protection per eMez.</summary>
    public required IReadOnlyList<double> Mez { get; init; }

    /// <summary>Gets mez resistance per eMez.</summary>
    public required IReadOnlyList<double> MezRes { get; init; }

    /// <summary>Gets debuff resistance per eEffectType.</summary>
    public required IReadOnlyList<double> DebuffRes { get; init; }

    /// <summary>Gets elusivity per eDamage.</summary>
    public required IReadOnlyList<double> Elusivity { get; init; }

    /// <summary>Gets HP regeneration.</summary>
    public required double HpRegen { get; init; }

    /// <summary>Gets max HP.</summary>
    public required double HpMax { get; init; }

    /// <summary>Gets absorb.</summary>
    public required double Absorb { get; init; }

    /// <summary>Gets endurance recovery.</summary>
    public required double EndRec { get; init; }

    /// <summary>Gets endurance use.</summary>
    public required double EndUse { get; init; }

    /// <summary>Gets max endurance.</summary>
    public required double EndMax { get; init; }

    /// <summary>Gets run speed.</summary>
    public required double RunSpd { get; init; }

    /// <summary>Gets max run speed.</summary>
    public required double MaxRunSpd { get; init; }

    /// <summary>Gets jump speed.</summary>
    public required double JumpSpd { get; init; }

    /// <summary>Gets max jump speed.</summary>
    public required double MaxJumpSpd { get; init; }

    /// <summary>Gets fly speed.</summary>
    public required double FlySpd { get; init; }

    /// <summary>Gets max fly speed.</summary>
    public required double MaxFlySpd { get; init; }

    /// <summary>Gets jump height.</summary>
    public required double JumpHeight { get; init; }

    /// <summary>Gets PvE stealth.</summary>
    public required double StealthPve { get; init; }

    /// <summary>Gets PvP stealth.</summary>
    public required double StealthPvp { get; init; }

    /// <summary>Gets threat level.</summary>
    public required double ThreatLevel { get; init; }

    /// <summary>Gets perception.</summary>
    public required double Perception { get; init; }

    /// <summary>Gets the recharge buff.</summary>
    public required double BuffHaste { get; init; }

    /// <summary>Gets the accuracy buff.</summary>
    public required double BuffAcc { get; init; }

    /// <summary>Gets the to-hit buff.</summary>
    public required double BuffToHit { get; init; }

    /// <summary>Gets the damage buff.</summary>
    public required double BuffDam { get; init; }

    /// <summary>Gets the endurance reduction buff.</summary>
    public required double BuffEndRdx { get; init; }

    /// <summary>Gets the range buff.</summary>
    public required double BuffRange { get; init; }

    /// <summary>Gets the heal buff.</summary>
    public required double BuffHeal { get; init; }
}
