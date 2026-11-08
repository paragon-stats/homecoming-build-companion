using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>
/// Mirror of <c>Character.TotalStatistics</c> (the Python port's <c>base_totals.TotalStatistics</c>).
/// Every field is quantized on load, as the Python <c>load_mids_totals</c> does.
/// </summary>
public sealed record TotalStatistics
{
    /// <summary>Gets defense per eDamage.</summary>
    [JsonConverter(typeof(F32ListJsonConverter))]
    public required IReadOnlyList<double> Def { get; init; }

    /// <summary>Gets resistance per eDamage.</summary>
    [JsonConverter(typeof(F32ListJsonConverter))]
    public required IReadOnlyList<double> Res { get; init; }

    /// <summary>Gets mez protection per eMez.</summary>
    [JsonConverter(typeof(F32ListJsonConverter))]
    public required IReadOnlyList<double> Mez { get; init; }

    /// <summary>Gets mez resistance per eMez.</summary>
    [JsonConverter(typeof(F32ListJsonConverter))]
    public required IReadOnlyList<double> MezRes { get; init; }

    /// <summary>Gets debuff resistance per eEffectType.</summary>
    [JsonConverter(typeof(F32ListJsonConverter))]
    public required IReadOnlyList<double> DebuffRes { get; init; }

    /// <summary>Gets elusivity per eDamage.</summary>
    [JsonConverter(typeof(F32ListJsonConverter))]
    public required IReadOnlyList<double> Elusivity { get; init; }

    /// <summary>Gets HP regeneration.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double HpRegen { get; init; }

    /// <summary>Gets max HP.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double HpMax { get; init; }

    /// <summary>Gets absorb.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double Absorb { get; init; }

    /// <summary>Gets endurance recovery.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double EndRec { get; init; }

    /// <summary>Gets endurance use.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double EndUse { get; init; }

    /// <summary>Gets max endurance.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double EndMax { get; init; }

    /// <summary>Gets run speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double RunSpd { get; init; }

    /// <summary>Gets max run speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxRunSpd { get; init; }

    /// <summary>Gets jump speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double JumpSpd { get; init; }

    /// <summary>Gets max jump speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxJumpSpd { get; init; }

    /// <summary>Gets fly speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double FlySpd { get; init; }

    /// <summary>Gets max fly speed.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double MaxFlySpd { get; init; }

    /// <summary>Gets jump height.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double JumpHeight { get; init; }

    /// <summary>Gets PvE stealth.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double StealthPve { get; init; }

    /// <summary>Gets PvP stealth.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double StealthPvp { get; init; }

    /// <summary>Gets threat level.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double ThreatLevel { get; init; }

    /// <summary>Gets perception.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double Perception { get; init; }

    /// <summary>Gets the recharge buff.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BuffHaste { get; init; }

    /// <summary>Gets the accuracy buff.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BuffAcc { get; init; }

    /// <summary>Gets the to-hit buff.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BuffToHit { get; init; }

    /// <summary>Gets the damage buff.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BuffDam { get; init; }

    /// <summary>Gets the endurance reduction buff.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BuffEndRdx { get; init; }

    /// <summary>Gets the range buff.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BuffRange { get; init; }

    /// <summary>Gets the heal buff.</summary>
    [JsonConverter(typeof(F32JsonConverter))]
    public required double BuffHeal { get; init; }
}
