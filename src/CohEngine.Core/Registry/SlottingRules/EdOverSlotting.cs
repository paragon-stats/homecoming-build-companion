using System.Globalization;

using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Registry.SlottingRules;

/// <summary>
/// P-ED-001 and P-ED-002: plain IOs that Enhancement Diversification makes nearly worthless (warnings). Both kinds are
/// classified once into the context; the two registered rules each read their own kind.
/// </summary>
internal static class EdOverSlotting
{
    /// <summary>The rule id of an IO slotted past the last threshold.</summary>
    public const string PastThreshold = "P-ED-001";

    /// <summary>The rule id of a copy that boosting the others would replace.</summary>
    public const string OverBoosting = "P-ED-002";

    private const string PlainIo = "InventO";
    private const int PlusFive = 9;
    private const int LastThreshold = 2;
    private const double Percent = 100.0;

    // Five points of the aspect is the least a freed slot must be worth for the copy to be kept.
    private const double KeepsAtLeast = 0.05;

    /// <summary>Runs P-ED-001.</summary>
    /// <param name="context">The slotting context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> RunPastThreshold(SlottingContext context)
    {
        return context.EdFindings.Where(finding => string.Equals(finding.RuleId, PastThreshold, StringComparison.Ordinal));
    }

    /// <summary>Runs P-ED-002.</summary>
    /// <param name="context">The slotting context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> RunOverBoosting(SlottingContext context)
    {
        return context.EdFindings.Where(finding => string.Equals(finding.RuleId, OverBoosting, StringComparison.Ordinal));
    }

    /// <summary>Classifies every power's plain IOs into both kinds of finding.</summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="slots">The build's slots by build index.</param>
    /// <param name="enhDb">The enhancement legality records.</param>
    /// <param name="enhEffects">Enhancement-mode effects by index; a slot whose enhancement is missing is skipped.</param>
    /// <param name="tables">The Maths.mhd tables.</param>
    /// <returns>The findings, per power in build order: P-ED-001 first, then P-ED-002.</returns>
    public static IReadOnlyList<Diagnostic> Classify(
        IReadOnlyList<Power> powers,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, EnhancementLegality> enhDb,
        IReadOnlyDictionary<int, EnhancementRecord> enhEffects,
        MathTables tables)
    {
        List<Diagnostic> found = [];
        foreach (Power power in powers)
        {
            // shortcut: SOs, HOs and set pieces are never flagged, upgrade when #21 weighs set bonuses against slots
            Item[] items = [.. Items(slots.GetValueOrDefault(power.BuildIndex, []), enhDb, enhEffects)];
            Calculator calc = new(enhEffects, tables);
            HashSet<int> flagged = [];
            found.AddRange(PastThresholdFindings(power, items, calc, flagged));
            found.AddRange(OverBoostingFindings(power, items, calc, flagged));
        }

        return found;
    }

