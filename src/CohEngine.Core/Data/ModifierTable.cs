namespace CohEngine.Core.Data;

/// <summary>One named AttribMod table: an id and a <c>[level][classColumn]</c> grid of single-precision cells.</summary>
public sealed record ModifierTable
{
    /// <summary>Gets the table name.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the base index.</summary>
    public required int BaseIndex { get; init; }

    /// <summary>Gets the grid, quantized on load.</summary>
    public required IReadOnlyList<IReadOnlyList<double>> Table { get; init; }
}
