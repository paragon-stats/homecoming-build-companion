using CohEngine.Core.Data;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Registry;

/// <summary>Looks a set's bonuses up with every magnitude already resolved, so no caller derives a scale.</summary>
public static class SetBonusLookup
{
    private static readonly string[] PvModes = ["Any", "PvE", "PvP"];

    /// <summary>Finds a set by its UID, ignoring case.</summary>
    /// <param name="setName">The set UID.</param>
    /// <param name="buildDirectory">A build dump directory; its class picks the modifier column.</param>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <param name="attribModPath"><c>AttribMod.json</c>; by default <c>attribmod/AttribMod.json</c> in the shared directory's parent.</param>
    /// <returns>The set's bonuses, or null when the loaded data holds no such set (E166).</returns>
    public static SetBonusView? Find(string setName, string buildDirectory, string midsDirectory, string? attribModPath = null)
    {
        ArgumentNullException.ThrowIfNull(setName);
        ArgumentNullException.ThrowIfNull(buildDirectory);
        ArgumentNullException.ThrowIfNull(midsDirectory);
        SetBonusDb setDb = SetBonusData.LoadSetBonusDb(Path.Join(midsDirectory, "enhancement_sets.json"), Path.Join(midsDirectory, "set_bonus_powers.json"));
        EnhancementSetDef? def = setDb.Sets.Values.FirstOrDefault(candidate => string.Equals(candidate.Uid, setName, StringComparison.OrdinalIgnoreCase));
        if (def is null)
        {
            return null;
        }

        Pipeline.Database data = DumpScoring.LoadDatabase(midsDirectory, attribModPath);
        int archetypeIndex = data.Classes.NidFromUidClass(TotalsData.LoadTotals(Path.Join(buildDirectory, "totals.json")).Class);
        return new SetBonusView(
            def.Uid,
            def.SetTypeName,
            [.. def.Bonus.Select(item => Tier(item, setDb, data, archetypeIndex))],
            [.. def.SpecialBonus.Select(item => Tier(item with { Slotted = 0 }, setDb, data, archetypeIndex))]);
    }

    private static BonusTierView Tier(BonusItem item, SetBonusDb setDb, Pipeline.Database data, int archetypeIndex)
    {
        return new BonusTierView(
            item.Slotted,
            PvModes[item.PvMode],
            [.. item.Index.Where(setDb.Powers.ContainsKey).Select(powerId => setDb.Powers[powerId]).SelectMany(power => power.Effects.Select(effect => Resolve(power, effect, data, archetypeIndex)))]);
    }

    private static BonusEffectView Resolve(SetBonusPower bonus, Effect effect, Pipeline.Database data, int archetypeIndex)
    {
        Power carrier = new()
        {
            BuildIndex = -1,
            NidPower = bonus.PowerId,
            FullName = bonus.FullName,
            StaticIndex = -1,
            PowerType = bonus.PowerType,
            ForcedClass = string.Empty,
            ClickBuff = false,
            Level = 0,
            EndCost = 0.0,
            ActivatePeriod = 0.0,
            ToggleCost = 0.0,
            VariableEnabled = false,
            StatInclude = true,
            VariableValue = 0,
            Effects = bonus.Effects,
            SetTypes = [],
            Enhancements = [],
            PickLevel = 0,
            RechargeTime = 0.0,
            CastTime = 0.0,
            InterruptTime = 0.0,
            IgnoreEnh = [],
        };
        double magnitude = EffectMagnitude.Mag(effect, carrier, data.Mods, data.Classes, archetypeIndex);
        string aspect = string.Equals(effect.DamageType, "None", StringComparison.Ordinal) ? effect.MezType : effect.DamageType;
        return new BonusEffectView(bonus.FullName, effect.EffectType, aspect, magnitude, effect.DisplayPercentage ? Math.Round(magnitude * 100.0, 4, MidpointRounding.ToEven) : null);
    }
}
