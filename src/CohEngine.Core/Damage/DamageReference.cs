using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>One power's Mids-computed damage inputs.</summary>
/// <param name="FullName">The power's full name.</param>
/// <param name="DamagePerActivation">Mids <c>DamagePerActivation</c>.</param>
/// <param name="CastTime">The buffed cast time.</param>
/// <param name="RechargeTime">The buffed recharge time.</param>
/// <param name="EndCost">The buffed endurance cost.</param>
/// <param name="ActivatePeriod">The toggle or auto tick period.</param>
/// <param name="PowerType">The Mids power type.</param>
/// <param name="StatInclude">Whether the power's buffs feed the build totals.</param>
/// <param name="SelfDamageBuff">The self damage buff fraction.</param>
/// <param name="SelfDamageBuffDuration">The buff duration.</param>
/// <param name="TargetResDebuff">The fraction of target resistance removed.</param>
/// <param name="TargetResDebuffDuration">The debuff duration.</param>
public sealed record PowerDamageRef(
    string FullName,
    double DamagePerActivation,
    double CastTime,
    double RechargeTime,
    double EndCost,
    double ActivatePeriod,
    string PowerType,
    bool StatInclude,
    double SelfDamageBuff,
    double SelfDamageBuffDuration,
    double TargetResDebuff,
    double TargetResDebuffDuration);

/// <summary>Reads the damage inputs Mids computed, standing in for the unported damage port.</summary>
public static class DamageReference
{
    private static readonly string[] CoreTypes = ["Smashing", "Lethal", "Fire", "Cold", "Energy", "Negative", "Toxic", "Psionic"];
    private static readonly string[] Files = ["enhanced_powers.json", "powers_effects.json"];

    /// <summary>Loads a Mids dump build directory into per-power damage references.</summary>
    /// <param name="buildDir">The directory holding <c>enhanced_powers.json</c> and <c>powers_effects.json</c>.</param>
    /// <returns>The references by power full name, in dump order.</returns>
    /// <exception cref="ChainException">E40 to E43 as the Python port raises them.</exception>
    public static IReadOnlyDictionary<string, PowerDamageRef> LoadDamageRefs(string buildDir)
    {
        foreach (string file in Files)
        {
            if (!File.Exists(Path.Combine(buildDir, file)))
            {
                throw new ChainException($"E40: {Path.Combine(buildDir, file)} not found; point build_dir at a Mids dump build directory");
            }
        }

        Dictionary<int, JsonElement> enhanced = ByBuildIndex(Path.Combine(buildDir, Files[0]));
        Dictionary<int, JsonElement> structural = ByBuildIndex(Path.Combine(buildDir, Files[1]));
        if (!enhanced.Keys.ToHashSet().SetEquals(structural.Keys))
        {
            string onlyEnhanced = IntList(enhanced.Keys.Except(structural.Keys));
            string onlyStructural = IntList(structural.Keys.Except(enhanced.Keys));
            throw new ChainException(
                $"E41: BuildIndex differs between dump files (only in enhanced_powers.json: {onlyEnhanced}; "
                + $"only in powers_effects.json: {onlyStructural}); re-dump both files from the same build");
        }

        Dictionary<string, PowerDamageRef> refs = new(StringComparer.Ordinal);
        foreach ((int index, JsonElement power) in enhanced)
        {
            JsonElement record = structural[index];
            string name = record.ReqStr("FullName");
            (double buff, double buffDuration, double debuff, double debuffDuration) = Fold(name, power.ReqItems("Effects"), record.ReqItems("Effects"));
            refs[name] = new PowerDamageRef(
                name,
                F32.Round(power.ReqNum("DamagePerActivation")),
                F32.Round(power.ReqNum("CastTime")),
                F32.Round(power.ReqNum("RechargeTime")),
                F32.Round(power.ReqNum("EndCost")),
                F32.Round(record.ReqNum("ActivatePeriod")),
                record.ReqStr("PowerType"),
                record.Req("StatInclude").GetBoolean(),
                buff,
                buffDuration,
                debuff,
                debuffDuration);
        }

        return refs;
    }

    private static string IntList(IEnumerable<int> values)
    {
        return "[" + string.Join(", ", values.Order().Select(value => value.ToString(CultureInfo.InvariantCulture))) + "]";
    }

    private static Dictionary<int, JsonElement> ByBuildIndex(string path)
    {
        Dictionary<int, JsonElement> result = [];
        foreach (JsonElement power in CanonicalJson.ReadJson(path, "E43").EnumerateArray())
        {
            result[power.Req("BuildIndex").GetInt32()] = power;
        }

        return result;
    }

    private static (double Buff, double BuffDuration, double Debuff, double DebuffDuration) Fold(string fullName, IReadOnlyList<JsonElement> enhanced, IReadOnlyList<JsonElement> structural)
    {
        Dictionary<int, JsonElement> byIndex = [];
        foreach (JsonElement effect in enhanced)
        {
            byIndex[effect.Req("Index").GetInt32()] = effect;
        }

        Dictionary<string, (double Mag, double Duration)> buff = [];
        Dictionary<string, (double Mag, double Duration)> debuff = [];
        foreach (JsonElement effect in structural)
        {
            string kind = effect.ReqStr("EffectType");
            string who = effect.ReqStr("ToWho");
            string damageType = effect.ReqStr("DamageType");
            bool isBuff = string.Equals(kind, "DamageBuff", StringComparison.Ordinal) && string.Equals(who, "Self", StringComparison.Ordinal);
            bool isDebuff = string.Equals(kind, "Resistance", StringComparison.Ordinal) && string.Equals(who, "Target", StringComparison.Ordinal);
            if (!CoreTypes.Contains(damageType) || !(isBuff || isDebuff))
            {
                continue;
            }

            int index = effect.Req("Index").GetInt32();
            if (!byIndex.TryGetValue(index, out JsonElement match))
            {
                throw new ChainException(
                    $"E42: {fullName} effect Index {index.ToString(CultureInfo.InvariantCulture)} ({kind} {damageType}) is in powers_effects.json "
                    + "but not enhanced_powers.json; re-dump both files from the same build");
            }

            double mag = F32.Round(match.ReqNum("Math_Mag"));
            double duration = F32.Round(match.ReqNum("Math_Duration"));
            Dictionary<string, (double Mag, double Duration)> table = isBuff ? buff : debuff;
            (double oldMag, double oldDuration) = table.GetValueOrDefault(damageType);
            table[damageType] = (oldMag + mag, Math.Max(oldDuration, duration));
        }

        (double buffMag, double buffDuration) = buff.Count > 0 ? buff.Values.First() : (0.0, 0.0);
        foreach ((double mag, double duration) in buff.Values)
        {
            if (mag > buffMag || (mag == buffMag && duration > buffDuration))
            {
                (buffMag, buffDuration) = (mag, duration);
            }
        }

        double debuffMag = 0.0;
        double debuffDuration = 0.0;
        if (debuff.Count == CoreTypes.Length)
        {
            (double Mag, double Duration) best = debuff.Values.First();
            foreach ((double Mag, double Duration) candidate in debuff.Values)
            {
                if (candidate.Mag > best.Mag)
                {
                    best = candidate;
                }
            }

            debuffMag = Math.Max(0.0, -best.Mag);
            debuffDuration = best.Duration;
        }

        return (buffMag, buffDuration, debuffMag, debuffDuration);
    }
}
