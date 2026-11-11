using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

using CohEngine.Core;
using CohEngine.Core.BuildFile;
using CohEngine.Core.Chain;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Profiles;
using CohEngine.Core.Registry;

using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace CohEngine.Mcp;

/// <summary>
/// The engine's MCP tools. Each is a thin call into Core: it checks the client's paths, calls the engine, and wraps the
/// answer with a hash of the data files it read. A failure reaches the client as a tool error carrying
/// the engine's own <c>E&lt;NN&gt;</c> message; a lookup that finds nothing is an unknown result, not an error.
/// </summary>
[McpServerToolType]
public sealed class EngineTools
{
    /// <summary>Lists every registry.</summary>
    /// <returns>The legality rules, hard-limit rules, enhancement aspect handlers and scoring metrics.</returns>
    [McpServerTool(Name = "rules", ReadOnly = true)]
    [Description("List the engine's rule registries: legality rules, hard-limit rules, enhancement aspect handlers and scoring metrics.")]
    public static RulesResult Rules()
    {
        return new RulesResult(DataVersion.Unknown, View(RuleRegistry.Legality), View(RuleRegistry.HardLimits), View(RuleRegistry.EnhancementAspects), View(RuleRegistry.Metrics));
    }

    /// <summary>Runs the legality registry over a build dump.</summary>
    /// <param name="buildDir">The build's Mids dump directory, or a <c>.mbd</c> file.</param>
    /// <param name="midsDir">The shared-dump directory; defaults to the build directory's grandparent, and is required for a <c>.mbd</c>.</param>
    /// <returns>The diagnostics.</returns>
    [McpServerTool(Name = "check_legality", ReadOnly = true)]
    [Description("Check a build's enhancement legality (placement, unique, mutex, lone set piece, duplicate set piece). Returns the diagnostics as validate --format json prints them.")]
    public static DiagnosticsResult CheckLegality(
        [Description("Path to the build's Mids dump directory, or to a .mbd file (then midsDir is required).")] string buildDir,
        [Description("Path to the shared dump directory; defaults to the build directory's grandparent, and is required for a .mbd.")] string? midsDir = null)
    {
        return Guard(() => Diagnose(buildDir, midsDir, ValidateChecks.Legality), Given(buildDir, midsDir));
    }

