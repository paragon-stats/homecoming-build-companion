using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>The evaluated model.</summary>
/// <param name="Sources">Every source result.</param>
/// <param name="Modifiers">Every modifier result.</param>
/// <param name="ScaledDps">The scaled DPS per source, in source order.</param>
/// <param name="TotalDps">The scaled total.</param>
/// <param name="TotalDrainPerSec">The total drain.</param>
/// <param name="TimeToEmpty">Seconds to empty.</param>
/// <param name="RemainderContext">The context the remainder-filling sources ran on.</param>
public sealed record DamageReport(
    IReadOnlyList<SourceResult> Sources,
    IReadOnlyList<ModifierResult> Modifiers,
    IReadOnlyList<KeyValuePair<string, double>> ScaledDps,
    double TotalDps,
    double TotalDrainPerSec,
    double TimeToEmpty,
    DamageContext RemainderContext);
