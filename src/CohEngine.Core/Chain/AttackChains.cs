using CohEngine.Core.DamageLayers;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Chain;

/// <summary>The attack-chain sequencer and planner.</summary>
public static class AttackChains
{
    private static readonly double ArcanaTick = F32.Round(0.132);

    /// <summary>Rounds a raw cast time up to the game's 0.132 s tick, plus one tick.</summary>
    /// <param name="castTime">The raw cast time.</param>
    /// <returns>The arcana cast time.</returns>
    public static double ArcanaCastTime(double castTime)
    {
        return F32.Round(F32.Round(Math.Ceiling(F32.Round(castTime / ArcanaTick)) + 1) * ArcanaTick);
    }

    /// <summary>Searches every subset of the chain attacks and ranks them by affordability, then damage.</summary>
    /// <param name="attacks">The attacks.</param>
    /// <param name="stance">The stance.</param>
    /// <param name="movement">The movement.</param>
    /// <param name="scenario">The scenario.</param>
    /// <param name="budget">The endurance budget.</param>
    /// <param name="timelineAvailable">The timeline share left.</param>
    /// <param name="otherDrainPerSec">The other sources' drain.</param>
    /// <param name="minCycles">The cycles an option must last to be affordable.</param>
    /// <param name="maxAttacks">The attack cap.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="EngineException">E45 when the timeline share is outside (0, 1].</exception>
    public static ChainPlan Plan(
        IEnumerable<ChainAttack> attacks,
        Stance stance,
        Movement movement,
        Scenario scenario,
        EnduranceBudget budget,
        double timelineAvailable = 1.0,
        double otherDrainPerSec = 0.0,
        double minCycles = 4.0,
        int maxAttacks = 12)
    {
        ArgumentNullException.ThrowIfNull(attacks);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(budget);
        if (timelineAvailable is not (> 0.0 and <= 1.0))
        {
            throw new EngineException(
                $"{ErrorCodes.E45}: timeline_available is {PyFloat.Repr(timelineAvailable)} but must lie in (0, 1]; pass the timeline share left after non-chain sources");
        }

        List<(ChainAttack Attack, double Cast)> rows =
        [
            .. attacks
                .Where(attack => attack.Profile.Role == Role.Chain && !(stance == Stance.Ranged && attack.Profile.NeedsMeleePosition))
                .Select(attack => (attack, ArcanaCastTime(attack.CastTime))),
        ];
        string[] dropped = [];
        if (rows.Count > maxAttacks)
        {
            rows = PySort.Sorted(rows, (left, right) => Compare(-F32.Round(left.Attack.Damage / left.Cast), -F32.Round(right.Attack.Damage / right.Cast), left.Attack.FullName, right.Attack.FullName));
            dropped = [.. rows.Skip(maxAttacks).Select(row => row.Attack.FullName)];
            rows = rows[..maxAttacks];
        }

        rows = PySort.Sorted(rows, (left, right) =>
        {
            int melee = (!left.Attack.Profile.NeedsMeleePosition).CompareTo(!right.Attack.Profile.NeedsMeleePosition);
            return melee != 0 ? melee : Compare(-left.Attack.RechargeTime, -right.Attack.RechargeTime, left.Attack.FullName, right.Attack.FullName);
        });

        double switchTime = movement.SwitchTime;
        double scaled = F32.Round(timelineAvailable);
        Row[] perAttack =
        [
            .. rows.Select(row => new Row(
                row.Attack.FullName,
                row.Attack.Profile.NeedsMeleePosition,
                row.Cast,
                F32.Round(row.Cast + row.Attack.RechargeTime),
                F32.Round(row.Attack.Damage * scenario.TargetsHit(row.Attack.Profile)),
                row.Attack.EndCost,
                row.Attack.TargetResDebuff,
                row.Attack.TargetResDebuffDuration)),
        ];
        List<ChainOption> options = [];
        for (int mask = 1; mask < (1 << perAttack.Length); mask++)
        {
            List<Row> picked = [];
            for (int i = 0; i < perAttack.Length; i++)
            {
                if (((mask >> i) & 1) != 0)
                {
                    picked.Add(perAttack[i]);
                }
            }

            bool mixed = picked.Any(row => row.Melee) && !picked.All(row => row.Melee);
            int switches = stance == Stance.Hybrid && mixed ? 2 : 0;
            if (switches != 0 && double.IsPositiveInfinity(switchTime))
            {
                continue;
            }

            double moveTime = switches != 0 ? F32.Round(switches * switchTime) : 0.0;
            double castSum = F32Sum(picked.Select(row => row.Cast));
            double cycleTime = Math.Max(F32.Round(castSum + moveTime), picked.Max(row => row.Cycle));
            double gap = F32.Round(cycleTime - F32.Round(castSum + moveTime));
            double damage = F32Sum(picked.Select(row => row.Damage));
            double endPerSec = F32.Round(F32.Round(F32Sum(picked.Select(row => row.EndCost)) / cycleTime) * scaled);
            double drain = F32.Round(otherDrainPerSec + endPerSec);
            double net = budget.NetPerSec(drain);
            double period = F32.Round(cycleTime / scaled);
            double resDebuff = F32Sum(picked.Where(row => row.Debuff > 0.0).Select(row => F32.Round(row.Debuff * Math.Min(1.0, F32.Round(row.DebuffDuration / period)))));
            options.Add(new ChainOption
            {
                Rotation = [.. picked.Select(row => row.Name)],
                Stance = stance,
                CastSum = castSum,
                Switches = switches,
                MoveTime = moveTime,
                CycleTime = cycleTime,
                Gap = gap,
                GapFree = gap <= 0.0,
                DamagePerCycle = damage,
                Dps = F32.Round(F32.Round(damage / cycleTime) * scaled),
                EndPerSec = endPerSec,
                NetEndPerSec = net,
                TimeToEmpty = budget.TimeToEmpty(drain),
                Sustainable = net >= 0.0,
                ResDebuff = resDebuff,
            });
        }

        bool Affordable(ChainOption option) => option.TimeToEmpty >= minCycles * F32.Round(option.CycleTime / scaled);
        int Rank(ChainOption left, ChainOption right)
        {
            int result = Affordable(right).CompareTo(Affordable(left));
            return result != 0 ? result : Tail(left, right);
        }

        List<ChainOption> ranked = PySort.Sorted(options, Rank);
        ChainOption? bestOverall = null;
        foreach (ChainOption option in ranked)
        {
            if (bestOverall is null || Tail(option, bestOverall) < 0)
            {
                bestOverall = option;
            }
        }

        return new ChainPlan(
            stance,
            ranked.FirstOrDefault(Affordable),
            ranked.FirstOrDefault(option => option.Sustainable),
            bestOverall,
            ranked,
            dropped);
    }

