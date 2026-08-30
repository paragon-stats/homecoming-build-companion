// MidsDump — test-only harness that loads the MidsReborn Homecoming database
// headless (no UI) and dumps the reference data the Python port (src/coh_engine)
// baselines its golden fixtures against.
//
// Usage: MidsDump <databases-dir> <output-dir>
//   <databases-dir>  e.g. <midsreborn-fork>/MidsReborn/Databases/Homecoming
//   <output-dir>     directory to write the JSON dumps into (created if missing)
//
// Load sequence mirrors MainModule.LoadDataAsync (MidsReborn MainModule.cs:145-218),
// minus graphics. Dialogs only fire on load errors.

using System.Text.Json;
using System.Text.Json.Serialization;
using Mids_Reborn;
using Mids_Reborn.Core;
using Mids_Reborn.Core.Base.Data_Classes;
using Mids_Reborn.Core.Base.Master_Classes;
using Mids_Reborn.Core.BuildFile;

namespace MidsDump;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: MidsDump <databases-dir> <output-dir>");
            return 2;
        }

        var dataPath = Path.GetFullPath(args[0]);
        var outDir = Path.GetFullPath(args[1]);
        if (!Directory.Exists(dataPath))
        {
            Console.Error.WriteLine($"E01: databases dir not found: {dataPath}");
            return 2;
        }
        Directory.CreateDirectory(outDir);

        // Mids' loaders expect a trailing separator when combining paths.
        if (!dataPath.EndsWith(Path.DirectorySeparatorChar))
        {
            dataPath += Path.DirectorySeparatorChar;
        }

        ConfigData.Initialize();

        if (!DatabaseAPI.LoadServerData(dataPath))
        {
            Console.Error.WriteLine("E02: LoadServerData failed");
            return 1;
        }

        DatabaseAPI.Database.AttribMods = new Modifiers();
        if (!DatabaseAPI.Database.AttribMods.Load(dataPath))
        {
            Console.Error.WriteLine("E03: AttribMods.Load failed");
            return 1;
        }

        DatabaseAPI.LoadTypeGrades(dataPath);

        if (!DatabaseAPI.LoadLevelsDatabase(dataPath))
        {
            Console.Error.WriteLine("E04: LoadLevelsDatabase failed");
            return 1;
        }

        if (!DatabaseAPI.LoadMainDatabase(dataPath))
        {
            Console.Error.WriteLine("E05: LoadMainDatabase failed");
            return 1;
        }

        if (!DatabaseAPI.LoadMaths(dataPath))
        {
            Console.Error.WriteLine("E06: LoadMaths failed");
            return 1;
        }

        if (!DatabaseAPI.LoadEffectIdsDatabase(dataPath))
        {
            Console.Error.WriteLine("E07: LoadEffectIdsDatabase failed");
            return 1;
        }

        if (!DatabaseAPI.LoadEnhancementClasses(dataPath))
        {
            Console.Error.WriteLine("E08: LoadEnhancementClasses failed");
            return 1;
        }

        DatabaseAPI.LoadEnhancementDb(dataPath);
        DatabaseAPI.LoadOrigins(dataPath);
        DatabaseAPI.LoadSalvage(dataPath);
        DatabaseAPI.LoadRecipes(dataPath);
        DatabaseAPI.LoadReplacementTable();
        DatabaseAPI.MatchAllIDs();
        DatabaseAPI.AssignSetBonusIndexes();
        DatabaseAPI.AssignRecipeIDs();

        // An empty character context so computed effect getters (e.g. Probability,
        // which parses an expression against character state) don't dereference a
        // null MidsContext.Character during the DB-level set_bonus_powers dump.
        MainModule.MidsController.Toon = new clsToonX();

        DumpVersion(outDir);
        DumpServerData(outDir);
        DumpArchetypes(outDir);
        DumpMaths(outDir);
        DumpEnhancementClasses(outDir);
        DumpEnhancements(outDir);
        DumpLegalityProbe(outDir);
        DumpEnhancementEffects(outDir);
        DumpEnhancementSets(outDir);
        DumpSetBonusPowers(outDir);
        DumpPowerIndex(outDir);
        DumpEnums(outDir);
        DumpConfig(outDir);
        DumpLevels(outDir);
        DumpIncarnates(outDir);

        // Optional third arg: a directory of .mbd sample builds. Each is loaded
        // through Mids and re-saved as a .mxd share block, giving the Python port
        // real Mids-produced .mxd fixtures to validate its reader against.
        // Pass "-" to skip.
        if (args.Length >= 3 && args[2] != "-")
        {
            ExportMxdBuilds(Path.GetFullPath(args[2]), outDir);
        }

        // Optional fourth arg: a directory of parity-build .mbd files. Each is
        // loaded through Mids, recomputed via GenerateBuffedPowerArray, and dumped
        // as builds/<name>/{powers_effects,totals}.json — the reference fixtures
        // the Python base-totals math is validated against.
        if (args.Length >= 4)
        {
            var failures = DumpParityBuilds(Path.GetFullPath(args[3]), outDir);
            if (failures > 0)
            {
                // Fail loudly: a silent OK on a bad re-baseline leaves the Python
                // parity suite validating against stale or missing fixtures.
                Console.Error.WriteLine($"E14: {failures} parity build(s) failed; dumps are incomplete");
                return 1;
            }
        }

        Console.WriteLine($"OK: dumps written to {outDir}");
        return 0;
    }

    private static void DumpEnums(string outDir)
    {
        // Name -> ordinal for every enum the engine indexes arrays by (Totals.Def
        // by eDamage, _selfBuffs.Effect by eEffectType/eStatType, ...). The Python
        // port must never guess these orderings.
        var enums = new Dictionary<string, Dictionary<string, int>>();
        var enumTypes = new[]
        {
            typeof(Enums.eEffectType), typeof(Enums.eDamage), typeof(Enums.eMez),
            typeof(Enums.eToWho), typeof(Enums.ePvX), typeof(Enums.eAspect),
            typeof(Enums.eAttribType), typeof(Enums.eEffectClass), typeof(Enums.eStacking),
            typeof(Enums.eSuppress), typeof(Enums.ePowerType), typeof(Enums.eSpecialCase),
            typeof(Enums.eStatType),
            // Enhancement-value pipeline: the port resolves a slotted
            // enhancement's aspect (eEnhance), grade/relative-level/type, and ED
            // schedule by ordinal from these maps.
            typeof(Enums.eEnhance), typeof(Enums.eEnhGrade), typeof(Enums.eEnhRelative),
            typeof(Enums.eType), typeof(Enums.eSchedule),
            // Set-bonus subsystem: eSetType maps an EnhancementSet to the powers
            // that accept it (Power.SetTypes). ePvX (tier-bonus PvMode gate) is
            // already dumped above.
            typeof(Enums.eSetType),
        };
        foreach (var t in enumTypes)
        {
            var values = new Dictionary<string, int>();
            foreach (var v in Enum.GetValues(t))
            {
                values[v.ToString()!] = Convert.ToInt32(v);
            }
            enums[t.Name] = values;
        }
        WriteJson(outDir, "enums.json", enums);
    }

    private static void DumpLevels(string outDir)
    {
        // The character-level schedule (Database.Levels, one LevelMap per level,
        // 0-based index i == in-game level i+1). LevelMap.Powers is the count of
        // powers pickable at that level; LevelMap.Slots is the count of enhancement
        // slots granted. The hard-limits validator reads these to check that a slot
        // lands on a real grant level and a power is picked at a real pick level —
        // anchored to game data, never a schedule reconstructed from memory.
        var levels = DatabaseAPI.Database.Levels;
        var mainPowers = DatabaseAPI.Database.Levels_MainPowers;
        WriteJson(outDir, "levels.json", new
        {
            Count = levels.Length,
            Convention = "levelIndex is 0-based; gameLevel = levelIndex + 1",
            SlotGrantLevels = levels
                .Select((lm, idx) => new { LevelIndex = idx, GameLevel = idx + 1, Slots = lm.Slots })
                .Where(x => x.Slots > 0)
                .ToList(),
            PowerPickLevels = mainPowers
                .Select(idx => new { LevelIndex = idx, GameLevel = idx + 1 })
                .ToList(),
            PerLevel = levels
                .Select((lm, idx) => new { LevelIndex = idx, GameLevel = idx + 1, lm.Powers, lm.Slots })
                .ToList(),
        });
    }

    private static void DumpConfig(string outDir)
    {
        // The config state the totals were computed under. The Python port pins
        // its assumptions (PvE mode, no suppression, level-50 build) to this dump.
        var cfg = MidsContext.Config;
        WriteJson(outDir, "config.json", new
        {
            Suppression = (int)cfg.Suppression,
            cfg.Inc.DisablePvE,
            cfg.ForceLevel,
            cfg.ScalingToHit,
            // The damage-math mode DamagePerActivation was computed under (defaults:
            // Average / Numeric). The port's DPS uses the same per-activation damage.
            DamageCalculate = cfg.DamageMath.Calculate.ToString(),
            DamageReturn = cfg.DamageMath.ReturnValue.ToString(),
        });
    }

    // Returns the number of parity builds that failed to load; Main turns a
    // non-zero count into a non-zero exit so a bad re-baseline is never masked.
    private static int DumpParityBuilds(string buildsDir, string outDir)
    {
        if (!Directory.Exists(buildsDir))
        {
            Console.Error.WriteLine($"E12: parity-builds dir not found: {buildsDir}");
            return 1;
        }

        var failures = 0;
        var byName = new Dictionary<string, string>();
        foreach (var mbdPath in Directory.EnumerateFiles(buildsDir, "*.mbd", SearchOption.AllDirectories))
        {
            var toon = new clsToonX();
            MainModule.MidsController.Toon = toon;
            if (!BuildManager.Instance.LoadFromFile(mbdPath))
            {
                Console.Error.WriteLine($"E13: LoadFromFile failed for {mbdPath}");
                failures++;
                continue;
            }

            toon.GenerateBuffedPowerArray();
            var name = Path.GetFileNameWithoutExtension(mbdPath);
            byName[name] = mbdPath;
            DumpOneBuild(Path.Combine(outDir, "builds", name), toon);
            DumpForumExport(Path.Combine(outDir, "builds", name), name);
        }

        failures += DumpExemplarVariants(byName, outDir);
        return failures;
    }

    // The forum export is MidsReborn's own emitter (ClsOutput.Build), so the markdown a build
    // proposal ships is generated rather than hand-transcribed — the format is a spec, not a
    // style choice (.claude/rules/build-creation.md P1).
    private static void DumpForumExport(string buildDir, string name)
    {
        try
        {
            if (MidsContext.Config.Export.FormatCode.Length == 0) MidsContext.Config.Export.ResetCodesToDefaults();
            if (MidsContext.Config.Export.ColorSchemes.Length == 0) MidsContext.Config.Export.ResetColorsToDefaults();
            // Pick the plain-text format code when one exists; BBCode markup is noise in markdown.
            var codes = MidsContext.Config.Export.FormatCode;
            for (var i = 0; i < codes.Length; i++)
            {
                if (!codes[i].Name.Contains("Forum", StringComparison.OrdinalIgnoreCase)) continue;
                MidsContext.Config.ExportTarget = i;
                break;
            }

            var text = new ClsOutput().Build("", true);
            Directory.CreateDirectory(buildDir);
            File.WriteAllText(Path.Combine(buildDir, "forum_export.txt"), text);
            Console.WriteLine($"wrote {Path.Combine(buildDir, "forum_export.txt")}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"E22: forum export failed for {name}: {ex.Message}");
        }
    }

    private static void DumpOneBuild(string buildDir, clsToonX toon)
    {
        Directory.CreateDirectory(buildDir);
        DumpBuildPowersEffects(buildDir, toon);
        DumpBuildSlots(buildDir, toon);
        DumpBuildEnhancedPowers(buildDir, toon);
        DumpBuildSetBonusVirtualPower(buildDir, toon);
        DumpBuildIncarnates(buildDir, toon);
        DumpBuildTotals(buildDir, toon);
    }

    private static void DumpBuildIncarnates(string buildDir, clsToonX toon)
    {
        // Each StatInclude incarnate's GrantPower-delivered enhancement, resolved to the
        // hidden granted sub-powers that actually carry it. An Alpha's raw db.Power holds
        // only GrantPower/RevokePower/LevelShift/SetMode; the real Accuracy/RechargeTime/…
        // enhancement lives on the sub-powers a GrantPower effect names via fx.Summon /
        // fx.nSummon -> db.Power[nSummon] (Power.ApplyGrantPowerEffects absorbs them). The
        // port applies these per-target via the per-aspect handler (pre-ED where IgnoreED
        // is false, post-ED where true) instead of porting GBPA's GrantPower absorption.
        //
        // The accept-gate (clsToonX.cs:1458) intersects the TARGET power's Enhancements
        // with the SUB-POWER's Enhancements (not the Alpha's, which is []); so each
        // sub-power carries its own gate list. Only Enhancement/DamageBuff sub-effects are
        // kept (the ones GBPA_ApplyIncarnateEnhancements routes by ETModifies); nested
        // GrantPower / GlobalChanceMod paths are dropped here and refused loudly in the port.
        var db = DatabaseAPI.Database;
        var incarnates = new List<object>();
        for (var i = 0; i < toon.CurrentBuild.Powers.Count; i++)
        {
            var entry = toon.CurrentBuild.Powers[i];
            if (entry?.Power == null || entry.NIDPower < 0 || !entry.StatInclude)
            {
                continue;
            }
            if (!entry.Power.FullName.StartsWith("Incarnate.", StringComparison.Ordinal))
            {
                continue;
            }

            var subPowers = new List<object>();
            foreach (var fx in entry.Power.Effects)
            {
                if (fx.EffectType != Enums.eEffectType.GrantPower || fx.nSummon <= -1
                    || fx.nSummon >= db.Power.Length || db.Power[fx.nSummon] == null)
                {
                    continue;
                }

                var sub = db.Power[fx.nSummon];
                var enhFx = sub.Effects
                    .Where(e => e.EffectType is Enums.eEffectType.Enhancement or Enums.eEffectType.DamageBuff)
                    .Select((e, eIdx) => SerializeEffect(e, eIdx))
                    .ToList();
                if (enhFx.Count == 0)
                {
                    continue;
                }

                subPowers.Add(new
                {
                    sub.FullName,
                    // The accept-gate list: the target build power must share an
                    // enhancement class with THIS sub-power for the enhancement to apply.
                    sub.Enhancements,
                    Effects = enhFx,
                });
            }

            incarnates.Add(new
            {
                BuildIndex = i,
                entry.Power.FullName,
                SubPowers = subPowers,
            });
        }
        WriteJson(buildDir, "incarnates.json", incarnates);
    }

    // A DB-level catalog of every incarnate powerset (ePowerSetType.Incarnate): the
    // authoritative port-format reference for the full incarnate buff set (CP6.2). For
    // each incarnate power it records the aspect tuples it delivers — its own effects and
    // the effects of its GrantPower-granted sub-powers (fx.nSummon -> db.Power[nSummon]) —
    // so the engine can enumerate which EffectType/ETModifies/Aspect each incarnate routes
    // (Strength = enhancement increment, Current/Absolute/Maximum = direct buff, Resistance
    // = resist, IgnoreED = the ED-bypass split). This is a compact descriptor (no
    // magnitudes/tables): it drives which aspects need routing, not their values.
    private static void DumpIncarnates(string outDir)
    {
        var db = DatabaseAPI.Database;
        var sets = new List<object>();
        // Character-affecting incarnate slots only: group "Incarnate" (drops the
        // "Incarnate_Pets" summon zoo) and skip the Lore pet-summoner sets. Lore summons
        // via Create_Entity, which is Totals-neutral for the character — Mids does not fold
        // pet buffs into the static build totals, so neither does the port (nothing to
        // catalog for parity).
        var incarnateSlots = db.Powersets.Where(p =>
            p is {SetType: Enums.ePowerSetType.Incarnate, GroupName: "Incarnate"}
            && !p.FullName.StartsWith("Incarnate.Lore", StringComparison.Ordinal));
        foreach (var ps in incarnateSlots)
        {
            // The delivered-aspect signature: the distinct (EffectType, ETModifies, Aspect,
            // IgnoreED) tuples this slot's powers apply — its own direct effects and the
            // effects of its GrantPower-granted sub-powers. Collapsed across the slot's
            // powers/types so the catalog is the CP6.2 routing driver (which aspects need
            // handling), not a per-power value dump. DamageType/MezType are summarized to
            // whether the aspect spans typed variants.
            var sig = new SortedDictionary<string, HashSet<string>>();
            foreach (var pw in ps.Powers.Where(p => p != null))
            {
                foreach (var fx in pw.Effects)
                {
                    AddAspectSignature(sig, fx, false);
                    if (fx.EffectType != Enums.eEffectType.GrantPower || fx.nSummon <= -1
                        || fx.nSummon >= db.Power.Length || db.Power[fx.nSummon] == null)
                    {
                        continue;
                    }

                    var grantedEnh = db.Power[fx.nSummon].Effects.Where(e =>
                        e.EffectType is Enums.eEffectType.Enhancement or Enums.eEffectType.DamageBuff);
                    foreach (var sfx in grantedEnh)
                    {
                        AddAspectSignature(sig, sfx, true);
                    }
                }
            }

            sets.Add(new
            {
                ps.FullName,
                ps.DisplayName,
                PowerCount = ps.Powers.Count(p => p != null),
                DeliveredAspects = sig.Select(kv => new { Aspect = kv.Key, Types = kv.Value.OrderBy(x => x).ToList() }),
            });
        }
        WriteJson(outDir, "incarnate_catalog.json", sets);
    }

    // Fold one effect into the powerset's aspect signature. The key is a stable
    // "EffectType|ETModifies|Aspect|IgnoreED|src" string; the value collects the
    // DamageType/MezType variants seen (so a typed Defense shows its type span without a
    // row per type).
    private static void AddAspectSignature(IDictionary<string, HashSet<string>> sig, IEffect fx, bool fromSub)
    {
        var key = string.Join("|",
            fx.EffectType.ToString(),
            fx.ETModifies.ToString(),
            fx.Aspect.ToString(),
            fx.IgnoreED ? "IgnoreED" : "ED",
            fromSub ? "granted" : "direct");
        if (!sig.TryGetValue(key, out var types))
        {
            types = new HashSet<string>();
            sig[key] = types;
        }
        if (fx.DamageType != Enums.eDamage.None)
        {
            types.Add(fx.DamageType.ToString());
        }
        if (fx.MezType != Enums.eMez.None)
        {
            types.Add(fx.MezType.ToString());
        }
    }

    // Builds recomputed at a lowered ForceLevel: the exemplar parity fixtures. At a
    // low ForceLevel, powers picked above it drop their set bonuses (pick Level >
    // ForceLevel) and slots placed at/above it stop contributing — the CP6 exemplar
    // gate the port threads through config.force_level. No -3 retention (Mids gates
    // purely on pick Level <= ForceLevel).
    private static readonly (string Build, int ForceLevel)[] ExemplarVariants =
    {
        ("shield_scrapper_set_bonuses", 25),
    };

    private static int DumpExemplarVariants(IReadOnlyDictionary<string, string> byName, string outDir)
    {
        var failures = 0;
        var savedForceLevel = MidsContext.Config.ForceLevel;
        foreach (var (buildName, forceLevel) in ExemplarVariants)
        {
            if (!byName.TryGetValue(buildName, out var mbdPath))
            {
                Console.Error.WriteLine($"E21: exemplar-variant source build not found: {buildName}");
                failures++;
                continue;
            }

            var toon = new clsToonX();
            MainModule.MidsController.Toon = toon;
            if (!BuildManager.Instance.LoadFromFile(mbdPath))
            {
                Console.Error.WriteLine($"E13: LoadFromFile failed for {mbdPath} (exemplar variant)");
                failures++;
                continue;
            }

            MidsContext.Config.ForceLevel = forceLevel;
            toon.GenerateBuffedPowerArray();
            DumpOneBuild(Path.Combine(outDir, "builds", $"{buildName}_exemplar{forceLevel}"), toon);
        }

        MidsContext.Config.ForceLevel = savedForceLevel;
        return failures;
    }

    private static void DumpBuildPowersEffects(string buildDir, clsToonX toon)
    {
        // The build's resolved DB powers with the full magnitude-driving effect
        // field set. The Python port computes base totals from THESE records; the
        // enum-valued fields are dumped as names (enums.json maps them to ordinals).
        //
        // Limitation: this dumps the raw master-DB power (db.Power[NIDPower]),
        // whose Effects are the pre-assembly set. GBPA assembly can add effects
        // that never appear here — sub-power absorption (GBPA_AddSubPowerEffects)
        // and power-override redirects (GBPA_ApplyPowerOverride). The paired
        // totals.json comes from GenerateBuffedPowerArray, so for a build with
        // such powers the two would disagree. No committed fixture uses one; a
        // fixture that does will need this widened to the assembled _buffedPowers.
        var db = DatabaseAPI.Database;
        var powers = new List<object>();
        for (var i = 0; i < toon.CurrentBuild.Powers.Count; i++)
        {
            var entry = toon.CurrentBuild.Powers[i];
            if (entry == null || entry.NIDPower < 0)
            {
                continue;
            }

            var pw = db.Power[entry.NIDPower];
            if (pw == null)
            {
                continue;
            }

            powers.Add(new
            {
                BuildIndex = i,
                entry.NIDPower,
                pw.FullName,
                pw.StaticIndex,
                PowerType = pw.PowerType.ToString(),
                pw.ForcedClass,
                pw.ClickBuff,
                // pw.Level is the DB minimum pick level; PickLevel is the level this
                // build actually took the power (PowerEntry.Level). The set-bonus
                // exemplar gate (Build.cs:1160) uses the PICK level, not the DB
                // minimum — the two coincide at ForceLevel 50 but diverge under
                // exemplar (a power whose DB min is 14 but picked at 30 drops at
                // ForceLevel 25). Dump both; the port gates on PickLevel.
                pw.Level,
                PickLevel = entry.Level,
                pw.EndCost,
                pw.ActivatePeriod,
                pw.ToggleCost,
                // Base scalars the derived-stat layer (CP6.1) needs as inputs: it
                // computes the buffed recharge/end-per-sec/DPS from these and the
                // slotted + global enhancement, then validates against enhanced_powers.
                // CastTime is not recharge-reduced (base == buffed here) but feeds the
                // end/sec and DPS cadence.
                pw.RechargeTime,
                CastTime = pw.CastTimeBase,
                pw.InterruptTime,
                // eEnhance aspects this power ignores (Power.IgnoreEnh). Pass 3 folds
                // the global _selfEnhance term into a per-power scalar only when the
                // aspect is NOT ignored (IgnoreEnhancement, Power.cs:1673) — e.g. One
                // with the Shield ignores RechargeTime, so its recharge stays at base.
                IgnoreEnh = pw.IgnoreEnh.Select(e => e.ToString()).ToList(),
                pw.VariableEnabled,
                entry.StatInclude,
                entry.VariableValue,
                // Whether the player spent one of the 24 picks on this power. Mids adds
                // powers the build never picked -- auto-granted sub-powers (Mystic Flight
                // grants Translocation), inherents and incarnates all come in through
                // AddPower with chosen=false. Group-name checks catch the last two; only
                // this flag distinguishes an auto-granted POOL sub-power, which otherwise
                // looks exactly like a real pool pick to the hard-limits validator.
                entry.Chosen,
                // The enhancement set types this power accepts (Power.SetTypes,
                // the raw eSetType ordinals as List<int>). An IO set is slottable
                // here only if its SetType ordinal is in this list — the power<->set
                // acceptance map the legality layer (and the set-bonus tally's
                // validity assumption) rests on. Empty means the power takes no set
                // IOs (e.g. Battle Agility). eSetType names are in enums.json.
                pw.SetTypes,
                // The enhancement class IDs this power accepts (Power.Enhancements,
                // int[] of EnhancementClasses[..].ID). A standard/generic IO is legal
                // here only if one of its ClassIds is in this list (IsEnhancementValid);
                // the standard-IO half of legality CP6 adds atop CP5's set-type gate.
                pw.Enhancements,
                // Power availability prerequisites (Power.Requires, Requirement.cs).
                // The hard-limits validator ports Build.MeetsRequirement over these:
                // RequiresPowers is an OR list of AND-pairs of prerequisite power
                // full-names (pool/ancillary tier unlocks live here, e.g. "take 2 of
                // the first 3" = (A&B)|(A&C)|(B&C)); RequiresPowersNot is the forbidden
                // (mutex) list; the Class allow/deny lists gate by archetype. Full-name
                // based so the port compares strings and needs no index cache or
                // expression evaluator (availability is a structured comparison, not
                // Expressions.Parse).
                RequiresPowers = pw.Requires.PowerID,
                RequiresPowersNot = pw.Requires.PowerIDNot,
                RequiresClass = pw.Requires.ClassName,
                RequiresClassNot = pw.Requires.ClassNameNot,
                Effects = pw.Effects.Select((fx, fxIdx) => SerializeEffect(fx, fxIdx)).ToList(),
            });
        }
        WriteJson(buildDir, "powers_effects.json", powers);
    }

    // Shared effect serializer: the full magnitude-driving field set the Python
    // port's effect model reads. Used for build powers (powers_effects.json) and
    // the referenced set_bonus_powers the virtual power clones effects from.
    private static object SerializeEffect(IEffect fx, int idx)
    {
        return new
        {
            Index = idx,
            EffectType = fx.EffectType.ToString(),
            DamageType = fx.DamageType.ToString(),
            MezType = fx.MezType.ToString(),
            ETModifies = fx.ETModifies.ToString(),
            fx.Scale,
            fx.nMagnitude,
            fx.nDuration,
            AttribType = fx.AttribType.ToString(),
            Aspect = fx.Aspect.ToString(),
            fx.ModifierTable,
            fx.nModifierTable,
            ToWho = fx.ToWho.ToString(),
            PvMode = fx.PvMode.ToString(),
            Stacking = fx.Stacking.ToString(),
            Suppression = (int)fx.Suppression,
            fx.Buffable,
            fx.Resistible,
            fx.IgnoreED,
            fx.IgnoreScaling,
            fx.VariableModified,
            fx.BaseProbability,
            fx.Probability,
            fx.ProcsPerMinute,
            fx.Ticks,
            fx.DelayedTime,
            EffectClass = fx.EffectClass.ToString(),
            SpecialCase = fx.SpecialCase.ToString(),
            fx.nIDClassName,
            fx.Absorbed_Effect,
            Absorbed_PowerType = fx.Absorbed_PowerType.ToString(),
            fx.Absorbed_Class_nID,
            ActiveConditionalsCount = fx.ActiveConditionals?.Count ?? 0,
            ExprMagnitude = fx.Expressions?.Magnitude ?? "",
            ExprDuration = fx.Expressions?.Duration ?? "",
            ExprProbability = fx.Expressions?.Probability ?? "",
            fx.DisplayPercentage,
        };
    }

    // Light serializer for the set-bonus virtual-power golden: the structural
    // identity the port keys effects by, plus the resolved Mag/BuffedMag. The
    // port assembles its virtual power by cloning set_bonus_powers effects (full
    // field set there) and validates the sequence, count, and magnitude here.
    private static object SerializeVirtualEffect(IEffect fx, int idx)
    {
        return new
        {
            Index = idx,
            EffectType = fx.EffectType.ToString(),
            DamageType = fx.DamageType.ToString(),
            MezType = fx.MezType.ToString(),
            ETModifies = fx.ETModifies.ToString(),
            fx.Mag,
            fx.BuffedMag,
        };
    }

    private static void DumpBuildSlots(string buildDir, clsToonX toon)
    {
        // The resolved per-power slot layout: for each build power, its SlotEntry
        // array as Mids resolved it (I9Slot already clamped Grade/IOLevel and
        // resolved the UID to an Enh nID). BuildIndex matches powers_effects.json.
        // The port aggregates enhancement value per aspect over these slots,
        // gating each by Enh > -1 and Level < ForceLevel (clsToonX.cs:1751), so
        // Level here is Mids' internal (0-based) slot-placement level, dumped raw.
        var builds = new List<object>();
        for (var i = 0; i < toon.CurrentBuild.Powers.Count; i++)
        {
            var entry = toon.CurrentBuild.Powers[i];
            if (entry == null || entry.NIDPower < 0 || entry.Slots == null)
            {
                continue;
            }

            builds.Add(new
            {
                BuildIndex = i,
                Slots = entry.Slots.Select(s => new
                {
                    s.Level,
                    s.IsInherent,
                    s.Enhancement.Enh,
                    Grade = (int)s.Enhancement.Grade,
                    GradeName = s.Enhancement.Grade.ToString(),
                    s.Enhancement.IOLevel,
                    RelativeLevel = (int)s.Enhancement.RelativeLevel,
                    RelativeLevelName = s.Enhancement.RelativeLevel.ToString(),
                }).ToList(),
            });
        }
        WriteJson(buildDir, "slots.json", builds);
    }

    private static void DumpBuildEnhancedPowers(string buildDir, clsToonX toon)
    {
        // The per-power enhanced (buffed) values GenerateBuffedPowerArray produced:
        // GetEnhancedPower(i) returns _buffedPowers[i] with the enhancement
        // multipliers already applied (RechargeTime/EndCost divided by their
        // multiplier, Range multiplied, each effect's Math_Mag = base * mult).
        // This is the per-power, per-aspect float32 reference the enhancement pipeline is
        // validated against (e.g. Hasten recharge 0.9908 surfaces as a reduced
        // RechargeTime, never in Totals).
        var powers = new List<object>();
        for (var i = 0; i < toon.CurrentBuild.Powers.Count; i++)
        {
            var entry = toon.CurrentBuild.Powers[i];
            if (entry == null || entry.NIDPower < 0)
            {
                continue;
            }

            var buffed = toon.GetEnhancedPower(i);
            if (buffed == null)
            {
                continue;
            }

            // Base (unenhanced, assembled) scalars: the port multiplies these by
            // its computed enhancement multipliers and must reproduce `buffed`.
            // Dumping them keeps the per-power parity assertion free of magic
            // base constants.
            var basePower = toon.GetBasePower(i);

            powers.Add(new
            {
                BuildIndex = i,
                buffed.FullName,
                buffed.Accuracy,
                buffed.AccuracyMult,
                buffed.EndCost,
                buffed.InterruptTime,
                buffed.Range,
                buffed.RechargeTime,
                buffed.CastTime,
                // Mids' authoritative damage-per-activation (FXGetDamageValue under
                // the default Numeric/Average config, dumped in config.json): the
                // reference the port's summed damage validates against; DPS is that
                // over the (recharge + cast + interrupt) cadence.
                DamagePerActivation = buffed.FXGetDamageValue(),
                Base = basePower == null
                    ? null
                    : new
                    {
                        basePower.Accuracy,
                        basePower.EndCost,
                        basePower.InterruptTime,
                        basePower.Range,
                        basePower.RechargeTime,
                        CastTime = basePower.CastTimeBase,
                    },
                Effects = buffed.Effects.Select((fx, fxIdx) => new
                {
                    Index = fxIdx,
                    // Structural identity so the parity test matches effects by
                    // (type, damage, mez, ETModifies) rather than array position:
                    // GBPA assembly can reorder/trim the buffed effect array
                    // relative to the raw powers_effects dump.
                    EffectType = fx.EffectType.ToString(),
                    DamageType = fx.DamageType.ToString(),
                    MezType = fx.MezType.ToString(),
                    ETModifies = fx.ETModifies.ToString(),
                    fx.Math_Mag,
                    fx.Math_Duration,
                }).ToList(),
            });
        }
        WriteJson(buildDir, "enhanced_powers.json", powers);
    }

    private static void DumpBuildTotals(string buildDir, clsToonX toon)
    {
        WriteJson(buildDir, "totals.json", new
        {
            Class = toon.Archetype?.ClassName,
            toon.Level,
            Totals = MapTotals(toon.Totals),
            TotalsCapped = MapTotals(toon.TotalsCapped),
        });
    }

    private static object MapTotals(Character.TotalStatistics t)
    {
        return new
        {
            t.Def,
            t.Res,
            t.Mez,
            t.MezRes,
            t.DebuffRes,
            t.Elusivity,
            t.HPRegen,
            t.HPMax,
            t.Absorb,
            t.EndRec,
            t.EndUse,
            t.EndMax,
            t.RunSpd,
            t.MaxRunSpd,
            t.JumpSpd,
            t.MaxJumpSpd,
            t.FlySpd,
            t.MaxFlySpd,
            t.JumpHeight,
            t.StealthPvE,
            t.StealthPvP,
            t.ThreatLevel,
            t.Perception,
            t.BuffHaste,
            t.BuffAcc,
            t.BuffToHit,
            t.BuffDam,
            t.BuffEndRdx,
            t.BuffRange,
            t.BuffHeal,
        };
    }

    private static void DumpEnhancements(string outDir)
    {
        // StaticIndex -> {UID, TypeID}: the map the .mxd slot reader needs to know
        // how many bytes follow each enhancement reference (the TypeID switch in
        // ReadSlotData). The in-memory nID is the array index.
        //
        // The legality layer (CP6) additionally reads:
        //   ClassIds  - Enhancement.ClassID[] resolved from EnhancementClasses index
        //               to the class .ID (int); Power.IsEnhancementValid tests these
        //               against Power.Enhancements. Resolving here keeps the port from
        //               needing the EnhancementClasses index table at check time.
        //   SubTypeId - GetValidEnhancements' subtype gate (0 = wildcard).
        //   Unique / MutExId / Superior / LongName - Build.EnhancementTest's build-wide
        //               unique / mutual-exclusion (superior-attuned, stealth) rules.
        var db = DatabaseAPI.Database;
        var classes = db.EnhancementClasses;
        var enhancements = db.Enhancements
            .Select((e, nid) => (e, nid))
            .Where(t => t.e != null)
            .Select(t => new
            {
                Nid = t.nid,
                t.e.StaticIndex,
                t.e.UID,
                t.e.LongName,
                TypeId = (int)t.e.TypeID,
                TypeName = t.e.TypeID.ToString(),
                SubTypeId = t.e.SubTypeID,
                // The owning set (-1 for non-set IOs); the SetO legality branch
                // resolves the set's SetType via this and tests it against Power.SetTypes.
                t.e.nIDSet,
                // ClassID indexes EnhancementClasses; the ID we test against
                // Power.Enhancements is EnhancementClasses[idx].ID. Guard the index
                // (a stray -1/out-of-range entry contributes no class).
                ClassIds = (t.e.ClassID ?? Array.Empty<int>())
                    .Where(idx => idx >= 0 && idx < classes.Length)
                    .Select(idx => classes[idx].ID)
                    .ToList(),
                t.e.Unique,
                MutExId = (int)t.e.MutExID,
                MutExName = t.e.MutExID.ToString(),
                t.e.Superior,
            })
            .ToList();
        WriteJson(outDir, "enhancements.json", enhancements);
    }

    // The (power, enhancement) pairs whose legality verdict CP6 asserts against.
    // Illegal slots cannot round-trip through a loaded build (CheckAndFixAllEnhancements
    // strips them on load), so the port validates its reject against Mids' own
    // IsEnhancementValid verdict for each pair rather than a loaded build.
    private static readonly (string Power, string Enh)[] LegalityProbePairs =
    {
        // Resolved rejections (mids-port-spec § legality-predicates):
        // a generic Recharge IO is class-illegal in One with the Shield (its
        // Power.Enhancements has no Recharge class); Force Feedback (a Knockback set)
        // is set-type-illegal in Soul Drain (SetTypes has no Knockback).
        ("Scrapper_Defense.Shield_Defense.One_with_the_Shield", "Crafted_Recharge"),
        ("Scrapper_Melee.Dark_Melee.Soul_Drain", "Crafted_Force_Feedback_A"),
        // A control legal placement: a Recharge IO in Hasten (accepts Recharge).
        ("Pool.Speed.Hasten", "Crafted_Recharge"),
    };

    private static void DumpLegalityProbe(string outDir)
    {
        // Per named pair: Mids' IsEnhancementValid verdict plus the inputs the port's
        // legality port reads (Power.Enhancements / SetTypes, enh ClassIds / set type).
        var db = DatabaseAPI.Database;
        var probes = new List<object>();
        foreach (var (powerName, enhUid) in LegalityProbePairs)
        {
            var power = DatabaseAPI.GetPowerByFullName(powerName);
            var enhNid = DatabaseAPI.GetEnhancementByUIDName(enhUid);
            if (power == null || enhNid < 0)
            {
                Console.Error.WriteLine($"E20: legality-probe pair unresolved: power={powerName} enh={enhUid}");
                continue;
            }

            var enh = db.Enhancements[enhNid];
            probes.Add(new
            {
                PowerFullName = powerName,
                power.SetTypes,
                PowerEnhancements = power.Enhancements,
                EnhUid = enhUid,
                EnhNid = enhNid,
                EnhTypeName = enh.TypeID.ToString(),
                EnhNidSet = enh.nIDSet,
                // IsValid is the reference verdict the port's IsEnhancementValid port
                // must reproduce for each pair (two false, one true).
                IsValid = power.IsEnhancementValid(enhNid),
            });
        }
        WriteJson(outDir, "legality_probe.json", probes);
    }

    private static void DumpEnhancementEffects(string outDir)
    {
        // Per-enhancement effect data the value pipeline consumes: TypeID +
        // Superior gate the schedule multiplier; each Enhancement-mode sEffect
        // carries the aspect (Enhance.ID as eEnhance, SubID for Mez), the ED
        // schedule, the per-effect Multiplier, and the buff/debuff sign gate.
        // Only Mode==Enhancement effects contribute numeric aspects
        // (GetEnhancementEffect, I9Slot.cs:47), so FX/proc effects are dropped.
        // Keyed by the in-memory nID (the array index enhancements.json also
        // exposes), so the port matches this to a resolved slot's Enh.
        var db = DatabaseAPI.Database;
        var records = db.Enhancements
            .Select((e, nid) => (e, nid))
            .Where(t => t.e != null)
            .Select(t => new
            {
                Nid = t.nid,
                t.e.StaticIndex,
                TypeId = (int)t.e.TypeID,
                TypeName = t.e.TypeID.ToString(),
                t.e.Superior,
                t.e.nIDSet,
                Effects = t.e.Effect
                    .Where(fx => fx.Mode == Enums.eEffMode.Enhancement)
                    .Select(fx => new
                    {
                        Mode = fx.Mode.ToString(),
                        BuffMode = fx.BuffMode.ToString(),
                        EnhanceId = fx.Enhance.ID,
                        EnhanceName = ((Enums.eEnhance)fx.Enhance.ID).ToString(),
                        EnhanceSubId = fx.Enhance.SubID,
                        Schedule = (int)fx.Schedule,
                        ScheduleName = fx.Schedule.ToString(),
                        fx.Multiplier,
                    })
                    .ToList(),
            })
            .ToList();
        WriteJson(outDir, "enhancement_effects.json", records);
    }

    private static void DumpEnhancementSets(string outDir)
    {
        // Per enhancement SET (keyed by nID = array index): the tier + special
        // bonus definitions the port assembles the set-bonus virtual power from.
        // Bonus[] are the tiered (>=2 pieces) count bonuses; SpecialBonus[] are the
        // per-enhancement globals/uniques (>=1 piece), paired 1:1 with Enhancements[]
        // by index. Index[] values are global set_bonus Power ids (see
        // set_bonus_powers.json); Slotted is the 1-based piece threshold; PvMode
        // gates tier activation. The port never guesses these — it reads them here.
        var sets = DatabaseAPI.Database.EnhancementSets
            .Select((s, nid) => (s, nid))
            .Where(t => t.s != null)
            .Select(t => new
            {
                Nid = t.nid,
                t.s.Uid,
                t.s.DisplayName,
                t.s.SetType,
                // The eSetType name this set maps to; matched against a power's
                // SetTypes to decide whether the set is slottable in that power.
                SetTypeName = ((Enums.eSetType)t.s.SetType).ToString(),
                t.s.Enhancements,
                Bonus = t.s.Bonus.Select(b => new
                {
                    b.Slotted,
                    PvMode = (int)b.PvMode,
                    PvModeName = b.PvMode.ToString(),
                    b.Index,
                }).ToList(),
                SpecialBonus = t.s.SpecialBonus.Select(b => new
                {
                    b.Slotted,
                    PvMode = (int)b.PvMode,
                    b.Index,
                }).ToList(),
            })
            .ToList();
        WriteJson(outDir, "enhancement_sets.json", sets);
    }

    private static void DumpSetBonusPowers(string outDir)
    {
        // The bonus powers referenced by any EnhancementSet Bonus/SpecialBonus
        // Index[]: the exact key space the set-bonus virtual power clones effects
        // from and that the Rule-of-Five counter is keyed on (Mids' setCount is
        // sized to the whole Power array because NidPowers("set_bonus") does not
        // resolve a real powerset — it returns every id; only these referenced ids
        // are ever counted). Keyed by global Power id. MyPet* mirror
        // ShouldSkipEffects (Build.cs:1229-1234): a bonus whose Target AND
        // EntitiesAffected are both MyPet-flagged is excluded from the character's
        // own totals (but still consumes a Rule-of-Five slot).
        var db = DatabaseAPI.Database;
        var referenced = new SortedSet<int>();
        foreach (var set in db.EnhancementSets.Where(s => s != null))
        {
            foreach (var b in set.Bonus.Concat(set.SpecialBonus))
            {
                foreach (var id in b.Index.Where(id => id > -1))
                {
                    referenced.Add(id);
                }
            }
        }

        var powers = referenced
            .Where(id => id < db.Power.Length && db.Power[id] != null)
            .Select(id =>
            {
                var pw = db.Power[id];
                return new
                {
                    PowerId = id,
                    pw.FullName,
                    PowerType = pw.PowerType.ToString(),
                    MyPetTarget = pw.Target.HasFlag(Enums.eEntity.MyPet),
                    MyPetEntities = pw.EntitiesAffected.HasFlag(Enums.eEntity.MyPet),
                    Effects = pw.Effects.Select((fx, fxIdx) => SerializeEffect(fx, fxIdx)).ToList(),
                };
            })
            .ToList();
        WriteJson(outDir, "set_bonus_powers.json", powers);
    }

    private static void DumpBuildSetBonusVirtualPower(string buildDir, clsToonX toon)
    {
        // The GOLDEN for the port's assembled set-bonus virtual power. After
        // GenerateBuffedPowerArray, CurrentBuild.SetBonusVirtualPower holds the
        // cloned, Rule-of-Five-filtered bonus effects Mids folds into totals. The
        // port assembles its own virtual power from enhancement_sets + set_bonus_powers
        // and must reproduce these effects (structural key + Mag) before the fold.
        var vp = toon.CurrentBuild?.SetBonusVirtualPower;
        var effects = vp?.Effects.Select((fx, fxIdx) => SerializeVirtualEffect(fx, fxIdx)).ToList()
                      ?? new List<object>();
        WriteJson(buildDir, "set_bonus_virtual_power.json", new { EffectCount = effects.Count, Effects = effects });
    }

    private static void ExportMxdBuilds(string samplesDir, string outDir)
    {
        if (!Directory.Exists(samplesDir))
        {
            Console.Error.WriteLine($"E09: samples dir not found: {samplesDir}");
            return;
        }

        var mxdDir = Path.Combine(outDir, "mxd");
        Directory.CreateDirectory(mxdDir);

        // Recurse: samples/builds is split into community/ and operator-builds/ subfolders.
        foreach (var mbdPath in Directory.EnumerateFiles(samplesDir, "*.mbd", SearchOption.AllDirectories))
        {
            // A fresh character per build so an earlier load never bleeds into the next.
            MainModule.MidsController.Toon = new clsToonX();
            if (!BuildManager.Instance.LoadFromFile(mbdPath))
            {
                Console.Error.WriteLine($"E10: LoadFromFile failed for {mbdPath}");
                continue;
            }

            var mxd = MidsCharacterFileFormat.MxDBuildSaveString(true, false);
            if (string.IsNullOrEmpty(mxd))
            {
                Console.Error.WriteLine($"E11: MxDBuildSaveString returned empty for {mbdPath}");
                continue;
            }

            var outPath = Path.Combine(mxdDir, Path.GetFileNameWithoutExtension(mbdPath) + ".mxd");
            File.WriteAllText(outPath, mxd);
            Console.WriteLine($"exported {outPath}");
        }
    }

    private static void WriteJson(string outDir, string name, object payload)
    {
        var path = Path.Combine(outDir, name);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOpts));
        Console.WriteLine($"wrote {path}");
    }

    private static void DumpVersion(string outDir)
    {
        WriteJson(outDir, "version.json", new
        {
            DatabaseVersion = DatabaseAPI.Database.Version.ToString(),
            DatabaseAPI.DatabaseName,
            DumpedAtUtc = DateTime.UtcNow.ToString("o"),
        });
    }

    private static void DumpServerData(string outDir)
    {
        var sd = DatabaseAPI.ServerData;
        WriteJson(outDir, "server_data.json", new
        {
            sd.BaseToHit,
            sd.MaxSlots,
            sd.EnableInherentSlotting,
            sd.BaseFlySpeed,
            sd.BaseJumpSpeed,
            sd.BaseJumpHeight,
            sd.BasePerception,
            sd.BaseRunSpeed,
            sd.MaxFlySpeed,
            sd.MaxJumpSpeed,
            sd.MaxJumpHeight,
            sd.MaxRunSpeed,
            sd.MaxMaxFlySpeed,
            sd.MaxMaxJumpSpeed,
            sd.MaxMaxRunSpeed,
        });
    }

    private static void DumpArchetypes(string outDir)
    {
        var ats = DatabaseAPI.Database.Classes.Select((at, idx) => new
        {
            Index = idx,
            at.ClassName,
            at.DisplayName,
            ClassType = at.ClassType.ToString(),
            at.Column,
            at.Playable,
            at.Hitpoints,
            at.HPCap,
            at.ResCap,
            at.DamageCap,
            at.RechargeCap,
            at.RecoveryCap,
            at.RegenCap,
            at.PerceptionCap,
            at.BaseRecovery,
            at.BaseRegen,
            at.BaseThreat,
        });
        WriteJson(outDir, "archetypes.json", ats.ToList());
    }

    private static void DumpMaths(string outDir)
    {
        var db = DatabaseAPI.Database;
        WriteJson(outDir, "maths.json", new
        {
            db.MultED,
            db.MultTO,
            db.MultDO,
            db.MultSO,
            db.MultHO,
            db.MultIO,
        });
    }

    private static void DumpEnhancementClasses(string outDir)
    {
        var classes = DatabaseAPI.Database.EnhancementClasses.Select(c => new
        {
            c.ID,
            c.ClassID,
            c.Name,
            c.ShortName,
            c.Desc,
        });
        WriteJson(outDir, "enhancement_classes.json", classes.ToList());
    }

    private static void DumpPowerIndex(string outDir)
    {
        // StaticIndex -> FullName map: the resolution layer .mxd build files need.
        var powers = DatabaseAPI.Database.Power
            .Where(p => p != null)
            .Select(p => new { p!.StaticIndex, p.FullName })
            .ToList();
        WriteJson(outDir, "power_static_index.json", powers);
    }
}
