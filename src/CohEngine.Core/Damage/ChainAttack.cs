using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>A damaging or damage-buffing power: cadence, damage per activation, profile, buff and debuff riders.</summary>
public sealed record ChainAttack
{
    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the damage per activation.</summary>
    public required double Damage { get; init; }

    /// <summary>Gets the raw cast time.</summary>
    public required double CastTime { get; init; }

    /// <summary>Gets the recharge time.</summary>
    public required double RechargeTime { get; init; }

    /// <summary>Gets the endurance cost.</summary>
    public required double EndCost { get; init; }

    /// <summary>Gets the profile.</summary>
    public required AttackProfile Profile { get; init; }

    /// <summary>Gets the toggle tick period.</summary>
    public double ActivatePeriod { get; init; }

    /// <summary>Gets the self damage buff fraction.</summary>
    public double SelfDamageBuff { get; init; }

    /// <summary>Gets the self damage buff duration.</summary>
    public double SelfDamageBuffDuration { get; init; }

    /// <summary>Gets the target -Res fraction.</summary>
    public double TargetResDebuff { get; init; }

    /// <summary>Gets the target -Res duration.</summary>
    public double TargetResDebuffDuration { get; init; }
}
