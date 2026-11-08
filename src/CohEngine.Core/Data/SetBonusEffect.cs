namespace CohEngine.Core.Data;

/// <summary>One effect of Mids' set-bonus virtual power, the reference the set-bonus tally is tested against.</summary>
public sealed record SetBonusEffect
{
    /// <summary>Gets the effect index.</summary>
    public required int Index { get; init; }

    /// <summary>Gets the eEffectType name.</summary>
    public required string EffectType { get; init; }

    /// <summary>Gets the eDamage name.</summary>
    public required string DamageType { get; init; }

    /// <summary>Gets the eMez name.</summary>
    public required string MezType { get; init; }

    /// <summary>Gets the modified eEffectType name.</summary>
    public required string EtModifies { get; init; }

    /// <summary>Gets the raw magnitude.</summary>
    public required double Mag { get; init; }

    /// <summary>Gets the buffed magnitude.</summary>
    public required double BuffedMag { get; init; }
}
