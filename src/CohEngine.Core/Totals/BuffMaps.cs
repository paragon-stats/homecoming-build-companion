using CohEngine.Core.Data;

namespace CohEngine.Core.Totals;

/// <summary>The name-to-ordinal maps the bucket routing indexes by.</summary>
/// <param name="Stat">eStatType.</param>
/// <param name="EType">eEffectType.</param>
/// <param name="EMez">eMez.</param>
/// <param name="EDamage">eDamage.</param>
internal sealed record BuffMaps(IReadOnlyDictionary<string, int> Stat, IReadOnlyDictionary<string, int> EType, IReadOnlyDictionary<string, int> EMez, IReadOnlyDictionary<string, int> EDamage)
{
    /// <summary>The four maps from the enum dump.</summary>
    /// <param name="enums">The enum maps.</param>
    /// <returns>The maps.</returns>
    public static BuffMaps From(EnumMaps enums)
    {
        return new BuffMaps(enums.Maps["eStatType"], enums.Maps["eEffectType"], enums.Maps["eMez"], enums.Maps["eDamage"]);
    }

    /// <summary>
    /// The Enhancement-effect boosts both passes share: Boosts, Boosts_Mez, Range and Heal. Only the buff pass takes
    /// the Range redirect.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <param name="value">Its magnitude.</param>
    /// <param name="buffs">The accumulator.</param>
    /// <param name="includeRange">Whether the Range redirect applies.</param>
    public void ApplyEnhancementBoosts(Effect effect, double value, BuffsX buffs, bool includeRange = true)
    {
        if (!string.Equals(effect.EffectType, "Enhancement", StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(effect.EtModifies, "Mez", StringComparison.Ordinal) && !string.Equals(effect.MezType, "None", StringComparison.Ordinal))
        {
            BuffsX.Add(buffs.BoostsMez, EMez[effect.MezType], value);
        }
        else if (effect.EtModifies is not ("None" or "Null" or "NullBool" or "Mez"))
        {
            BuffsX.Add(buffs.Boosts, EType[effect.EtModifies], value);
        }

        if (includeRange && string.Equals(effect.EtModifies, "Range", StringComparison.Ordinal))
        {
            BuffsX.Add(buffs.Effect, EType["Range"], value);
        }

        if (string.Equals(effect.EtModifies, "Heal", StringComparison.Ordinal))
        {
            BuffsX.Add(buffs.Effect, EType["Heal"], value);
        }
    }
}
