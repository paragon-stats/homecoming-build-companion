using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Statistics;

/// <summary>Max HP (AT-capped) and the HP regenerated per second, which scales with it.</summary>
/// <param name="MaxHp">The max HP.</param>
/// <param name="RegenPerSec">The regeneration per second.</param>
public sealed record HealthBudget(double MaxHp, double RegenPerSec);

/// <summary>The endurance and health budgets a build starts from, and the Mids totals loader they read.</summary>
public static class EnduranceBudgets
{
    private const double BaseEnd = 100.0;
    private const int Level50 = 49;

    private static readonly double RegenMagic = F32.Round(1.66666662693024);
    private static readonly double BaseMagic = F32.Round(1.666667);

    /// <summary>The AT's hard caps at level 50 from canonical <c>attrib_max_max</c>: max endurance and max HP.</summary>
    /// <param name="archetypesDir">The canonical archetypes directory.</param>
    /// <param name="className">The class name.</param>
    /// <returns>The caps.</returns>
    /// <exception cref="ChainException">E32 when no canonical archetype file exists; E33 when it is not valid JSON.</exception>
    public static (double MaxEnd, double MaxHp) LoadCanonicalCaps(string archetypesDir, string className)
    {
        ArgumentNullException.ThrowIfNull(className);
        string stem = (className.StartsWith("Class_", StringComparison.Ordinal) ? className["Class_".Length..] : className).ToLowerInvariant();
        string path = Path.Combine(archetypesDir, stem + ".json");
        if (!File.Exists(path))
        {
            throw new ChainException(
                $"E32: no canonical archetype file {path} for {className}; pass the AT class name "
                + "(e.g. Class_Arachnos_Soldier) or restore data/canonical/archetypes");
        }

        JsonElement caps = CanonicalJson.ReadJson(path, "E33").Req("attrib_max_max");
        return (F32.Round(caps.Req("endurance")[Level50].GetDouble()), F32.Round(caps.Req("hit_points")[Level50].GetDouble()));
    }

    /// <summary>The endurance pool, recovery per second and toggle drain per second a build starts from.</summary>
    /// <param name="totals">The uncapped totals.</param>
    /// <param name="totalsCapped">The capped totals.</param>
    /// <param name="archetype">The build's class record.</param>
    /// <param name="maxEndCap">The AT's hard max-endurance cap, or null.</param>
    /// <returns>The budget.</returns>
    public static EnduranceBudget BuildEnduranceBudget(TotalStatistics totals, TotalStatistics totalsCapped, Archetype archetype, double? maxEndCap = null)
    {
        ArgumentNullException.ThrowIfNull(totals);
        ArgumentNullException.ThrowIfNull(totalsCapped);
        ArgumentNullException.ThrowIfNull(archetype);
        double maxEnd = F32.Round(BaseEnd + totals.EndMax);
        if (maxEndCap is { } cap && maxEnd > cap)
        {
            maxEnd = F32.Round(cap);
            totalsCapped = totalsCapped with { EndMax = F32.Round(cap - BaseEnd) };
        }

        return new EnduranceBudget(maxEnd, RecoveryPerSec(totalsCapped, archetype), totals.EndUse);
    }

    /// <summary>Max HP and HP regenerated per second over the capped totals.</summary>
    /// <param name="totalsCapped">The capped totals.</param>
    /// <param name="archetype">The class record.</param>
    /// <returns>The budget.</returns>
    public static HealthBudget BuildHealthBudget(TotalStatistics totalsCapped, Archetype archetype)
    {
        ArgumentNullException.ThrowIfNull(totalsCapped);
        ArgumentNullException.ThrowIfNull(archetype);
        double rate = F32.Round(F32.Round(totalsCapped.HpRegen + 1.0) * F32.Round(archetype.BaseRegen * RegenMagic));
        return new HealthBudget(totalsCapped.HpMax, F32.Round(rate * totalsCapped.HpMax / 100.0));
    }

    /// <summary>Reads a Mids dump <c>totals.json</c> into the uncapped and capped totals.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The totals and the class name.</returns>
    /// <exception cref="ChainException">E31 when a section or field is missing; E33 when the file is not valid JSON.</exception>
    public static (TotalStatistics Totals, TotalStatistics Capped, string ClassName) LoadMidsTotals(string path)
    {
        JsonElement data = CanonicalJson.ReadJson(path, "E33");
        TotalStatistics totals = TotalsFrom(Required(data, "Totals", "<file>", path), "Totals", path);
        TotalStatistics capped = TotalsFrom(Required(data, "TotalsCapped", "<file>", path), "TotalsCapped", path);
        return (totals, capped, data.ReqStr("Class"));
    }

    private static double RecoveryPerSec(TotalStatistics capped, Archetype archetype)
    {
        double factor = F32.Round((capped.EndRec + 1.0) * F32.Round(archetype.BaseRecovery * BaseMagic));
        return F32.Round(factor * F32.Round((capped.EndMax / 100.0) + 1.0));
    }

    private static JsonElement Required(JsonElement section, string key, string name, string path)
    {
        return section.ValueKind == JsonValueKind.Object && section.TryGetProperty(key, out JsonElement value)
            ? value
            : throw new ChainException($"E31: {path}: '{name}' has no key '{key}'; re-run tools/mids-dump to regenerate totals.json");
    }

    private static TotalStatistics TotalsFrom(JsonElement section, string name, string path)
    {
        double[] Vector(string key) => [.. Required(section, key, name, path).EnumerateArray().Select(cell => F32.Round(cell.GetDouble()))];
        double Scalar(string key) => F32.Round(Required(section, key, name, path).GetDouble());
        return new TotalStatistics
        {
            Def = Vector("Def"),
            Res = Vector("Res"),
            Mez = Vector("Mez"),
            MezRes = Vector("MezRes"),
            DebuffRes = Vector("DebuffRes"),
            Elusivity = Vector("Elusivity"),
            HpRegen = Scalar("HPRegen"),
            HpMax = Scalar("HPMax"),
            Absorb = Scalar("Absorb"),
            EndRec = Scalar("EndRec"),
            EndUse = Scalar("EndUse"),
            EndMax = Scalar("EndMax"),
            RunSpd = Scalar("RunSpd"),
            MaxRunSpd = Scalar("MaxRunSpd"),
            JumpSpd = Scalar("JumpSpd"),
            MaxJumpSpd = Scalar("MaxJumpSpd"),
            FlySpd = Scalar("FlySpd"),
            MaxFlySpd = Scalar("MaxFlySpd"),
            JumpHeight = Scalar("JumpHeight"),
            StealthPve = Scalar("StealthPvE"),
            StealthPvp = Scalar("StealthPvP"),
            ThreatLevel = Scalar("ThreatLevel"),
            Perception = Scalar("Perception"),
            BuffHaste = Scalar("BuffHaste"),
            BuffAcc = Scalar("BuffAcc"),
            BuffToHit = Scalar("BuffToHit"),
            BuffDam = Scalar("BuffDam"),
            BuffEndRdx = Scalar("BuffEndRdx"),
            BuffRange = Scalar("BuffRange"),
            BuffHeal = Scalar("BuffHeal"),
        };
    }
}
