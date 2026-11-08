using System.Globalization;
using System.Text.Json;

using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Pets;
using CohEngine.Core.Statistics;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Chain;

/// <summary>One stance's evaluated model, its rotation and its diagnostics.</summary>
/// <param name="Stance">The stance.</param>
/// <param name="Report">The damage report.</param>
/// <param name="Chain">The chosen rotation.</param>
/// <param name="Diagnostics">The diagnostics.</param>
/// <param name="Form">The form.</param>
/// <param name="Plan">The plan.</param>
public sealed record StanceReport(Stance Stance, DamageReport Report, ChainOption? Chain, IReadOnlyList<ChainDiagnostic> Diagnostics, string Form = "", ChainPlan? Plan = null);
