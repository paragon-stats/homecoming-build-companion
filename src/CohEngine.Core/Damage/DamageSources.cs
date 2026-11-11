using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Damage;

/// <summary>Damage sources: attack chain, damage auras, location patches and buff casts.</summary>
public static class DamageSources
{
    /// <summary>The buff-cast source name.</summary>
    public const string SourceBuffCast = "buff_cast";

    /// <summary>The attacks usable in the context's form: mode-free ones in the base form, else those requiring the form.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The attacks.</returns>
    public static IReadOnlyList<ChainAttack> AttacksForForm(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Form.Length == 0
            ? [.. context.Attacks.Where(attack => attack.Profile.ModesRequired.Count == 0)]
            : [.. context.Attacks.Where(attack => attack.Profile.ModesRequired.Contains(context.Form, StringComparer.Ordinal))];
    }

    /// <summary>The planner's full result for a context.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The plan.</returns>
    public static ChainPlan ChainPlan(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AttackChains.Plan(
            AttacksForForm(context),
            context.Stance,
            context.Movement,
            context.Scenario,
            context.Budget,
            context.TimelineAvailable,
            context.OtherDrainPerSec,
            context.Assume("chain.min_cycles", 4.0));
    }

    /// <summary>The planner's pick: best affordable rotation, else the best overall, else null.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The option.</returns>
    public static ChainOption? ChosenChain(DamageContext context)
    {
        ChainPlan plan = ChainPlan(context);
        return plan.BestAffordable ?? plan.BestOverall;
    }

    /// <summary>The planned attack rotation on the timeline left after patches and buff casts.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The result, or null.</returns>
    public static SourceResult? ChainSource(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ChainOption? option = ChosenChain(context);
        if (option is null)
        {
            return null;
        }

        Dictionary<string, ChainAttack> byName = context.Attacks.ToLastWinsDictionaryByName();
        (string, double)[] detail =
        [
            .. option.Rotation.Select(name => (name, F32.Round(F32.Round(byName[name].Damage * context.Scenario.TargetsHit(byName[name].Profile)) / option.CycleTime * context.TimelineAvailable))),
        ];
        double cycles = F32.Round(option.TimeToEmpty / option.CycleTime);
        string kind;
        if (option.Sustainable)
        {
            kind = "sustainable";
        }
        else if (cycles >= context.Assume("chain.min_cycles", 4.0))
        {
            kind = $"affordable: empties in {PyFloat.Fixed(option.TimeToEmpty, 1)} s, {PyFloat.Fixed(cycles, 0)} cycles";
        }
        else
        {
            kind = $"not affordable (best overall): empties in {PyFloat.Fixed(option.TimeToEmpty, 1)} s, {PyFloat.Fixed(cycles, 1)} cycles";
        }

        return new SourceResult
        {
            Source = DamageModel.SourceChain,
            Dps = option.Dps,
            EndPerSec = option.EndPerSec,
            Detail = detail,
            Label = $"chain, {kind}; damage is the Mids reference figure standing in for #31",
        };
    }

    /// <summary>The fraction of time the character stands where a damage aura reaches foes.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The share.</returns>
    /// <exception cref="EngineException">E56 when <c>hybrid.melee_share</c> lies outside [0, 1].</exception>
    public static double MeleeShare(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Stance == Stance.Melee)
        {
            return 1.0;
        }

        if (context.Stance == Stance.Ranged)
        {
            return 0.0;
        }

        if (context.Assumptions.TryGetValue("hybrid.melee_share", out double share))
        {
            if (share is not (>= 0.0 and <= 1.0))
            {
                throw new EngineException($"{ErrorCodes.E56}: assumption hybrid.melee_share={PyFloat.Repr(share)} is outside [0, 1]; set it within that range");
            }

            return share;
        }

        ChainOption? chain = ChosenChain(context);
        if (chain is null)
        {
            return 0.0;
        }

        Dictionary<string, ChainAttack> melee = context.Attacks.Where(attack => attack.Profile.NeedsMeleePosition).ToLastWinsDictionaryByName();
        string[] meleeNames = [.. chain.Rotation.Where(melee.ContainsKey)];
        if (meleeNames.Length == 0)
        {
            return 0.0;
        }

