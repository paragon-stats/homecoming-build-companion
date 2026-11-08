namespace CohEngine.Core.Data;

/// <summary>The unenhanced per-power scalars of <c>enhanced_powers.json</c>.</summary>
public sealed record PowerScalars
{
    /// <summary>Gets the accuracy.</summary>
    public required double Accuracy { get; init; }

    /// <summary>Gets the end cost.</summary>
    public required double EndCost { get; init; }

    /// <summary>Gets the interrupt time.</summary>
    public required double InterruptTime { get; init; }

    /// <summary>Gets the range.</summary>
    public required double Range { get; init; }

    /// <summary>Gets the recharge time.</summary>
    public required double RechargeTime { get; init; }

    /// <summary>Gets the cast time.</summary>
    public required double CastTime { get; init; }
}
