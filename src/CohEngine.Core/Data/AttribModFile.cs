namespace CohEngine.Core.Data;

/// <summary>The <c>AttribMod.json</c> root.</summary>
public sealed record AttribModFile
{
    /// <summary>Gets the tables.</summary>
    public required IReadOnlyList<ModifierTable> Modifier { get; init; }
}
