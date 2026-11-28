namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>The one decision on a build's two level 1 picks, read by H-POWER-006 and by the <c>.mbd</c> writer's E46.</summary>
internal enum LevelOnePairVerdict
{
    /// <summary>A primary pick and then a secondary pick.</summary>
    InOrder,

    /// <summary>A secondary pick and then a primary pick.</summary>
    Swapped,

    /// <summary>The first pick is not in the primary's category.</summary>
    FirstNotPrimary,

    /// <summary>The first pick is primary but the second is not in the secondary's category.</summary>
    SecondNotSecondary,

    /// <summary>The primary or secondary power set is missing, or there are fewer than two level 1 picks.</summary>
    CannotTell,
}
