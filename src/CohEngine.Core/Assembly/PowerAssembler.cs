using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Assembly;

/// <summary>
/// GBPA_SubPass0_AssemblePowerEntry: one build power's effect list as Mids builds it before computing totals, from a
/// power override, the slotted enhancements' own effects, pet absorption, GrantPower replacement, ExecutePower
/// expansion and sub-power effects, in that order.
/// </summary>
internal static class PowerAssembler
{
    private const int GrantRounds = 100;
    private const int ExecuteDepth = 5;

    /// <summary>One build power's assembled effects, before the cross-power pass.</summary>
    /// <param name="buildPower">The build power.</param>
    /// <param name="slots">Its slots.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <param name="setDb">The set records.</param>
    /// <param name="powers">The database powers assembly reaches.</param>
    /// <param name="entities">The summoned entities.</param>
    /// <param name="archetypeIndex">The build's class index.</param>
    /// <param name="conditions">Whether a conditional GrantPower's conditions hold.</param>
    /// <returns>The assembled effects.</returns>
    /// <exception cref="InvalidDataException">E88 for an override redirect the power takes; E90 for a pointer to a record the inputs do not hold.</exception>
    /// <exception cref="NotSupportedException">E89 for an entity or powerset the export does not hold; E91 for a GrantPower gated on a special case.</exception>
    public static AssembledEffects AssemblePower(
        BuildPowerAssembly buildPower,
        IReadOnlyList<SlotRef> slots,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        SetBonusDb setDb,
        IReadOnlyDictionary<int, AssemblyPower> powers,
        AssemblyEntities entities,
        int archetypeIndex,
        Func<Effect, bool> conditions)
    {
        AssemblyInputs inputs = new(powers, entities, archetypeIndex);
        (AssemblyPower power, List<Effect> effects) = Assemble(buildPower, slots, enhDb, setDb, inputs, conditions);
        return new AssembledEffects(Reindex(effects), power.IgnoreEnh);
    }

    /// <summary>The build's powers with their assembled effects and ignored aspects, and the scalars expressions read.</summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="build">The assembly inputs by build index.</param>
    /// <param name="assembled">The assembled effects by build index.</param>
    /// <returns>The powers, assembled.</returns>
    public static List<Power> ApplyAssembly(IReadOnlyList<Power> powers, IReadOnlyDictionary<int, BuildPowerAssembly> build, IReadOnlyDictionary<int, AssembledEffects> assembled)
    {
        List<Power> output = [];
        foreach (Power power in powers)
        {
            AssembledEffects result = assembled[power.BuildIndex];
            AssemblyPower own = build[power.BuildIndex].Power;
            IReadOnlyList<string> ignoreEnh = result.IgnoreEnh.SetEquals(power.IgnoreEnh) ? power.IgnoreEnh : [.. result.IgnoreEnh.Order(StringComparer.Ordinal)];
            output.Add(power with
            {
                Effects = result.Effects,
                IgnoreEnh = ignoreEnh,
                AoeModifier = own.AoeModifier,
                BaseRechargeTime = own.BaseRechargeTime,
            });
        }

        return output;
    }

    /// <summary>The power with its targeting and ignored aspects replaced, as pet absorption copies them.</summary>
    /// <param name="power">The power.</param>
    /// <param name="affected">The entities it affects.</param>
    /// <param name="autoHit">The entities it auto-hits.</param>
    /// <param name="ignoreEnh">The eEnhance names it ignores.</param>
    /// <returns>A copy with those three replaced.</returns>
    public static AssemblyPower WithTargets(AssemblyPower power, IReadOnlySet<string> affected, IReadOnlySet<string> autoHit, IReadOnlySet<string> ignoreEnh)
    {
        return new AssemblyPower(affected.ToHashSet(StringComparer.Ordinal), autoHit.ToHashSet(StringComparer.Ordinal), power.Enhancements.ToHashSet(), ignoreEnh.ToHashSet(StringComparer.Ordinal))
        {
            Nid = power.Nid,
            FullName = power.FullName,
            PowerType = power.PowerType,
            PowerSetType = power.PowerSetType,
            ActivatePeriod = power.ActivatePeriod,
            RechargeTime = power.RechargeTime,
            BaseRechargeTime = power.BaseRechargeTime,
            CastTime = power.CastTime,
            AoeModifier = power.AoeModifier,
            HasGrantPowerEffect = power.HasGrantPowerEffect,
            HasPowerOverrideEffect = power.HasPowerOverrideEffect,
            AbsorbSummonEffects = power.AbsorbSummonEffects,
            AbsorbSummonAttributes = power.AbsorbSummonAttributes,
            Effects = power.Effects,
        };
    }

