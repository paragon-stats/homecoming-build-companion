using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>One granted sub-power carrying an incarnate aspect (the Python port's <c>incarnate.IncarnateSubPower</c>).</summary>
public sealed record IncarnateSubPower
{
    /// <summary>Initializes a new instance of the <see cref="IncarnateSubPower"/> class.</summary>
    /// <remarks>The sets are copied into read-only sets, so a shared cached record cannot be changed.</remarks>
    /// <param name="enhancementsJson">The Enhancements members as the dump lists them.</param>
    [JsonConstructor]
    public IncarnateSubPower(ISet<int> enhancementsJson)
    {
        ArgumentNullException.ThrowIfNull(enhancementsJson);
        Enhancements = enhancementsJson.ToHashSet();
    }

    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the EnhancementClass ids the sub-power's accept gate lists.</summary>
    [JsonIgnore]
    public IReadOnlySet<int> Enhancements { get; }

    /// <summary>Gets the effects.</summary>
    public required IReadOnlyList<Effect> Effects { get; init; }

    /// <summary>Gets <see cref="Enhancements"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("Enhancements")]
    internal ISet<int> EnhancementsJson => (ISet<int>)Enhancements;
}
