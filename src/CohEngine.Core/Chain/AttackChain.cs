using CohEngine.Core.Damage;
using CohEngine.Core.Maths;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Chain;

/// <summary>A build's attack rotation and the endurance it costs per second.</summary>
/// <param name="Rotation">The attack full names, longest recharge first.</param>
/// <param name="CastSum">The animation-locked cycle floor.</param>
/// <param name="CycleTime">The real cycle.</param>
/// <param name="Gap">Idle time per cycle.</param>
/// <param name="GapFree">Whether the gap is zero.</param>
/// <param name="EndPerSec">The chain's endurance drain.</param>
public sealed record AttackChain(IReadOnlyList<string> Rotation, double CastSum, double CycleTime, double Gap, bool GapFree, double EndPerSec);
