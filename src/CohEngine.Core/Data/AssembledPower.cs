namespace CohEngine.Core.Data;

/// <summary>One record of <c>assembled_powers.json</c>: Mids' own assembled effect list for a StatInclude build power.</summary>
public sealed record AssembledPower
{
    /// <summary>Gets the power's index in the build.</summary>
    public required int BuildIndex { get; init; }

    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the assembled effects.</summary>
    public required IReadOnlyList<Effect> Effects { get; init; }
}
