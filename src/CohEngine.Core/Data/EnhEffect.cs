using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>One Enhancement-mode effect an enhancement grants (the Python port's <c>enhancement.EnhEffect</c>).</summary>
public sealed record EnhEffect
{
    /// <summary>Gets the eEnhance name: the aspect this effect boosts.</summary>
    [JsonPropertyName("EnhanceName")]
    public required string Enhance { get; init; }

    /// <summary>Gets the sub-aspect id (Mez), or -1.</summary>
    public required int EnhanceSubId { get; init; }

    /// <summary>Gets the eSchedule ordinal.</summary>
    public required int Schedule { get; init; }

    /// <summary>Gets the per-effect multiplier.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double Multiplier { get; init; }

    /// <summary>Gets the eBuffDebuff name: Any, BuffOnly or DeBuffOnly.</summary>
    public required string BuffMode { get; init; }
}
