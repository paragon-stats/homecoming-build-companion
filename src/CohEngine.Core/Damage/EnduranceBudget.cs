using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Damage;

/// <summary>The endurance a build has to spend: pool, income and committed toggle drain (per second).</summary>
/// <param name="MaxEnd">The pool.</param>
/// <param name="RecoveryPerSec">The income.</param>
/// <param name="ToggleDrainPerSec">The toggle drain.</param>
public sealed record EnduranceBudget(double MaxEnd, double RecoveryPerSec, double ToggleDrainPerSec)
{
    /// <summary>Recovery minus toggle drain minus extra drain.</summary>
    /// <param name="extraDrain">Chain, patch and summon drain.</param>
    /// <returns>The net rate.</returns>
    public double NetPerSec(double extraDrain = 0.0)
    {
        return F32.Round(RecoveryPerSec - ToggleDrainPerSec - extraDrain);
    }

    /// <summary>Seconds from full to empty at the net rate; infinity when sustainable.</summary>
    /// <param name="extraDrain">Extra drain.</param>
    /// <returns>The time.</returns>
    public double TimeToEmpty(double extraDrain = 0.0)
    {
        double net = NetPerSec(extraDrain);
        return net >= 0.0 ? double.PositiveInfinity : F32.Round(MaxEnd / -net);
    }
}
