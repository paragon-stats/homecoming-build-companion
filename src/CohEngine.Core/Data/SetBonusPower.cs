using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>A referenced set-bonus power (the Python port's <c>set_bonuses.SetBonusPower</c>).</summary>
public sealed record SetBonusPower
{
    /// <summary>Gets the global power id.</summary>
    public required int PowerId { get; init; }

    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the ePowerType name.</summary>
    public required string PowerType { get; init; }

    /// <summary>Gets a value indicating whether the power targets MyPet.</summary>
    public required bool MyPetTarget { get; init; }

    /// <summary>Gets a value indicating whether the power affects MyPet entities.</summary>
    public required bool MyPetEntities { get; init; }

    /// <summary>Gets the effects; a power named for Slow marks every effect Debuff.</summary>
    public required IReadOnlyList<Effect> Effects { get; init; }

    /// <summary>Gets a value indicating whether <c>ShouldSkipEffects</c> applies: target and entities both MyPet.</summary>
    [JsonIgnore]
    public bool MyPetOnly => MyPetTarget && MyPetEntities;
}
