using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Enhancement;

/// <summary>What slot values are read against: the enhancement records, the exemplar cutoff and the Maths.mhd tables.</summary>
/// <param name="EnhDb">The enhancement records.</param>
/// <param name="ForceLevel">The exemplar cutoff; a slot placed at or above it does not count.</param>
/// <param name="Tables">The Maths.mhd tables.</param>
internal sealed record EnhancementTables(IReadOnlyDictionary<int, EnhancementRecord> EnhDb, int ForceLevel, MathTables Tables);
