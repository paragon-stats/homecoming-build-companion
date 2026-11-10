namespace CohEngine.Core.Registry;

/// <summary>A set's bonuses.</summary>
/// <param name="Set">The set UID.</param>
/// <param name="SetType">The set type name.</param>
/// <param name="Tiers">The bonuses by piece count.</param>
/// <param name="Special">The per-piece globals and uniques.</param>
public sealed record SetBonusView(string Set, string SetType, IReadOnlyList<BonusTierView> Tiers, IReadOnlyList<BonusTierView> Special);
