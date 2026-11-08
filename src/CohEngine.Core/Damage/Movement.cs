using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>How fast the character closes or opens range, and the standoff it keeps.</summary>
/// <param name="SpeedFtPerSec">The speed.</param>
/// <param name="StandoffFt">The standoff.</param>
public sealed record Movement(double SpeedFtPerSec, double StandoffFt)
{
    /// <summary>Gets the seconds to cross the standoff once.</summary>
    public double SwitchTime => SpeedFtPerSec > 0.0 ? F32.Round(StandoffFt / SpeedFtPerSec) : double.PositiveInfinity;
}
