using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Damage;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Pets;
using CohEngine.Core.Statistics;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Chain;

/// <summary>Everything loaded once for a build; stance-specific contexts derive from the context.</summary>
/// <param name="Context">The melee-stance context.</param>
/// <param name="Health">The health budget.</param>
/// <param name="Capped">The capped totals.</param>
/// <param name="Breakpoints">The breakpoints.</param>
/// <param name="ClicksAlwaysOn">Whether no clicks-off dump was found.</param>
/// <param name="Source">The dump the figures came from.</param>
/// <param name="Skipped">The (power, reason) of every power left out of the model.</param>
public sealed record ChainSetup(
    DamageContext Context,
    HealthBudget Health,
    TotalStatistics Capped,
    Breakpoints Breakpoints,
    bool ClicksAlwaysOn,
    string Source,
    IReadOnlyList<(string Name, string Reason)> Skipped);
