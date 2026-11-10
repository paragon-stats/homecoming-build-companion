using System.ComponentModel;
using System.Text.Json;

using CohEngine.Core;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Profiles;
using CohEngine.Core.Registry;

using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace CohEngine.Mcp;

/// <summary>
/// The engine's MCP tools. Each is a thin call into Core: it checks the client's paths, calls the engine, and wraps the
/// answer with the engine build it came from. A failure reaches the client as a tool error carrying the engine's own
/// <c>E&lt;NN&gt;</c> message.
/// </summary>
[McpServerToolType]
public sealed class EngineTools
{
    private static readonly string DataVersion = EngineInfo.Version;

    /// <summary>Lists every registry.</summary>
    /// <returns>The legality rules, hard-limit rules, enhancement aspect handlers and scoring metrics.</returns>
    [McpServerTool(Name = "rules", ReadOnly = true)]
    [Description("List the engine's rule registries: legality rules, hard-limit rules, enhancement aspect handlers and scoring metrics.")]
    public static RulesResult Rules()
    {
        return new RulesResult(DataVersion, View(RuleRegistry.Legality), View(RuleRegistry.HardLimits), View(RuleRegistry.EnhancementAspects), View(RuleRegistry.Metrics));
    }

    /// <summary>Runs the legality registry over a build dump.</summary>
    /// <param name="buildDir">The build's Mids dump directory.</param>
    /// <param name="midsDir">The shared-dump directory; defaults to the build directory's grandparent.</param>
    /// <returns>The diagnostics.</returns>
    [McpServerTool(Name = "check_legality", ReadOnly = true)]
    [Description("Check a build dump's enhancement legality (placement, unique, mutex, lone set piece, duplicate set piece). Returns the diagnostics as validate --format json prints them.")]
    public static DiagnosticsResult CheckLegality(
        [Description("Path to the build's Mids dump directory.")] string buildDir,
        [Description("Path to the shared dump directory; defaults to the build directory's grandparent.")] string? midsDir = null)
    {
        return Guard(() => Diagnose(buildDir, midsDir, ValidateChecks.Legality), Given(buildDir, midsDir));
    }

    /// <summary>Runs the hard-limit registry over a build dump.</summary>
    /// <param name="buildDir">The build's Mids dump directory.</param>
    /// <param name="midsDir">The shared-dump directory; defaults to the build directory's grandparent.</param>
    /// <returns>The diagnostics.</returns>
    [McpServerTool(Name = "check_hard_limits", ReadOnly = true)]
    [Description("Check a build dump against the game's hard limits (slots, picks, pools, levels). Returns the diagnostics as validate --format json prints them.")]
    public static DiagnosticsResult CheckHardLimits(
        [Description("Path to the build's Mids dump directory.")] string buildDir,
        [Description("Path to the shared dump directory; defaults to the build directory's grandparent.")] string? midsDir = null)
    {
        return Guard(() => Diagnose(buildDir, midsDir, ValidateChecks.HardLimits), Given(buildDir, midsDir));
    }

    /// <summary>Computes the registered scoring metrics for a build dump.</summary>
    /// <param name="buildDir">The build's Mids dump directory.</param>
    /// <param name="midsDir">The shared-dump directory; defaults to the build directory's grandparent.</param>
    /// <param name="attribModPath">The AttribMod.json file; defaults to attribmod/AttribMod.json beside the shared directory.</param>
    /// <param name="mathsPath">The Maths.mhd file; defaults to Maths.mhd beside the shared directory.</param>
    /// <returns>The metrics.</returns>
    [McpServerTool(Name = "score", ReadOnly = true)]
    [Description("Compute the registered scoring metrics (positional defense minimum, minimum resistance, perma-Hasten, perma-Domination) for a build dump.")]
    public static ScoreResult Score(
        [Description("Path to the build's Mids dump directory.")] string buildDir,
        [Description("Path to the shared dump directory; defaults to the build directory's grandparent.")] string? midsDir = null,
        [Description("Path to AttribMod.json; defaults to attribmod/AttribMod.json in the shared directory's parent.")] string? attribModPath = null,
        [Description("Path to Maths.mhd; defaults to Maths.mhd in the shared directory's parent.")] string? mathsPath = null)
    {
        return Guard(
            () =>
            {
                ComputedBuild build = Compute(buildDir, midsDir, attribModPath, mathsPath, out _);
                return new ScoreResult(
                    DataVersion,
                    build.ClassName,
                    [.. build.Metrics.Select(metric => new MetricView(metric.Name, metric.Value.IsFlag ? null : metric.Value.Number, metric.Value.IsFlag ? metric.Value.IsTrue : null))]);
            },
            Given(buildDir, midsDir));
    }