    private static IEnumerable<Item> Items(IReadOnlyList<SlotRef> slots, IReadOnlyDictionary<int, EnhancementLegality> enhDb, IReadOnlyDictionary<int, EnhancementRecord> enhEffects)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            SlotRef slot = slots[index];
            if (slot.Enh >= 0 && enhDb.TryGetValue(slot.Enh, out EnhancementLegality? legality) && enhEffects.TryGetValue(slot.Enh, out EnhancementRecord? record))
            {
                yield return new Item(index + 1, slot, legality, record);
            }
        }
    }

    private static IEnumerable<Diagnostic> PastThresholdFindings(Power power, Item[] items, Calculator calc, HashSet<int> flagged)
    {
        List<Item> running = [.. items.Where(item => !item.IsPlain)];
        foreach (Item plain in items.Where(item => item.IsPlain))
        {
            Aspect[] aspects = plain.Aspects();
            if (aspects.Length > 0 && aspects.All(aspect => calc.Sum(running, aspect, skipEd: true) > calc.Last(aspect)))
            {
                flagged.Add(plain.Position);
                yield return PastThresholdFinding(power, plain, aspects[0], running, calc);
            }

            running.Add(plain);
        }
    }

    private static Diagnostic PastThresholdFinding(Power power, Item plain, Aspect aspect, List<Item> running, Calculator calc)
    {
        double before = calc.Sum(running, aspect, skipEd: true);
        double adds = calc.Sum([.. running, plain], aspect, skipEd: false) - calc.Sum(running, aspect, skipEd: false);
        string message = string.Create(CultureInfo.InvariantCulture, $"'{plain.Legality.LongName}' in '{power.FullName}' slot {plain.Position} adds {Pct(adds)}% {aspect.Name}: the power's {aspect.Name} is {Pct(before)}% ")
            + $"before Enhancement Diversification, past the {Pct(calc.Last(aspect))}% last threshold, so this slot counts at 15% of its {Pct(calc.Sum([plain], aspect, skipEd: true))}% value";
        return new Diagnostic(PastThreshold, "warning", message)
        {
            Fix = "remove this slot and place it where it adds more; boosting the remaining IOs to +5 recovers most of the value",
        };
    }

    private static IEnumerable<Diagnostic> OverBoostingFindings(Power power, Item[] items, Calculator calc, HashSet<int> flagged)
    {
        foreach (Item[] copies in items.Where(item => item.IsPlain).GroupBy(item => item.Slot.Enh).Select(group => group.ToArray()))
        {
            Aspect[] aspects = copies[0].Aspects();
            if (copies.Length < 2 || aspects.Length == 0 || copies.Any(copy => flagged.Contains(copy.Position)))
            {
                continue;
            }

            Item removed = copies[^1];
            Item[] without = [.. items.Where(item => item.Position != removed.Position)];
            Item[] boosted = [.. without.Select(item => copies.Any(copy => copy.Position == item.Position) ? item with { Slot = item.Slot with { RelativeLevel = PlusFive } } : item)];
            double current = calc.Sum(items, aspects[0], skipEd: false);
            double alternative = calc.Sum(boosted, aspects[0], skipEd: false);
            if (current - alternative < KeepsAtLeast)
            {
                yield return OverBoostingFinding(power, removed, aspects[0], (current, alternative, calc.Sum(without, aspects[0], skipEd: false)), copies.Length - 1);
            }
        }
    }

    private static Diagnostic OverBoostingFinding(Power power, Item removed, Aspect aspect, (double Current, double Alternative, double Without) values, int others)
    {
        string message = string.Create(CultureInfo.InvariantCulture, $"'{removed.Legality.LongName}' in '{power.FullName}' slot {removed.Position} adds {Pct(values.Current - values.Without)}% {aspect.Name} over boosting: ")
            + string.Create(CultureInfo.InvariantCulture, $"the power reaches {Pct(values.Current)}% with it and {Pct(values.Alternative)}% with the other {others} boosted to +5");
        return new Diagnostic(OverBoosting, "warning", message)
        {
            Fix = string.Create(CultureInfo.InvariantCulture, $"remove this slot and boost the remaining {others} IOs to +5 with Enhancement Boosters"),
        };
    }

    private static string Pct(double fraction)
    {
        return (fraction * Percent).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private readonly record struct Aspect(string Name, int Sub);

    private readonly record struct Item(int Position, SlotRef Slot, EnhancementLegality Legality, EnhancementRecord Record)
    {
        public bool IsPlain => string.Equals(Legality.TypeName, PlainIo, StringComparison.Ordinal);

        public Aspect[] Aspects()
        {
            return [.. Record.Effects.Where(effect => effect.Schedule >= 0).Select(effect => new Aspect(effect.Enhance, effect.EnhanceSubId)).Distinct()];
        }
    }

    private sealed record Calculator(IReadOnlyDictionary<int, EnhancementRecord> EnhEffects, MathTables Tables)
    {
        public double Sum(IEnumerable<Item> items, Aspect aspect, bool skipEd)
        {
            return Pipeline.AggregateAndEd(items.Select(item => item.Slot), aspect.Name, new EnhancementTables(EnhEffects, int.MaxValue, Tables), aspect.Sub, mag: 1.0, skipEd: skipEd);
        }

        public double Last(Aspect aspect)
        {
            return Tables.MultEd[(int)Pipeline.ScheduleForEnhance(aspect.Name, aspect.Sub)][LastThreshold];
        }
    }
}
