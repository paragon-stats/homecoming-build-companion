using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Pets;

/// <summary>Pet damage: summon powers to <see cref="PetSummon"/> to the pet damage source.</summary>
public static class PetDamage
{
    /// <summary>The pet damage enhancement assumed when a summon has no override: three pet-damage enhancements at the ED ceiling.</summary>
    public const double DefaultPetDamageEnhancement = 0.95;

    private const string Approximation = "pet AI not modelled: each pet runs its best chain; enhancement approximate";

    private static readonly EnduranceBudget Unlimited = new(1.0e9, 0.0, 0.0);

    /// <summary>The AttribMod table index of a name.</summary>
    /// <param name="mods">The tables.</param>
    /// <param name="name">The table name.</param>
    /// <param name="power">The power using it.</param>
    /// <returns>The index.</returns>
    /// <exception cref="EngineException">E61 when the table is missing.</exception>
    public static int ModifierTable(AttribMods mods, string name, string power)
    {
        ArgumentNullException.ThrowIfNull(mods);
        int index = mods.NidFromUidAttribMod(name);
        return index < 0
            ? throw new EngineException($"{ErrorCodes.E61}: {power} uses modifier table {PyText.Repr(name)}, which the AttribMod table lacks; reload AttribMod.json")
            : index;
    }

    /// <summary>Resolves each pet summon power into one <see cref="PetSummon"/> per summoned entity.</summary>
    /// <param name="summonPowers">The summon attacks.</param>
    /// <param name="canonicalRoot">The canonical data root, holding <c>powers/</c> and <c>entities/</c>.</param>
    /// <param name="attribmod">The AttribMod tables.</param>
    /// <param name="classes">The class table.</param>
    /// <param name="casterLevel">The caster level.</param>
    /// <param name="damageEnhancement">Per-summon pet damage enhancement overrides.</param>
    /// <returns>The summons.</returns>
    /// <exception cref="EngineException">E60 to E64 as the Python port raises them.</exception>
    public static IReadOnlyList<PetSummon> BuildPetSummons(
        IEnumerable<ChainAttack> summonPowers,
        string canonicalRoot,
        AttribMods attribmod,
        ArchetypeDb classes,
        int casterLevel = 50,
        IReadOnlyDictionary<string, double>? damageEnhancement = null)
    {
        ArgumentNullException.ThrowIfNull(summonPowers);
        ArgumentNullException.ThrowIfNull(classes);
        string powersRoot = Path.Combine(canonicalRoot, "powers");
        List<PetSummon> result = [];
        foreach (ChainAttack summon in summonPowers)
        {
            string name = summon.FullName;
            if (summon.Profile.Role != Role.PetSummon)
            {
                throw new EngineException($"{ErrorCodes.E64}: {name} has role {summon.Profile.Role.Value()}, expected pet_summon; pass summon powers only");
            }

            double enhancement = damageEnhancement is not null && damageEnhancement.TryGetValue(name, out double over) ? over : DefaultPetDamageEnhancement;
            JsonElement power = CanonicalJson.LoadCanonicalPowers(powersRoot, [name])[name];
            int position = 0;
            foreach (Entity entity in Summoned(power, casterLevel))
            {
                string path = Path.Combine(canonicalRoot, "entities", entity.Name.ToLowerInvariant() + ".json");
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    throw new EngineException($"{ErrorCodes.E60}: entity {entity.Name} (summoned by {name}) has no file {path}; restore the canonical entity");
                }

                JsonElement data = CanonicalJson.ReadJson(path, ErrorCodes.E63);
                int petClass = ClassIndex(data, classes);
                int level = PetLevel(entity.Table, casterLevel, attribmod, classes, petClass, name);
                string[] names = [.. data.Req("defaults").Req("power_full_names").EnumerateArray().Select(item => item.GetString() ?? string.Empty)];
                IReadOnlyDictionary<string, JsonElement> powers;
                try
                {
                    powers = CanonicalJson.LoadCanonicalPowers(powersRoot, names);
                }
                catch (EngineException error)
                {
                    throw new EngineException($"{ErrorCodes.E60}: pet powers of {entity.Name} missing ({error.Message}); restore the canonical files", error);
                }

                List<ChainAttack> attacks = [];
                foreach (JsonElement petPower in powers.Values)
                {
                    AttackProfile? profile = PetProfile(petPower);
                    if (profile is null)
                    {
                        continue;
                    }

                    attacks.Add(new ChainAttack
                    {
                        FullName = petPower.ReqStr("full_name"),
                        Damage = F32.Round(DamagePerActivation(petPower, attribmod, classes, petClass, level) * (1.0 + enhancement)),
                        CastTime = petPower.ReqNum("activation_time"),
                        RechargeTime = petPower.ReqNum("recharge_time"),
                        EndCost = 0.0,
                        Profile = profile,
                        ActivatePeriod = petPower.ReqNum("activate_period"),
                    });
                }

                IReadOnlyList<string> displayNames = data.Items("levels") is { Count: > 0 } levels ? levels[0].Strings("display_names") : [];
                string petName = displayNames.Count > 0 ? displayNames[0] : entity.Name;
                result.Add(new PetSummon
                {
                    SummonName = name,
                    PetName = petName,
                    PetAttacks = attacks,
                    PetCount = entity.Count,
                    Duration = entity.Duration,
                    SummonCastTime = position == 0 ? summon.CastTime : 0.0,
                    SummonRecharge = summon.RechargeTime,
                    SummonEndCost = position == 0 ? summon.EndCost : 0.0,
                    DamageBuffBaseline = F32.Round(1.0 + enhancement),
                    Label = $"{entity.Name} level {level}" + (attacks.Count > 0 ? string.Empty : " (no damaging attacks found)"),
                });
                position++;
            }
        }