    /// <summary>Re-indexes effects so each index is its list position.</summary>
    /// <param name="effects">The effects.</param>
    /// <returns>The effects, re-indexed.</returns>
    public static Effect[] Reindex(IEnumerable<Effect> effects)
    {
        return [.. effects.Select((effect, i) => effect with { Index = i })];
    }

    /// <summary>The power and its effect list after every SubPass0 step.</summary>
    /// <param name="buildPower">The build power.</param>
    /// <param name="slots">Its slots.</param>
    /// <param name="enhDb">The enhancement records.</param>
    /// <param name="setDb">The set records.</param>
    /// <param name="inputs">The assembly inputs.</param>
    /// <param name="conditions">Whether a conditional GrantPower's conditions hold.</param>
    /// <returns>The power, with any attributes pet absorption copied, and its effects.</returns>
    public static (AssemblyPower Power, List<Effect> Effects) Assemble(
        BuildPowerAssembly buildPower,
        IReadOnlyList<SlotRef> slots,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        SetBonusDb setDb,
        AssemblyInputs inputs,
        Func<Effect, bool> conditions)
    {
        AssemblyPower power = buildPower.Power;
        CheckOverride(power);
        List<Effect> effects = [.. power.Effects];
        (List<Effect> enhEffects, bool enhGrants) = EnhancementEffects(buildPower, slots, effects, enhDb, setDb, inputs);
        effects.AddRange(enhEffects);
        power = PetAbsorption.AbsorbPets(power, effects, inputs);
        if (power.HasGrantPowerEffect || enhGrants)
        {
            ApplyGrants(effects, power, inputs, conditions);
        }

        Dictionary<string, AssemblyPower> byName = new(StringComparer.Ordinal);
        foreach (AssemblyPower candidate in inputs.Powers.Values)
        {
            byName[candidate.FullName] = candidate;
        }

        effects = ExpandExecutes(effects, byName);
        foreach (SubPower sub in buildPower.SubPowers.Where(sub => sub.NidPower > -1 && sub.StatInclude))
        {
            AssemblyPower subPower = AssemblyInputs.Record(inputs.Powers, sub.NidPower, "sub-power", $"build power {buildPower.BuildIndex} ({power.FullName})");
            effects.AddRange(subPower.Effects.Select(effect => effect with { AbsorbedEffect = true, AbsorbedPowerNid = sub.NidPower, AbsorbedPowerType = subPower.PowerType }));
        }

        return (power, effects);
    }

    private static void CheckOverride(AssemblyPower power)
    {
        if (!power.HasPowerOverrideEffect)
        {
            return;
        }

        foreach (Effect effect in power.Effects)
        {
            if (string.Equals(effect.EffectType, "PowerRedirect", StringComparison.Ordinal) && Math.Abs(effect.Probability - 1) < 0.01 && string.Equals(effect.SpecialCase, "None", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"E88: {power.FullName} effect {effect.Index} redirects the power, but the export carries no "
                    + "redirect target; expected the target power in the assembly inputs, found none; export the "
                    + "effect's Override power before assembling this build");
            }
        }
    }

