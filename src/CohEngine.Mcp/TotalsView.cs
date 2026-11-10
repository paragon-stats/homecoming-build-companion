namespace CohEngine.Mcp;

/// <summary>One set of totals, with the per-type vectors named.</summary>
/// <param name="DefensePercent">Defense per damage type, as a percentage.</param>
/// <param name="ResistancePercent">Resistance per damage type, as a percentage.</param>
/// <param name="Mez">Mez protection per mez type.</param>
/// <param name="MezResist">Mez resistance per mez type.</param>
/// <param name="DebuffResist">Debuff resistance per effect type.</param>
/// <param name="Elusivity">Elusivity per damage type.</param>
/// <param name="Scalars">The single-value totals (recovery, recharge buff, HP and the rest), named as <c>totals.json</c> names them.</param>
public sealed record TotalsView(
    IReadOnlyDictionary<string, double> DefensePercent,
    IReadOnlyDictionary<string, double> ResistancePercent,
    IReadOnlyDictionary<string, double> Mez,
    IReadOnlyDictionary<string, double> MezResist,
    IReadOnlyDictionary<string, double> DebuffResist,
    IReadOnlyDictionary<string, double> Elusivity,
    IReadOnlyDictionary<string, double> Scalars);
