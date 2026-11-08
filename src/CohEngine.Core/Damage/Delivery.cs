using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>Where an attack is delivered from.</summary>
public enum Delivery
{
    /// <summary>Within melee reach.</summary>
    Melee,

    /// <summary>At range.</summary>
    Ranged,
}
