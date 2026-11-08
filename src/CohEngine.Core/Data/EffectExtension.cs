using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>The extended keys of one effect in a build's <c>power_assembly.json</c>.</summary>
public sealed record EffectExtension
{
    /// <summary>Gets the effect index the keys belong to.</summary>
    public required int Index { get; init; }

    /// <summary>Gets the summoned entity's UID.</summary>
    public required string Summon { get; init; }

    /// <summary>Gets the summoned entity's index.</summary>
    public required int NSummon { get; init; }

    /// <summary>Gets the duration.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double Duration { get; init; }

    /// <summary>Gets the effect id.</summary>
    public required string EffectId { get; init; }

    /// <summary>Gets the reward name.</summary>
    public required string Reward { get; init; }

    /// <summary>Gets the active conditionals.</summary>
    public required IReadOnlyList<KeyValuePair<string, string>> ActiveConditionals { get; init; }

    /// <summary>Gets a value indicating whether the effect is enhancement-granted.</summary>
    public required bool IsEnhancementEffect { get; init; }

    /// <summary>Gets the absorbed power's index.</summary>
    [JsonPropertyName("Absorbed_Power_nID")]
    public required int AbsorbedPowerNid { get; init; }
}
