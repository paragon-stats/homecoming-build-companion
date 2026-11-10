using CohEngine.Core.Data;

namespace CohEngine.Core.BuildFile;

/// <summary>What the writer, the invariants and the resolution read: one bundle from one Mids dump.</summary>
/// <param name="Enhancements">The enhancements by UID.</param>
/// <param name="SetBounds">The IO-level bounds by set index.</param>
/// <param name="Archetypes">The class table.</param>
/// <param name="Powers">The powers by full name.</param>
public sealed record MbdDb(
    IReadOnlyDictionary<string, EnhInfo> Enhancements,
    IReadOnlyDictionary<int, (int Min, int Max)> SetBounds,
    ArchetypeDb Archetypes,
    IReadOnlyDictionary<string, PowerTemplate> Powers);
