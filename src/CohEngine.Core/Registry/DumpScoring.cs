using CohEngine.Core.Assembly;
using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;
using CohEngine.Core.SetBonuses;
using CohEngine.Core.Totals;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Registry;

/// <summary>
/// Computes a build dump's totals and scoring metrics: the same phase order the parity tests run (assembly when the
/// dump carries it, slots, set bonuses, incarnates), then the registered metrics over the result.
/// </summary>
public static class DumpScoring
{
    /// <summary>Gets the shared-dump directory <c>validate</c> assumes for a build dump: the build directory's grandparent.</summary>
    /// <param name="buildDirectory">The build's dump directory.</param>
    /// <returns>The directory.</returns>
    public static string DefaultMidsDirectory(string buildDirectory)
    {
        ArgumentNullException.ThrowIfNull(buildDirectory);
        string full = Path.GetFullPath(buildDirectory.TrimEnd('/', '\\'));
        return Path.GetDirectoryName(Path.GetDirectoryName(full)) ?? string.Empty;
    }

    /// <summary>Computes a build dump.</summary>
    /// <param name="buildDirectory">The build's dump directory.</param>
    /// <param name="midsDirectory">The shared-dump directory; its parent holds <c>attribmod/AttribMod.json</c> and <c>Maths.mhd</c>.</param>
    /// <returns>The totals and every metric.</returns>
    public static ComputedBuild Compute(string buildDirectory, string midsDirectory)
    {
        ArgumentNullException.ThrowIfNull(buildDirectory);
        ArgumentNullException.ThrowIfNull(midsDirectory);
        Pipeline.Database data = LoadDatabase(midsDirectory);
        MathTables tables = MathTables.Load(Path.Join(SharedRoot(midsDirectory), "Maths.mhd"));
        IReadOnlyDictionary<int, EnhancementRecord> enhDb = EnhancementData.LoadEnhancementEffects(Path.Join(midsDirectory, "enhancement_effects.json"));
        SetBonusDb setDb = SetBonusData.LoadSetBonusDb(Path.Join(midsDirectory, "enhancement_sets.json"), Path.Join(midsDirectory, "set_bonus_powers.json"));
        string slotsPath = Path.Join(buildDirectory, "slots.json");
        IReadOnlyList<Power> raw = EffectData.LoadPowersEffects(Path.Join(buildDirectory, "powers_effects.json"));
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = EnhancementData.LoadSlotEntries(slotsPath).ToDictionary(entry => entry.BuildIndex, entry => entry.Slots);
        string className = TotalsData.LoadTotals(Path.Join(buildDirectory, "totals.json")).Class;
        int archetypeIndex = data.Classes.NidFromUidClass(className);
        ExpressionContext expression = new()
        {
            PrevHpMax = 0.0,
            BuildPowerNames = raw.Select(power => power.FullName).ToHashSet(StringComparer.Ordinal),
            ActivePowerNames = raw.Where(power => power.StatInclude).Select(power => power.FullName).ToHashSet(StringComparer.Ordinal),
            VariableValues = raw.ToLastWinsDictionary(power => power.FullName, power => power.VariableValue),
        };

        string assemblyPath = Path.Join(buildDirectory, "power_assembly.json");
        bool assembled = File.Exists(assemblyPath);
        IReadOnlyList<Power> powers = assembled
            ? Assemble(raw, AssemblyData.LoadBuildPowerAssembly(assemblyPath, slotsPath, raw), slots, enhDb, setDb, midsDirectory, archetypeIndex, expression)
            : raw;
        Pipeline.BuildOptions options = new()
        {
            Slots = slots,
            EnhDb = enhDb,
            Tables = tables,
            SetDb = setDb,
            Incarnates = IncarnateData.LoadIncarnates(Path.Join(buildDirectory, "incarnates.json")),
            Expr = assembled ? expression : null,
        };
        BaseTotals result = Pipeline.ComputeBaseTotals(powers, className, data, options);
        PowerStatistics.StatsContext context = new()
        {
            Config = data.Config,
            Tables = tables,
            EnhDb = enhDb,
            GlobalEnhance = result.GlobalEnhance,
            RechargeCap = data.Classes.Classes[archetypeIndex].RechargeCap,
            IncarnateScalar = result.IncarnateAddends?.Scalar ?? new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>(),
        };
        IReadOnlyList<PowerStats> stats = PowerStatistics.ComputeBuildStats(raw, slots, context);
        IReadOnlyList<MetricReading> metrics = [.. RuleRegistry.ScoringMetrics.Select(metric => new MetricReading(metric.Info.Id, metric.Evaluate(result.Totals, stats)))];
        return new ComputedBuild(className, result.Totals, result.TotalsCapped, metrics);
    }

    /// <summary>Gets the directory holding the shared dump directory; <c>attribmod/AttribMod.json</c> and <c>Maths.mhd</c> sit there.</summary>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <returns>Its parent directory.</returns>
    internal static string SharedRoot(string midsDirectory)
    {
        return Path.GetDirectoryName(Path.GetFullPath(midsDirectory.TrimEnd('/', '\\'))) ?? string.Empty;
    }

    /// <summary>Loads the database every build is computed against from the shared dumps.</summary>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <returns>The database.</returns>
    internal static Pipeline.Database LoadDatabase(string midsDirectory)
    {
        return new(
            ArchetypeData.LoadAttribMod(Path.Join(SharedRoot(midsDirectory), "attribmod", "AttribMod.json")),
            ArchetypeData.LoadArchetypes(Path.Join(midsDirectory, "archetypes.json")),
            TotalsData.LoadEnumMaps(Path.Join(midsDirectory, "enums.json")),
            TotalsData.LoadEngineConfig(Path.Join(midsDirectory, "config.json")),
            TotalsData.LoadServerData(Path.Join(midsDirectory, "server_data.json")));
    }

    private static List<Power> Assemble(
        IReadOnlyList<Power> raw,
        IReadOnlyDictionary<int, BuildPowerAssembly> assembly,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        SetBonusDb setDb,
        string midsDirectory,
        int archetypeIndex,
        ExpressionContext expression)
    {
        Power bonus = Accumulator.BuildSetBonusPower(raw, slots, enhDb, setDb, 50, false);
        Dictionary<int, AssembledEffects> effects = CrossPowerPass.AssembleBuild(
            assembly,
            slots,
            raw.ToLastWinsDictionary(power => power.BuildIndex, power => power.StatInclude),
            enhDb,
            setDb,
            AssemblyData.LoadAssemblyPowers(Path.Join(midsDirectory, "assembly_powers.json")),
            AssemblyData.LoadAssemblyEntities(Path.Join(midsDirectory, "assembly_entities.json")),
            archetypeIndex,
            effect => Conditionals.ConditionalsMet(effect, expression),
            bonus.Effects);
        return PowerAssembler.ApplyAssembly(raw, assembly, effects);
    }
}
