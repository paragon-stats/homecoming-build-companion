using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Registry;

/// <summary>A build dump after the totals phases.</summary>
/// <param name="ClassName">The build's class name.</param>
/// <param name="Data">The database it was computed against, as dumped.</param>
/// <param name="Tables">The Maths.mhd tables.</param>
/// <param name="EnhDb">The enhancement records.</param>
/// <param name="Slots">The build's slots by build index.</param>
/// <param name="RawPowers">The powers as dumped.</param>
/// <param name="Powers">The powers after assembly, when the dump carries it.</param>
/// <param name="Result">The computed totals.</param>
internal sealed record BuiltBuild(
    string ClassName,
    Pipeline.Database Data,
    MathTables Tables,
    IReadOnlyDictionary<int, EnhancementRecord> EnhDb,
    IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> Slots,
    IReadOnlyList<Power> RawPowers,
    IReadOnlyList<Power> Powers,
    BaseTotals Result);
