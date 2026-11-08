using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>A summoned entity a power absorbs (the Python port's <c>assembly_data.AssemblyEntity</c>).</summary>
public sealed record AssemblyEntity
{
    /// <summary>Initializes a new instance of the <see cref="AssemblyEntity"/> class.</summary>
    /// <remarks>The sets are copied into read-only sets, so a shared cached record cannot be changed.</remarks>
    /// <param name="firstPowersetEffectTypesJson">The FirstPowersetEffectTypes members as the dump lists them.</param>
    [JsonConstructor]
    public AssemblyEntity(IReadOnlyList<ISet<string>> firstPowersetEffectTypesJson)
    {
        ArgumentNullException.ThrowIfNull(firstPowersetEffectTypesJson);
        FirstPowersetEffectTypes = [.. firstPowersetEffectTypesJson.Select(set => (IReadOnlySet<string>)set.ToHashSet(StringComparer.Ordinal))];
    }

    /// <summary>Gets the entity index.</summary>
    public required int Nid { get; init; }

    /// <summary>Gets the entity UID.</summary>
    public required string Uid { get; init; }

    /// <summary>Gets the entity's class index.</summary>
    public required int ClassNid { get; init; }

    /// <summary>Gets the entity's powerset indexes in order.</summary>
    public required IReadOnlyList<int> PowersetNids { get; init; }

    /// <summary>Gets, per power of the first powerset, its effect types.</summary>
    [JsonIgnore]
    public IReadOnlyList<IReadOnlySet<string>> FirstPowersetEffectTypes { get; }

    /// <summary>Gets <see cref="FirstPowersetEffectTypes"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("FirstPowersetEffectTypes")]
    internal IReadOnlyList<ISet<string>> FirstPowersetEffectTypesJson => [.. FirstPowersetEffectTypes.Select(set => (ISet<string>)set)];
}
