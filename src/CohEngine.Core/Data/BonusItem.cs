namespace CohEngine.Core.Data;

/// <summary>One tier or special bonus (the Python port's <c>set_bonuses.BonusItem</c>).</summary>
public sealed record BonusItem
{
    /// <summary>Gets the 1-based piece threshold; the special bonuses ignore it.</summary>
    public required int Slotted { get; init; }

    /// <summary>Gets the ePvX ordinal gating the tier.</summary>
    public required int PvMode { get; init; }

    /// <summary>Gets the global set-bonus power ids the bonus grants.</summary>
    public required IReadOnlyList<int> Index { get; init; }
}
