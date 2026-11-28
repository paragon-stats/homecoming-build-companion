using System.Text.Json.Nodes;

namespace CohEngine.Analyzers.Tests.Ci;

/// <summary>The committed ruleset and security-and-analysis request bodies.</summary>
public sealed class RulesetBodyTests
{
    private const string MainRuleset = "main-ruleset.json";

    private static readonly string[] ReadOnlyFields = ["id", "node_id", "_links", "created_at", "updated_at", "source", "source_type", "current_user_can_bypass"];

    [Fact]
    public void Bypass_is_the_operator_in_pull_request_mode()
    {
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""[{ "actor_id": 35460307, "actor_type": "User", "bypass_mode": "pull_request" }]"""),
            Rulesets.Load(MainRuleset)["bypass_actors"]));
    }

    [Fact]
    public void The_pull_request_rule_requires_code_owner_review()
    {
        JsonNode parameters = Rulesets.Rule(Rulesets.Load(MainRuleset), "pull_request")["parameters"]!;
        Assert.Equal(1, (int)parameters["required_approving_review_count"]!);
        Assert.True((bool)parameters["require_code_owner_review"]!);
    }

    [Fact]
    public void The_ruleset_carries_no_read_only_field()
    {
        Assert.Empty(Rulesets.Load(MainRuleset).AsObject().Select(pair => pair.Key).Intersect(ReadOnlyFields, StringComparer.Ordinal));
    }

    [Fact]
    public void The_ruleset_pins_every_rule_type()
    {
        Assert.Equal(
            ["code_quality", "code_scanning", "deletion", "non_fast_forward", "pull_request", "required_signatures", "required_status_checks"],
            Rulesets.Load(MainRuleset)["rules"]!.AsArray().Select(rule => (string)rule!["type"]!).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    [Fact]
    public void The_code_scanning_rule_gates_on_all_codeql_alerts()
    {
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{ "code_scanning_tools": [{ "tool": "CodeQL", "alerts_threshold": "all", "security_alerts_threshold": "all" }] }"""),
            Rulesets.Rule(Rulesets.Load(MainRuleset), "code_scanning")["parameters"]));
    }

    [Fact]
    public void Every_required_check_is_a_github_actions_check()
    {
        JsonArray checks = Rulesets.Rule(Rulesets.Load(MainRuleset), "required_status_checks")["parameters"]!["required_status_checks"]!.AsArray();
        Assert.All(checks, check => Assert.Equal(15368, (int)check!["integration_id"]!));
        Assert.Contains("sonar", Rulesets.RequiredContexts(MainRuleset), StringComparer.Ordinal);
    }

    [Fact]
    public void The_rollback_is_the_main_ruleset_without_the_code_scanning_rule()
    {
        JsonNode main = Rulesets.Load(MainRuleset);
        JsonArray rules = main["rules"]!.AsArray();
        rules.Remove(rules.Single(rule => string.Equals((string?)rule!["type"], "code_scanning", StringComparison.Ordinal)));

        Assert.True(JsonNode.DeepEquals(main, Rulesets.Load("main-ruleset.rollback.json")));
    }

    [Fact]
    public void The_fallback_body_enables_the_two_remaining_settings()
    {
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{ "security_and_analysis": { "secret_scanning": { "status": "enabled" }, "secret_scanning_push_protection": { "status": "enabled" } } }"""),
            Rulesets.Load("security-and-analysis.fallback.json")));
    }

    [Fact]
    public void The_security_body_enables_exactly_the_three_settings()
    {
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{ "security_and_analysis": { "secret_scanning": { "status": "enabled" }, "secret_scanning_non_provider_patterns": { "status": "enabled" }, "secret_scanning_push_protection": { "status": "enabled" } } }"""),
            Rulesets.Load("security-and-analysis.json")));
    }
}
