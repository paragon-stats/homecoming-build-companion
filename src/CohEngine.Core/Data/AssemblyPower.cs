using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>A power as assembly reads it (the Python port's <c>assembly_data.AssemblyPower</c>).</summary>
public sealed record AssemblyPower
{
    /// <summary>Initializes a new instance of the <see cref="AssemblyPower"/> class.</summary>
    /// <remarks>The sets are copied into read-only sets, so a shared cached record cannot be changed.</remarks>
    /// <param name="entitiesAffectedJson">The EntitiesAffected members as the dump lists them.</param>
    /// <param name="entitiesAutoHitJson">The EntitiesAutoHit members as the dump lists them.</param>
    /// <param name="enhancementsJson">The Enhancements members as the dump lists them.</param>
    /// <param name="ignoreEnhJson">The IgnoreEnh members as the dump lists them.</param>
    [JsonConstructor]
    public AssemblyPower(ISet<string> entitiesAffectedJson, ISet<string> entitiesAutoHitJson, ISet<int> enhancementsJson, ISet<string> ignoreEnhJson)
    {
        ArgumentNullException.ThrowIfNull(entitiesAffectedJson);
        ArgumentNullException.ThrowIfNull(entitiesAutoHitJson);
        ArgumentNullException.ThrowIfNull(enhancementsJson);
        ArgumentNullException.ThrowIfNull(ignoreEnhJson);
        EntitiesAffected = entitiesAffectedJson.ToHashSet(StringComparer.Ordinal);
        EntitiesAutoHit = entitiesAutoHitJson.ToHashSet(StringComparer.Ordinal);
        Enhancements = enhancementsJson.ToHashSet();
        IgnoreEnh = ignoreEnhJson.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Gets the database index.</summary>
    public required int Nid { get; init; }

    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the ePowerType name.</summary>
    public required string PowerType { get; init; }

    /// <summary>Gets the ePowerSetType name.</summary>
    public required string PowerSetType { get; init; }

    /// <summary>Gets the eEntity names the power affects.</summary>
    [JsonIgnore]
    public IReadOnlySet<string> EntitiesAffected { get; }

    /// <summary>Gets the eEntity names the power auto-hits.</summary>
    [JsonIgnore]
    public IReadOnlySet<string> EntitiesAutoHit { get; }

    /// <summary>Gets the activate period.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double ActivatePeriod { get; init; }

    /// <summary>Gets the recharge time.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double RechargeTime { get; init; }

    /// <summary>Gets the base recharge time.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BaseRechargeTime { get; init; }

    /// <summary>Gets the cast time.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double CastTime { get; init; }

    /// <summary>Gets the AoE modifier.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double AoeModifier { get; init; }

    /// <summary>Gets the EnhancementClass ids the power accepts.</summary>
    [JsonIgnore]
    public IReadOnlySet<int> Enhancements { get; }

    /// <summary>Gets a value indicating whether the power has a GrantPower effect.</summary>
    public required bool HasGrantPowerEffect { get; init; }

    /// <summary>Gets a value indicating whether the power has a PowerOverride effect.</summary>
    public required bool HasPowerOverrideEffect { get; init; }

    /// <summary>Gets a value indicating whether the power absorbs its summon's effects.</summary>
    public required bool AbsorbSummonEffects { get; init; }

    /// <summary>Gets a value indicating whether the power absorbs its summon's attributes.</summary>
    public required bool AbsorbSummonAttributes { get; init; }

    /// <summary>Gets the eEnhance names the power ignores.</summary>
    [JsonIgnore]
    public IReadOnlySet<string> IgnoreEnh { get; }

    /// <summary>Gets the effects in the extended record shape.</summary>
    public required IReadOnlyList<Effect> Effects { get; init; }

    /// <summary>Gets <see cref="EntitiesAffected"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("EntitiesAffected")]
    internal ISet<string> EntitiesAffectedJson => (ISet<string>)EntitiesAffected;

    /// <summary>Gets <see cref="EntitiesAutoHit"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("EntitiesAutoHit")]
    internal ISet<string> EntitiesAutoHitJson => (ISet<string>)EntitiesAutoHit;

    /// <summary>Gets <see cref="Enhancements"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("Enhancements")]
    internal ISet<int> EnhancementsJson => (ISet<int>)Enhancements;

    /// <summary>Gets <see cref="IgnoreEnh"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("IgnoreEnh")]
    internal ISet<string> IgnoreEnhJson => (ISet<string>)IgnoreEnh;
}
