using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>A summon power with its pets' attacks precomputed.</summary>
public sealed record PetSummon
{
    /// <summary>Gets the summon power name.</summary>
    public required string SummonName { get; init; }

    /// <summary>Gets the pet name.</summary>
    public required string PetName { get; init; }

    /// <summary>Gets one pet's attacks.</summary>
    public required IReadOnlyList<ChainAttack> PetAttacks { get; init; }

    /// <summary>Gets the pet count.</summary>
    public required int PetCount { get; init; }

    /// <summary>Gets the lifetime, infinity for a permanent pet.</summary>
    public required double Duration { get; init; }

    /// <summary>Gets the summon cast time.</summary>
    public required double SummonCastTime { get; init; }

    /// <summary>Gets the summon recharge.</summary>
    public required double SummonRecharge { get; init; }

    /// <summary>Gets the summon endurance cost.</summary>
    public required double SummonEndCost { get; init; }

    /// <summary>Gets 1 plus the pet's damage strength.</summary>
    public double DamageBuffBaseline { get; init; } = 1.0;

    /// <summary>Gets the label.</summary>
    public string Label { get; init; } = string.Empty;
}
