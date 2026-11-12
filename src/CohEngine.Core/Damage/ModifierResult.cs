using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Damage;

/// <summary>One modifier: its multiplier at full uptime, its uptime, and the assumption behind it.</summary>
public sealed record ModifierResult
{
    /// <summary>Gets the name.</summary>
    public required string Modifier { get; init; }

    /// <summary>Gets the multiplier at full uptime.</summary>
    public required double Multiplier { get; init; }

    /// <summary>Gets the uptime.</summary>
    public double Uptime { get; init; } = 1.0;

    /// <summary>Gets the sources it scales, or null for all.</summary>
    public IReadOnlySet<string>? AppliesTo { get; init; }

    /// <summary>Gets the assumption text.</summary>
    public string Assumption { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether this is a damage buff that joins the strength already applied.</summary>
    public bool Additive { get; init; }

    /// <summary>The uptime-weighted factor; an additive buff is divided by the baseline.</summary>
    /// <param name="baseline">1 plus the strength already applied.</param>
    /// <returns>The factor.</returns>
    public double Scale(double baseline = 1.0)
    {
        double bonus = F32.Round((Multiplier - 1.0) * Uptime);
        return Additive ? F32.Round(1.0 + (bonus / baseline)) : F32.Round(1.0 + bonus);
    }
}
