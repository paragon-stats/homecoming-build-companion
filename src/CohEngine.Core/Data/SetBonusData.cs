namespace CohEngine.Core.Data;

/// <summary>Loaders for <c>enhancement_sets.json</c>, <c>set_bonus_powers.json</c> and a build's virtual power.</summary>
public static class SetBonusData
{
    /// <summary>Parses <c>enhancement_sets.json</c>, keyed by set index.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The sets by index.</returns>
    public static IReadOnlyDictionary<int, EnhancementSetDef> LoadEnhancementSets(string path)
    {
        return Json.Load(path, SetBonusJsonContext.Default.IReadOnlyListEnhancementSetDef).ToDictionary(set => set.Nid);
    }

    /// <summary>Parses <c>set_bonus_powers.json</c>, keyed by global power id.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The powers by id.</returns>
    public static IReadOnlyDictionary<int, SetBonusPower> LoadSetBonusPowers(string path)
    {
        return Json.Load(path, SetBonusJsonContext.Default.IReadOnlyListSetBonusPower).ToDictionary(
            power => power.PowerId,
            power =>
            {
                // Mids marks every effect of a set-bonus power named for Slow as a debuff.
                string mode = power.FullName[(power.FullName.LastIndexOf('.') + 1)..].Contains("Slow", StringComparison.Ordinal) ? "Debuff" : "Normal";
                return power with { Effects = [.. power.Effects.Select(effect => effect with { BuffMode = mode })] };
            });
    }

    /// <summary>Loads the set definitions and the referenced bonus powers together.</summary>
    /// <param name="setsPath">The <c>enhancement_sets.json</c> file.</param>
    /// <param name="powersPath">The <c>set_bonus_powers.json</c> file.</param>
    /// <returns>The set-bonus database.</returns>
    public static SetBonusDb LoadSetBonusDb(string setsPath, string powersPath)
    {
        return new SetBonusDb(LoadEnhancementSets(setsPath), LoadSetBonusPowers(powersPath));
    }

    /// <summary>Parses a build's <c>set_bonus_virtual_power.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>Mids' virtual power.</returns>
    public static SetBonusVirtualPower LoadSetBonusVirtualPower(string path)
    {
        return Json.Load(path, SetBonusJsonContext.Default.SetBonusVirtualPower);
    }
}