        return result;
    }

    /// <summary>Pets' own best chains, scaled by count and by how much of the time the pets are alive.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The result.</returns>
    public static SourceResult? PetSource(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Pets.Count == 0)
        {
            return new SourceResult { Source = DamageModel.SourcePet, Dps = 0.0, Label = "no pets summoned" };
        }

        List<(string Name, double Dps)> detail = [];
        List<(double Dps, double Baseline)> weights = [];
        List<string> uptimes = [];
        double share = 0.0;
        double end = 0.0;
        foreach (PetSummon pet in context.Pets)
        {
            ChainPlan plan = AttackChains.Plan(pet.PetAttacks, Stance.Melee, context.Movement, context.Scenario, Unlimited);
            double single = plan.BestOverall?.Dps ?? 0.0;
            double arcana = pet.SummonCastTime > 0 ? AttackChains.ArcanaCastTime(pet.SummonCastTime) : 0.0;
            double cycle = F32.Round(arcana + pet.SummonRecharge);
            bool timed = !double.IsPositiveInfinity(pet.Duration);
            double uptime = timed && cycle > 0 ? Math.Min(1.0, F32.Round(pet.Duration / cycle)) : 1.0;
            double dps = F32.Round(F32.Round(single * pet.PetCount) * uptime);
            detail.Add((pet.PetName, dps));
            weights.Add((dps, pet.DamageBuffBaseline));
            uptimes.Add($"{pet.PetName} x{pet.PetCount.ToString(CultureInfo.InvariantCulture)} uptime {PyFloat.Percent(uptime, 0)}" + (timed ? string.Empty : " (permanent)"));
            if (timed && cycle > 0)
            {
                share = F32.Round(share + F32.Round(arcana / cycle));
                end = F32.Round(end + F32.Round(pet.SummonEndCost / cycle));
            }
        }

        double total = F32.Round(PyFloat.Sum(detail.Select(item => item.Dps)));
        double baseline = total > 0 ? F32.Round(PyFloat.Sum(weights.Select(item => item.Dps * item.Baseline)) / total) : 1.0;
        return new SourceResult
        {
            Source = DamageModel.SourcePet,
            Dps = total,
            TimelineShare = share,
            EndPerSec = end,
            Detail = detail,
            Label = $"{Approximation}; {string.Join(", ", uptimes)}",
            DamageBuffBaseline = baseline,
        };
    }

    private static IEnumerable<(JsonElement Effect, JsonElement Template)> EffectsOf(IEnumerable<JsonElement> effects)
    {
        foreach (JsonElement effect in effects)
        {
            if (!string.Equals(effect.Str("is_pvp"), "PVP_ONLY", StringComparison.Ordinal))
            {
                foreach (JsonElement template in effect.Items("templates"))
                {
                    yield return (effect, template);
                }
            }

            foreach ((JsonElement, JsonElement) pair in EffectsOf(effect.Items("child_effects")))
            {
                yield return pair;
            }
        }
    }

    private static bool TryNumber(string text, string unit, out double value)
    {
        value = 0.0;
        return string.Equals(unit, "seconds", StringComparison.Ordinal) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static (string Number, string Unit) Split(string text)
    {
        int space = text.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (text, string.Empty) : (text[..space], text[(space + 1)..]);
    }

    private static double Duration(string text, string power)
    {
        if (string.Equals(text, "UntilKilled", StringComparison.Ordinal))
        {
            return double.PositiveInfinity;
        }

        (string number, string unit) = Split(text);
        return TryNumber(number, unit, out double value)
            ? value
            : throw new EngineException($"{ErrorCodes.E62}: {power} Create_Entity duration is {PyText.Repr(text)}, expected 'N seconds' or 'UntilKilled'; extend pet_damage._duration");
    }

    private static double Seconds(string text, string power)
    {
        if (text.Length == 0 || string.Equals(text, "UntilKilled", StringComparison.Ordinal))
        {
            return 0.0;
        }

        (string number, string unit) = Split(text);
        return TryNumber(number, unit, out double value)
            ? value
            : throw new EngineException($"{ErrorCodes.E62}: {power} damage template duration is {PyText.Repr(text)}, expected 'N seconds'; extend pet_damage._seconds");
    }

    /// <summary>How a Create_Entity condition spawns: "" always, null never, else the alternate group's condition.</summary>
    private static string? SpawnKey(string condition, int casterLevel)
    {
        const string head = "source>combatlevel";
        string text = condition.Trim();
        if (!text.StartsWith(head, StringComparison.Ordinal))
        {
            return text;
        }

        string rest = text[head.Length..].TrimStart();
        if (!rest.StartsWith('>'))
        {
            return text;
        }

        string digits = rest[1..].TrimStart();
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            return text;
        }

        return casterLevel > int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture) ? string.Empty : null;
    }

    private static List<Entity> Summoned(JsonElement power, int casterLevel)
    {
        string fullName = power.ReqStr("full_name");
        List<string> order = [];
        Dictionary<string, (double Duration, string Table)> found = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, int>> groups = new(StringComparer.Ordinal);
        foreach ((JsonElement effect, JsonElement template) in EffectsOf(power.ReqItems("effects")))
        {
            if (!template.ReqItems("attribs").Any(item => string.Equals(item.GetString(), "Create_Entity", StringComparison.Ordinal)))
            {
                continue;
            }

            string name = (template.Opt("params") ?? default).Str("entity_def");
            if (name.Length == 0)
            {
                throw new EngineException($"{ErrorCodes.E62}: {fullName} has a Create_Entity template with no params.entity_def; check the canonical power file");
            }

            double duration = Duration(template.ReqStr("duration"), fullName);
            if (!found.ContainsKey(name))
            {
                found[name] = (duration, template.ReqStr("table"));
                order.Add(name);
                groups[name] = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            string? key = SpawnKey(effect.Str("requires_expression"), casterLevel);
            if (key is not null)
            {
                groups[name][key] = groups[name].GetValueOrDefault(key) + 1;
            }
        }

        if (order.Count == 0)
        {
            throw new EngineException($"{ErrorCodes.E62}: {fullName} has no Create_Entity template; pass only pet summon powers");
        }

        List<Entity> entities = [];
        foreach (string name in order)
        {
            int alternates = groups[name].Where(pair => pair.Key.Length > 0).Select(pair => pair.Value).DefaultIfEmpty(0).Max();
            int count = groups[name].GetValueOrDefault(string.Empty) + alternates;
            if (count > 0)
            {
                entities.Add(new Entity(name, count, found[name].Duration, found[name].Table));
            }
        }

        return entities;
    }

    private static int ClassIndex(JsonElement entity, ArchetypeDb classes)
    {
        string raw = entity.Req("defaults").ReqStr("character_class_name");
        foreach (string candidate in new[] { "Class_" + raw, "Class_" + string.Join('_', raw.Split('_').Reverse()) })
        {
            int index = classes.NidFromUidClass(candidate);
            if (index >= 0)
            {
                return index;
            }
        }

        throw new EngineException(
            $"{ErrorCodes.E61}: entity {entity.ReqStr("name")} has class {PyText.Repr(raw)} with no Class_{raw} or reversed-token class in the "
            + "archetype table; add the class to the archetypes dump");
    }

    private static int PetLevel(string table, int casterLevel, AttribMods mods, ArchetypeDb classes, int petClass, string power)
    {
        if (table.EndsWith("ones", StringComparison.OrdinalIgnoreCase))
        {
            return casterLevel;
        }

        int level = (int)EffectMagnitude.Modifier(mods, classes.Classes, petClass, ModifierTable(mods, table, power), casterLevel - 1);
        return level < 1
            ? throw new EngineException($"{ErrorCodes.E61}: table {table} gives pet level {level.ToString(CultureInfo.InvariantCulture)} at caster level {casterLevel.ToString(CultureInfo.InvariantCulture)}; pass a caster_level in 1-50")
            : level;
    }

    private static double DamagePerActivation(JsonElement power, AttribMods mods, ArchetypeDb classes, int petClass, int level)
    {
        double total = 0.0;
        foreach ((JsonElement effect, JsonElement template) in EffectsOf(power.ReqItems("effects")))
        {
            bool foeDamage = string.Equals(template.ReqStr("aspect"), "Absolute", StringComparison.Ordinal)
                && !string.Equals(template.ReqStr("target"), "Self", StringComparison.Ordinal)
                && template.ReqItems("attribs").Any(item => (item.GetString() ?? string.Empty).EndsWith("_Dmg", StringComparison.Ordinal) && !(item.GetString() ?? string.Empty).StartsWith("Heal", StringComparison.Ordinal));
            if (!foeDamage || !AttackProfiles.AppliesInPve(effect) || effect.ReqNum("ppm") > 0)
            {
                continue;
            }

            string fullName = power.ReqStr("full_name");
            double value = Math.Abs(EffectMagnitude.Modifier(mods, classes.Classes, petClass, ModifierTable(mods, template.ReqStr("table"), fullName), level - 1));
            double period = template.ReqNum("application_period");
            double span = Seconds(template.ReqStr("duration"), fullName);
            double ticks = period > 0 && span > 0 ? Math.Floor(span / period) + 1 : 1;
            total += value * template.ReqNum("scale") * template.ReqNum("magnitude") * effect.ReqNum("chance") * ticks;
        }

        return F32.Round(total);
    }

    private static AttackProfile? PetProfile(JsonElement power)
    {
        if (!string.Equals(power.ReqStr("type"), "Click", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            AttackProfile profile = AttackProfiles.Classify(power);
            return profile.Role == Role.Chain ? profile : null;
        }
        catch (EngineException error) when (error.Message.StartsWith(ErrorCodes.E37, StringComparison.Ordinal))
        {
            return null;
        }
    }

    private sealed record Entity(string Name, int Count, double Duration, string Table);
}
