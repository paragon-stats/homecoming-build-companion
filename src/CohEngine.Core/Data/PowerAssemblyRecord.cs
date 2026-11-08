using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>One build power record of <c>power_assembly.json</c>.</summary>
public sealed record PowerAssemblyRecord
{
    /// <summary>Gets the power's index in the build.</summary>
    public required int BuildIndex { get; init; }

    /// <summary>Gets the ePowerSetType name.</summary>
    public required string PowerSetType { get; init; }

    /// <summary>Gets the eEntity names the power affects.</summary>
    public required ISet<string> EntitiesAffected { get; init; }

    /// <summary>Gets the eEntity names the power auto-hits.</summary>
    public required ISet<string> EntitiesAutoHit { get; init; }

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
    public required ISet<string> IgnoreEnh { get; init; }

    /// <summary>Gets the entry's sub-powers.</summary>
    public required IReadOnlyList<SubPower> SubPowers { get; init; }

    /// <summary>Gets the extended keys of the power's effects.</summary>
    public required IReadOnlyList<EffectExtension> Effects { get; init; }
}
