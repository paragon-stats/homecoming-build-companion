using System.Globalization;
using System.Text.Json;

using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;
using CohEngine.Core.Pets;
using CohEngine.Core.Statistics;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Chain;

/// <summary>The chain report: build a damage context from a Mids dump, evaluate it per stance, and render it.</summary>
public static class ChainReport
{
    /// <summary>A typical level-50 average of an attack's damage enhancement.</summary>
    public const double DefaultDamageEnhancementAvg = 0.95;

    /// <summary>The movement modes.</summary>
    public static readonly IReadOnlyList<string> Moves = ["run", "jump", "fly"];

    private const int DefenseMelee = 10;
    private const int DefenseRanged = 11;

    private static readonly string[] PetDataGaps = ["E60", "E61", "E62"];
    private static readonly string[] CoreTypes = ["Smashing_Dmg", "Lethal_Dmg", "Fire_Dmg", "Cold_Dmg", "Energy_Dmg", "Negative_Energy_Dmg", "Psionic_Dmg", "Toxic_Dmg"];

    private static readonly IReadOnlyList<(string Name, Func<RuleContext, IEnumerable<Diagnostic>> Run)> Rules =
    [
        ("_rule_end_002", RuleEnd002),
        ("_rule_chain_001", RuleChain001),
        ("_rule_chain_002", RuleChain002),
        ("_rule_chain_002_skipped", RuleChain002Skipped),
    ];

    /// <summary>The names of the registered chain rules, in registration order.</summary>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> RegisteredChainRules()
    {
        return [.. Rules.Select(rule => rule.Name)];
    }

    /// <summary>Parses <c>KEY=VALUE</c> strings into an assumptions mapping.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The assumptions.</returns>
    /// <exception cref="EngineException">E67 for an item without a key or a finite number.</exception>
    public static IReadOnlyDictionary<string, double> ParseAssumptions(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Dictionary<string, double> result = new(StringComparer.Ordinal);
        foreach (string item in items)
        {
            int split = item.IndexOf('=', StringComparison.Ordinal);
            string key = split < 0 ? item : item[..split];
            string raw = split < 0 ? string.Empty : item[(split + 1)..];
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value) || key.Trim().Length == 0)
            {
                throw new EngineException(
                    $"{ErrorCodes.E67}: assumption {PyText.Repr(item)} is not KEY=VALUE with a finite numeric value; "
                    + "write e.g. --assume Class_Brute.fury_level=50");
            }

