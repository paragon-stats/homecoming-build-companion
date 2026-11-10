using System.Text.Json;

using CohEngine.Core.DamageLayers;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Chain;

/// <summary>Attack classification: one canonical power to one <see cref="AttackProfile"/>.</summary>
public static class AttackProfiles
{
    private const string PveAlways = "target>enttype eq 'critter'";

    private static readonly string[] SelfTargets = ["Self", "SelfAndPets"];
    private static readonly string[] PetCategories = ["Pet Damage", "Recharge Intensive Pets"];
    private static readonly string[] MeleeCategories = ["Melee Damage", "Melee AoE Damage"];
    private static readonly string[] RangedCategories = ["Ranged Damage", "Ranged AoE Damage", "Sniper Attacks"];
    private static readonly string[] ShapeNames = ["SingleTarget", "Cone", "Location", "Chain"];

    /// <summary>Whether an effect group fires in PvE: not PvP-only, and unconditional or gated only on the critter test.</summary>
    /// <param name="effect">The effect group.</param>
    /// <returns>Whether it applies.</returns>
    public static bool AppliesInPve(JsonElement effect)
    {
        if (string.Equals(effect.Str("is_pvp"), "PVP_ONLY", StringComparison.Ordinal))
        {
            return false;
        }

        string requires = effect.Str("requires_expression").Trim();
        return requires.Length == 0 || string.Equals(requires, PveAlways, StringComparison.Ordinal);
    }

    /// <summary>Every template of the effects, recursing through child effects.</summary>
    /// <param name="effects">The effect groups.</param>
    /// <returns>The templates.</returns>
    public static IEnumerable<JsonElement> Templates(IEnumerable<JsonElement> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        foreach (JsonElement effect in effects)
        {
            foreach (JsonElement template in effect.Items("templates"))
            {
                yield return template;
            }

            foreach (JsonElement template in Templates(effect.Items("child_effects")))
            {
                yield return template;
            }
        }
    }

    /// <summary>Whether one attribute is foe -Res: direction comes from the table name, the plain table's sign decides.</summary>
    /// <param name="attrib">The attribute.</param>
    /// <param name="aspect">The aspect.</param>
    /// <param name="target">The target.</param>
    /// <param name="table">The modifier table.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>Whether it is foe -Res.</returns>
    public static bool FoeRes(string attrib, string aspect, string target, string table, double scale)
    {
        ArgumentNullException.ThrowIfNull(attrib);
        ArgumentNullException.ThrowIfNull(table);
        return attrib.EndsWith("_Dmg", StringComparison.Ordinal)
            && !string.Equals(attrib, "Heal_Dmg", StringComparison.Ordinal)
            && string.Equals(aspect, "Resistance", StringComparison.Ordinal)
            && !SelfTargets.Contains(target)
            && table.Contains("Res_Dmg", StringComparison.Ordinal)
            && !table.Contains("Buff", StringComparison.Ordinal)
            && (table.Contains("Debuff", StringComparison.Ordinal) || scale < 0);
    }

    /// <summary>The PvE-applicable templates that put -Res on foes, from the power itself and its linked powers.</summary>
    /// <param name="power">The canonical power.</param>
    /// <param name="resolve">The resolver, or null.</param>
    /// <returns>The templates.</returns>
    public static IReadOnlyList<JsonElement> FoeResDebuffTemplates(JsonElement power, PowerResolver? resolve)
    {
        List<JsonElement> own = [.. Templates(power.ReqItems("effects"))];
        List<JsonElement> sources = [power];
        if (resolve is not null)
        {
            sources.AddRange(LinkedPowers(own, resolve));
        }

        return
        [
            .. sources
                .SelectMany(source => PveTemplates(source.ReqItems("effects")))
                .Where(template => template.ReqItems("attribs").Any(attrib => FoeRes(attrib.GetString() ?? string.Empty, template.ReqStr("aspect"), template.ReqStr("target"), template.Str("table"), template.Num("scale", 0.0)))),
        ];
    }

