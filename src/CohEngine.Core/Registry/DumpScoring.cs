using CohEngine.Core.Assembly;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;
using CohEngine.Core.SetBonuses;
using CohEngine.Core.Totals;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Registry;

/// <summary>
/// Computes a build dump's totals and scoring metrics: assembly when the dump carries it, slots, set bonuses and
/// incarnates, then the registered metrics over the result. This is the one pipeline entry; the parity tests call it too.
/// </summary>
public static class DumpScoring
{
    /// <summary>Gets the shared-dump directory <c>validate</c> assumes for a build dump: the build directory's grandparent.</summary>
    /// <param name="buildDirectory">The build's dump directory.</param>
    /// <returns>The directory.</returns>
    /// <exception cref="EngineException">E164: the build directory is at or next to the filesystem root, so it has no grandparent.</exception>
    public static string DefaultMidsDirectory(string buildDirectory)
    {
        ArgumentNullException.ThrowIfNull(buildDirectory);
        string full = Path.GetFullPath(Path.TrimEndingDirectorySeparator(buildDirectory));
        return Path.GetDirectoryName(Path.GetDirectoryName(full))
            ?? throw new EngineException($"{ErrorCodes.E164}: cannot derive the shared dump directory from '{buildDirectory}', which is at or next to the filesystem root; pass the shared dump directory explicitly");
    }

    /// <summary>Computes a build dump.</summary>
    /// <param name="buildDirectory">The build's dump directory.</param>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <param name="attribModPath"><c>AttribMod.json</c>; by default <c>attribmod/AttribMod.json</c> in the shared directory's parent.</param>
    /// <param name="mathsPath"><c>Maths.mhd</c>; by default in the shared directory's parent.</param>
    /// <returns>The totals and every metric.</returns>
    public static ComputedBuild Compute(string buildDirectory, string midsDirectory, string? attribModPath = null, string? mathsPath = null)
    {
        BuiltBuild built = Build(buildDirectory, midsDirectory, attribModPath, mathsPath);
        PowerStatistics.StatsContext context = new()
        {
            Config = built.Data.Config,
            Tables = built.Tables,
            EnhDb = built.EnhDb,
            GlobalEnhance = built.Result.GlobalEnhance,
            RechargeCap = built.Data.Classes.Classes[built.Data.Classes.NidFromUidClass(built.ClassName)].RechargeCap,
            IncarnateScalar = built.Result.IncarnateAddends?.Scalar ?? new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>(),
        };
        IReadOnlyList<PowerStats> stats = PowerStatistics.ComputeBuildStats(built.RawPowers, built.Slots, context);
        IReadOnlyList<MetricReading> metrics = [.. RuleRegistry.ScoringMetrics.Select(metric => new MetricReading(metric.Info.Id, metric.Evaluate(built.Result.Totals, stats)))];
        return new ComputedBuild(built.ClassName, built.Result.Totals, built.Result.TotalsCapped, metrics);
    }

    /// <summary>Gets the directory holding the shared dump directory.</summary>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <returns>Its parent directory.</returns>
    /// <exception cref="EngineException">E164: the shared directory is a filesystem root.</exception>
    internal static string SharedRoot(string midsDirectory)
    {
        return Path.GetDirectoryName(Path.GetFullPath(Path.TrimEndingDirectorySeparator(midsDirectory)))
            ?? throw new EngineException($"{ErrorCodes.E164}: cannot find the files that sit beside the shared dump directory '{midsDirectory}', which is a filesystem root; pass their paths explicitly");
    }

    /// <summary>Loads the database every build is computed against from the shared dumps.</summary>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <param name="attribModPath"><c>AttribMod.json</c>, or null for the default beside the shared directory.</param>
    /// <returns>The database.</returns>
    internal static Pipeline.Database LoadDatabase(string midsDirectory, string? attribModPath)
    {
        return new(
            ArchetypeData.LoadAttribMod(attribModPath ?? Path.Join(SharedRoot(midsDirectory), "attribmod", "AttribMod.json")),
            ArchetypeData.LoadArchetypes(Path.Join(midsDirectory, "archetypes.json")),
            TotalsData.LoadEnumMaps(Path.Join(midsDirectory, "enums.json")),
            TotalsData.LoadEngineConfig(Path.Join(midsDirectory, "config.json")),
            TotalsData.LoadServerData(Path.Join(midsDirectory, "server_data.json")));
    }

    /// <summary>Runs the phases up to the totals.</summary>
    /// <param name="buildDirectory">The build's dump directory.</param>
    /// <param name="midsDirectory">The shared-dump directory.</param>
    /// <param name="attribModPath"><c>AttribMod.json</c>, or null for the default.</param>
    /// <param name="mathsPath"><c>Maths.mhd</c>, or null for the default.</param>
    /// <param name="forceLevel">The exemplar level, or null for the dumped configuration.</param>
    /// <returns>The build and what was computed for it.</returns>
    internal static BuiltBuild Build(string buildDirectory, string midsDirectory, string? attribModPath = null, string? mathsPath = null, int? forceLevel = null)
    {
        ArgumentNullException.ThrowIfNull(buildDirectory);
        ArgumentNullException.ThrowIfNull(midsDirectory);
        Pipeline.Database data = LoadDatabase(midsDirectory, attribModPath);
        MathTables tables = MathTables.Load(mathsPath ?? Path.Join(SharedRoot(midsDirectory), "Maths.mhd"));
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
        Pipeline.Database computeData = forceLevel is { } level ? data with { Config = data.Config with { ForceLevel = level } } : data;
        return new BuiltBuild(className, data, tables, enhDb, slots, raw, powers, Pipeline.ComputeBaseTotals(powers, className, computeData, options));
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
