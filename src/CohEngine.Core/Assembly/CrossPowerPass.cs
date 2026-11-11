using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Assembly;

/// <summary>
/// The cross-power pass after every power is assembled: GBPA_ApplyIncarnateEnhancements with effect type GrantPower,
/// which absorbs the GlobalBoost effects of the other active powers into each power, then the build's chance
/// modifiers (<c>GenerateModifyEffectsArray</c>).
/// </summary>
internal static class CrossPowerPass
{
    private static readonly HashSet<string> SlottableSetTypes = new(["Primary", "Secondary", "Ancillary", "Inherent", "Pool"], StringComparer.Ordinal);

    /// <summary>
    /// Every build power's assembled effects, by build index. Each power, StatInclude or not, absorbs from every
    /// other StatInclude power in build order; a source already processed as a target carries what it absorbed.
    /// </summary>
    /// <param name="build">The build powers' assembly inputs by build index.</param>
    /// <param name="slots">The build's slots by build index.</param>
    /// <param name="statInclude">Each build power's StatInclude flag.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <param name="setDb">The set records.</param>
    /// <param name="powers">The database powers assembly reaches.</param>
    /// <param name="entities">The summoned entities.</param>
    /// <param name="archetypeIndex">The build's class index.</param>
    /// <param name="conditions">Whether a conditional GrantPower's conditions hold.</param>
    /// <param name="setBonusEffects">The set-bonus virtual power's effects, whose chance modifiers follow the build powers'.</param>
    /// <returns>The assembled effects by build index.</returns>
    public static Dictionary<int, AssembledEffects> AssembleBuild(
        IReadOnlyDictionary<int, BuildPowerAssembly> build,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, bool> statInclude,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        SetBonusDb setDb,
        IReadOnlyDictionary<int, AssemblyPower> powers,
        AssemblyEntities entities,
        int archetypeIndex,
        Func<Effect, bool> conditions,
        IReadOnlyList<Effect>? setBonusEffects = null)
    {
        AssemblyInputs inputs = new(EveryPower(powers, build), entities, archetypeIndex);
        int[] order = [.. build.Keys.Order()];
        Dictionary<int, (AssemblyPower Power, List<Effect> Effects)> assembled = [];
        foreach (int i in order)
        {
            assembled[i] = PowerAssembler.Assemble(build[i], slots.GetValueOrDefault(i, []), enhDb, setDb, inputs, conditions);
        }

        Dictionary<string, double> mods = new(StringComparer.Ordinal);
        foreach (int target in order)
        {
            foreach (int source in order.Where(source => source != target && statInclude[source]))
            {
                AbsorbGlobal(assembled[target].Effects, assembled[target].Power, assembled[source].Effects, assembled[source].Power, inputs, mods);
            }
        }

        GlobalChanceMods(order, assembled, statInclude, setBonusEffects ?? [], mods);
        return assembled.ToDictionary(
            entry => entry.Key,
            entry => new AssembledEffects(PowerAssembler.Reindex(entry.Value.Effects.Select(effect => effect with { Probability = Chance.Probability(effect, mods) })), entry.Value.Power.IgnoreEnh));
    }

    /// <summary>
    /// The database powers, then the build's own, since an effect absorbed from a build power points at it; a
    /// database record wins a shared index, and the order is the one Python's ChainMap iterates.
    /// </summary>
    private static Dictionary<int, AssemblyPower> EveryPower(IReadOnlyDictionary<int, AssemblyPower> powers, IReadOnlyDictionary<int, BuildPowerAssembly> build)
    {
        Dictionary<int, AssemblyPower> every = [];
        foreach (BuildPowerAssembly entry in build.Values)
        {
            every[entry.Power.Nid] = powers.GetValueOrDefault(entry.Power.Nid, entry.Power);
        }

        foreach ((int nid, AssemblyPower power) in powers)
        {
            every.TryAdd(nid, power);
        }

        return every;
    }

    /// <summary><c>PowerGrantsMap.GetGrantRoot</c>: the source grant an absorbed position falls under, if any.</summary>
    private static Func<int, Effect?> GrantRoot(AssemblyPower source, List<Effect> sourceFx, AssemblyInputs inputs)
    {
        List<(int Low, int High, Effect Grant)> ranges = [];
        int start = source.Effects.Count;
        foreach (Effect effect in sourceFx)
        {
            if (!string.Equals(effect.EffectType, "GrantPower", StringComparison.Ordinal))
            {
                continue;
            }

            int count = effect.NSummon >= 0 ? AssemblyInputs.Record(inputs.Powers, effect.NSummon, "granted power", source.FullName).Effects.Count : 0;
            ranges.Add((start, start + count, effect));
            start += count;
        }

        return offset =>
        {
            int position = offset + source.Effects.Count;
            return ranges.Where(range => range.Low <= position && position < range.High).Select(range => range.Grant).FirstOrDefault();
        };
    }

