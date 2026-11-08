namespace CohEngine.Core.Data;

/// <summary>A database enhancement's value-relevant fields (the Python port's <c>enhancement.EnhancementRecord</c>).</summary>
public sealed record EnhancementRecord
{
    /// <summary>Gets the enhancement's database index.</summary>
    public required int Nid { get; init; }

    /// <summary>Gets the enhancement's static index.</summary>
    public required int StaticIndex { get; init; }

    /// <summary>Gets the eType name: None, Normal, InventO, SpecialO or SetO.</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets a value indicating whether the enhancement is Superior.</summary>
    public required bool Superior { get; init; }

    /// <summary>Gets the Enhancement-mode effects; the harness drops FX and proc effects.</summary>
    public required IReadOnlyList<EnhEffect> Effects { get; init; }

    /// <summary>Gets the set this piece belongs to, or -1.</summary>
    public required int NidSet { get; init; }

    /// <summary>Gets a value indicating whether the enhancement is a proc.</summary>
    public required bool IsProc { get; init; }

    /// <summary>Gets the index of the enhancement's own power, or -1.</summary>
    public required int PowerNid { get; init; }
}