    /// <summary>Sequences a build's attacks into a rotation and derives its endurance drain.</summary>
    /// <param name="powerStats">The per-power statistics.</param>
    /// <returns>The chain.</returns>
    internal static AttackChain Build(IEnumerable<PowerStats> powerStats)
    {
        ArgumentNullException.ThrowIfNull(powerStats);
        List<PowerStats> attacks = [.. powerStats.Where(stats => stats.IsAttack)];
        if (attacks.Count == 0)
        {
            return new AttackChain([], 0.0, 0.0, 0.0, true, 0.0);
        }

        double castSum = F32Sum(attacks.Select(stats => stats.CastTime));
        double endSum = F32Sum(attacks.Select(stats => stats.EndCost));
        double bottleneck = attacks.Max(stats => F32.Round(stats.CastTime + stats.RechargeTime));
        double cycleTime = Math.Max(castSum, bottleneck);
        double gap = F32.Round(cycleTime - castSum);
        string[] rotation = [.. PySort.Sorted(attacks, (left, right) => Compare(-left.RechargeTime, -right.RechargeTime, left.FullName, right.FullName)).Select(stats => stats.FullName)];
        double endPerSec = cycleTime > 0.0 ? F32.Round(endSum / cycleTime) : 0.0;
        return new AttackChain(rotation, castSum, cycleTime, gap, gap <= 0.0, endPerSec);
    }

    private static double F32Sum(IEnumerable<double> values)
    {
        double total = 0.0;
        foreach (double value in values)
        {
            total = F32.Round(total + value);
        }

        return total;
    }

    private static int Compare(double left, double right, string leftName, string rightName)
    {
        int result = left.CompareTo(right);
        return result != 0 ? result : string.CompareOrdinal(leftName, rightName);
    }

    /// <summary>Orders by score descending, then gap, rotation length and rotation names.</summary>
    private static int Tail(ChainOption left, ChainOption right)
    {
        int result = (-left.Score).CompareTo(-right.Score);
        if (result == 0)
        {
            result = left.Gap.CompareTo(right.Gap);
        }

        if (result == 0)
        {
            result = left.Rotation.Count.CompareTo(right.Rotation.Count);
        }

        for (int i = 0; result == 0 && i < left.Rotation.Count; i++)
        {
            result = string.CompareOrdinal(left.Rotation[i], right.Rotation[i]);
        }

        return result;
    }

    private readonly record struct Row(string Name, bool Melee, double Cast, double Cycle, double Damage, double EndCost, double Debuff, double DebuffDuration);
}
