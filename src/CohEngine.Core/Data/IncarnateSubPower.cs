namespace CohEngine.Core.Data;

/// <summary>One granted sub-power carrying an incarnate aspect (the Python port's <c>incarnate.IncarnateSubPower</c>).</summary>
public sealed record IncarnateSubPower
{
    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the EnhancementClass ids the sub-power's accept gate lists.</summary>
    public required ISet<int> Enhancements { get; init; }

    /// <summary>Gets the effects.</summary>
    public required IReadOnlyList<Effect> Effects { get; init; }
}