    /// <summary>Computes a build dump's base totals.</summary>
    /// <param name="buildDir">The build's Mids dump directory.</param>
    /// <param name="midsDir">The shared-dump directory; defaults to the build directory's grandparent.</param>
    /// <param name="attribModPath">The AttribMod.json file; defaults to attribmod/AttribMod.json beside the shared directory.</param>
    /// <param name="mathsPath">The Maths.mhd file; defaults to Maths.mhd beside the shared directory.</param>
    /// <returns>The uncapped and capped totals.</returns>
    [McpServerTool(Name = "totals", ReadOnly = true)]
    [Description("Compute a build dump's base totals (defense, resistance, recovery, recharge and the rest), uncapped and capped, with defense and resistance already as percentages.")]
    public static TotalsResult Totals(
        [Description("Path to the build's Mids dump directory.")] string buildDir,
        [Description("Path to the shared dump directory; defaults to the build directory's grandparent.")] string? midsDir = null,
        [Description("Path to AttribMod.json; defaults to attribmod/AttribMod.json in the shared directory's parent.")] string? attribModPath = null,
        [Description("Path to Maths.mhd; defaults to Maths.mhd in the shared directory's parent.")] string? mathsPath = null)
    {
        return Guard(
            () =>
            {
                ComputedBuild build = Compute(buildDir, midsDir, attribModPath, mathsPath, out string mids);
                EnumMaps enums = TotalsData.LoadEnumMaps(Path.Join(mids, "enums.json"));
                return new TotalsResult(DataVersion, build.ClassName, View(build.Totals, enums), View(build.TotalsCapped, enums));
            },
            Given(buildDir, midsDir));
    }

    /// <summary>Looks a set's bonuses up.</summary>
    /// <param name="setName">The set UID.</param>
    /// <param name="buildDir">A build dump directory, whose class picks the modifier column.</param>
    /// <param name="midsDir">The shared-dump directory; defaults to the build directory's grandparent.</param>
    /// <param name="attribModPath">The AttribMod.json file; defaults to attribmod/AttribMod.json beside the shared directory.</param>
    /// <returns>The set's bonuses with every magnitude resolved.</returns>
    [McpServerTool(Name = "set_bonus_lookup", ReadOnly = true)]
    [Description("Look up an enhancement set's bonuses by set name (UID). Magnitudes come back resolved through the modifier tables, with a percentage where the game shows one; no scale arithmetic is needed.")]
    public static SetBonusResult SetBonusLookup(
        [Description("The set's UID, for example the name as the Mids database spells it.")] string setName,
        [Description("Path to any build dump directory; its class picks the modifier column.")] string buildDir,
        [Description("Path to the shared dump directory; defaults to the build directory's grandparent.")] string? midsDir = null,
        [Description("Path to AttribMod.json; defaults to attribmod/AttribMod.json in the shared directory's parent.")] string? attribModPath = null)
    {
        return Guard(
            () =>
            {
                RequireDirectory(buildDir, nameof(buildDir));
                string mids = MidsOf(buildDir, midsDir);
                RequireFile(attribModPath, nameof(attribModPath));
                return new SetBonusResult(DataVersion, Core.Registry.SetBonusLookup.Find(setName, buildDir, mids, attribModPath));
            },
            Given(buildDir, midsDir));
    }

