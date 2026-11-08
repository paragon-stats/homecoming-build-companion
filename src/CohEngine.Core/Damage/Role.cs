using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>How a power shapes the play cycle.</summary>
public enum Role
{
    /// <summary>In the chain.</summary>
    Chain,

    /// <summary>A damage aura.</summary>
    Aura,

    /// <summary>A location patch.</summary>
    Patch,

    /// <summary>A pet summon.</summary>
    PetSummon,

    /// <summary>A self damage-buff click.</summary>
    BuffClick,

    /// <summary>A click that buffs damage or puts -Res on foes.</summary>
    Support,

    /// <summary>A toggle or auto holding -Res on foes.</summary>
    PersistentDebuff,
}
