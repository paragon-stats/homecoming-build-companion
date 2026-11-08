using CohEngine.Core.Data;

namespace CohEngine.Core.SetBonuses;

/// <summary><c>GetSetBonusVirtualPower</c>'s fold: the Rule of Five and the MyPet skip, then the surviving effects cloned.</summary>
internal static class RuleOfFive
{
    /// <summary>At most five instances of an exact bonus id fold; the counter is incremented before the test.</summary>
    private const int Limit = 6;

    /// <summary>
    /// Folds the activated bonus ids into effects. The counter keys on the exact set_bonus power id and counts before
    /// both the limit test and the MyPet skip, so a skipped bonus still consumes a slot. A bonus power the dump lacks
    /// models <c>Database.Power[id] == null</c>: it counts but adds nothing.
    /// </summary>
    /// <param name="ordered">The tallies with resolved bonus ids, in fold order.</param>
    /// <param name="setDb">The set records.</param>
    /// <returns>The virtual power's effects.</returns>
    public static List<Effect> Fold(IEnumerable<SetInfo> ordered, SetBonusDb setDb)
    {
        Dictionary<int, int> counter = [];
        List<Effect> effects = [];
        foreach (SetInfo info in ordered)
        {
            foreach (int pid in info.PowerIds)
            {
                if (pid < 0)
                {
                    continue;
                }

                counter[pid] = counter.GetValueOrDefault(pid, 0) + 1;
                SetBonusPower? bonusPower = setDb.Powers.GetValueOrDefault(pid);
                if (bonusPower is not null && bonusPower.MyPetOnly)
                {
                    continue;
                }

                if (counter[pid] < Limit && bonusPower is not null)
                {
                    effects.AddRange(bonusPower.Effects);
                }
            }
        }

        return effects;
    }
}
