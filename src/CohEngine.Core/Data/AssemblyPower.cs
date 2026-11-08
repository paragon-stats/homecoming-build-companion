using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>A power as assembly reads it (the Python port's <c>assembly_data.AssemblyPower</c>).</summary>
public sealed record AssemblyPower
{
    /// <summary>Gets the database index.</summary>
    public required int Nid { get; init; }

    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the ePowerType name.</summary>
    public required string PowerType { get; init; }

    /// <summary>Gets the ePowerSetType name.</summary>
    public required string PowerSetType { get; init; }

    /// <summary>Gets the eEntity names the power affects.</summary>
    public required ISet<string> EntitiesAffected { get; init; }

    /// <summary>Gets the eEntity names the power auto-hits.</summary>
    public required ISet<string> EntitiesAutoHit { get; init; }

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
    public required ISet<int> Enhancements { get; init; }

    /// <summary>Gets a value indicating whether the power has a GrantPower effect.</summary>
    public required bool HasGrantPowerEffect { get; init; }

    /// <summary>Gets a value indicating whether the power has a PowerOverride effect.</summary>
    public required bool HasPowerOverrideEffect { get; init; }

    /// <summary>Gets a value indicating whether the power absorbs its summon's effects.</summary>
    public required bool AbsorbSummonEffects { get; init; }

    /// <summary>Gets a value indicating whether the power absorbs its summon's attributes.</summary>
    public required bool AbsorbSummonAttributes { get; init; }

    /// <summary>Gets the eEnhance names the power ignores.</summary>
    public required ISet<string> IgnoreEnh { get; init; }

    /// <summary>Gets the effects in the extended record shape.</summary>
    public required IReadOnlyList<Effect> Effects { get; init; }
}