    /// <summary>
    /// How many times a pets-only effect is copied onto a summon power: once per power of the summoned entity's first
    /// powerset that has an effect of the same type, and once more when the last of those powers has one, as Mids'
    /// loop leaves its flag set for the common add.
    /// </summary>
    private static int PetsOnlyCopies(Effect effect, List<Effect> current, AssemblyEntities entities, string where)
    {
        string uid = current.First(candidate => string.Equals(candidate.EffectType, "EntCreate", StringComparison.Ordinal)).Summon;
        AssemblyEntity entity = entities.Entities.Values.FirstOrDefault(candidate => string.Equals(candidate.Uid, uid, StringComparison.Ordinal))
            ?? throw new NotSupportedException(
                $"E89: {where} summons entity '{uid}', which assembly_entities.json does not hold; expected every "
                + "entity a pets-only enhancement effect reads; re-run the Mids dump harness so the export carries it");
        bool[] matches = [.. entity.FirstPowersetEffectTypes.Select(types => types.Contains(effect.EffectType))];
        return matches.Count(match => match) + (matches.Length > 0 && matches[^1] ? 1 : 0);
    }

    /// <summary>GBPA_AddEnhFX: the slotted enhancements' own power effects, and whether one is a GrantPower.</summary>
    private static (List<Effect> Effects, bool Grants) EnhancementEffects(
        BuildPowerAssembly buildPower,
        IReadOnlyList<SlotRef> slots,
        List<Effect> current,
        IReadOnlyDictionary<int, EnhancementRecord> enhDb,
        SetBonusDb setDb,
        AssemblyInputs inputs)
    {
        string where = $"build power {buildPower.BuildIndex} ({buildPower.Power.FullName})";
        bool summons = current.Any(effect => string.Equals(effect.EffectType, "EntCreate", StringComparison.Ordinal));
        List<Effect> output = [];
        bool grants = false;
        foreach (SlotRef slot in slots)
        {
            if (slot.Enh < 0)
            {
                continue;
            }

            EnhancementRecord enh = AssemblyInputs.Record(enhDb, slot.Enh, "enhancement", where);
            if (enh.PowerNid < 0 || (buildPower.ProcInclude && enh.IsProc) || enh.NidSet < 0)
            {
                continue;
            }

            EnhancementSetDef setDef = AssemblyInputs.Record(setDb.Sets, enh.NidSet, "enhancement set", $"enhancement {enh.Nid}");
            int position = IndexOf(setDef.Enhancements, slot.Enh);
            foreach (Effect effect in AssemblyInputs.Record(inputs.Powers, enh.PowerNid, "power", $"enhancement {enh.Nid}").Effects)
            {
                int copies;
                if (summons && effect.Reward.Contains("Set_Bonus", StringComparison.Ordinal) && setDef.SetTypeName.Contains("Pet", StringComparison.Ordinal))
                {
                    copies = PetsOnlyCopies(effect, current, inputs.Entities, where);
                }
                else if (position < 0 || setDef.SpecialBonus[position].Index.Count != 0 || (enh.Effects.Count != 0 && IsBoostTable(effect.ModifierTable)))
                {
                    continue;
                }
                else
                {
                    copies = 1;
                }

                Effect copy = effect with { IsEnhancementEffect = true, IgnoreScaling = enh.IsProc, AbsorbedEffect = true, Buffable = false, AbsorbedPowerNid = -1 };
                output.AddRange(Enumerable.Repeat(copy, copies));
                grants = grants || (copies > 0 && string.Equals(effect.EffectType, "GrantPower", StringComparison.Ordinal));
            }
        }

        return (output, grants);
    }

