using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Registry;

/// <summary>The dumps every build shares, loaded once and only read afterwards.</summary>
/// <param name="Data">The database totals are computed against.</param>
/// <param name="Tables">The Maths.mhd tables.</param>
/// <param name="EnhDb">The enhancement records.</param>
/// <param name="SetDb">The set-bonus database.</param>
/// <param name="AssemblyPowers">The database-wide assembly powers, read on first use.</param>
/// <param name="AssemblyEntities">The summoned entities, read on first use.</param>
internal sealed record SharedDumps(
    Pipeline.Database Data,
    MathTables Tables,
    IReadOnlyDictionary<int, EnhancementRecord> EnhDb,
    SetBonusDb SetDb,
    Lazy<IReadOnlyDictionary<int, AssemblyPower>> AssemblyPowers,
    Lazy<AssemblyEntities> AssemblyEntities);
