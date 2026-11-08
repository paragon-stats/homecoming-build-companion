using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.ScoringMetrics;

/// <summary>A profile score: the fraction of targets met, the per-metric verdicts and the diagnostics.</summary>
/// <param name="Score">The fraction of targets met.</param>
/// <param name="Met">Whether each metric's target was met; two targets on one metric collide and the last wins.</param>
/// <param name="Diagnostics">The unmet-goal, cap and endurance warnings.</param>
internal sealed record ScoreResult(double Score, IReadOnlyDictionary<string, bool> Met, IReadOnlyList<Diagnostic> Diagnostics);