    /// <summary>Classifies a canonical power once: delivery, shape, role and area figures.</summary>
    /// <param name="power">The canonical power.</param>
    /// <param name="meleeRangeFtMax">The melee range threshold.</param>
    /// <param name="resolve">Follows <c>Execute_Power</c> and <c>Create_Entity</c> links one level.</param>
    /// <returns>The profile.</returns>
    /// <exception cref="EngineException">E36 for an unmapped area; E37 for a power that is not damaging, a damage buff or a -Res debuff.</exception>
    public static AttackProfile Classify(JsonElement power, double meleeRangeFtMax = 20.0, PowerResolver? resolve = null)
    {
        Shape shape = ShapeOf(power);
        double range = power.ReqNum("range");
        string[] categories = [.. power.Req("allowed_boostset_cats").EnumerateArray().Select(item => item.GetString() ?? string.Empty)];
        return new AttackProfile(
            power.ReqStr("full_name"),
            range <= meleeRangeFtMax ? Delivery.Melee : Delivery.Ranged,
            shape,
            RoleOf(power, resolve),
            range,
            power.ReqNum("radius"),
            power.ReqNum("arc"),
            (int)power.ReqNum("max_targets_hit"),
            categories,
            power.Strings("modes_required"));
    }

    /// <summary>Describes a conflict between the verdict's delivery and the power's set categories, else null.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The message, or null.</returns>
    public static string? CategoryDisagreement(AttackProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string[] melee = [.. profile.SetCategories.Where(MeleeCategories.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        string[] ranged = [.. profile.SetCategories.Where(RangedCategories.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (profile.Delivery == Delivery.Melee && ranged.Length > 0 && melee.Length == 0)
        {
            return $"{profile.FullName} is classified MELEE (range {PyFloat.General(profile.RangeFt)} ft) but its set categories "
                + $"{PyText.List(ranged)} are ranged; check the melee range threshold or the power's range";
        }

        if (profile.Delivery == Delivery.Ranged && melee.Length > 0 && ranged.Length == 0)
        {
            return $"{profile.FullName} is classified RANGED (range {PyFloat.General(profile.RangeFt)} ft) but its set categories "
                + $"{PyText.List(melee)} are melee; check the melee range threshold or the power's range";
        }

        return null;
    }

    private static bool IsNoModeRedirect(string condition)
    {
        const string prefix = "!Source.Mode?(";
        if (string.Equals(condition, "Always", StringComparison.Ordinal))
        {
            return true;
        }

        return condition.StartsWith(prefix, StringComparison.Ordinal)
            && condition.EndsWith(')')
            && condition.Length > prefix.Length + 1
            && condition[prefix.Length..^1].All(letter => char.IsLetterOrDigit(letter) || letter == '_');
    }

    private static IEnumerable<JsonElement> PveTemplates(IEnumerable<JsonElement> effects)
    {
        foreach (JsonElement effect in effects)
        {
            if (AppliesInPve(effect))
            {
                foreach (JsonElement template in effect.Items("templates"))
                {
                    yield return template;
                }
            }

            foreach (JsonElement template in PveTemplates(effect.Items("child_effects")))
            {
                yield return template;
            }
        }
    }

    private static Shape ShapeOf(JsonElement power)
    {
        string area = power.ReqStr("effect_area");
        if (string.Equals(area, "AoE", StringComparison.Ordinal))
        {
            return power.ReqNum("range") > 0 ? Shape.TargetedAoe : Shape.Pbaoe;
        }

        return area switch
        {
            "SingleTarget" => Shape.SingleTarget,
            "Cone" => Shape.Cone,
            "Location" => Shape.Location,
            "Chain" => Shape.TargetedAoe,
            _ => throw new EngineException(
                $"{ErrorCodes.E36}: {power.ReqStr("full_name")} has effect_area {PyText.Repr(area)}, expected one of "
                + $"{PyText.List(["AoE", .. ShapeNames])}; add the value to attack_profile._SHAPES or filter the power"),
        };
    }

    private static Facts FactsOf(IEnumerable<JsonElement> templates, bool linked)
    {
        Facts found = default;
        foreach (JsonElement template in templates)
        {
            string table = template.Str("table");
            string aspect = template.ReqStr("aspect");
            string target = template.ReqStr("target");
            foreach (JsonElement attribElement in template.ReqItems("attribs"))
            {
                string attrib = attribElement.GetString() ?? string.Empty;
                bool damage = attrib.EndsWith("_Dmg", StringComparison.Ordinal) && !string.Equals(attrib, "Heal_Dmg", StringComparison.Ordinal);
                found.CreatesEntity |= string.Equals(attrib, "Create_Entity", StringComparison.Ordinal);
                found.FoeDamage |= damage && string.Equals(aspect, "Absolute", StringComparison.Ordinal) && !string.Equals(target, "Self", StringComparison.Ordinal);
                found.FoeRes |= FoeRes(attrib, aspect, target, table, template.Num("scale", 0.0));
                if (damage && string.Equals(aspect, "Strength", StringComparison.Ordinal) && table.Contains("Buff_Dmg", StringComparison.Ordinal))
                {
                    if (linked || SelfTargets.Contains(target))
                    {
                        found.SelfBuff = true;
                    }
                    else
                    {
                        found.TeamBuff = true;
                    }
                }
            }
        }

        return found;
    }

    private static IEnumerable<JsonElement> LinkedPowers(IEnumerable<JsonElement> templates, PowerResolver resolve)
    {
        List<string> names = [];
        foreach (JsonElement template in templates)
        {
            JsonElement parameters = template.Opt("params") ?? default;
            List<string> attribs = [.. template.ReqItems("attribs").Select(item => item.GetString() ?? string.Empty)];
            if (attribs.Contains("Execute_Power"))
            {
                names.AddRange(parameters.Strings("power_names"));
            }

            if (attribs.Contains("Create_Entity"))
            {
                names.AddRange(parameters.Strings("power_names"));
                names.AddRange(parameters.Strings("redirects"));
                string? entity = parameters.Str("entity_def");
                if (entity.Length > 0)
                {
                    JsonElement? definition = resolve(entity);
                    names.AddRange((definition?.Opt("defaults") ?? default).Strings("power_full_names"));
                }
            }
        }

        foreach (string name in names)
        {
            JsonElement? linked = resolve(name);
            if (linked is not null)
            {
                yield return linked.Value;
            }
        }
    }

    private static JsonElement? DefaultRedirect(JsonElement power, PowerResolver resolve)
    {
        foreach (JsonElement redirect in power.Items("redirect"))
        {
            string condition = redirect.Str("condition_expression", "Always").Trim();
            if (IsNoModeRedirect(condition))
            {
                return resolve(redirect.ReqStr("name"));
            }
        }

        return null;
    }

    private static Role RoleOf(JsonElement power, PowerResolver? resolve)
    {
        IReadOnlyList<JsonElement> effects = power.ReqItems("effects");
        if (effects.Count == 0 && resolve is not null && DefaultRedirect(power, resolve) is { } target)
        {
            effects = target.ReqItems("effects");
        }

        List<JsonElement> templates = [.. Templates(effects)];
        Facts own = FactsOf(templates, linked: false);
        Facts link = FactsOf(resolve is null ? [] : LinkedPowers(templates, resolve).SelectMany(linkedPower => Templates(linkedPower.ReqItems("effects"))), linked: true);
        bool foeRes = own.FoeRes || link.FoeRes;
        string type = power.ReqStr("type");
        bool persistent = type is "Toggle" or "Auto";
        bool petCategory = power.Req("allowed_boostset_cats").EnumerateArray().Any(item => PetCategories.Contains(item.GetString()));
        if (string.Equals(type, "Click", StringComparison.Ordinal) && own.FoeDamage && !petCategory)
        {
            return Role.Chain;
        }

        if (own.CreatesEntity)
        {
            if (petCategory)
            {
                return Role.PetSummon;
            }

            return foeRes && !link.FoeDamage ? Role.Support : Role.Patch;
        }

        if (own.FoeDamage || link.FoeDamage)
        {
            return persistent ? Role.Aura : Role.Chain;
        }

        if (string.Equals(type, "Click", StringComparison.Ordinal))
        {
            if (foeRes || own.TeamBuff)
            {
                return Role.Support;
            }

            if (own.SelfBuff || link.SelfBuff)
            {
                return Role.BuffClick;
            }
        }
        else if (persistent && foeRes)
        {
            return Role.PersistentDebuff;
        }

        throw new EngineException(
            $"{ErrorCodes.E37}: {power.ReqStr("full_name")} (type {type}) has no entity creation, foe damage, foe -Res or "
            + "click damage buff; filter it out before classifying (Mids already counts self and team damage-buff toggles)");
    }

    private struct Facts
    {
        public bool CreatesEntity;
        public bool FoeDamage;
        public bool FoeRes;
        public bool SelfBuff;
        public bool TeamBuff;
    }
}
