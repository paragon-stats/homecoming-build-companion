namespace CohEngine.Core.Data;

/// <summary>One power of <c>enhanced_powers.json</c>: Mids' buffed scalars and effect magnitudes.</summary>
public sealed record EnhancedPower
{
    /// <summary>Gets the power's index in the build.</summary>
    public required int BuildIndex { get; init; }

    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the buffed accuracy.</summary>
    public required double Accuracy { get; init; }

    /// <summary>Gets the accuracy multiplier.</summary>
    public required double AccuracyMult { get; init; }

    /// <summary>Gets the buffed end cost.</summary>
    public required double EndCost { get; init; }

    /// <summary>Gets the buffed interrupt time.</summary>
    public required double InterruptTime { get; init; }

    /// <summary>Gets the buffed range.</summary>
    public required double Range { get; init; }

    /// <summary>Gets the buffed recharge time.</summary>
    public required double RechargeTime { get; init; }

    /// <summary>Gets the cast time.</summary>
    public required double CastTime { get; init; }

    /// <summary>Gets the damage per activation.</summary>
    public required double DamagePerActivation { get; init; }

    /// <summary>Gets the unenhanced scalars.</summary>
    public required PowerScalars Base { get; init; }

    /// <summary>Gets the enhanced effects.</summary>
    public required IReadOnlyList<EnhancedEffect> Effects { get; init; }
}
