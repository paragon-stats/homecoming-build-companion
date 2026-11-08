using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>One build power record of <c>power_assembly.json</c>.</summary>
public sealed record PowerAssemblyRecord
{
    /// <summary>Initializes a new instance of the <see cref="PowerAssemblyRecord"/> class.</summary>
    /// <remarks>The sets are copied into read-only sets, so a shared cached record cannot be changed.</remarks>
    /// <param name="entitiesAffectedJson">The EntitiesAffected members as the dump lists them.</param>
    /// <param name="entitiesAutoHitJson">The EntitiesAutoHit members as the dump lists them.</param>
    /// <param name="ignoreEnhJson">The IgnoreEnh members as the dump lists them.</param>
    [JsonConstructor]
    public PowerAssemblyRecord(ISet<string> entitiesAffectedJson, ISet<string> entitiesAutoHitJson, ISet<string> ignoreEnhJson)
    {
        ArgumentNullException.ThrowIfNull(entitiesAffectedJson);
        ArgumentNullException.ThrowIfNull(entitiesAutoHitJson);
        ArgumentNullException.ThrowIfNull(ignoreEnhJson);
        EntitiesAffected = entitiesAffectedJson.ToHashSet(StringComparer.Ordinal);
        EntitiesAutoHit = entitiesAutoHitJson.ToHashSet(StringComparer.Ordinal);
        IgnoreEnh = ignoreEnhJson.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Gets the power's index in the build.</summary>
    public required int BuildIndex { get; init; }

    /// <summary>Gets the ePowerSetType name.</summary>
    public required string PowerSetType { get; init; }

    /// <summary>Gets the eEntity names the power affects.</summary>
    [JsonIgnore]
    public IReadOnlySet<string> EntitiesAffected { get; }

    /// <summary>Gets the eEntity names the power auto-hits.</summary>
    [JsonIgnore]
    public IReadOnlySet<string> EntitiesAutoHit { get; }

    /// <summary>Gets the base recharge time.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BaseRechargeTime { get; init; }

    /// <summary>Gets the AoE modifier.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double AoeModifier { get; init; }

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

    /// <summary>Gets the entry's sub-powers.</summary>
    public required IReadOnlyList<SubPower> SubPowers { get; init; }

    /// <summary>Gets the extended keys of the power's effects.</summary>
    public required IReadOnlyList<EffectExtension> Effects { get; init; }

    /// <summary>Gets <see cref="EntitiesAffected"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("EntitiesAffected")]
    internal ISet<string> EntitiesAffectedJson => (ISet<string>)EntitiesAffected;

    /// <summary>Gets <see cref="EntitiesAutoHit"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("EntitiesAutoHit")]
    internal ISet<string> EntitiesAutoHitJson => (ISet<string>)EntitiesAutoHit;

    /// <summary>Gets <see cref="IgnoreEnh"/> in the shape JSON reads and writes it.</summary>
    [JsonInclude]
    [JsonPropertyName("IgnoreEnh")]
    internal ISet<string> IgnoreEnhJson => (ISet<string>)IgnoreEnh;
}
