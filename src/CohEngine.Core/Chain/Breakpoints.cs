using System.Globalization;
using System.Text.Json;

using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Pets;
using CohEngine.Core.Statistics;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Chain;

/// <summary>The universal breakpoints the chain planner reads from <c>strategy/breakpoints.json</c>.</summary>
/// <param name="SoftCapDefensePve">The PvE defense soft cap, in percent.</param>
/// <param name="RangedStandoffFt">The default ranged standoff.</param>
/// <param name="MeleeRangeFtMax">The melee range threshold.</param>
/// <param name="ChainMinCycles">The cycles a chain must last to be affordable.</param>
public sealed record Breakpoints(double SoftCapDefensePve, double RangedStandoffFt, double MeleeRangeFtMax, double ChainMinCycles);
