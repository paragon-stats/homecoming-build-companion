using System.Globalization;

using CohEngine.Core.Data;

namespace CohEngine.Core.Statistics;

/// <summary>Damage per second for a power: deferred until the critical and conditional machinery is ported.</summary>
public static class PowerDps
{
    /// <summary>Fails loud with <c>E15</c> rather than return a damage sum that overcounts.</summary>
    /// <param name="power">The power.</param>
    /// <param name="slots">Its slots.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">E15 always.</exception>
    public static double Compute(Power power, IReadOnlyList<SlotRef> slots)
    {
        ArgumentNullException.ThrowIfNull(power);
        ArgumentNullException.ThrowIfNull(slots);
        throw new NotSupportedException(
            $"E15: DPS for {power.FullName} requires the FXGetDamageValue critical/conditional port "
            + "(deferred; a plain buffed-damage sum overcounts). Slots supplied: "
            + slots.Count(slot => slot.Enh >= 0).ToString(CultureInfo.InvariantCulture));
    }
}
