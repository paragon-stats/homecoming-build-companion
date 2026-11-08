using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>Server-wide constants (the Python port's <c>base_totals.ServerData</c>).</summary>
public sealed record ServerData
{
    /// <summary>Gets the base to-hit.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BaseToHit { get; init; }

    /// <summary>Gets the added-slot cap.</summary>
    public required int MaxSlots { get; init; }

    /// <summary>Gets a value indicating whether Health and Stamina bonus slots are granted.</summary>
    public required bool EnableInherentSlotting { get; init; }

    /// <summary>Gets the base fly speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BaseFlySpeed { get; init; }

    /// <summary>Gets the base jump speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BaseJumpSpeed { get; init; }

    /// <summary>Gets the base jump height.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BaseJumpHeight { get; init; }

    /// <summary>Gets the base perception.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BasePerception { get; init; }

    /// <summary>Gets the base run speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BaseRunSpeed { get; init; }

    /// <summary>Gets the max fly speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxFlySpeed { get; init; }

    /// <summary>Gets the max jump speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxJumpSpeed { get; init; }

    /// <summary>Gets the max jump height.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxJumpHeight { get; init; }

    /// <summary>Gets the max run speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxRunSpeed { get; init; }

    /// <summary>Gets the hard max fly speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxMaxFlySpeed { get; init; }

    /// <summary>Gets the hard max jump speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxMaxJumpSpeed { get; init; }

    /// <summary>Gets the hard max run speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxMaxRunSpeed { get; init; }
}
