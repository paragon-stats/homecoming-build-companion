using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Totals;

/// <summary>The totals fold, GBD_Totals: the BuffsX accumulators folded into Totals, then clamped into TotalsCapped.</summary>
internal static class ActorTotalsCalculator
{
    // C# float.Epsilon, the smallest positive subnormal single.
    private const double FloatEpsilon = 1.401298464324817e-45;

    /// <summary>Folds the accumulators into the totals.</summary>
    /// <param name="request">The accumulators and the build's inputs.</param>
    /// <returns>The totals and the global terms.</returns>
    public static BaseTotals Calculate(TotalsRequest request)
    {
        EnumMaps enums = request.Context.Enums;
        IReadOnlyDictionary<string, int> stat = enums.Maps["eStatType"];
        BuffsX selfBuffs = request.SelfBuffs;
        BuffsX selfEnhance = request.SelfEnhance;
        Archetype archetype = request.Archetype;
        ServerData server = request.Server;
        TotalsBuilder totals = new(enums);

        // The global EnduranceDiscount reduces every toggle's cost; it is the value BuffEndRdx reports below.
        double globalEndRdx = selfEnhance.Effect[stat["BuffEndRdx"]];
        bool canFly = ToggleEndAndFly(request, totals, globalEndRdx);
        FoldAndCopyVectors(totals, selfBuffs);

        totals.EndMax = selfBuffs.MaxEnd;
        totals.BuffAcc = F32.Round(selfEnhance.Effect[stat["BuffAcc"]] + selfBuffs.Effect[stat["BuffAcc"]]);
        totals.BuffEndRdx = selfEnhance.Effect[stat["BuffEndRdx"]];
        totals.BuffHaste = F32.Round(selfEnhance.Effect[stat["Haste"]] + selfBuffs.Effect[stat["Haste"]]);
        totals.BuffToHit = selfBuffs.Effect[stat["ToHit"]];
        totals.Perception = F32.Round(server.BasePerception * F32.Round(1 + selfBuffs.Effect[stat["Perception"]]));
        totals.StealthPve = selfBuffs.Effect[stat["StealthPvE"]];
        totals.StealthPvp = selfBuffs.Effect[stat["StealthPvP"]];
        totals.ThreatLevel = selfBuffs.Effect[stat["ThreatLevel"]];
        totals.HpRegen = selfBuffs.Effect[stat["HPRegen"]];
        totals.EndRec = selfBuffs.Effect[stat["EndRec"]];
        totals.Absorb = selfBuffs.Effect[stat["Absorb"]];
        totals.BuffRange = selfBuffs.Effect[stat["Range"]];
        totals.HpMax = F32.Round(selfBuffs.Effect[stat["HPMax"]] + archetype.Hitpoints);

        Movement(totals, selfBuffs, server, stat, canFly);
        BuffDamage(totals, selfBuffs, selfEnhance);
        totals.BuffHeal = selfBuffs.Effect[stat["Heal"]];

        // Only the ApplyPvpDr defense pass is ported for PvP mode; the PVP_Resist_Bonus injection is not.
        if (request.Context.Config.DisablePve)
        {
            for (int index = 0; index < totals.Def.Length; index++)
            {
                totals.Def[index] = CalculatePvpDr(totals.Def[index]);
            }
        }

        IReadOnlyDictionary<string, int> eType = enums.Maps["eEffectType"];
        GlobalEnhance globalEnhance = new(
            selfEnhance.Effect[stat["Haste"]],
            selfEnhance.Effect[stat["BuffEndRdx"]],
            selfEnhance.Effect[stat["BuffAcc"]],
            selfEnhance.Effect[eType["InterruptTime"]],
            selfEnhance.Effect[eType["Range"]]);
        return new BaseTotals(totals.Build(), ApplyCaps(totals, archetype, server).Build(), globalEnhance);
    }