        if (meleeNames.Length == chain.Rotation.Count)
        {
            return 1.0;
        }

        double inMelee = F32.Round(PyFloat.Sum(meleeNames.Select(name => AttackChains.ArcanaCastTime(melee[name].CastTime))) + (chain.MoveTime / 2.0));
        return F32.Round(inMelee / chain.CycleTime);
    }

    /// <summary>Damage auras: damage per tick over the tick period, on every foe in reach, while in melee.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The result, or null.</returns>
    /// <exception cref="EngineException">E55 when an aura has an activate period of 0.</exception>
    public static SourceResult? AuraSource(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<ChainAttack> auras = context.ByRole(Role.Aura);
        if (auras.Count == 0)
        {
            return null;
        }

        double share = MeleeShare(context);
        if (Exact.Same(share, 0.0))
        {
            return null;
        }

        List<(string Name, double Dps)> detail = [];
        foreach (ChainAttack aura in auras)
        {
            if (aura.ActivatePeriod <= 0.0)
            {
                throw new EngineException(
                    $"{ErrorCodes.E55}: aura {aura.FullName} has activate_period {PyFloat.Repr(aura.ActivatePeriod)} but must be > 0; supply the toggle's tick period");
            }

            detail.Add((aura.FullName, F32.Round(F32.Round(aura.Damage * context.Scenario.TargetsHit(aura.Profile)) / aura.ActivatePeriod * share)));
        }

        return new SourceResult
        {
            Source = DamageModel.SourceAura,
            Dps = F32.Round(PyFloat.Sum(detail.Select(item => item.Dps))),
            Detail = detail,
            Label = $"damage auras at melee share {PyFloat.Fixed(share, 2)}; per-tick damage assumed",
        };
    }

    /// <summary>Location patches cast on cooldown; placeable at range, so every stance gets them.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The result, or null.</returns>
    public static SourceResult? PatchSource(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ChainAttack[] patches = [.. context.ByRole(Role.Patch).Where(attack => attack.Damage > 0.0)];
        if (patches.Length == 0)
        {
            return null;
        }

        List<(string Name, double Dps)> detail = [];
        double share = 0.0;
        double drain = 0.0;
        foreach (ChainAttack patch in patches)
        {
            (double cast, double cycle) = Cadence(patch);
            detail.Add((patch.FullName, F32.Round(F32.Round(patch.Damage * context.Scenario.TargetsHit(patch.Profile)) / cycle)));
            share = F32.Round(share + (cast / cycle));
            drain = F32.Round(drain + (patch.EndCost / cycle));
        }

        return new SourceResult
        {
            Source = DamageModel.SourcePatch,
            Dps = F32.Round(PyFloat.Sum(detail.Select(item => item.Dps))),
            TimelineShare = share,
            EndPerSec = drain,
            Detail = detail,
            Label = "location patches cast on cooldown",
        };
    }

    /// <summary>Buff clicks and support powers deal no damage but spend cast time and endurance.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The result, or null.</returns>
    public static SourceResult? BuffCastSource(DamageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ChainAttack[] buffs = [.. context.ByRole(Role.BuffClick), .. context.ByRole(Role.Support)];
        if (buffs.Length == 0)
        {
            return null;
        }

        double share = 0.0;
        double drain = 0.0;
        foreach (ChainAttack buff in buffs)
        {
            (double cast, double cycle) = Cadence(buff);
            share = F32.Round(share + (cast / cycle));
            drain = F32.Round(drain + (buff.EndCost / cycle));
        }

        return new SourceResult
        {
            Source = SourceBuffCast,
            Dps = 0.0,
            TimelineShare = share,
            EndPerSec = drain,
            Label = "buff clicks cast on cooldown",
        };
    }

    private static Dictionary<string, ChainAttack> ToLastWinsDictionaryByName(this IEnumerable<ChainAttack> attacks)
    {
        Dictionary<string, ChainAttack> result = new(StringComparer.Ordinal);
        foreach (ChainAttack attack in attacks)
        {
            result[attack.FullName] = attack;
        }

        return result;
    }

    private static (double Cast, double Cycle) Cadence(ChainAttack attack)
    {
        double cast = AttackChains.ArcanaCastTime(attack.CastTime);
        return (cast, F32.Round(cast + attack.RechargeTime));
    }
}
