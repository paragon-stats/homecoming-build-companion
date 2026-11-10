using System.Text.Json;

using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;

namespace CohEngine.Core.Tests.Registry;

/// <summary>Synthetic powers and slots for the rule tests, and the shared legality dump.</summary>
internal static class RegistryMake
{
    private static readonly Lazy<IReadOnlyDictionary<int, EnhancementLegality>> Legality =
        new(() => EnhancementLegality.Load(Path.Join(Fixtures.Store.MidsDirectory, "enhancements.json")));

    public static IReadOnlyDictionary<int, EnhancementLegality> EnhLeg => Legality.Value;

    public static Power Power(int buildIndex, string fullName, int[]? setTypes = null, int[]? enhancements = null)
    {
        return Make.Power() with
        {
            BuildIndex = buildIndex,
            NidPower = buildIndex,
            FullName = fullName,
            StaticIndex = -1,
            PowerType = "Click",
            SetTypes = setTypes ?? [],
            Enhancements = enhancements ?? [],
        };
    }

    public static SlotRef Slot(int enh, int level = 1, bool inherent = false, int ioLevel = 49)
    {
        return new SlotRef { Level = level, IsInherent = inherent, Enh = enh, Grade = 0, IoLevel = ioLevel, RelativeLevel = 4 };
    }

    public static IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> Slots(params (int BuildIndex, SlotRef[] Slots)[] entries)
    {
        return entries.ToDictionary(entry => entry.BuildIndex, entry => (IReadOnlyList<SlotRef>)entry.Slots);
    }

    public static int SetType(int enhNid)
    {
        return Fixtures.Store.SetBonuses.Sets[EnhLeg[enhNid].NidSet].SetType;
    }

    public static int Nid(string uid)
    {
        return EnhLeg.First(pair => string.Equals(pair.Value.Uid, uid, StringComparison.Ordinal)).Key;
    }

    public static IReadOnlyList<JsonElement> Probe()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Join(Fixtures.Store.MidsDirectory, "legality_probe.json")));
        return [.. document.RootElement.EnumerateArray().Select(element => element.Clone())];
    }

    public static IReadOnlyList<Diagnostic> Legal(IReadOnlyList<Power> powers, IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots, IReadOnlyDictionary<int, EnhancementRecord>? effects = null)
    {
        return LegalityRunner.Check(powers, slots, EnhLeg, Fixtures.Store.SetBonuses, effects);
    }

    public static bool Has(IEnumerable<Diagnostic> diagnostics, string ruleId)
    {
        return diagnostics.Any(diagnostic => string.Equals(diagnostic.RuleId, ruleId, StringComparison.Ordinal));
    }
}
