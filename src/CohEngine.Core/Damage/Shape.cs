using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>The attack's area.</summary>
public enum Shape
{
    /// <summary>One target.</summary>
    SingleTarget,

    /// <summary>A cone.</summary>
    Cone,

    /// <summary>A sphere on a target.</summary>
    TargetedAoe,

    /// <summary>A sphere on self.</summary>
    Pbaoe,

    /// <summary>A placed location.</summary>
    Location,
}
