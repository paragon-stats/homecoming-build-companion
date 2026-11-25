using CohEngine.Core.Data;

namespace CohEngine.Core.Assembly;

/// <summary>The database records assembly reads, the same for every build.</summary>
/// <param name="EnhDb">The enhancement records.</param>
/// <param name="SetDb">The set records.</param>
/// <param name="Powers">The database powers assembly reaches.</param>
/// <param name="Entities">The summoned entities.</param>
internal sealed record AssemblyDatabase(IReadOnlyDictionary<int, EnhancementRecord> EnhDb, SetBonusDb SetDb, IReadOnlyDictionary<int, AssemblyPower> Powers, AssemblyEntities Entities);
