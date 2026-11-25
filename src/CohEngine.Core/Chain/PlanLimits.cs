namespace CohEngine.Core.Chain;

/// <summary>The bounds a chain plan works within.</summary>
/// <param name="TimelineAvailable">The timeline share left.</param>
/// <param name="OtherDrainPerSec">The other sources' drain.</param>
/// <param name="MinCycles">The cycles an option must last to be affordable.</param>
/// <param name="MaxAttacks">The attack cap.</param>
public sealed record PlanLimits(double TimelineAvailable = 1.0, double OtherDrainPerSec = 0.0, double MinCycles = 4.0, int MaxAttacks = 12);
