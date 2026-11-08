using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>
/// One power effect record, mirroring the harness dump field for field (the Python port's <c>effect.Effect</c>).
/// Enum-valued fields hold the C# enum member names; expression fields stay unparsed strings.
/// </summary>
public sealed record Effect
{
    /// <summary>Gets the effect's index within its power.</summary>
    public required int Index { get; init; }

    /// <summary>Gets the eEffectType name.</summary>
    public required string EffectType { get; init; }

    /// <summary>Gets the eDamage name.</summary>
    public required string DamageType { get; init; }

    /// <summary>Gets the eMez name.</summary>
    public required string MezType { get; init; }

    /// <summary>Gets the eEffectType name an Enhancement effect modifies.</summary>
    public required string EtModifies { get; init; }

    /// <summary>Gets the scale.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double Scale { get; init; }

    /// <summary>Gets the stored magnitude.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double NMagnitude { get; init; }

    /// <summary>Gets the stored duration.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double NDuration { get; init; }

    /// <summary>Gets the eAttribType name.</summary>
    public required string AttribType { get; init; }

    /// <summary>Gets the eAspect name.</summary>
    public required string Aspect { get; init; }

    /// <summary>Gets the AttribMod table name.</summary>
    public required string ModifierTable { get; init; }

    /// <summary>Gets the AttribMod table index.</summary>
    public required int NModifierTable { get; init; }

    /// <summary>Gets the eToWho name.</summary>
    public required string ToWho { get; init; }

    /// <summary>Gets the ePvX name.</summary>
    public required string PvMode { get; init; }

    /// <summary>Gets the eStacking name.</summary>
    public required string Stacking { get; init; }

    /// <summary>Gets the eSuppress flags.</summary>
    public required int Suppression { get; init; }

    /// <summary>Gets a value indicating whether the effect is buffable.</summary>
    public required bool Buffable { get; init; }

    /// <summary>Gets a value indicating whether the effect is resistible.</summary>
    public required bool Resistible { get; init; }

    /// <summary>Gets a value indicating whether the effect ignores ED.</summary>
    public required bool IgnoreEd { get; init; }

    /// <summary>Gets a value indicating whether the effect ignores variable scaling.</summary>
    public required bool IgnoreScaling { get; init; }

    /// <summary>Gets a value indicating whether variable scaling applies.</summary>
    public required bool VariableModified { get; init; }

    /// <summary>Gets the base probability.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BaseProbability { get; init; }

    /// <summary>Gets the probability.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double Probability { get; init; }

    /// <summary>Gets the procs per minute.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double ProcsPerMinute { get; init; }

    /// <summary>Gets the tick count.</summary>
    public required int Ticks { get; init; }

    /// <summary>Gets the delay before the effect applies.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double DelayedTime { get; init; }

    /// <summary>Gets the eEffectClass name.</summary>
    public required string EffectClass { get; init; }

    /// <summary>Gets the eSpecialCase name.</summary>
    public required string SpecialCase { get; init; }

    /// <summary>Gets the class index the effect names.</summary>
    public required int NIdClassName { get; init; }

    /// <summary>Gets a value indicating whether the effect was absorbed from another power.</summary>
    [JsonPropertyName("Absorbed_Effect")]
    public required bool AbsorbedEffect { get; init; }

    /// <summary>Gets the absorbed power's ePowerType name.</summary>
    [JsonPropertyName("Absorbed_PowerType")]
    public required string AbsorbedPowerType { get; init; }

    /// <summary>Gets the absorbed effect's class index, or -1.</summary>
    [JsonPropertyName("Absorbed_Class_nID")]
    public required int AbsorbedClassNId { get; init; }

    /// <summary>Gets the number of active conditionals.</summary>
    public required int ActiveConditionalsCount { get; init; }

    /// <summary>Gets the magnitude expression, unparsed.</summary>
    public required string ExprMagnitude { get; init; }

    /// <summary>Gets the duration expression, unparsed.</summary>
    public required string ExprDuration { get; init; }

    /// <summary>Gets the probability expression, unparsed.</summary>
    public required string ExprProbability { get; init; }

    /// <summary>Gets a value indicating whether the magnitude displays as a percentage.</summary>
    public required bool DisplayPercentage { get; init; }

    // The optional properties are settable, not init: the JSON source generator writes default(T) into an absent
    // init-only property, which would lose the initializer value the Python loader defaults to.
    /// <summary>Gets a value indicating whether <c>GBPA_AddEnhFX</c> added the effect; false for a raw database effect.</summary>
    public bool IsEnhancementEffect { get; set; }

    /// <summary>Gets the eBuffMode name: Debuff only on the effects of a set-bonus power named for Slow.</summary>
    [JsonIgnore]
    public string BuffMode { get; init; } = "Normal";

    /// <summary>Gets the summoned entity's UID (assembly records only).</summary>
    public string Summon { get; set; } = string.Empty;

    /// <summary>Gets the summoned entity's index (assembly records only).</summary>
    public int NSummon { get; set; } = -1;

    /// <summary>Gets the effect duration (assembly records only).</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public double Duration { get; set; }

    /// <summary>Gets the effect id (assembly records only).</summary>
    public string EffectId { get; set; } = string.Empty;

    /// <summary>Gets the reward name (assembly records only).</summary>
    public string Reward { get; set; } = string.Empty;

    /// <summary>Gets the active conditionals as key/value pairs (assembly records only).</summary>
    [JsonPropertyName("ActiveConditionals")]
    public IReadOnlyList<KeyValuePair<string, string>> Conditionals { get; set; } = [];

    /// <summary>Gets the absorbed power's index, or -1 (assembly records only).</summary>
    [JsonPropertyName("Absorbed_Power_nID")]
    public int AbsorbedPowerNid { get; set; } = -1;
}