            result[key.Trim()] = value;
        }

        return result;
    }

    /// <summary>Parses <c>single</c> or <c>spawn:N</c> into a scenario.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The scenario.</returns>
    /// <exception cref="EngineException">E68 for any other text or N below 1.</exception>
    public static Scenario ParseTarget(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.Equals(text, "single", StringComparison.Ordinal))
        {
            return new Scenario(1);
        }

        int colon = text.IndexOf(':', StringComparison.Ordinal);
        string head = colon < 0 ? text : text[..colon];
        string count = colon < 0 ? string.Empty : text[(colon + 1)..];
        if (string.Equals(head, "spawn", StringComparison.Ordinal) && count.Length > 0 && count.All(char.IsAsciiDigit) && int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out int targets) && targets >= 1)
        {
            return new Scenario(targets);
        }

        throw new EngineException($"{ErrorCodes.E68}: target {PyText.Repr(text)} is neither 'single' nor 'spawn:N' (N >= 1); write e.g. --target spawn:5");
    }

    /// <summary>Loads the universal breakpoints.</summary>
    /// <param name="path">The <c>breakpoints.json</c> file.</param>
    /// <returns>The breakpoints.</returns>
    public static Breakpoints LoadBreakpoints(string path)
    {
        JsonElement universal = CanonicalJson.ReadJson(path, ErrorCodes.E33).Req("universal");
        return new Breakpoints(universal.ReqNum("soft_cap_defense_pve"), universal.ReqNum("ranged_standoff_ft"), universal.ReqNum("melee_range_ft_max"), universal.ReqNum("chain_min_cycles"));
    }

    /// <summary>The -Res a power puts on foes, read from its own and linked canonical templates for the build's AT.</summary>
    /// <param name="power">The canonical power.</param>
    /// <param name="resolve">The resolver.</param>
    /// <param name="mods">The AttribMod tables.</param>
    /// <param name="classes">The class table.</param>
    /// <param name="classIndex">The build's class index.</param>
    /// <returns>The magnitude and duration.</returns>
    public static (double Magnitude, double Duration) LinkedResDebuff(JsonElement power, PowerResolver resolve, AttribMods mods, ArchetypeDb classes, int classIndex)
    {
        ArgumentNullException.ThrowIfNull(mods);
        ArgumentNullException.ThrowIfNull(classes);
        Dictionary<string, double> totals = new(StringComparer.Ordinal);
        double duration = 0.0;
        foreach (JsonElement template in AttackProfiles.FoeResDebuffTemplates(power, resolve))
        {
            int table = PetDamage.ModifierTable(mods, template.Str("table"), power.ReqStr("full_name"));
            double value = Math.Abs(template.ReqNum("scale") * template.Num("magnitude", 1.0) * EffectMagnitude.Modifier(mods, classes.Classes, classIndex, table, 49));
            foreach (JsonElement attrib in template.ReqItems("attribs"))
            {
                string key = attrib.GetString() ?? string.Empty;
                totals[key] = totals.GetValueOrDefault(key) + value;
            }

            duration = Math.Max(duration, Seconds(template.Opt("duration")?.GetString()));
        }

        foreach (JsonElement effect in power.Items("effects"))
        {
            foreach (JsonElement template in effect.Items("templates"))
            {
                if (template.ReqItems("attribs").Any(item => string.Equals(item.GetString(), "Create_Entity", StringComparison.Ordinal)))
                {
                    duration = Math.Max(duration, Seconds(template.Opt("duration")?.GetString()));
                }
            }
        }

        double smallest = CoreTypes.Min(type => totals.GetValueOrDefault(type));
        return (F32.Round(smallest), F32.Round(duration));
    }

    /// <summary>Loads a dumped build into a melee-stance context plus the budgets reports need.</summary>
    /// <param name="request">The inputs.</param>
    /// <returns>The setup.</returns>
    /// <exception cref="EngineException">E65 to E71 as the Python port raises them; loaders raise their own codes.</exception>
    public static ChainSetup BuildChainSetup(ChainRequest request)
    {
        return BuildChainSetup(request, null);
    }

    /// <summary>Evaluates the damage model for each stance and form and folds the chain rules over it.</summary>
    /// <param name="setup">The setup.</param>
    /// <param name="stances">The stances.</param>
    /// <param name="path">The anchor path for diagnostics.</param>
    /// <param name="forms">The forms; every form of the build when null.</param>
    /// <returns>The reports.</returns>
    /// <exception cref="EngineException">E65 when a requested form is not one of the build's forms.</exception>
    public static IReadOnlyList<StanceReport> RunChain(ChainSetup setup, IEnumerable<Stance> stances, string path = "build", IReadOnlyList<string>? forms = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(stances);
        IReadOnlyList<string> known = AvailableForms(setup.Context);
        IReadOnlyList<string> wanted = forms ?? known;
        string? unknown = wanted.FirstOrDefault(form => !known.Contains(form, StringComparer.Ordinal));
        if (unknown is not null)
        {
            string names = string.Join(", ", known.Select(form => PyText.Repr(form.Length == 0 ? "base" : form)));
            throw new EngineException($"{ErrorCodes.E65}: form {PyText.Repr(unknown)} is not a form of this build; choose one of {names} ('' is base)");
        }

        List<StanceReport> results = [];
        foreach (Stance stance in stances)
        {
            foreach (string form in wanted)
            {
                DamageContext context = setup.Context with { Stance = stance, Form = form };
                DamageReport report = DamageModel.Evaluate(context);
                ChainPlan plan = DamageSources.ChainPlan(report.RemainderContext);
                ChainOption? chain = plan.BestAffordable ?? plan.BestOverall;
                RuleContext rule = new(setup, context, report, chain, path);
                results.Add(new StanceReport(stance, report, chain, [.. Rules.SelectMany(item => item.Run(rule))], form, plan));
            }
        }

        return results;
    }

    /// <summary>The base form (empty) then every mode some attack requires, sorted.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The forms.</returns>
    public static IReadOnlyList<string> AvailableForms(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return [string.Empty, .. context.Attacks.SelectMany(attack => attack.Profile.ModesRequired).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>One stance's report as JSON-safe data (infinity becomes null).</summary>
    /// <param name="setup">The setup.</param>
    /// <param name="result">The stance report.</param>
    /// <returns>The ordered object.</returns>
    public static PyObject StanceToJson(ChainSetup setup, StanceReport result)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(result);
        DamageReport report = result.Report;
        ChainOption? chain = result.Chain;
        EnduranceBudget budget = setup.Context.Budget;
        SourceResult[] chainSources = [.. report.Sources.Where(source => string.Equals(source.Source, DamageModel.SourceChain, StringComparison.Ordinal))];
        object chainDrain = chainSources.Length == 0 ? 0L : (object)PyFloat.Sum(chainSources.Select(source => source.EndPerSec));
        double chainDrainValue = chainSources.Length == 0 ? 0.0 : (double)chainDrain;

        PyObject unscaled = [];
        PyObject detail = [];
        PyObject labels = [];
        foreach (SourceResult source in report.Sources)
        {
            unscaled.Add(source.Source, source.Dps);
            if (source.Detail.Count > 0)
            {
                detail.Add(source.Source, source.Detail.Select(item => (object?)new PyObject { { "name", item.Name }, { "dps", item.Dps } }).ToList());
            }

            if (source.Label.Length > 0)
            {
                labels.Add(source.Source, source.Label);
            }
        }

        PyObject layers = [];
        foreach (KeyValuePair<string, double> layer in report.ScaledDps)
        {
            layers.Add(layer.Key, layer.Value);
        }

        return new PyObject
        {
            { "stance", result.Stance.Value() },
            { "form", result.Form },
            {
                "rotation",
                chain is null
                    ? null
                    : new PyObject
                    {
                        { "attacks", chain.Rotation.Select(name => (object?)name).ToList() },
                        { "cycle_time_s", chain.CycleTime },
                        { "gap_s", chain.Gap },
                        { "move_time_s", chain.MoveTime },
                        { "sustainable", chain.Sustainable },
                    }
            },
            {
                "alternatives",
                new PyObject
                {
                    { "best_sustainable", Alternative(result.Plan?.BestSustainable) },
                    { "best_overall", Alternative(result.Plan?.BestOverall) },
                }
            },
            {
                "dps",
                new PyObject
                {
                    { "total", report.TotalDps },
                    { "layers", layers },
                    { "unscaled", unscaled },
                    { "detail", detail },
                    { "labels", labels },
                }
            },
            {
                "modifiers",
                report.Modifiers.Select(modifier => (object?)new PyObject
                {
                    { "name", modifier.Modifier },
                    { "multiplier", modifier.Multiplier },
                    { "uptime", modifier.Uptime },
                    { "factor", modifier.Scale(setup.Context.DamageBuffBaseline) },
                    { "assumption", modifier.Assumption },
                    { "applies_to", modifier.AppliesTo?.Order(StringComparer.Ordinal).Select(name => (object?)name).ToList() },
                }).ToList()
            },
            {
                "endurance",
                new PyObject
                {
                    { "max", budget.MaxEnd },
                    { "recovery_per_s", budget.RecoveryPerSec },
                    { "toggle_drain_per_s", budget.ToggleDrainPerSec },
                    { "chain_drain_per_s", chainDrain },
                    { "other_drain_per_s", F32.Round(report.TotalDrainPerSec - chainDrainValue) },
                    { "net_per_s", budget.NetPerSec(report.TotalDrainPerSec) },
                    { "time_to_empty_s", double.IsInfinity(report.TimeToEmpty) ? null : report.TimeToEmpty },
                }
            },
            { "health", new PyObject { { "max_hp", setup.Health.MaxHp }, { "regen_per_sec", setup.Health.RegenPerSec } } },
            { "diagnostics", result.Diagnostics.Select(item => (object?)DiagnosticJson(item)).ToList() },
        };
    }

    /// <summary>One stance's report as the human-readable block.</summary>
    /// <param name="setup">The setup.</param>
    /// <param name="result">The stance report.</param>
    /// <returns>The text.</returns>
    public static string StanceToText(ChainSetup setup, StanceReport result)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(result);
        PyObject data = StanceToJson(setup, result);
        PyObject? rotation = (PyObject?)Get(data, "rotation");
        PyObject dps = (PyObject)Get(data, "dps")!;
        PyObject endurance = (PyObject)Get(data, "endurance")!;
        PyObject health = (PyObject)Get(data, "health")!;
        string form = (string)Get(data, "form")!;
        List<string> lines = [$"== {(string)Get(data, "stance")!} stance{(form.Length > 0 ? $", {form} form" : string.Empty)} =="];
        if (rotation is null)
        {
            lines.Add("rotation: none (no chain attack is usable in this stance)");
        }
        else
        {
            lines.Add($"rotation: {string.Join(" > ", (List<object?>)Get(rotation, "attacks")!)}");
            lines.Add(
                $"  cycle {PyFloat.Fixed((double)Get(rotation, "cycle_time_s")!, 2)} s, gap {PyFloat.Fixed((double)Get(rotation, "gap_s")!, 2)} s, "
                + $"move {PyFloat.Fixed((double)Get(rotation, "move_time_s")!, 2)} s, {((bool)Get(rotation, "sustainable")! ? "sustainable" : "not sustainable")}");
        }

        PyObject alternatives = (PyObject)Get(data, "alternatives")!;
        foreach ((string key, string title) in new[] { ("best_sustainable", "best sustainable"), ("best_overall", "best overall") })
        {
            PyObject? alt = (PyObject?)Get(alternatives, key);
            string shown = alt is null ? "none" : $"{string.Join(" > ", (List<object?>)Get(alt, "attacks")!)} (chain {PyFloat.Fixed((double)Get(alt, "chain_dps")!, 1)} DPS)";
            lines.Add($"  {title}: {shown}");
        }

        lines.Add("dps (scaled; items below each layer are before modifiers):");
        PyObject labels = (PyObject)Get(dps, "labels")!;
        PyObject details = (PyObject)Get(dps, "detail")!;
        foreach (KeyValuePair<string, object?> layer in (PyObject)Get(dps, "layers")!)
        {
            object? label = Get(labels, layer.Key);
            lines.Add($"  {layer.Key}: {PyFloat.Fixed((double)layer.Value!, 1)}" + (label is not null ? $"  [{(string)label}]" : string.Empty));
            if (Get(details, layer.Key) is List<object?> items)
            {
                lines.AddRange(items.Select(item => $"    {(string)Get((PyObject)item!, "name")!}: {PyFloat.Fixed((double)Get((PyObject)item!, "dps")!, 1)}"));
            }
        }

        lines.Add($"  total: {PyFloat.Fixed((double)Get(dps, "total")!, 1)}");
        lines.Add("modifiers:");
        List<object?> modifiers = (List<object?>)Get(data, "modifiers")!;
        if (modifiers.Count == 0)
        {
            lines.Add("  none");
        }

        foreach (PyObject modifier in modifiers.Cast<PyObject>())
        {
            string assumption = (string)Get(modifier, "assumption")!;
            lines.Add($"  {(string)Get(modifier, "name")!}: x{PyFloat.Fixed((double)Get(modifier, "factor")!, 3)} ({(assumption.Length > 0 ? assumption : "no assumption")})");
        }

        object? tte = Get(endurance, "time_to_empty_s");
        lines.Add(
            $"endurance: max {PyFloat.Fixed((double)Get(endurance, "max")!, 1)}, recovery {PyFloat.Fixed((double)Get(endurance, "recovery_per_s")!, 2)}/s, "
            + $"toggles {PyFloat.Fixed((double)Get(endurance, "toggle_drain_per_s")!, 2)}/s, chain {PyFloat.Fixed(AsDouble(Get(endurance, "chain_drain_per_s")), 2)}/s, "
            + $"other {PyFloat.Fixed((double)Get(endurance, "other_drain_per_s")!, 2)}/s, net {PyFloat.Fixed((double)Get(endurance, "net_per_s")!, 2, plus: true)}/s, "
            + $"time to empty {(tte is null ? "sustainable" : PyFloat.Fixed((double)tte, 1) + " s")}");
        lines.Add($"health: max {PyFloat.Fixed((double)Get(health, "max_hp")!, 1)}, regen {PyFloat.Fixed((double)Get(health, "regen_per_sec")!, 2)}/s");
        lines.AddRange(result.Diagnostics.Select(item => item.FormatText()));
        return string.Join("\n", lines);
    }

    internal static ChainSetup BuildChainSetup(ChainRequest request, ChainHooks? hooks)
    {
        ArgumentNullException.ThrowIfNull(request);
        string repoRoot = request.RepoRoot ?? ChainPaths.RepoRoot();
        string buildDir = request.BuildDir;
        Breakpoints breakpoints = LoadBreakpoints(Path.Combine(repoRoot, "strategy", "breakpoints.json"));
        string canonical = request.Canonical ?? Path.Combine(repoRoot, "data", "canonical");
        string defaultOff = Path.Combine(Path.GetDirectoryName(buildDir) ?? string.Empty, Path.GetFileName(buildDir) + "_clicksoff");
        if (request.ClicksOffDir is not null && !Directory.Exists(request.ClicksOffDir))
        {
            throw new EngineException($"{ErrorCodes.E65}: clicks-off directory {request.ClicksOffDir} not found; fix --clicksoff-dir or omit it");
        }

        string source = request.ClicksOffDir ?? (Directory.Exists(defaultOff) ? defaultOff : buildDir);
        double standoff = request.StandoffFt ?? breakpoints.RangedStandoffFt;
        if (standoff <= 0.0)
        {
            throw new EngineException($"{ErrorCodes.E70}: standoff is {PyFloat.General(standoff)} ft but must be positive; pass --standoff with feet > 0");
        }

        (TotalStatistics totals, TotalStatistics capped, string className) = EnduranceBudgets.LoadMidsTotals(RequiredFile(Path.Combine(source, "totals.json"), "totals file"));
        string archetypesFile = RequiredFile(Path.Combine(request.MidsDir, "archetypes.json"), "archetype table");
        ArchetypeDb classes = ArchetypeData.LoadArchetypes(archetypesFile);
        int index = classes.NidFromUidClass(className);
        if (index < 0)
        {
            throw new EngineException($"{ErrorCodes.E65}: class {PyText.Repr(className)} is not in {archetypesFile}; re-dump the build");
        }

        Archetype archetype = classes.Classes[index];
        IReadOnlyDictionary<string, PowerDamageRef> refs = DamageReference.LoadDamageRefs(source);
        (IReadOnlyList<ChainAttack> attackList, IReadOnlyList<(string Name, string Reason)> skippedList) = hooks?.Attacks is { } attackHook
            ? attackHook(refs, canonical, breakpoints.MeleeRangeFtMax)
            : Attacks(refs, canonical, breakpoints.MeleeRangeFtMax, hooks?.Classify);
        if (!attackList.Any(attack => attack.Profile.Role != Role.PetSummon))
        {
            throw new EngineException($"{ErrorCodes.E71}: no power in {source} deals damage or buffs damage; point the build at an attacker");
        }

        AttribMods? mods = request.MidsDb is not null ? ArchetypeData.LoadAttribMod(Path.Combine(request.MidsDb, "AttribMod.json")) : null;
        (IReadOnlyList<ChainAttack> attacks, IReadOnlyList<(string Name, string Reason)> debuffSkips) = FillLinkedDebuffs(attackList, canonical, mods, classes, index);
        List<(string Name, string Reason)> skipped = [.. skippedList, .. debuffSkips];
        List<ChainAttack> summons = [.. attacks.Where(attack => attack.Profile.Role == Role.PetSummon)];
        IReadOnlyList<PetSummon> pets = [];
        if (summons.Count > 0)
        {
            if (mods is null)
            {
                throw new EngineException(
                    $"{ErrorCodes.E66}: {PyText.List(summons.Select(summon => summon.FullName))} summon pets but --mids-db is not set; "
                    + "pass --mids-db with the directory holding AttribMod.json");
            }

            (pets, IReadOnlyList<(string, string)> petSkips) = BuildPets(summons, canonical, mods, classes, hooks?.BuildPets);
            skipped.AddRange(petSkips);
        }

        double maxEndCap = EnduranceBudgets.LoadCanonicalCaps(Path.Combine(canonical, "archetypes"), className).MaxEnd;
        Dictionary<string, double> merged = new(StringComparer.Ordinal) { ["chain.min_cycles"] = breakpoints.ChainMinCycles };
        foreach (KeyValuePair<string, double> pair in request.Assumptions ?? new Dictionary<string, double>(StringComparer.Ordinal))
        {
            merged[pair.Key] = pair.Value;
        }

        double enhancementAvg = merged.GetValueOrDefault("damage.enhancement_avg", DefaultDamageEnhancementAvg);
        DamageContext context = new()
        {
            Attacks = attacks,
            Budget = EnduranceBudgets.BuildEnduranceBudget(totals, capped, archetype, maxEndCap),
            Stance = Stance.Melee,
            Scenario = request.Scenario ?? new Scenario(),
            Movement = new Movement(Speed(capped, request.Move), standoff),
            ArchetypeClass = className,
            Assumptions = merged,
            DamageBuffBaseline = F32.Round(1.0 + capped.BuffDam + enhancementAvg),
            Pets = pets,
        };
        return new ChainSetup(context, EnduranceBudgets.BuildHealthBudget(capped, archetype), capped, breakpoints, string.Equals(source, buildDir, StringComparison.Ordinal), source, skipped);
    }

    internal static (Dictionary<string, JsonElement> Found, List<(string Name, string Reason)> Skipped) Candidates(IReadOnlyDictionary<string, PowerDamageRef> refs, string powersRoot)
    {
        List<KeyValuePair<string, PowerDamageRef>> kept = [.. refs.Where(pair => !pair.Key.StartsWith("Incarnate.", StringComparison.Ordinal))];
        Dictionary<string, JsonElement> found = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonElement> pair in CanonicalJson.LoadCanonicalPowers(powersRoot, kept.Where(item => HasSignal(item.Value)).Select(item => item.Key)))
        {
            found[pair.Key] = pair.Value;
        }

        List<(string, string)> skipped = [];
        foreach ((string name, PowerDamageRef reference) in kept)
        {
            if (HasSignal(reference))
            {
                continue;
            }

            JsonElement power;
            try
            {
                power = CanonicalJson.LoadCanonicalPowers(powersRoot, [name])[name];
            }
            catch (EngineException error) when (error.Message.StartsWith(ErrorCodes.E35, StringComparison.Ordinal))
            {
                if (Reportable(name))
                {
                    skipped.Add((name, "no canonical file (E35)"));
                }

                continue;
            }

            if (CreatesEntity(power.ReqItems("effects")))
            {
                found[name] = power;
            }
        }

        return (found, skipped);
    }

    /// <summary>One diagnostic as the ordered JSON object: required keys always, optional context when set.</summary>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The object.</returns>
    internal static PyObject DiagnosticJson(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        PyObject result = new()
        {
            { "rule_id", diagnostic.RuleId },
            { "severity", diagnostic.Severity },
            { "path", diagnostic.Path },
            { "line", (long)diagnostic.Line },
            { "column", (long)diagnostic.Column },
            { "message", diagnostic.Message },
        };
        if (diagnostic.Fix.Length > 0)
        {
            result.Add("fix", diagnostic.Fix);
        }

        if (diagnostic.Expected is not null)
        {
            result.Add("expected", diagnostic.Expected);
        }

        if (diagnostic.Actual is not null)
        {
            result.Add("actual", diagnostic.Actual);
        }

        return result;
    }

    private static object? Get(PyObject source, string key)
    {
        return source.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.Ordinal)).Value;
    }

    private static double AsDouble(object? value)
    {
        return value is long whole ? whole : (double)value!;
    }

    private static PyObject? Alternative(ChainOption? option)
    {
        return option is null
            ? null
            : new PyObject
            {
                { "attacks", option.Rotation.Select(name => (object?)name).ToList() },
                { "chain_dps", option.Dps },
                { "sustainable", option.Sustainable },
            };
    }

    private static double Seconds(string? text)
    {
        const string suffix = " seconds";
        string trimmed = (text ?? string.Empty).Trim();
        string number = trimmed.EndsWith(suffix, StringComparison.Ordinal) ? trimmed[..^suffix.Length] : string.Empty;
        return number.Length > 0 && number.All(letter => char.IsAsciiDigit(letter) || letter == '.') && double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;
    }

    private static string RequiredFile(string path, string what)
    {
        return File.Exists(path) ? path : throw new EngineException($"{ErrorCodes.E65}: {what} {path} not found; point the argument at a Mids dump directory");
    }

    private static double Speed(TotalStatistics capped, string move)
    {
        double speed = move switch
        {
            "run" => capped.RunSpd,
            "jump" => capped.JumpSpd,
            _ => capped.FlySpd,
        };
        return speed <= 0.0
            ? throw new EngineException($"{ErrorCodes.E69}: movement mode {PyText.Repr(move)} has speed {PyFloat.General(speed)} ft/s in this build; choose another --move (a build without a flight power has no fly speed)")
            : speed;
    }

    private static bool CreatesEntity(IEnumerable<JsonElement> effects)
    {
        return effects.Any(effect => effect.Items("templates").Any(template => template.ReqItems("attribs").Any(item => string.Equals(item.GetString(), "Create_Entity", StringComparison.Ordinal))))
            || effects.Any(effect => CreatesEntity(effect.Items("child_effects")));
    }

    private static bool HasSignal(PowerDamageRef reference)
    {
        bool clickBuff = string.Equals(reference.PowerType, "Click", StringComparison.Ordinal) && reference.SelfDamageBuff != 0.0;
        return reference.DamagePerActivation != 0.0 || clickBuff || reference.TargetResDebuff != 0.0;
    }

    private static bool Reportable(string name)
    {
        return !name.StartsWith("Inherent.", StringComparison.Ordinal) && !name.StartsWith("Incarnate.", StringComparison.Ordinal);
    }

    private static (IReadOnlyList<ChainAttack> Attacks, IReadOnlyList<(string Name, string Reason)> Skipped) Attacks(
        IReadOnlyDictionary<string, PowerDamageRef> refs,
        string canonicalRoot,
        double meleeRangeFtMax,
        Func<JsonElement, double, PowerResolver?, AttackProfile>? classify)
    {
        string powersRoot = Path.Combine(canonicalRoot, "powers");
        (Dictionary<string, JsonElement> powers, List<(string, string)> skipped) = Candidates(refs, powersRoot);
        PowerResolver resolve = CanonicalJson.CanonicalResolver(powersRoot);
        List<ChainAttack> attacks = [];
        foreach ((string name, PowerDamageRef reference) in refs)
        {
            if (!powers.TryGetValue(name, out JsonElement power))
            {
                continue;
            }

            AttackProfile profile;
            try
            {
                profile = classify is not null ? classify(power, meleeRangeFtMax, resolve) : AttackProfiles.Classify(power, meleeRangeFtMax, resolve);
            }
            catch (EngineException error) when (error.Message.StartsWith(ErrorCodes.E37, StringComparison.Ordinal))
            {
                if (Reportable(name))
                {
                    skipped.Add((name, $"not classifiable as an attack ({error.Message})"));
                }

                continue;
            }

            attacks.Add(new ChainAttack
            {
                FullName = name,
                Damage = reference.DamagePerActivation,
                CastTime = reference.CastTime,
                RechargeTime = reference.RechargeTime,
                EndCost = reference.EndCost,
                Profile = profile,
                ActivatePeriod = reference.ActivatePeriod,
                SelfDamageBuff = reference.SelfDamageBuff,
                SelfDamageBuffDuration = reference.SelfDamageBuffDuration,
                TargetResDebuff = reference.TargetResDebuff,
                TargetResDebuffDuration = reference.TargetResDebuffDuration,
            });
        }

        return (attacks, skipped);
    }

    private static (IReadOnlyList<ChainAttack> Attacks, IReadOnlyList<(string Name, string Reason)> Skipped) FillLinkedDebuffs(
        IReadOnlyList<ChainAttack> attacks,
        string canonical,
        AttribMods? mods,
        ArchetypeDb classes,
        int classIndex)
    {
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Path.Combine(canonical, "powers"));
        List<ChainAttack> filled = [];
        List<(string, string)> skipped = [];
        foreach (ChainAttack attack in attacks)
        {
            JsonElement? power = attack.Profile.Role is Role.Patch or Role.Support or Role.PersistentDebuff ? resolve(attack.FullName) : null;
            if (power is null || attack.TargetResDebuff > 0.0 || AttackProfiles.FoeResDebuffTemplates(power.Value, resolve).Count == 0)
            {
                filled.Add(attack);
            }
            else if (mods is null)
            {
                filled.Add(attack);
                skipped.Add((attack.FullName, "-Res not modelled, the dump lacks it; pass --mids-db to read it"));
            }
            else
            {
                (double magnitude, double duration) = LinkedResDebuff(power.Value, resolve, mods, classes, classIndex);
                filled.Add(attack with { TargetResDebuff = magnitude, TargetResDebuffDuration = duration });
            }
        }

        return (filled, skipped);
    }

    private static (IReadOnlyList<PetSummon> Pets, IReadOnlyList<(string Name, string Reason)> Skipped) BuildPets(
        IReadOnlyList<ChainAttack> summons,
        string canonical,
        AttribMods attribmod,
        ArchetypeDb classes,
        Func<IReadOnlyList<ChainAttack>, string, AttribMods, ArchetypeDb, IReadOnlyList<PetSummon>>? hook)
    {
        List<PetSummon> pets = [];
        List<(string, string)> skipped = [];
        foreach (ChainAttack summon in summons)
        {
            try
            {
                pets.AddRange(hook is not null ? hook([summon], canonical, attribmod, classes) : PetDamage.BuildPetSummons([summon], canonical, attribmod, classes));
            }
            catch (EngineException error) when (PetDataGaps.Any(code => error.Message.StartsWith(code, StringComparison.Ordinal)))
            {
                skipped.Add((summon.FullName, $"pet not modelled, data gap ({error.Message})"));
            }
        }

        return (pets, skipped);
    }

    private static IEnumerable<Diagnostic> RuleEnd002(RuleContext rule)
    {
        double cycles = rule.Context.Assumptions.TryGetValue("chain.min_cycles", out double value) ? value : throw new MissingKeyException("chain.min_cycles");
        ChainOption? chain = rule.Chain;
        if (chain is null || chain.Sustainable)
        {
            yield break;
        }

        double minimum = F32.Round(cycles * chain.CycleTime);
        if (chain.TimeToEmpty < minimum)
        {
            string message = $"{rule.Context.Stance.Value()} stance: no affordable chain; the best chain empties endurance in "
                + $"{PyFloat.Fixed(chain.TimeToEmpty, 1)} s, under {PyFloat.General(cycles)} cycles of {PyFloat.Fixed(chain.CycleTime, 2)} s ({PyFloat.Fixed(minimum, 1)} s)";
            yield return new Diagnostic("P-END-002", "warning", message)
            {
                Path = rule.Path,
                Fix = "add recovery or endurance reduction, drop toggles, or drop the costliest attacks from the chain",
                Expected = $">= {PyFloat.General(cycles)} cycles ({PyFloat.Fixed(minimum, 1)} s)",
                Actual = $"{PyFloat.Fixed(chain.TimeToEmpty, 1)} s",
            };
        }
    }

    private static IEnumerable<Diagnostic> RuleChain001(RuleContext rule)
    {
        Stance stance = rule.Context.Stance;
        int position = stance == Stance.Ranged ? DefenseRanged : DefenseMelee;
        double softCap = rule.Setup.Breakpoints.SoftCapDefensePve;
        double actual = F32.Round(rule.Setup.Capped.Def[position] * 100.0);
        if (actual < softCap)
        {
            string kind = stance == Stance.Ranged ? "ranged" : "melee";
            string message = $"{stance.Value()} stance: {kind} defense is {PyFloat.Fixed(actual, 1)}%, under the {PyFloat.General(softCap)}% soft cap";
            yield return new Diagnostic("P-CHAIN-001", "info", message)
            {
                Path = rule.Path,
                Expected = $">= {PyFloat.General(softCap)}%",
                Actual = $"{PyFloat.Fixed(actual, 1)}%",
            };
        }
    }

    private static IEnumerable<Diagnostic> RuleChain002(RuleContext rule)
    {
        if (rule.Setup.ClicksAlwaysOn)
        {
            yield return new Diagnostic("P-CHAIN-002", "info", "no clicks-off dump found; click buffs are counted always-on")
            {
                Path = rule.Path,
                Fix = "dump the build with its click buffs off and pass it as --clicksoff-dir",
            };
        }

        foreach (ChainAttack attack in rule.Context.Attacks)
        {
            string? message = AttackProfiles.CategoryDisagreement(attack.Profile);
            if (message is not null)
            {
                yield return new Diagnostic("P-CHAIN-002", "info", message) { Path = rule.Path };
            }
        }
    }

    private static IEnumerable<Diagnostic> RuleChain002Skipped(RuleContext rule)
    {
        foreach ((string name, string reason) in rule.Setup.Skipped)
        {
            yield return new Diagnostic("P-CHAIN-002", "info", $"power {name} is not modelled: {reason}")
            {
                Path = rule.Path,
                Fix = $"check {name} against canonical data; its damage is missing from every layer until it is classified",
            };
        }
    }
}
