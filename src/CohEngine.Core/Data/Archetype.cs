namespace CohEngine.Core.Data;

/// <summary>One class record (the Python port's <c>archetypes.Archetype</c>); the caps stay unquantized as the Python reads them.</summary>
public sealed record Archetype
{
    /// <summary>Gets the class index.</summary>
    public required int Index { get; init; }

    /// <summary>Gets the class name.</summary>
    public required string ClassName { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the class type.</summary>
    public required string ClassType { get; init; }

    /// <summary>Gets the AttribMod column: a stored indirection, never assumed equal to the index.</summary>
    public required int Column { get; init; }

    /// <summary>Gets a value indicating whether the class is playable.</summary>
    public required bool Playable { get; init; }

    /// <summary>Gets the base hit points.</summary>
    public required int Hitpoints { get; init; }

    /// <summary>Gets the hit point cap.</summary>
    public required double HpCap { get; init; }

    /// <summary>Gets the resistance cap.</summary>
    public required double ResCap { get; init; }

    /// <summary>Gets the damage cap.</summary>
    public required double DamageCap { get; init; }

    /// <summary>Gets the recharge cap.</summary>
    public required double RechargeCap { get; init; }

    /// <summary>Gets the recovery cap.</summary>
    public required double RecoveryCap { get; init; }

    /// <summary>Gets the regeneration cap.</summary>
    public required double RegenCap { get; init; }

    /// <summary>Gets the perception cap.</summary>
    public required double PerceptionCap { get; init; }

    /// <summary>Gets the base recovery.</summary>
    public required double BaseRecovery { get; init; }

    /// <summary>Gets the base regeneration.</summary>
    public required double BaseRegen { get; init; }

    /// <summary>Gets the base threat.</summary>
    public required double BaseThreat { get; init; }

    /// <summary>Gets the ancillary pool names.</summary>
    public required IReadOnlyList<string> Ancillary { get; init; }

    /// <summary>Gets the inherent powers.</summary>
    public required IReadOnlyList<InherentPower> InherentPowers { get; init; }
}
