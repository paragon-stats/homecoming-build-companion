using CohEngine.Core.DamageLayers;
using CohEngine.Core.Maths;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Chain;

/// <summary>One candidate rotation: a subset of attacks fired once per cycle, with its cadence and cost.</summary>
public sealed record ChainOption
{
    /// <summary>Gets the rotation.</summary>
    public required IReadOnlyList<string> Rotation { get; init; }

    /// <summary>Gets the stance.</summary>
    public required Stance Stance { get; init; }

    /// <summary>Gets the arcana cast total.</summary>
    public required double CastSum { get; init; }

    /// <summary>Gets the number of standoff crossings.</summary>
    public required int Switches { get; init; }

    /// <summary>Gets the stance-switch time per cycle.</summary>
    public required double MoveTime { get; init; }

    /// <summary>Gets the cycle time.</summary>
    public required double CycleTime { get; init; }

    /// <summary>Gets the idle time.</summary>
    public required double Gap { get; init; }

    /// <summary>Gets a value indicating whether the gap is zero.</summary>
    public required bool GapFree { get; init; }

    /// <summary>Gets the damage per cycle.</summary>
    public required double DamagePerCycle { get; init; }

    /// <summary>Gets the DPS, scaled by the timeline share.</summary>
    public required double Dps { get; init; }

    /// <summary>Gets the endurance per second, scaled.</summary>
    public required double EndPerSec { get; init; }

    /// <summary>Gets the net endurance per second.</summary>
    public required double NetEndPerSec { get; init; }

    /// <summary>Gets the time to empty; infinity when sustainable.</summary>
    public required double TimeToEmpty { get; init; }

    /// <summary>Gets a value indicating whether the option never drains.</summary>
    public required bool Sustainable { get; init; }

    /// <summary>Gets the stacked target -Res the rotation keeps up.</summary>
    public double ResDebuff { get; init; }

    /// <summary>Gets the debuff-adjusted DPS used for ranking.</summary>
    public double Score => F32.Round(Dps * (1.0 + ResDebuff));
}
