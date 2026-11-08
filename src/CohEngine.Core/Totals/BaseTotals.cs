using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Totals.Incarnate;

namespace CohEngine.Core.Totals;

/// <summary>The uncapped and capped totals, the global terms, and the incarnate addends the per-power layer reads.</summary>
/// <param name="Totals">The uncapped totals.</param>
/// <param name="TotalsCapped">The AT- and server-capped totals.</param>
/// <param name="GlobalEnhance">The global <c>_selfEnhance</c> scalar terms.</param>
/// <param name="IncarnateAddends">The resolved incarnate addends, or null when no incarnate was supplied.</param>
internal sealed record BaseTotals(TotalStatistics Totals, TotalStatistics TotalsCapped, GlobalEnhance GlobalEnhance, IncarnateAddends? IncarnateAddends = null);
