using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry.HardLimitRules;

namespace CohEngine.Core.Registry;

/// <summary>Runs the hard-limit registry: builds the context once and folds every registered rule over it.</summary>
public static class HardLimitRunner
{
    /// <summary>Every hard-limit violation of a build; an empty list means the build is legal.</summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="slots">The build's slots by build index.</param>
    /// <param name="schedule">The level schedule.</param>
    /// <param name="maxSlots">The added-slot cap.</param>
    /// <param name="format">The <c>.mbd</c> file facts that arm the <c>H-FMT</c> rules; null leaves them silent.</param>
    /// <returns>The diagnostics in rule order.</returns>
    public static IReadOnlyList<Diagnostic> Check(
        IReadOnlyList<Power> powers,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        LevelSchedule schedule,
        int maxSlots,
        MbdFormat? format = null)
    {
        ArgumentNullException.ThrowIfNull(powers);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(schedule);
        HardLimitsContext context = BuildContext(powers, slots, schedule, maxSlots, format);
        return [.. RuleRegistry.HardLimitRules.SelectMany(rule => rule.Run(context))];
    }

    /// <summary>
    /// Assembles the shared context. Picks exclude anything the player spent no pick on: unchosen powers, and the
    /// Inherent, Incarnate and Temporary_Powers groups by name, so dumps predating the Chosen field classify right.
    /// </summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="slots">The build's slots by build index.</param>
    /// <param name="schedule">The level schedule.</param>
    /// <param name="maxSlots">The added-slot cap.</param>
    /// <param name="format">The <c>.mbd</c> file facts, or null.</param>
    /// <returns>The context.</returns>
    internal static HardLimitsContext BuildContext(
        IReadOnlyList<Power> powers,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        LevelSchedule schedule,
        int maxSlots,
        MbdFormat? format)
    {
        Power[] picks = [.. powers.Where(power => power.BuildIndex >= 0 && power.Chosen && !PowerGroups.IsGranted(power))];
        Dictionary<int, int> addedByPower = [];
        Dictionary<int, int> addedByLevel = [];
        List<(Power Power, SlotRef Slot)> addedPairs = [];
        foreach (Power power in powers)
        {
            // A power's first slot is its free default slot and an inherent slot is the (disabled) inherent-slotting grant; neither draws from the budget.
            SlotRef[] added = [.. slots.GetValueOrDefault(power.BuildIndex, []).Skip(1).Where(slot => !slot.IsInherent)];
            if (added.Length > 0)
            {
                addedByPower[power.BuildIndex] = added.Length;
            }

            foreach (SlotRef slot in added)
            {
                addedByLevel[slot.Level] = addedByLevel.GetValueOrDefault(slot.Level) + 1;
                addedPairs.Add((power, slot));
            }
        }

        return new HardLimitsContext(powers, slots, schedule, maxSlots, picks, addedByPower, addedByLevel, addedPairs, format);
    }
}
