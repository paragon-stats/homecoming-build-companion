using CohEngine.Core.Data;

namespace CohEngine.Core.Exemplar;

/// <summary>
/// Exemplar availability (the +5 rule) and the ForceLevel-gated recompute. Availability is a build-authoring predicate
/// that does not enter the totals; the recompute lowers <c>ForceLevel</c>, which gates set bonuses on the power's pick
/// level and slot values on the slot level with no -3 retention, as Mids does.
/// </summary>
public static class ExemplarWindow
{
    /// <summary>Gets how far above the exemplar level a power or slot stays available.</summary>
    public const int AvailabilityOffset = 5;

    /// <summary>Whether a power picked at the level is available when exemplared to the other level.</summary>
    /// <param name="pickLevel">The pick level.</param>
    /// <param name="exemplarLevel">The exemplar level.</param>
    /// <returns>True when the pick level is within the +5 window.</returns>
    public static bool IsPowerAvailable(int pickLevel, int exemplarLevel)
    {
        return pickLevel <= exemplarLevel + AvailabilityOffset;
    }

    /// <summary>Whether a slot placed at the level is available when exemplared to the other level.</summary>
    /// <param name="slotLevel">The slot level.</param>
    /// <param name="exemplarLevel">The exemplar level.</param>
    /// <returns>True when the slot level is within the +5 window.</returns>
    public static bool IsSlotAvailable(int slotLevel, int exemplarLevel)
    {
        return slotLevel <= exemplarLevel + AvailabilityOffset;
    }

    /// <summary>The powers available at the exemplar level, by the build's pick level and not the database minimum.</summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="exemplarLevel">The exemplar level.</param>
    /// <returns>The available powers in order.</returns>
    public static IReadOnlyList<Power> AvailablePowers(IReadOnlyList<Power> powers, int exemplarLevel)
    {
        ArgumentNullException.ThrowIfNull(powers);
        return [.. powers.Where(power => IsPowerAvailable(power.PickLevel, exemplarLevel))];
    }

    /// <summary>A copy of the configuration with only <c>ForceLevel</c> replaced, for the exemplar recompute.</summary>
    /// <param name="config">The configuration.</param>
    /// <param name="forceLevel">The exemplar level.</param>
    /// <returns>The copy.</returns>
    public static EngineConfig WithForceLevel(EngineConfig config, int forceLevel)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config with { ForceLevel = forceLevel };
    }
}
