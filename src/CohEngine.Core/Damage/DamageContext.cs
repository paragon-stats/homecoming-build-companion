using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>Everything a source or modifier reads, built once per evaluation.</summary>
public sealed record DamageContext
{
    /// <summary>Gets the attacks.</summary>
    public required IReadOnlyList<ChainAttack> Attacks { get; init; }

    /// <summary>Gets the endurance budget.</summary>
    public required EnduranceBudget Budget { get; init; }

    /// <summary>Gets the stance.</summary>
    public required Stance Stance { get; init; }

    /// <summary>Gets the scenario.</summary>
    public required Scenario Scenario { get; init; }

    /// <summary>Gets the movement.</summary>
    public required Movement Movement { get; init; }

    /// <summary>Gets the archetype class name.</summary>
    public required string ArchetypeClass { get; init; }

    /// <summary>Gets the named assumptions.</summary>
    public IReadOnlyDictionary<string, double> Assumptions { get; init; } = new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>Gets 1 plus the damage strength already applied.</summary>
    public double DamageBuffBaseline { get; init; } = 1.0;

    /// <summary>Gets the pets.</summary>
    public IReadOnlyList<PetSummon> Pets { get; init; } = [];

    /// <summary>Gets the timeline share left for the chain.</summary>
    public double TimelineAvailable { get; init; } = 1.0;

    /// <summary>Gets the drain of the non-chain sources.</summary>
    public double OtherDrainPerSec { get; init; }

    /// <summary>Gets the form the chain runs in; empty is the base form.</summary>
    public string Form { get; init; } = string.Empty;

    /// <summary>The context's attacks with the given role, in input order.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The attacks.</returns>
    public IReadOnlyList<ChainAttack> ByRole(Role role)
    {
        return [.. Attacks.Where(attack => attack.Profile.Role == role)];
    }

    /// <summary>Reads an assumption.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The default.</param>
    /// <returns>The value.</returns>
    public double Assume(string key, double fallback)
    {
        return Assumptions.TryGetValue(key, out double value) ? value : fallback;
    }
}
