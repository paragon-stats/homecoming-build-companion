using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>One enhanced effect of <c>enhanced_powers.json</c>.</summary>
public sealed record EnhancedEffect
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

    /// <summary>Gets the enhanced magnitude.</summary>
    [JsonPropertyName("Math_Mag")]
    public required double MathMag { get; init; }

    /// <summary>Gets the enhanced duration.</summary>
    [JsonPropertyName("Math_Duration")]
    public required double MathDuration { get; init; }
}