    /// <summary>Looks a power up in a build dump.</summary>
    /// <param name="fullName">The power's full name.</param>
    /// <param name="buildDir">The build's Mids dump directory.</param>
    /// <returns>The power's record.</returns>
    [McpServerTool(Name = "power_lookup", ReadOnly = true)]
    [Description("Look up a power by full name in a build dump's powers data. available_level and pick_level are game levels (1-based), not the database's 0-based values.")]
    public static PowerResult PowerLookup(
        [Description("The power's full name, for example Scrapper_Melee.Dark_Melee.Shadow_Punch.")] string fullName,
        [Description("Path to the build's Mids dump directory.")] string buildDir)
    {
        return Guard(
            () =>
            {
                RequireDirectory(buildDir, nameof(buildDir));
                Power power = EffectData.LoadPowersEffects(Path.Join(buildDir, "powers_effects.json"))
                    .FirstOrDefault(candidate => string.Equals(candidate.FullName, fullName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new EngineException($"{ErrorCodes.E167}: no power named '{fullName}' in {buildDir}; check the full name against the dump's powers_effects.json");
                return new PowerResult(
                    DataVersion,
                    new PowerView(
                        power.FullName,
                        power.PowerType,
                        power.Level + 1,
                        power.PickLevel + 1,
                        power.RechargeTime,
                        power.CastTime,
                        power.EndCost,
                        power.ActivatePeriod,
                        power.StatInclude,
                        [.. power.Effects.Select(effect => effect.EffectType).Distinct(StringComparer.Ordinal)]));
            },
            Given(buildDir, null));
    }

    /// <summary>The attack-chain planner, which is not part of this build.</summary>
    /// <returns>Nothing: it always fails.</returns>
    [McpServerTool(Name = "chain", ReadOnly = true)]
    [Description("Plan an attack chain for a build. Not available in this build: it always returns an error.")]
    public static string Chain()
    {
        throw new McpException($"{ErrorCodes.E165}: the chain tool is not available in this build; use score or totals for the build's numbers instead");
    }

    /// <summary>Names a per-type vector by its enum, as a percentage when asked.</summary>
    /// <param name="values">The vector, indexed by the enum.</param>
    /// <param name="enums">The enum maps.</param>
    /// <param name="enumName">The enum the vector is indexed by.</param>
    /// <param name="percent">Whether to scale the values to percentages.</param>
    /// <returns>The values by member name.</returns>
    /// <exception cref="EngineException">E168: the enum maps hold no name for an index of the vector.</exception>
    internal static Dictionary<string, double> Named(IReadOnlyList<double> values, EnumMaps enums, string enumName, bool percent)
    {
        IReadOnlyDictionary<int, string> names = enums.Inverse.GetValueOrDefault(enumName) ?? new Dictionary<int, string>();
        Dictionary<string, double> named = new(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            string name = names.GetValueOrDefault(index)
                ?? throw new EngineException($"{ErrorCodes.E168}: the shared enums.json has no {enumName} member at index {index}; use the enums.json dumped with the build");
            named[name] = percent ? Math.Round(values[index] * 100.0, 4, MidpointRounding.ToEven) : values[index];
        }

        return named;
    }

    /// <summary>Maps the engine's failures to tool errors; a message never carries a path the client did not supply.</summary>
    private static T Guard<T>(Func<T> run, string given)
    {
        try
        {
            return run();
        }
        catch (Exception exception) when (exception is EngineException or ArgumentException)
        {
            throw new McpException(exception.Message, exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            string file = exception is FileNotFoundException { FileName: { } path } ? $"'{Path.GetFileName(path)}'" : "an input file";
            throw new McpException($"{ErrorCodes.E72}: cannot read {file} (from {given}); check that the path exists, is readable and holds a Mids dump", exception);
        }
        catch (Exception exception) when (exception is InvalidDataException or KeyNotFoundException or JsonException)
        {
            throw new McpException($"{ErrorCodes.E72}: an input from {given} is missing a key or holds the wrong shape; re-dump the build or check the data matches its schema", exception);
        }
    }

    private static string Given(string buildDir, string? midsDir)
    {
        return midsDir is null ? $"buildDir '{buildDir}'" : $"buildDir '{buildDir}', midsDir '{midsDir}'";
    }

    private static void RequireDirectory(string path, string parameter)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new EngineException($"{ErrorCodes.E164}: {parameter} '{path}' does not exist; give an existing directory");
        }
    }

    private static void RequireFile(string? path, string parameter)
    {
        if (path is not null && !File.Exists(path))
        {
            throw new EngineException($"{ErrorCodes.E164}: {parameter} '{path}' does not exist; give an existing file");
        }
    }

    private static string MidsOf(string buildDir, string? midsDir)
    {
        string mids = midsDir ?? DumpScoring.DefaultMidsDirectory(buildDir);
        RequireDirectory(mids, nameof(midsDir));
        return mids;
    }

    private static DiagnosticsResult Diagnose(string buildDir, string? midsDir, ValidateChecks checks)
    {
        RequireDirectory(buildDir, nameof(buildDir));
        IReadOnlyList<Diagnostic> diagnostics = ValidateCommand.RunDump(buildDir, MidsOf(buildDir, midsDir), checks);
        using JsonDocument document = JsonDocument.Parse(DiagnosticFormatter.FormatJson(diagnostics));
        return new DiagnosticsResult(DataVersion, DiagnosticFormatter.ExitCode(diagnostics), document.RootElement.Clone());
    }

    private static ComputedBuild Compute(string buildDir, string? midsDir, string? attribModPath, string? mathsPath, out string mids)
    {
        RequireDirectory(buildDir, nameof(buildDir));
        mids = MidsOf(buildDir, midsDir);
        RequireFile(attribModPath, nameof(attribModPath));
        RequireFile(mathsPath, nameof(mathsPath));
        return DumpScoring.Compute(buildDir, mids, attribModPath, mathsPath);
    }

    private static List<RuleView> View(IReadOnlyList<RuleInfo> rules)
    {
        return [.. rules.Select(rule => new RuleView(rule.Id, rule.Summary))];
    }

    private static TotalsView View(TotalStatistics totals, EnumMaps enums)
    {
        return new TotalsView(
            Named(totals.Def, enums, "eDamage", percent: true),
            Named(totals.Res, enums, "eDamage", percent: true),
            Named(totals.Mez, enums, "eMez", percent: false),
            Named(totals.MezRes, enums, "eMez", percent: false),
            Named(totals.DebuffRes, enums, "eEffectType", percent: false),
            Named(totals.Elusivity, enums, "eDamage", percent: false),
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["HPRegen"] = totals.HpRegen,
                ["HPMax"] = totals.HpMax,
                ["Absorb"] = totals.Absorb,
                ["EndRec"] = totals.EndRec,
                ["EndUse"] = totals.EndUse,
                ["EndMax"] = totals.EndMax,
                ["RunSpd"] = totals.RunSpd,
                ["MaxRunSpd"] = totals.MaxRunSpd,
                ["JumpSpd"] = totals.JumpSpd,
                ["MaxJumpSpd"] = totals.MaxJumpSpd,
                ["FlySpd"] = totals.FlySpd,
                ["MaxFlySpd"] = totals.MaxFlySpd,
                ["JumpHeight"] = totals.JumpHeight,
                ["StealthPvE"] = totals.StealthPve,
                ["StealthPvP"] = totals.StealthPvp,
                ["ThreatLevel"] = totals.ThreatLevel,
                ["Perception"] = totals.Perception,
                ["BuffHaste"] = totals.BuffHaste,
                ["BuffAcc"] = totals.BuffAcc,
                ["BuffToHit"] = totals.BuffToHit,
                ["BuffDam"] = totals.BuffDam,
                ["BuffEndRdx"] = totals.BuffEndRdx,
                ["BuffRange"] = totals.BuffRange,
                ["BuffHeal"] = totals.BuffHeal,
            });
    }
}
