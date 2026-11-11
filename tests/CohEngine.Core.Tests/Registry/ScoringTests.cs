using System.Text.Json;

using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Profiles;
using CohEngine.Core.Registry;
using CohEngine.Core.Registry.ScoringMetrics;
using CohEngine.Core.Tests.Parity;
using CohEngine.Core.Totals;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Tests.Registry;

public sealed class ScoringTests
{
    private static readonly Lazy<(BaseTotals Base, IReadOnlyList<PowerStats> Stats, Archetype Scrapper)> Computed = new(() =>
    {
        BuildInputs inputs = new("shield_scrapper_set_bonuses");
        BaseTotals totals = EngineRun.Totals(inputs, EngineRun.Slotted(inputs) with { SetDb = Fixtures.Store.SetBonuses });
        Archetype scrapper = Fixtures.Store.Archetypes.Classes[inputs.ArchetypeIndex];
        PowerStatistics.StatsContext context = new()
        {
            Config = Fixtures.Store.Config,
            Tables = Fixtures.Store.Maths,
            EnhDb = Fixtures.Store.EnhancementEffects,
            GlobalEnhance = totals.GlobalEnhance,
            RechargeCap = scrapper.RechargeCap,
        };
        return (totals, PowerStatistics.ComputeBuildStats(inputs.Powers, inputs.Slots, context), scrapper);
    });

    private static readonly string Strategy = Path.Join(Fixtures.Store.RepositoryRoot, "strategy");

    private static BaseTotals Base => Computed.Value.Base;

    private static IReadOnlyList<PowerStats> Stats => Computed.Value.Stats;

    private static Archetype Scrapper => Computed.Value.Scrapper;

    private static BuildProfile Profile(string name)
    {
        JsonElement raw = ProfileResolver.LoadBuildProfiles(Path.Join(Strategy, "build_profiles.json"));
        return ProfileResolver.Resolve(name, raw, ProfileResolver.LoadBreakpoints(Path.Join(Strategy, "breakpoints.json")), "Scrapper");
    }

    private static BuildProfile Custom(params Target[] targets)
    {
        return new BuildProfile("x", "X", [], targets);
    }

    private static ScoreResult Score(BuildProfile profile, TotalStatistics? totals = null, IReadOnlyList<PowerStats>? stats = null, double chainDrain = 0.0)
    {
        return Scoring.ScoreBuild(profile, totals ?? Base.Totals, Base.TotalsCapped, stats ?? Stats, Scrapper, chainDrain);
    }

    [Fact]
    public void Positional_defense_min_is_the_lowest_of_melee_ranged_aoe()
    {
        Assert.Equal(14.47, Scoring.DefensePositionalMin(Base.Totals), 0.1);
    }

    [Fact]
    public void The_survivability_build_is_not_positionally_softcapped()
    {
        Assert.True(Scoring.DefensePositionalMin(Base.Totals) < 45.0);
    }

    [Fact]
    public void Resist_min_is_below_the_scrapper_cap()
    {
        Assert.True(Scoring.ResistMin(Base.Totals) < 75.0);
    }

    [Fact]
    public void Hasten_is_not_perma_and_a_scrapper_has_no_domination()
    {
        Assert.Equal((false, false), (Scoring.PermaRecharge(Stats, ".Hasten", Scoring.HastenDuration), Scoring.PermaRecharge(Stats, ".Domination", Scoring.DominationDuration)));
    }

    [Fact]
    public void A_recharge_at_or_under_the_duration_is_perma()
    {
        PowerStats fast = Stats.First(stats => stats.FullName.EndsWith(".Hasten", StringComparison.Ordinal)) with { RechargeTime = 100.0 };

        Assert.True(Scoring.PermaRecharge([fast], ".Hasten", Scoring.HastenDuration));
    }

