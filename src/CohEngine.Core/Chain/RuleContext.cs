using System.Globalization;
using System.Text.Json;

using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Pets;
using CohEngine.Core.Statistics;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Chain;

/// <summary>What a chain rule reads: one stance's evaluation and the setup it came from.</summary>
/// <param name="Setup">The setup.</param>
/// <param name="Context">The stance context.</param>
/// <param name="Report">The report.</param>
/// <param name="Chain">The chosen rotation.</param>
/// <param name="Path">The anchor path.</param>
public sealed record RuleContext(ChainSetup Setup, DamageContext Context, DamageReport Report, ChainOption? Chain, string Path);
