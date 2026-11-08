namespace CohEngine.Core.Data;

/// <summary>An enhancement set's bonus definition (the Python port's <c>set_bonuses.EnhancementSetDef</c>).</summary>
public sealed record EnhancementSetDef
{
    /// <summary>Gets the set index.</summary>
    public required int Nid { get; init; }

    /// <summary>Gets the set UID.</summary>
    public required string Uid { get; init; }

    /// <summary>Gets the eSetType ordinal.</summary>
    public required int SetType { get; init; }

    /// <summary>Gets the eSetType name.</summary>
    public required string SetTypeName { get; init; }

    /// <summary>Gets the member enhancement indexes; pairs one to one with the special bonuses.</summary>
    public required IReadOnlyList<int> Enhancements { get; init; }

    /// <summary>Gets the tiered count bonuses.</summary>
    public required IReadOnlyList<BonusItem> Bonus { get; init; }

    /// <summary>Gets the per-enhancement globals and uniques.</summary>
    public required IReadOnlyList<BonusItem> SpecialBonus { get; init; }
}