    /// <summary>
    /// GBPA_ApplyIncarnateEnhancements with effect type GrantPower and no buffed power, editing in place; it also enters
    /// in <paramref name="mods"/> the chance modifier of each id the absorbed effects and their grants carry.
    /// </summary>
    private static void AbsorbGlobal(List<Effect> targetFx, AssemblyPower target, List<Effect> sourceFx, AssemblyPower source, AssemblyInputs inputs, Dictionary<string, double> mods)
    {
        if (sourceFx.Count == 0 || target.Enhancements.Count == 0 || !SlottableSetTypes.Contains(target.PowerSetType))
        {
            return;
        }

        Func<int, Effect?> grantRoot = GrantRoot(source, sourceFx, inputs);
        int start = targetFx.Count;
        foreach (Effect effect in sourceFx)
        {
            if (Skipped(effect, source))
            {
                continue;
            }

            string where = $"{source.FullName} effect {effect.Index}";
            AssemblyPower owner = effect.AbsorbedEffect && effect.AbsorbedPowerNid > -1 ? AssemblyInputs.Record(inputs.Powers, effect.AbsorbedPowerNid, "power", where) : source;
            if (!target.Enhancements.Overlaps(owner.Enhancements))
            {
                continue;
            }

            if (string.Equals(effect.EffectType, "GrantPower", StringComparison.Ordinal))
            {
                AssemblyPower granted = AssemblyInputs.Record(inputs.Powers, effect.NSummon, "granted power", where);
                targetFx.AddRange(Absorption.Absorb(granted, effect.Duration, inputs.ArchetypeIndex, inputs));

                // Mids rewrites every effect of the target here, not only the ones just absorbed.
                for (int k = 0; k < targetFx.Count; k++)
                {
                    Effect rewritten = targetFx[k] with
                    {
                        ToWho = "Target",
                        AbsorbedEffect = true,
                        IsEnhancementEffect = effect.IsEnhancementEffect,
                        BaseProbability = F32.Round(targetFx[k].BaseProbability * effect.BaseProbability),
                        Ticks = effect.Ticks,
                    };
                    targetFx[k] = Chance.WithProbability(rewritten, F32.Round(Chance.Probability(rewritten, mods) * Chance.Probability(effect, mods)), mods);
                }
            }
            else
            {
                Effect absorbed = Absorption.Absorb(source, effect.Duration, inputs.ArchetypeIndex, inputs, chosen: effect).Single();
                targetFx.Add(absorbed with { ToWho = "Target", AbsorbedEffect = true, IsEnhancementEffect = effect.IsEnhancementEffect, Ticks = effect.Ticks });
            }
        }

        SettleChances(targetFx, start, grantRoot, sourceFx, mods);
    }

    private static bool Skipped(Effect effect, AssemblyPower source)
    {
        return string.Equals(effect.EffectClass, "Ignored", StringComparison.Ordinal)
            || effect.EffectType is "Enhancement" or "DamageBuff"
            || effect.IgnoreEd
            || (!string.Equals(source.PowerType, "GlobalBoost", StringComparison.Ordinal) && !(effect.AbsorbedEffect && string.Equals(effect.AbsorbedPowerType, "GlobalBoost", StringComparison.Ordinal)))
            || (string.Equals(effect.EffectType, "GrantPower", StringComparison.Ordinal) && effect.AbsorbedEffect);
    }

    /// <summary>Enters the absorbed effects' chance ids in <paramref name="mods"/>, then scales each by the probability of its grant.</summary>
    private static void SettleChances(List<Effect> targetFx, int start, Func<int, Effect?> grantRoot, List<Effect> sourceFx, Dictionary<string, double> mods)
    {
        List<string> flags = [];
        for (int k = start; k < targetFx.Count; k++)
        {
            foreach (Effect effect in new[] { grantRoot(k - start), targetFx[k] }.OfType<Effect>().Where(effect => effect.EffectId.Trim().Length != 0 && !flags.Contains(effect.EffectId, StringComparer.Ordinal)))
            {
                flags.Add(effect.EffectId);
            }
        }

        foreach (string flag in flags.Where(flag => !mods.ContainsKey(flag)))
        {
            double sum = 0.0;
            foreach (Effect effect in sourceFx.Where(effect => string.Equals(effect.Reward, flag, StringComparison.Ordinal)))
            {
                sum += effect.Scale;
            }

            mods[flag] = F32.Round(sum);
        }

        for (int k = start; k < targetFx.Count; k++)
        {
            Effect? root = grantRoot(k - start);

            // A position no grant covers reads 0, as the float FirstOrDefault in Mids does.
            double grantProbability = root is not null ? Chance.Probability(root, mods) : 0.0;
            targetFx[k] = Chance.WithProbability(targetFx[k], F32.Round(Chance.Probability(targetFx[k], mods) * grantProbability), mods);
        }
    }

    /// <summary><c>GenerateModifyEffectsArray</c>: each reward's first GlobalChanceMod scale, build powers then set bonuses.</summary>
    private static void GlobalChanceMods(int[] order, Dictionary<int, (AssemblyPower Power, List<Effect> Effects)> assembled, IReadOnlyDictionary<int, bool> statInclude, IReadOnlyList<Effect> setBonusEffects, Dictionary<string, double> mods)
    {
        IEnumerable<Effect> sources = order.Where(i => statInclude[i]).SelectMany(i => assembled[i].Effects);
        foreach (Effect effect in sources.Concat(setBonusEffects).Where(effect => string.Equals(effect.EffectType, "GlobalChanceMod", StringComparison.Ordinal) && effect.Reward.Length != 0 && !mods.ContainsKey(effect.Reward)))
        {
            mods[effect.Reward] = effect.Scale;
        }
    }
}