    private static int IndexOf(IReadOnlyList<int> values, int value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsBoostTable(string table)
    {
        return table.StartsWith("Melee_Boosts_", StringComparison.Ordinal) || table.StartsWith("Ranged_Boosts_", StringComparison.Ordinal);
    }

    /// <summary><c>Effect.CanGrantPower</c>.</summary>
    private static bool CanGrant(Effect effect, Func<Effect, bool> conditions)
    {
        if (!string.Equals(effect.SpecialCase, "None", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"E91: GrantPower effect {effect.Index} ({effect.Summon}) is gated on special case {effect.SpecialCase}; "
                + "expected special case None, found one; the character state it reads is not modelled");
        }

        return effect.Conditionals.Count == 0 || conditions(effect);
    }

    /// <summary><c>Power.ApplyGrantPowerEffects</c>, editing <paramref name="effects"/> in place.</summary>
    private static void ApplyGrants(List<Effect> effects, AssemblyPower power, AssemblyInputs inputs, Func<Effect, bool> conditions)
    {
        for (int round = 0; round < GrantRounds; round++)
        {
            List<int> found = [];
            for (int i = 0; i < effects.Count; i++)
            {
                Effect effect = effects[i];
                if (string.Equals(effect.EffectType, "GrantPower", StringComparison.Ordinal) && !string.Equals(effect.EffectClass, "Ignored", StringComparison.Ordinal) && effect.NSummon > -1 && CanGrant(effect, conditions))
                {
                    found.Add(i);
                }
            }

            if (found.Count == 0)
            {
                return;
            }

            foreach (int i in found)
            {
                Effect grant = effects[i] = effects[i] with { EffectClass = "Ignored" };
                AssemblyPower granted = AssemblyInputs.Record(inputs.Powers, grant.NSummon, "granted power", $"{power.FullName} effect {grant.Index}");
                effects.AddRange(Absorption.Absorb(granted, grant.Duration, inputs.ArchetypeIndex, inputs));
                effects[^1] = Granted(effects[^1], grant, power);
            }
        }
    }

    /// <summary>The last effect a grant absorbed, retargeted and reweighted by the grant.</summary>
    private static Effect Granted(Effect last, Effect grant, AssemblyPower power)
    {
        if (grant.AbsorbedPowerNid > -1)
        {
            last = last with { AbsorbedPowerType = grant.AbsorbedPowerType };
        }

        if (!string.Equals(last.EffectType, "GrantPower", StringComparison.Ordinal))
        {
            last = last with { ToWho = grant.ToWho };
        }

        if (string.Equals(last.ToWho, "All", StringComparison.Ordinal) && !(power.EntitiesAffected.Contains("Caster") && power.EntitiesAffected.Contains("Friend") && power.EntitiesAffected.Contains("Foe")))
        {
            last = last with { ToWho = "Target" };
        }

        last = last with { IsEnhancementEffect = grant.IsEnhancementEffect };
        if (grant.BaseProbability < 1)
        {
            last = Chance.WithProbability(last, F32.Round(Chance.Probability(last, Chance.None) * Chance.Probability(grant, Chance.None)), Chance.None);
        }

        return last;
    }

    /// <summary>An executed power's effect as it takes the place of the ExecutePower effect that ran it.</summary>
    private static Effect Inherit(Effect effect, Effect caller)
    {
        double own = Chance.Probability(effect, Chance.None);
        double called = Chance.Probability(caller, Chance.None);
        effect = effect with
        {
            BaseProbability = called is 0.0 or 1.0 ? own : Math.Min(own, called),
            NDuration = caller.NDuration > 0 ? caller.NDuration : effect.NDuration,
            DelayedTime = F32.Round(effect.DelayedTime + caller.DelayedTime),
            ProcsPerMinute = caller.ProcsPerMinute,
            Conditionals = [.. effect.Conditionals, .. caller.Conditionals],
            ActiveConditionalsCount = effect.ActiveConditionalsCount + caller.ActiveConditionalsCount,
            Ticks = caller.Ticks > 0 && effect.Ticks == 0 ? caller.Ticks : effect.Ticks,
        };
        return effect with { Probability = Chance.Probability(effect, Chance.None) };
    }

    /// <summary><c>Power.ProcessExecutesInner</c>: each ExecutePower replaced in place by the executed power's effects.</summary>
    private static List<Effect> ExpandExecutes(IEnumerable<Effect> effects, Dictionary<string, AssemblyPower> byName, int depth = 0)
    {
        List<Effect> output = [];
        if (depth > ExecuteDepth)
        {
            return output;
        }

        foreach (Effect effect in effects)
        {
            if (!string.Equals(effect.EffectType, "ExecutePower", StringComparison.Ordinal))
            {
                output.Add(effect);
                continue;
            }

            if (effect.Summon.Length != 0 && byName.TryGetValue(effect.Summon, out AssemblyPower? executed))
            {
                output.AddRange(ExpandExecutes(executed.Effects, byName, depth + 1).Select(sub => Inherit(sub, effect)));
            }
        }

        return output;
    }
}
