using CohEngine.Core.Damage;
using CohEngine.Core.Maths;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Chain;

/// <summary>The planner's result: every usable option ranked, the headline picks, and what was left out.</summary>
/// <param name="Stance">The stance.</param>
/// <param name="BestAffordable">The best affordable option.</param>
/// <param name="BestSustainable">The best sustainable option.</param>
/// <param name="BestOverall">The best option ignoring endurance.</param>
/// <param name="Ranked">All options, ranked.</param>
/// <param name="Dropped">Attacks excluded by the attack cap.</param>
public sealed record ChainPlan(
    Stance Stance,
    ChainOption? BestAffordable,
    ChainOption? BestSustainable,
    ChainOption? BestOverall,
    IReadOnlyList<ChainOption> Ranked,
    IReadOnlyList<string> Dropped);