    /// <summary>
    /// <c>CalculatePvpDr</c> with the 1.2f and 1.0f arguments: the bracket is computed in double, narrowed, then the final
    /// float multiply.
    /// </summary>
    /// <param name="value">The defense.</param>
    /// <param name="factorA">The first constant, single precision.</param>
    /// <param name="factorB">The second constant, single precision.</param>
    /// <returns>The diminished defense.</returns>
    public static double CalculatePvpDr(double value, double factorA = 1.2000000476837158, double factorB = 1.0)
    {
        double factor = F32.Round(1.0 - (Math.Abs(Math.Atan(factorA * value)) * (2.0 / Math.PI) * factorB));
        return F32.Round(value * factor);
    }

    /// <summary>
    /// Sums the buffed toggle costs into EndUse and reports whether the build can fly. Each toggle's cost is its EndCost
    /// over the EnduranceDiscount fold, the same fold the per-power layer uses, then over its activate period.
    /// </summary>
    private static bool ToggleEndAndFly(TotalsRequest request, TotalsBuilder totals, double globalEndRdx)
    {
        bool canFly = false;
        foreach (Power power in request.Included)
        {
            if (string.Equals(power.PowerType, "Toggle", StringComparison.Ordinal))
            {
                (_, double postEd) = request.IncarnateScalar.GetValueOrDefault((power.BuildIndex, AspectRouting.End.Aspect), (0.0, 0.0));
                double divisor = AspectRouting.FoldScalar(AspectRouting.End, request.ToggleEndAgg.GetValueOrDefault(power.BuildIndex, 0.0), globalEndRdx, power.IgnoreEnh, request.Archetype.RechargeCap, postEd);
                double buffedEndCost = F32.Round(power.EndCost / divisor);
                double cost = power.ActivatePeriod > 0 ? F32.Round(buffedEndCost / power.ActivatePeriod) : buffedEndCost;
                totals.EndUse = F32.Round(totals.EndUse + cost);
            }

            foreach (Effect effect in power.Effects.Where(effect => string.Equals(effect.EffectType, "Fly", StringComparison.Ordinal) && request.Context.Mag(effect, power) > 0))
            {
                canFly = true;
            }
        }

        return canFly;
    }

    /// <summary>The generic-defense fold at index 0, then the vector copies; MezRes and DebuffRes are scaled by 100 here.</summary>
    private static void FoldAndCopyVectors(TotalsBuilder totals, BuffsX selfBuffs)
    {
        if (Math.Abs(selfBuffs.Defense[0]) > FloatEpsilon)
        {
            for (int index = 1; index < selfBuffs.Defense.Length; index++)
            {
                selfBuffs.Defense[index] = F32.Round(selfBuffs.Defense[index] + selfBuffs.Defense[0]);
            }
        }

        for (int index = 0; index < selfBuffs.Defense.Length; index++)
        {
            totals.Def[index] = selfBuffs.Defense[index];
            totals.Res[index] = selfBuffs.Resistance[index];
            totals.Elusivity[index] = selfBuffs.Elusivity[index];
        }

        for (int index = 0; index < selfBuffs.StatusProtection.Length; index++)
        {
            totals.Mez[index] = selfBuffs.StatusProtection[index];
            totals.MezRes[index] = F32.Round(selfBuffs.StatusResistance[index] * 100);
        }

        for (int index = 0; index < selfBuffs.DebuffResistance.Length; index++)
        {
            totals.DebuffRes[index] = F32.Round(selfBuffs.DebuffResistance[index] * 100);
        }
    }

    /// <summary>Movement speeds, their MaxSpeed caps, the MaxMax clamps, and no fly speed without a fly power.</summary>
    private static void Movement(TotalsBuilder totals, BuffsX selfBuffs, ServerData server, IReadOnlyDictionary<string, int> stat, bool canFly)
    {
        totals.FlySpd = Speed(selfBuffs.Effect[stat["FlySpeed"]], server.BaseFlySpeed);
        totals.RunSpd = Speed(selfBuffs.Effect[stat["RunSpeed"]], server.BaseRunSpeed);
        totals.JumpSpd = Speed(selfBuffs.Effect[stat["JumpSpeed"]], server.BaseJumpSpeed);
        totals.JumpHeight = Speed(selfBuffs.Effect[stat["JumpHeight"]], server.BaseJumpHeight);
        totals.MaxFlySpd = F32.Round(server.MaxFlySpeed + F32.Round(selfBuffs.Effect[stat["MaxFlySpeed"]] * server.BaseFlySpeed));
        totals.MaxRunSpd = F32.Round(server.MaxRunSpeed + F32.Round(selfBuffs.Effect[stat["MaxRunSpeed"]] * server.BaseRunSpeed));
        totals.MaxJumpSpd = F32.Round(server.MaxJumpSpeed + F32.Round(selfBuffs.Effect[stat["MaxJumpSpeed"]] * server.BaseJumpSpeed));
        totals.FlySpd = Math.Min(totals.FlySpd, server.MaxMaxFlySpeed);
        totals.RunSpd = Math.Min(totals.RunSpd, server.MaxMaxRunSpeed);
        totals.JumpSpd = Math.Min(totals.JumpSpd, server.MaxMaxJumpSpeed);
        if (!canFly)
        {
            totals.FlySpd = 0.0;
        }
    }

