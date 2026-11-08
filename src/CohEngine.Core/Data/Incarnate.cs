namespace CohEngine.Core.Data;

/// <summary>One StatInclude <c>Incarnate.*</c> build power (the Python port's <c>incarnate.Incarnate</c>).</summary>
public sealed record Incarnate
{
    /// <summary>Gets the power's index in the build.</summary>
    public required int BuildIndex { get; init; }

    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the granted sub-powers.</summary>
    public required IReadOnlyList<IncarnateSubPower> SubPowers { get; init; }
}