    /// <summary>Runs the hard-limit registry over a build dump.</summary>
    /// <param name="buildDir">The build's Mids dump directory, or a <c>.mbd</c> file.</param>
    /// <param name="midsDir">The shared-dump directory; defaults to the build directory's grandparent, and is required for a <c>.mbd</c>.</param>
    /// <returns>The diagnostics.</returns>
    [McpServerTool(Name = "check_hard_limits", ReadOnly = true)]
    [Description("Check a build against the game's hard limits (slots, picks, pools, levels). Returns the diagnostics as validate --format json prints them.")]
    public static DiagnosticsResult CheckHardLimits(
        [Description("Path to the build's Mids dump directory, or to a .mbd file (then midsDir is required).")] string buildDir,
        [Description("Path to the shared dump directory; defaults to the build directory's grandparent, and is required for a .mbd.")] string? midsDir = null)
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
                ComputedBuild build = Compute(buildDir, midsDir, attribModPath, mathsPath, out string mids);
                return new ScoreResult(
                    DataVersion.Of(mids, buildDir, DumpScoring.AttribModPath(mids, attribModPath), DumpScoring.MathsPath(mids, mathsPath)),
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
                return new TotalsResult(DataVersion.Of(mids, buildDir, DumpScoring.AttribModPath(mids, attribModPath), DumpScoring.MathsPath(mids, mathsPath)), build.ClassName, View(build.Totals, enums), View(build.TotalsCapped, enums));
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
                RequireDump(buildDir);
                string mids = MidsOf(buildDir, midsDir);
                RequireFile(attribModPath, nameof(attribModPath));
                SetBonusView? set = Core.Registry.SetBonusLookup.Find(setName, buildDir, mids, attribModPath);
                return new SetBonusResult(DataVersion.Of(mids, buildDir, DumpScoring.AttribModPath(mids, attribModPath)), set, set is null ? $"{ErrorCodes.E166}: no set named '{setName}' in the loaded set-bonus data; check the set's UID spelling" : null);
            },
            Given(buildDir, midsDir));
    }

    /// <summary>Looks a power up in a build dump.</summary>
    /// <param name="fullName">The power's full name.</param>
    /// <param name="buildDir">The build's Mids dump directory.</param>
    /// <returns>The power's record.</returns>
    [McpServerTool(Name = "power_lookup", ReadOnly = true)]
    [Description("Look up a power by full name in a build dump's powers data. available_level is the game level the power is first available at (the dump already holds it 1-based); pick_level is the game level the build took it at (the dump holds it 0-based, so 1 is added).")]
    public static PowerResult PowerLookup(
        [Description("The power's full name, for example Scrapper_Melee.Dark_Melee.Shadow_Punch.")] string fullName,
        [Description("Path to the build's Mids dump directory.")] string buildDir)
    {
        return Guard(
            () =>
            {
                RequireDump(buildDir);
                string file = Path.Join(buildDir, "powers_effects.json");
                Power? power = EffectData.LoadPowersEffects(file)
                    .FirstOrDefault(candidate => string.Equals(candidate.FullName, fullName, StringComparison.OrdinalIgnoreCase));
                if (power is null)
                {
                    return new PowerResult(DataVersion.Of(buildDir), null, $"{ErrorCodes.E167}: no power named '{fullName}' in {buildDir}; check the full name against the dump's powers_effects.json");
                }

                // Older dumps lack these two keys and the loader fills them in; the record in the file says which are real.
                using JsonDocument raw = JsonDocument.Parse(File.ReadAllBytes(file));
                JsonElement entry = raw.RootElement.EnumerateArray().First(candidate => string.Equals(candidate.GetProperty("FullName").GetString(), power.FullName, StringComparison.Ordinal));
                bool hasRequires = entry.TryGetProperty("RequiresPowers", out _);
                bool hasChosen = entry.TryGetProperty("Chosen", out _);
                List<string> unknown = [.. new[] { (hasRequires, "requires_powers"), (hasChosen, "chosen") }.Where(field => !field.Item1).Select(field => field.Item2)];
                return new PowerResult(
                    DataVersion.Of(buildDir),
                    new PowerView(
                        power.FullName,
                        power.PowerType,
                        power.Level,
                        power.PickLevel + 1,
                        power.RechargeTime,
                        power.CastTime,
                        power.EndCost,
                        power.ActivatePeriod,
                        power.StatInclude,
                        [.. power.Effects.Select(effect => effect.EffectType).Distinct(StringComparer.Ordinal)],
                        hasRequires ? power.RequiresPowers : null,
                        hasChosen ? power.Chosen : null),
                    null,
                    unknown.Count == 0 ? null : unknown);
            },
            Given(buildDir, null));
    }

    /// <summary>Plans a build dump's attack chains: the same plan <c>coh-engine chain --format json</c> prints.</summary>
    /// <param name="buildDir">The build's Mids dump directory.</param>
    /// <param name="midsDir">The shared-dump directory; defaults to the build directory's grandparent.</param>
    /// <param name="stance">Which stance to plan: melee, ranged, hybrid or all.</param>
    /// <param name="form">The form to plan for; omitted for the build's own.</param>
    /// <param name="target">The scenario: single, or a spawn as spawn:N.</param>
    /// <param name="move">How the character moves between attacks: run, jump or fly.</param>
    /// <param name="standoff">The ranged standoff in feet; omitted for the default.</param>
    /// <param name="midsDb">The directory holding AttribMod.json, which prices pets; omitted to skip pets.</param>
    /// <returns>One plan per stance.</returns>
    [McpServerTool(Name = "chain", ReadOnly = true)]
    [Description("Plan the build's attack chains (rotation, cycle time, damage per second, alternatives) with the engine's chain planner. Returns one plan per stance, as chain --format json prints them.")]
    public static ChainResult Chain(
        [Description("Path to the build's Mids dump directory.")] string buildDir,
        [Description("Path to the shared dump directory; defaults to the build directory's grandparent.")] string? midsDir = null,
        [Description("The stance to plan: melee, ranged, hybrid or all.")] string stance = "all",
        [Description("The form to plan for; omit for the build's own.")] string? form = null,
        [Description("The scenario: single, or spawn:N for a spawn of N.")] string target = "single",
        [Description("How the character moves between attacks: run, jump or fly.")] string move = "run",
        [Description("The ranged standoff in feet; omit for the default.")] double? standoff = null,
        [Description("Path to the directory holding AttribMod.json, which prices pets; omit to leave pets out.")] string? midsDb = null)
    {
        return Guard(
            () =>
            {
                RequireDump(buildDir);
                List<string> args = [buildDir, "--stance", stance, "--target", target, "--move", move, "--format", "json"];
                AddOption(args, "--mids-dir", midsDir);
                AddOption(args, "--form", form);
                AddOption(args, "--standoff", standoff?.ToString("R", CultureInfo.InvariantCulture));
                AddOption(args, "--mids-db", midsDb);
                using StringWriter output = new();
                using StringWriter error = new();
                int code = ChainCommand.Run(args, output, error);

                // The command reports a bad input on stderr, its last line being the message; a plan with findings still prints.
                string failure = error.ToString().TrimEnd().Split('\n')[^1].Trim();
                if (failure.Length != 0)
                {
                    throw new EngineException(failure[(failure.IndexOf("error: ", StringComparison.Ordinal) + "error: ".Length)..]);
                }

                string root = ChainPaths.RepoRoot();
                List<string> read = [midsDir ?? DumpScoring.DefaultMidsDirectory(buildDir), buildDir, Path.Join(root, "strategy", "breakpoints.json"), Path.Join(root, "data", "canonical", "manifest.json"), Path.Join(root, "manifest.json")];
                if (midsDb is not null)
                {
                    read.Add(Path.Join(midsDb, "AttribMod.json"));
                }

                using JsonDocument plan = JsonDocument.Parse(output.ToString());
                return new ChainResult(DataVersion.Of([.. read]), code, plan.RootElement.Clone());
            },
            Given(buildDir, midsDir));
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

    private static void AddOption(List<string> args, string name, string? value)
    {
        if (value is not null)
        {
            args.Add(name);
            args.Add(value);
        }
    }

    private static bool IsMbd(string buildDir)
    {
        return string.Equals(Path.GetExtension(buildDir), ".mbd", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Requires a Mids dump directory: a <c>.mbd</c> file carries no power effects, so no total can be computed from it.</summary>
    private static void RequireDump(string buildDir)
    {
        if (IsMbd(buildDir))
        {
            throw new EngineException($"{ErrorCodes.E180}: buildDir '{buildDir}' is a .mbd file, which holds no power effects; give the build's Mids dump directory for this tool");
        }

        RequireDirectory(buildDir, nameof(buildDir));
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
        IReadOnlyList<Diagnostic> diagnostics;
        string version;
        if (IsMbd(buildDir))
        {
            // A .mbd sits beside no shared dumps, so the client names them.
            RequireFile(buildDir, nameof(buildDir));
            string mids = midsDir ?? throw new EngineException($"{ErrorCodes.E164}: midsDir is required with a .mbd file; give the shared dump directory");
            RequireDirectory(mids, nameof(midsDir));
            diagnostics = ValidateCommand.RunMbd(new MbdBuildSource(mids), buildDir, mids, checks);
            version = DataVersion.Of(buildDir, mids);
        }
        else
        {
            RequireDirectory(buildDir, nameof(buildDir));
            string mids = MidsOf(buildDir, midsDir);
            diagnostics = ValidateCommand.RunDump(buildDir, mids, checks);
            version = DataVersion.Of(mids, buildDir);
        }

        using JsonDocument document = JsonDocument.Parse(DiagnosticFormatter.FormatJson(diagnostics));
        return new DiagnosticsResult(version, DiagnosticFormatter.ExitCode(diagnostics), document.RootElement.Clone());
    }

    private static ComputedBuild Compute(string buildDir, string? midsDir, string? attribModPath, string? mathsPath, out string mids)
    {
        RequireDump(buildDir);
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
