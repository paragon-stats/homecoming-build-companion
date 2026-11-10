using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;
using CohEngine.Core.Statistics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Statistics;

// Ported from tests/test_endurance_budget.py. test_loader_accepts_str_path has no counterpart: every path here is a string.
public sealed class EnduranceBudgetsTests
{
    private static readonly string Totals = Path.Join(Fixtures.Store.MidsDirectory, "builds", "shield_scrapper_set_bonuses", "totals.json");
    private static readonly string Archetypes = Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical", "archetypes");

    public static TheoryData<string> Corrupt => [string.Empty, "{\"Totals\": {", "not json"];

    private static Archetype Scrapper => Fixtures.Store.Archetypes.Classes.Single(item => string.Equals(item.ClassName, "Class_Scrapper", StringComparison.Ordinal));

    private static double Recovery(TotalStatistics capped)
    {
        double factor = F32.Round((capped.EndRec + 1.0) * F32.Round(Scrapper.BaseRecovery * F32.Round(1.666667)));
        return F32.Round(factor * F32.Round((capped.EndMax / 100.0) + 1.0));
    }

    private static JsonElement Raw(string section)
    {
        return CanonicalJson.ReadJson(Totals, ErrorCodes.E33).Req(section);
    }

    [Fact]
    public void The_loader_round_trips_the_endurance_fields()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        Assert.Equal(F32.Round(Raw("Totals").ReqNum("EndRec")), totals.EndRec);
        Assert.Equal(F32.Round(Raw("Totals").ReqNum("EndUse")), totals.EndUse);
        Assert.Equal(F32.Round(Raw("Totals").ReqNum("EndMax")), totals.EndMax);
        Assert.Equal(F32.Round(Raw("TotalsCapped").ReqNum("EndRec")), capped.EndRec);
        Assert.Equal(F32.Round(Raw("Totals").Req("Def")[11].GetDouble()), totals.Def[11]);
        Assert.Equal(Raw("Totals").ReqItems("Res").Count, totals.Res.Count);
    }

    [Fact]
    public void The_loader_populates_every_totals_field()
    {
        (TotalStatistics totals, _, _) = EnduranceBudgets.LoadMidsTotals(Totals);
        JsonElement raw = Raw("Totals");
        (string Key, double Value)[] scalars =
        [
            ("HPRegen", totals.HpRegen), ("HPMax", totals.HpMax), ("Absorb", totals.Absorb), ("EndRec", totals.EndRec), ("EndUse", totals.EndUse), ("EndMax", totals.EndMax),
            ("RunSpd", totals.RunSpd), ("MaxRunSpd", totals.MaxRunSpd), ("JumpSpd", totals.JumpSpd), ("MaxJumpSpd", totals.MaxJumpSpd), ("FlySpd", totals.FlySpd), ("MaxFlySpd", totals.MaxFlySpd),
            ("JumpHeight", totals.JumpHeight), ("StealthPvE", totals.StealthPve), ("StealthPvP", totals.StealthPvp), ("ThreatLevel", totals.ThreatLevel), ("Perception", totals.Perception),
            ("BuffHaste", totals.BuffHaste), ("BuffAcc", totals.BuffAcc), ("BuffToHit", totals.BuffToHit), ("BuffDam", totals.BuffDam), ("BuffEndRdx", totals.BuffEndRdx), ("BuffRange", totals.BuffRange), ("BuffHeal", totals.BuffHeal),
        ];
        (string Key, IReadOnlyList<double> Value)[] vectors = [("Def", totals.Def), ("Res", totals.Res), ("Mez", totals.Mez), ("MezRes", totals.MezRes), ("DebuffRes", totals.DebuffRes), ("Elusivity", totals.Elusivity)];

        Assert.All(scalars, scalar => Assert.Equal(F32.Round(raw.ReqNum(scalar.Key)), scalar.Value));
        Assert.All(vectors, vector => Assert.Equal(raw.ReqItems(vector.Key).Select(cell => F32.Round(cell.GetDouble())), vector.Value));
        Assert.Equal(30, scalars.Length + vectors.Length);
    }

    [Theory]
    [MemberData(nameof(Corrupt))]
    public void A_corrupt_totals_file_is_e33(string content)
    {
        using TempTree tree = new();
        string path = tree.Write("totals.json", content);

        string message = Assert.Throws<EngineException>(() => EnduranceBudgets.LoadMidsTotals(path)).Message;

        Assert.True(message.StartsWith("E33: ", StringComparison.Ordinal) && message.Contains("totals.json is not valid JSON", StringComparison.Ordinal), message);
    }

    [Fact]
    public void A_corrupt_archetype_file_is_e33()
    {
        using TempTree tree = new();
        tree.Write("tanker.json", string.Empty);

        string message = Assert.Throws<EngineException>(() => EnduranceBudgets.LoadCanonicalCaps(tree.Root, "Class_Tanker")).Message;

        Assert.True(message.StartsWith("E33: ", StringComparison.Ordinal) && message.Contains("tanker.json is not valid JSON", StringComparison.Ordinal), message);
    }

    [Fact]
    public void A_missing_key_is_e31_naming_the_section_and_the_key()
    {
        using TempTree tree = new();
        string text = File.ReadAllText(Totals);
        int capped = text.IndexOf("\"TotalsCapped\"", StringComparison.Ordinal);
        string path = tree.Write("totals.json", string.Concat(text.AsSpan(0, capped), text[capped..].Replace("\"EndMax\"", "\"Gone\"", StringComparison.Ordinal)));

        string message = Assert.Throws<EngineException>(() => EnduranceBudgets.LoadMidsTotals(path)).Message;

        Assert.Contains("'TotalsCapped' has no key 'EndMax'", message, StringComparison.Ordinal);
        Assert.StartsWith("E31", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_section_is_e31()
    {
        using TempTree tree = new();
        string path = tree.Write("totals.json", "{}");

        Assert.Contains("has no key 'Totals'", Assert.Throws<EngineException>(() => EnduranceBudgets.LoadMidsTotals(path)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fixture_build_is_sustainable()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        EnduranceBudget budget = EnduranceBudgets.BuildEnduranceBudget(totals, capped, Scrapper);

        Assert.Equal((Recovery(capped), totals.EndUse, 100.0, true), (budget.RecoveryPerSec, budget.ToggleDrainPerSec, budget.MaxEnd, double.IsPositiveInfinity(budget.TimeToEmpty())));
    }

    [Fact]
    public void Max_end_is_100_plus_the_uncapped_end_max()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        Assert.Equal(125.0, EnduranceBudgets.BuildEnduranceBudget(totals with { EndMax = 25.0 }, capped with { EndMax = 0.0 }, Scrapper).MaxEnd);
    }

    [Fact]
    public void A_draining_case_has_an_exact_time_to_empty()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);
        double recovery = Recovery(capped);
        double drain = F32.Round(recovery + 2.0);

        EnduranceBudget budget = EnduranceBudgets.BuildEnduranceBudget(totals with { EndUse = drain }, capped, Scrapper);

        Assert.Equal(F32.Round(100.0 / -F32.Round(recovery - drain)), budget.TimeToEmpty());
    }

    [Fact]
    public void Canonical_caps_are_read_per_archetype()
    {
        (double endCap, double hpCap) = EnduranceBudgets.LoadCanonicalCaps(Archetypes, "Class_Arachnos_Soldier");

        Assert.Equal((F32.Round(365.0), F32.Round(2409.5176), F32.Round(3533.958984375)), (endCap, hpCap, EnduranceBudgets.LoadCanonicalCaps(Archetypes, "Class_Tanker").MaxHp));
    }

    [Fact]
    public void An_unknown_class_has_no_canonical_caps()
    {
        Assert.StartsWith("E32", Assert.Throws<EngineException>(() => EnduranceBudgets.LoadCanonicalCaps(Archetypes, "Class_Nobody")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_max_end_cap_clamps_the_pool_and_the_recovery_scaling()
    {
        (TotalStatistics totals, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        EnduranceBudget over = EnduranceBudgets.BuildEnduranceBudget(totals with { EndMax = 300.0 }, capped with { EndMax = 300.0 }, Scrapper, 365.0);
        EnduranceBudget under = EnduranceBudgets.BuildEnduranceBudget(totals with { EndMax = 20.0 }, capped with { EndMax = 20.0 }, Scrapper, 365.0);

        Assert.Equal((F32.Round(365.0), Recovery(capped with { EndMax = 265.0 })), (over.MaxEnd, over.RecoveryPerSec));
        Assert.Equal((F32.Round(120.0), Recovery(capped with { EndMax = 20.0 })), (under.MaxEnd, under.RecoveryPerSec));
    }

    [Fact]
    public void Hp_regeneration_scales_with_the_capped_max_hp()
    {
        (_, TotalStatistics capped, _) = EnduranceBudgets.LoadMidsTotals(Totals);

        HealthBudget health = EnduranceBudgets.BuildHealthBudget(capped, Scrapper);
        HealthBudget bigger = EnduranceBudgets.BuildHealthBudget(capped with { HpMax = F32.Round(capped.HpMax * 2.0) }, Scrapper);
        double expected = F32.Round(F32.Round(F32.Round(capped.HpRegen + 1.0) * F32.Round(Scrapper.BaseRegen * 1.6666666)) * capped.HpMax / 100.0);

        Assert.Equal((capped.HpMax, expected), (health.MaxHp, health.RegenPerSec));
        Near.Rel(health.RegenPerSec * 2.0, bigger.RegenPerSec, 1e-6);
    }

    [Fact]
    public void The_totals_reader_returns_the_class()
    {
        Assert.Equal("Class_Scrapper", EnduranceBudgets.LoadMidsTotals(Totals).ClassName);
    }
}
