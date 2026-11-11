using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Damage;

/// <summary>Damage modifiers: click buffs and target resistance debuffs.</summary>
public static class DamageModifiers
{
    /// <summary>Self damage-buff clicks and support powers, additive; they scale the character's own damage, not pets'.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The result, or null.</returns>
    public static ModifierResult? ClickBuffModifier(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ChainAttack[] buffs = [.. context.Attacks.Where(attack => attack.Profile.Role is Role.BuffClick or Role.Support && attack.SelfDamageBuff > 0.0)];
        if (buffs.Length == 0)
        {
            return null;
        }

        double[] ups = [.. buffs.Select(attack => Uptime(attack, attack.SelfDamageBuffDuration))];
        double bonus = F32.Round(PyFloat.Sum(buffs.Select((attack, index) => F32.Round(attack.SelfDamageBuff * ups[index]))));
        string text = string.Join("; ", buffs.Select((attack, index) => $"{attack.FullName} +{PyFloat.Percent(attack.SelfDamageBuff, 0)} at {PyFloat.Percent(ups[index], 0)} uptime"));
        return new ModifierResult
        {
            Modifier = "click_buff",
            Multiplier = F32.Round(1.0 + bonus),
            Uptime = 1.0,
            AppliesTo = new HashSet<string>(StringComparer.Ordinal) { DamageModel.SourceChain, DamageModel.SourceAura, DamageModel.SourcePatch },
            Assumption = $"click buffs fired on cooldown: {text}",
            Additive = true,
        };
    }

    /// <summary>Resistance debuffs on the target: damage factor (1 - r + d) / (1 - r); pets benefit too.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The result, or null.</returns>
    /// <exception cref="EngineException">E57 when <c>target.resistance</c> lies outside [-1, 0.95].</exception>
    public static ModifierResult? TargetDebuffModifier(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ChainOption? chain = DamageSources.ChosenChain(context);
        HashSet<string> rotation = chain is not null ? [.. chain.Rotation] : [];
        ChainAttack[] chainDebuffs = [.. context.Attacks.Where(attack => attack.TargetResDebuff > 0.0 && attack.Profile.Role == Role.Chain && rotation.Contains(attack.FullName))];
        List<(ChainAttack Attack, double Uptime)> others = [];
        foreach (ChainAttack attack in context.Attacks)
        {
            if (attack.TargetResDebuff <= 0.0)
            {
                continue;
            }

            if (attack.Profile.Role is Role.Patch or Role.Support)
            {
                others.Add((attack, Uptime(attack, attack.TargetResDebuffDuration)));
            }
            else if (attack.Profile.Role == Role.PersistentDebuff)
            {
                double uptime = attack.Profile.NeedsMeleePosition ? DamageSources.MeleeShare(context) : 1.0;
                if (uptime > 0.0)
                {
                    others.Add((attack, uptime));
                }
            }
        }

        if (chainDebuffs.Length == 0 && others.Count == 0)
        {
            return null;
        }

        double resistance = context.Assume("target.resistance", 0.0);
        if (resistance is not (>= -1.0 and <= 0.95))
        {
            throw new EngineException($"{ErrorCodes.E57}: assumption target.resistance={PyFloat.Repr(resistance)} is outside [-1, 0.95]; set it within that range");
        }

        double chainPart = chain?.ResDebuff ?? 0.0;
        double stacked = F32.Round(chainPart + PyFloat.Sum(others.Select(item => F32.Round(item.Attack.TargetResDebuff * item.Uptime))));
        List<string> parts = [.. chainDebuffs.Select(attack => $"{attack.FullName} -{PyFloat.Percent(attack.TargetResDebuff, 0)} res")];
        parts.AddRange(others.Select(item => $"{item.Attack.FullName} -{PyFloat.Percent(item.Attack.TargetResDebuff, 0)} res ({item.Attack.Profile.Role.Value()}) at {PyFloat.Percent(item.Uptime, 0)} uptime"));
        return new ModifierResult
        {
            Modifier = "target_debuff",
            Multiplier = F32.Round(F32.Round(1.0 - resistance + stacked) / F32.Round(1.0 - resistance)),
            Uptime = 1.0,
            AppliesTo = null,
            Assumption = $"debuffs from the chosen chain, patches, support and persistent powers against {PyFloat.Percent(resistance, 0)} resistance, "
                + $"stacked -{PyFloat.Percent(stacked, 1)}: {string.Join("; ", parts)}",
            Additive = false,
        };
    }

    private static double Uptime(ChainAttack attack, double duration)
    {
        return Math.Min(1.0, F32.Round(duration / F32.Round(AttackChains.ArcanaCastTime(attack.CastTime) + attack.RechargeTime)));
    }
}