    [Fact]
    public void Soft_cap_defense_reports_unmet_with_a_warning()
    {
        ScoreResult result = Score(Profile("soft-cap-defense"));

        Assert.Equal((0.0, false), (result.Score, result.Met["defense_positional_min"]));
        Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.RuleId, "P-GOAL-002", StringComparison.Ordinal) && string.Equals(diagnostic.Severity, "warning", StringComparison.Ordinal));
    }

    [Fact]
    public void The_unmet_goal_message_names_the_values()
    {
        Diagnostic goal = Score(Profile("soft-cap-defense")).Diagnostics.First(diagnostic => string.Equals(diagnostic.RuleId, "P-GOAL-002", StringComparison.Ordinal));

        Assert.Equal(
            ($"Soft-cap defense: defense_positional_min = {PyText.NumberRepr(Scoring.DefensePositionalMin(Base.Totals))} does not meet >= 45.0", "raise defense_positional_min to satisfy the Soft-cap defense target", (object)45.0),
            (goal.Message, goal.Fix, goal.Expected));
    }

    [Fact]
    public void A_profile_with_no_targets_scores_full()
    {
        ScoreResult result = Score(Profile("theme-rp"));

        Assert.Equal((1.0, 0), (result.Score, result.Met.Count));
    }

    [Fact]
    public void Toggle_endurance_is_sustainable_for_the_build()
    {
        Assert.True(Scoring.EnduranceRecoveryPerSec(Base.TotalsCapped, Scrapper) > Base.Totals.EndUse);
        Assert.Null(Scoring.EnduranceDiagnostic(Base.Totals, Base.TotalsCapped, Scrapper));
    }

    [Fact]
    public void Drain_over_recovery_warns_with_toggle_only_detail()
    {
        Diagnostic? found = Scoring.EnduranceDiagnostic(Base.Totals with { EndUse = 99.0 }, Base.TotalsCapped, Scrapper);

        Assert.Equal(("P-END-001", "warning", true), (found!.RuleId, found.Severity, found.Message.Contains("(toggles) exceeds", StringComparison.Ordinal)));
    }

    [Fact]
    public void Chain_drain_adds_to_toggle_drain()
    {
        Diagnostic? found = Scoring.EnduranceDiagnostic(Base.Totals, Base.TotalsCapped, Scrapper, chainDrain: 1.0);

        Assert.Contains("chain 1.000", found!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sustainable_build_with_no_targets_has_no_endurance_warning()
    {
        Assert.DoesNotContain(Score(Custom(), stats: []).Diagnostics, diagnostic => string.Equals(diagnostic.RuleId, "P-END-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Heavy_drain_adds_an_endurance_warning_to_the_score()
    {
        Assert.Contains(Score(Custom(), Base.Totals with { EndUse = 99.0 }).Diagnostics, diagnostic => string.Equals(diagnostic.RuleId, "P-END-001", StringComparison.Ordinal));
    }

    [Fact]
    public void An_over_cap_resistance_warns_and_never_errors()
    {
        double[] res = [.. Base.Totals.Res];
        res[1] = 0.99;

        IReadOnlyList<Diagnostic> found = Score(Profile("resist-cap"), Base.Totals with { Res = res }).Diagnostics;

        Assert.Equal(("warning", false), (found.First(diagnostic => string.Equals(diagnostic.RuleId, "P-CAP-001", StringComparison.Ordinal)).Severity, found.Any(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.Ordinal))));
    }

    [Fact]
    public void An_over_cap_scalar_warns_by_name()
    {
        IReadOnlyList<Diagnostic> found = Scoring.CapAdherenceDiagnostics(Base.Totals with { BuffHaste = 99.0 }, Base.TotalsCapped);

        Assert.Contains(found, diagnostic => diagnostic.Message.StartsWith("BuffHaste 99.000 exceeds the AT cap", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_over_cap_field_is_named()
    {
        TotalStatistics over = Base.Totals with { BuffDam = 99.0, HpRegen = 999.0, EndRec = 99.0 };

        string[] labels = [.. Scoring.CapAdherenceDiagnostics(over, Base.TotalsCapped).Select(diagnostic => diagnostic.Message.Split(' ')[0])];

        string[] expected = ["BuffDam", "HPRegen", "EndRec"];

        Assert.True(expected.SequenceEqual(labels.Where(label => !label.StartsWith("Res", StringComparison.Ordinal)), StringComparer.Ordinal));
    }

    [Fact]
    public void A_le_target_is_met()
    {
        ScoreResult result = Score(Custom(new Target("resist_min", "<=", MetricValue.Of(100.0))));

        Assert.Equal((1.0, true), (result.Score, result.Met["resist_min"]));
    }

    [Fact]
    public void An_eq_target_compares_flags()
    {
        ScoreResult result = Score(Custom(new Target("perma_dom", "==", MetricValue.Flag(true))));

        Assert.False(result.Met["perma_dom"]);
    }

    [Fact]
    public void Perma_hasten_profile_reports_unmet()
    {
        ScoreResult result = Score(Profile("perma-hasten"));

        Assert.Equal((0.0, false), (result.Score, result.Met["perma_hasten"]));
    }

    [Fact]
    public void Two_targets_on_one_metric_both_count()
    {
        ScoreResult result = Score(Custom(new Target("resist_min", ">=", MetricValue.Of(0.0)), new Target("resist_min", "<=", MetricValue.Of(100.0))));

        Assert.Equal(1.0, result.Score);
    }

    [Fact]
    public void An_unknown_metric_is_e17()
    {
        EngineException exception = Assert.Throws<EngineException>(() => Score(Custom(new Target("bogus", ">=", MetricValue.Of(1.0)))));

        Assert.StartsWith("E17: unknown scoring metric 'bogus'; register it with @metric('bogus')", exception.Message, StringComparison.Ordinal);
        Assert.EndsWith("(known metrics: ['defense_positional_min', 'perma_dom', 'perma_hasten', 'resist_min'])", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_op_is_e18()
    {
        EngineException exception = Assert.Throws<EngineException>(() => Score(Custom(new Target("resist_min", "!=", MetricValue.Of(1.0)))));

        Assert.StartsWith("E18: unknown comparison op '!='", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_metric_registry_lists_four_names_in_order()
    {
        string[] expected = ["defense_positional_min", "perma_dom", "perma_hasten", "resist_min"];

        Assert.True(expected.SequenceEqual(RuleRegistry.Metrics.Select(metric => metric.Id), StringComparer.Ordinal));
        Assert.True(RuleRegistry.Metrics.Select(metric => metric.Id).SequenceEqual(RuleRegistry.Metrics.Select(metric => metric.Id).Order(StringComparer.Ordinal), StringComparer.Ordinal));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => Scoring.PermaRecharge(null!, ".Hasten", 1.0));
        Assert.Throws<ArgumentNullException>(() => Scoring.EnduranceDiagnostic(null!, Base.TotalsCapped, Scrapper));
        Assert.Throws<ArgumentNullException>(() => Scoring.CapAdherenceDiagnostics(null!, Base.TotalsCapped));
        Assert.Throws<ArgumentNullException>(() => Scoring.CapAdherenceDiagnostics(Base.Totals, null!));
        Assert.Throws<ArgumentNullException>(() => Scoring.Meets(MetricValue.Of(1.0), null!));
        Assert.Throws<ArgumentNullException>(() => Scoring.ScoreBuild(null!, Base.Totals, Base.TotalsCapped, Stats, Scrapper, 0.0));
    }

    [Fact]
    public void Metric_values_print_and_box_as_python_does()
    {
        Assert.Equal(("True", "45.0", (object)true, (object)2.5), (MetricValue.Flag(true).ToPython(), MetricValue.Of(45.0).ToPython(), MetricValue.Flag(true).ToDiagnosticValue(), MetricValue.Of(2.5).ToDiagnosticValue()));
    }
}
