using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Statistics;

/// <summary>Max HP (AT-capped) and the HP regenerated per second, which scales with it.</summary>
/// <param name="MaxHp">The max HP.</param>
/// <param name="RegenPerSec">The regeneration per second.</param>
public sealed record HealthBudget(double MaxHp, double RegenPerSec);
