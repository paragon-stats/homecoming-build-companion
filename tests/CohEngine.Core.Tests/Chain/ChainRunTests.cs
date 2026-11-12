using CohEngine.Core.Chain;
using CohEngine.Core.Damage;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Chain;

public sealed class ChainRunTests
{
    private static readonly string Build = Path.Join(Fixtures.Store.MidsDirectory, "builds", "shield_scrapper_set_bonuses");
    private static readonly Stance[] AllStances = [Stance.Melee, Stance.Ranged, Stance.Hybrid];

    private static ChainSetup Setup(ChainHooks? hooks = null)
    {
        return ChainReport.BuildChainSetup(new ChainRequest { BuildDir = Build, MidsDir = Fixtures.Store.MidsDirectory, RepoRoot = Fixtures.Store.RepositoryRoot }, hooks);
    }

    private static ChainSetup Synthetic(IReadOnlyList<ChainAttack> attacks, Stance stance = Stance.Melee)
    {
        ChainSetup real = Setup();
        return real with { Context = ChainFactory.Context(attacks, stance) with { Assumptions = new Dictionary<string, double>(StringComparer.Ordinal) { ["chain.min_cycles"] = 4.0 } } };
    }

    private static IEnumerable<string> Rules(ChainSetup setup, Stance stance = Stance.Melee)
    {
        return ChainReport.RunChain(setup, [stance]).SelectMany(result => result.Diagnostics).Select(diagnostic => diagnostic.RuleId + " " + diagnostic.Severity);
    }

    [Fact]
    public void Each_requested_stance_gets_a_report()
    {
        Assert.Equal("Melee Ranged Hybrid", string.Join(' ', ChainReport.RunChain(Setup(), AllStances).Select(result => result.Stance)));
    }

    [Fact]
    public void An_unknown_form_is_e65()
    {
        Assert.Equal(
            "E65: form 'Nova' is not a form of this build; choose one of 'base' ('' is base)",
            Assert.Throws<EngineException>(() => ChainReport.RunChain(Setup(), AllStances, "p", ["Nova"])).Message);
    }

    [Fact]
    public void A_form_runs_only_the_attacks_that_require_it()
    {
        ChainHooks hooks = new()
        {
            Classify = (power, range, resolve) =>
            {
                AttackProfile profile = AttackProfiles.Classify(power, range, resolve);
                return profile.FullName.EndsWith(".Smite", StringComparison.Ordinal) ? profile with { ModesRequired = ["Nova"] } : profile;
            },
        };
        StanceReport nova = Assert.Single(ChainReport.RunChain(Setup(hooks), [Stance.Melee], "p", ["Nova"]));

        Assert.EndsWith(".Smite", Assert.Single(nova.Chain!.Rotation), StringComparison.Ordinal);
    }

    [Fact]
    public void The_forms_are_base_then_the_sorted_modes_the_attacks_require()
    {
        ChainAttack zeta = ChainFactory.Attack("Z") with { Profile = ChainFactory.Profile(modes: ["Zeta", "Alpha"]) };

        Assert.Equal(",Alpha,Zeta", string.Join(',', ChainReport.AvailableForms(ChainFactory.Context([zeta, ChainFactory.Attack("Plain")]))));
    }

    [Fact]
    public void A_chain_that_empties_before_the_minimum_cycles_warns()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Hit", end: 50.0)]);
        setup = setup with { Context = setup.Context with { Budget = new EnduranceBudget(20.0, 0.0, 5.0) } };

