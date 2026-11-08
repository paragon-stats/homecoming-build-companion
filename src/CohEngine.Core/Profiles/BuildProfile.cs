namespace CohEngine.Core.Profiles;

/// <summary>A resolved build-type profile: its targets and survival, damage and recharge priority.</summary>
/// <param name="Name">The profile key.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Priority">The priority order.</param>
/// <param name="Targets">The resolved targets.</param>
public sealed record BuildProfile(string Name, string DisplayName, IReadOnlyList<string> Priority, IReadOnlyList<Target> Targets);
