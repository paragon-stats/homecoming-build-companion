using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Damage;

/// <summary>Where the character stands while running the chain.</summary>
public enum Stance
{
    /// <summary>In melee.</summary>
    Melee,

    /// <summary>At range.</summary>
    Ranged,

    /// <summary>Switching between the two.</summary>
    Hybrid,
}
