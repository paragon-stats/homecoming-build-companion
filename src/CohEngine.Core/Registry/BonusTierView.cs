namespace CohEngine.Core.Registry;

/// <summary>The effects a set grants at one piece count.</summary>
/// <param name="Pieces">The piece threshold; 0 for a special (per-piece) bonus.</param>
/// <param name="Mode">Which game mode the bonus applies in: <c>Any</c>, <c>PvE</c> or <c>PvP</c>.</param>
/// <param name="Effects">The resolved effects.</param>
public sealed record BonusTierView(int Pieces, string Mode, IReadOnlyList<BonusEffectView> Effects);