        Assert.Contains("P-END-002 warning", Rules(setup), StringComparer.Ordinal);
    }

    [Fact]
    public void A_missing_minimum_cycles_assumption_is_a_missing_key()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Hit")]);
        setup = setup with { Context = setup.Context with { Assumptions = new Dictionary<string, double>(StringComparer.Ordinal) } };

        Assert.Equal("'chain.min_cycles'", Assert.Throws<MissingKeyException>(() => ChainReport.RunChain(setup, [Stance.Melee])).Message);
    }

    [Fact]
    public void Melee_defense_under_the_soft_cap_is_reported_for_melee_and_ranged_defense_for_ranged()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Hit")]);

        Assert.Equal("melee ranged", string.Join(' ', ChainReport.RunChain(setup, [Stance.Melee, Stance.Ranged]).Select(result => result.Diagnostics.Single(diagnostic => string.Equals(diagnostic.RuleId, "P-CHAIN-001", StringComparison.Ordinal)).Message.Split(' ')[2])));
    }

    [Fact]
    public void Defense_at_the_soft_cap_is_not_reported()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Hit")]);
        setup = setup with { Capped = setup.Capped with { Def = [.. Enumerable.Repeat(0.5, 16)] } };

        Assert.DoesNotContain("P-CHAIN-001 info", Rules(setup), StringComparer.Ordinal);
    }

    [Fact]
    public void A_missing_clicks_off_dump_and_a_category_disagreement_are_info_findings()
    {
        ChainAttack disagreeing = ChainFactory.Attack("Far", delivery: Delivery.Melee) with { Profile = ChainFactory.Profile(delivery: Delivery.Melee) with { SetCategories = ["Ranged Damage"] } };
        ChainSetup setup = Synthetic([disagreeing]) with { ClicksAlwaysOn = true };

        Assert.Equal(2, Rules(setup).Count(rule => string.Equals(rule, "P-CHAIN-002 info", StringComparison.Ordinal)));
    }

    [Fact]
    public void Each_skipped_power_is_reported_with_a_fix()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Hit")]) with { Skipped = [("A.B.Gone", "no canonical file (E35)")] };

        Diagnostic found = ChainReport.RunChain(setup, [Stance.Melee], "p").Single().Diagnostics.Single(diagnostic => diagnostic.Message.StartsWith("power A.B.Gone", StringComparison.Ordinal));

        Assert.Equal("power A.B.Gone is not modelled: no canonical file (E35)|check A.B.Gone against canonical data; its damage is missing from every layer until it is classified", found.Message + "|" + found.Fix);
    }

    [Fact]
    public void A_stance_with_no_usable_attack_says_so_and_lists_no_modifiers()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Aura", Role.Aura) with { ActivatePeriod = 2.0 }]);

        string text = ChainReport.StanceToText(setup, ChainReport.RunChain(setup, [Stance.Melee]).Single());

        Assert.Contains("rotation: none (no chain attack is usable in this stance)\n  best sustainable: none\n  best overall: none", text, StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_chain_the_chain_drain_is_the_integer_zero()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Aura", Role.Aura) with { ActivatePeriod = 2.0 }]);

        string json = PyJson.Dumps(ChainReport.StanceToJson(setup, ChainReport.RunChain(setup, [Stance.Melee]).Single()));

        Assert.Contains("\"chain_drain_per_s\": 0,\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sustainable_run_prints_sustainable_and_a_form_names_itself()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Hit", end: 0.1) with { Profile = ChainFactory.Profile(modes: ["Nova"]) }]);

        string text = ChainReport.StanceToText(setup, ChainReport.RunChain(setup, [Stance.Melee], "p", ["Nova"]).Single());

        Assert.Contains("== melee stance, Nova form ==", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Modifiers_print_their_factor_and_assumption()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Hit")]);
        setup = setup with { Context = setup.Context with { ArchetypeClass = "Class_Brute" } };

        string text = ChainReport.StanceToText(setup, ChainReport.RunChain(setup, [Stance.Melee]).Single());

        Assert.Contains("  brute_fury: x", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_neutral_class_still_prints_its_modifier_line()
    {
        ChainSetup setup = Synthetic([ChainFactory.Attack("Hit")]);

        string text = ChainReport.StanceToText(setup, ChainReport.RunChain(setup, [Stance.Melee]).Single());

        Assert.Contains("modifiers:\n  none\nendurance:", text, StringComparison.Ordinal);
    }
}
