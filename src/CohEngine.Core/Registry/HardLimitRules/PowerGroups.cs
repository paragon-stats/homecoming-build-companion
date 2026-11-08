using CohEngine.Core.Data;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>The game's hard limits and the group names that classify a power as picked or granted.</summary>
internal static class PowerGroups
{
    /// <summary>The most power picks a build has (inherents excluded).</summary>
    public const int MaxPowerPicks = 24;

    /// <summary>The most added slots one power holds.</summary>
    public const int MaxAddedSlotsPerPower = 5;

    /// <summary>The most power pools a build draws from.</summary>
    public const int MaxPowerPools = 4;

    /// <summary>The group of the power pools.</summary>
    public const string Pool = "Pool";

    /// <summary>The group of the Ancillary and Patron pools.</summary>
    public const string Ancillary = "Epic";

    /// <summary>The first dotted segment of a power's full name.</summary>
    /// <param name="power">The power.</param>
    /// <returns>The group.</returns>
    public static string Group(Power power)
    {
        int dot = power.FullName.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? power.FullName : power.FullName[..dot];
    }

    /// <summary>The <c>Group.PowerSet</c> prefix of a power's full name.</summary>
    /// <param name="power">The power.</param>
    /// <returns>The powerset key.</returns>
    public static string PowersetKey(Power power)
    {
        return string.Join('.', power.FullName.Split('.').Take(2));
    }

    /// <summary>
    /// Whether a power is granted rather than picked: Inherent powers are auto-granted (though slottable), Incarnates
    /// are a separate crafting system with no slots, and accolades are earned temporary powers.
    /// </summary>
    /// <param name="power">The power.</param>
    /// <returns>True when the power never consumes one of the 24 picks.</returns>
    public static bool IsGranted(Power power)
    {
        return Group(power) is "Inherent" or "Incarnate" or "Temporary_Powers";
    }
}
