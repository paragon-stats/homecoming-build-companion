using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>One resolved build power with its database effect records (the Python port's <c>effect.Power</c>).</summary>
public sealed record Power
{
    /// <summary>Gets the power's index in the build.</summary>
    public required int BuildIndex { get; init; }

    /// <summary>Gets the power's database index.</summary>
    public required int NidPower { get; init; }

    /// <summary>Gets the power's full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the power's static index.</summary>
    public required int StaticIndex { get; init; }

    /// <summary>Gets the ePowerType name.</summary>
    public required string PowerType { get; init; }

    /// <summary>Gets the forced class name (pet powers), or empty.</summary>
    public required string ForcedClass { get; init; }

    /// <summary>Gets a value indicating whether the power is a click buff.</summary>
    public required bool ClickBuff { get; init; }

    /// <summary>Gets the database minimum pick level.</summary>
    public required int Level { get; init; }

    /// <summary>Gets the end cost.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double EndCost { get; init; }

    /// <summary>Gets the activate period.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double ActivatePeriod { get; init; }

    /// <summary>Gets the toggle cost.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double ToggleCost { get; init; }

    /// <summary>Gets a value indicating whether the power scales by a variable.</summary>
    public required bool VariableEnabled { get; init; }

    /// <summary>Gets a value indicating whether the power counts toward totals.</summary>
    public required bool StatInclude { get; init; }

    /// <summary>Gets the build entry's variable value.</summary>
    public required int VariableValue { get; init; }

    /// <summary>Gets the power's effects.</summary>
    public required IReadOnlyList<Effect> Effects { get; init; }

    /// <summary>Gets the eSetType ordinals the power accepts; an explicit empty list accepts none.</summary>
    public required IReadOnlyList<int> SetTypes { get; init; }

    /// <summary>Gets the EnhancementClass ids the power accepts.</summary>
    public required IReadOnlyList<int> Enhancements { get; init; }

    /// <summary>Gets the 0-based level the build picked the power.</summary>
    public required int PickLevel { get; init; }

    /// <summary>Gets the base recharge time.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double RechargeTime { get; init; }

    /// <summary>Gets the base cast time.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double CastTime { get; init; }

    /// <summary>Gets the base interrupt time.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double InterruptTime { get; init; }

    /// <summary>Gets the eEnhance names the power ignores.</summary>
    public required IReadOnlyList<string> IgnoreEnh { get; init; }

    // The optional properties are settable, not init: the JSON source generator writes default(T) into an absent
    // init-only property, which would lose the initializer value the Python loader defaults to.
    /// <summary>Gets the prerequisite OR list of AND groups of power full names; absent in older dumps.</summary>
    public IReadOnlyList<IReadOnlyList<string>> RequiresPowers { get; set; } = [];

    /// <summary>Gets the forbidden OR list of AND groups of power full names; absent in older dumps.</summary>
    public IReadOnlyList<IReadOnlyList<string>> RequiresPowersNot { get; set; } = [];

    /// <summary>Gets a value indicating whether a pick was spent on the power; older dumps carry only real picks.</summary>
    public bool Chosen { get; set; } = true;

    /// <summary>Gets the AoE modifier magnitude expressions read; Mids' default for a power with no record.</summary>
    [JsonIgnore]
    public double AoeModifier { get; init; } = 1.0;

    /// <summary>Gets the base recharge time magnitude expressions read.</summary>
    [JsonIgnore]
    public double BaseRechargeTime { get; init; }
}
