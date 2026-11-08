using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>The <c>MidsContext.Config</c> state the reference totals were computed under (the Python port's <c>base_totals.EngineConfig</c>).</summary>
public sealed record EngineConfig
{
    /// <summary>Gets the eSuppress flags.</summary>
    public required int Suppression { get; init; }

    /// <summary>Gets a value indicating whether PvE is disabled.</summary>
    public required bool DisablePve { get; init; }

    /// <summary>Gets the exemplar level.</summary>
    public required int ForceLevel { get; init; }

    /// <summary>Gets the scaling to-hit.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double ScalingToHit { get; init; }
}
