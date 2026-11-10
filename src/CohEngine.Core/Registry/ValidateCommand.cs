using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry;

/// <summary>
/// The <c>validate</c> command's work: loads a build and the shared dumps, then folds the hard-limit and legality
/// registries over it and returns their combined diagnostics.
/// </summary>
public static class ValidateCommand
{
    /// <summary>
    /// Validates a Mids dump directory. <paramref name="buildDirectory"/> holds <c>powers_effects.json</c> and
    /// <c>slots.json</c>; <paramref name="midsDirectory"/> holds the shared dumps. <c>enhancement_effects.json</c> is
    /// passed to the legality registry because P-SET-001's pure-proc exemption keys on it; without it the rule warns on
    /// every proc a build slots alone.
    /// </summary>
    /// <param name="buildDirectory">The build's dump directory.</param>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <param name="checks">Which registries to run; both by default.</param>
    /// <returns>The hard-limit diagnostics followed by the legality diagnostics.</returns>
    public static IReadOnlyList<Diagnostic> RunDump(string buildDirectory, string midsDirectory, ValidateChecks checks = ValidateChecks.All)
    {
        ArgumentNullException.ThrowIfNull(buildDirectory);
        IReadOnlyList<Power> powers = EffectData.LoadPowersEffects(Path.Combine(buildDirectory, "powers_effects.json"));
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = EnhancementData.LoadBuildSlots(Path.Combine(buildDirectory, "slots.json"));
        return Fold(powers, slots, midsDirectory, null, checks);
    }

    /// <summary>Validates a bare <c>.mbd</c> file through the source that resolves it.</summary>
    /// <param name="source">The resolver.</param>
    /// <param name="path">The <c>.mbd</c> file.</param>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <returns>The hard-limit diagnostics followed by the legality diagnostics.</returns>
    public static IReadOnlyList<Diagnostic> RunMbd(IMbdBuildSource source, string path, string midsDirectory)
    {
        ArgumentNullException.ThrowIfNull(source);
        MbdBuild build = source.Load(path, midsDirectory);
        return Fold(build.Powers, build.Slots, midsDirectory, build.Format, ValidateChecks.All);
    }

    private static IReadOnlyList<Diagnostic> Fold(IReadOnlyList<Power> powers, IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots, string midsDirectory, MbdFormat? format, ValidateChecks checks)
    {
        ArgumentNullException.ThrowIfNull(midsDirectory);
        IReadOnlyDictionary<int, EnhancementLegality> enhancements = EnhancementLegality.Load(Path.Combine(midsDirectory, "enhancements.json"));
        SetBonusDb setDb = SetBonusData.LoadSetBonusDb(Path.Combine(midsDirectory, "enhancement_sets.json"), Path.Combine(midsDirectory, "set_bonus_powers.json"));
        LevelSchedule schedule = LevelData.LoadLevelSchedule(Path.Combine(midsDirectory, "levels.json"));
        ServerData server = TotalsData.LoadServerData(Path.Combine(midsDirectory, "server_data.json"));
        IReadOnlyDictionary<int, EnhancementRecord> enhEffects = EnhancementData.LoadEnhancementEffects(Path.Combine(midsDirectory, "enhancement_effects.json"));
        return
        [
            .. checks.HasFlag(ValidateChecks.HardLimits) ? HardLimitRunner.Check(powers, slots, schedule, server.MaxSlots, format) : [],
            .. checks.HasFlag(ValidateChecks.Legality) ? LegalityRunner.Check(powers, slots, enhancements, setDb, enhEffects) : [],
        ];
    }
}
