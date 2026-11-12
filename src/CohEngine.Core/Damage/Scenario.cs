using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Damage;

/// <summary>The fight being modelled: how many foes stand in reach of an area attack.</summary>
/// <param name="Targets">The foe count.</param>
public sealed record Scenario(int Targets = 1)
{
    /// <summary>Single-target powers hit one foe; area powers hit up to their cap.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The count hit.</returns>
    public int TargetsHit(AttackProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Shape == Shape.SingleTarget)
        {
            return 1;
        }

        return Math.Max(1, Math.Min(profile.MaxTargets != 0 ? profile.MaxTargets : Targets, Targets));
    }
}
