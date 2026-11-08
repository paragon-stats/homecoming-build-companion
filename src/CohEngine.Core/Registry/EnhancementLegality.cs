using CohEngine.Core.Data;

namespace CohEngine.Core.Registry;

/// <summary>The legality-relevant fields of one enhancement (from <c>enhancements.json</c>).</summary>
public sealed record EnhancementLegality
{
    /// <summary>Gets the enhancement's database index.</summary>
    public required int Nid { get; init; }

    /// <summary>Gets the enhancement UID.</summary>
    public required string Uid { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string LongName { get; init; }

    /// <summary>Gets the eType name: None, Normal, InventO, SpecialO or SetO.</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets the enhancement class ids, tested against the power's accepted classes.</summary>
    public required IReadOnlyList<int> ClassIds { get; init; }

    /// <summary>Gets the owning set, or -1 for a non-set IO.</summary>
    public required int NidSet { get; init; }

    /// <summary>Gets a value indicating whether the IO may be slotted once per build.</summary>
    public required bool Unique { get; init; }

    /// <summary>Gets the eEnhMutex name: None, Stealth or ArchetypeA to ArchetypeF.</summary>
    public required string MutexName { get; init; }

    /// <summary>Gets a value indicating whether the IO carries the Superior value flag.</summary>
    public required bool Superior { get; init; }

    /// <summary>Parses an <c>enhancements.json</c> dump, keyed by index; a repeated index keeps the last record.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The records by index.</returns>
    public static IReadOnlyDictionary<int, EnhancementLegality> Load(string path)
    {
        return Json.Load(path, EnhancementLegalityJsonContext.Default.IReadOnlyListEnhancementLegality).ToLastWinsDictionary(record => record.Nid);
    }
}