    /// <summary>Damage seeded from <c>_selfEnhance</c> when the buffs hold none, then the min, average and max BuffDam pick.</summary>
    private static void BuffDamage(TotalsBuilder totals, BuffsX selfBuffs, BuffsX selfEnhance)
    {
        if (selfBuffs.Damage.All(value => Math.Abs(value) < FloatEpsilon))
        {
            for (int i = 0; i < selfBuffs.Damage.Length; i++)
            {
                selfBuffs.Damage[i] = F32.Round(selfBuffs.Damage[i] + selfEnhance.Damage[i]);
            }
        }

        double[] slice = selfBuffs.Damage[1..8];
        double minDmg = slice.Min();
        double maxDmg = slice.Max();

        // LINQ Average over float[] sums in double in order, divides, then narrows; this matches Mids, not the
        // Python port, whose sum() is compensated since Python 3.12.
        double sum = 0.0;
        foreach (double value in slice)
        {
            sum += value;
        }

        double avgDmg = F32.Round(sum / slice.Length);

        // Damage is float[], so each difference is itself narrowed before the comparison.
        double hiGap = F32.Round(maxDmg - avgDmg);
        double loGap = F32.Round(avgDmg - minDmg);
        if (hiGap < loGap)
        {
            totals.BuffDam = maxDmg;
        }
        else if (hiGap > loGap && minDmg > 0)
        {
            totals.BuffDam = minDmg;
        }
        else
        {
            totals.BuffDam = maxDmg;
        }
    }

    /// <summary>The value copy of the totals clamped to the AT and server caps: TotalsCapped.</summary>
    private static TotalsBuilder ApplyCaps(TotalsBuilder totals, Archetype archetype, ServerData server)
    {
        TotalsBuilder capped = totals.Assign();
        capped.BuffDam = Math.Min(capped.BuffDam, F32.Round(archetype.DamageCap - 1));
        capped.BuffHaste = Math.Min(capped.BuffHaste, F32.Round(archetype.RechargeCap - 1));
        capped.HpRegen = Math.Min(capped.HpRegen, F32.Round(archetype.RegenCap - 1));
        capped.EndRec = Math.Min(capped.EndRec, F32.Round(archetype.RecoveryCap - 1));
        for (int index = 0; index < capped.Res.Length; index++)
        {
            capped.Res[index] = Math.Min(capped.Res[index], F32.Round(archetype.ResCap));
        }

        if (archetype.HpCap > 0)
        {
            capped.HpMax = Math.Min(capped.HpMax, F32.Round(archetype.HpCap));
            capped.Absorb = Math.Min(capped.Absorb, capped.HpMax);
        }

        capped.RunSpd = Math.Min(capped.RunSpd, totals.MaxRunSpd);
        capped.JumpSpd = Math.Min(capped.JumpSpd, totals.MaxJumpSpd);
        capped.FlySpd = Math.Min(capped.FlySpd, totals.MaxFlySpd);
        capped.JumpHeight = Math.Min(capped.JumpHeight, server.MaxJumpHeight);
        capped.Perception = Math.Min(capped.Perception, F32.Round(archetype.PerceptionCap));
        return capped;
    }

    private static double Speed(double effectValue, double baseSpeed)
    {
        return F32.Round(F32.Round(1 + Math.Max(effectValue, F32.Round(-0.9))) * baseSpeed);
    }
}
