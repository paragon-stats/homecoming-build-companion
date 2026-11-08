namespace CohEngine.Core.Data;

/// <summary>An inherent power Mids adds to a build of the class without a pick.</summary>
public sealed record InherentPower
{
    /// <summary>Gets the power's full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the power's database minimum level.</summary>
    public required int Level { get; init; }
}
