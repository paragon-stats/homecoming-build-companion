using System.Text.Json.Nodes;

namespace CohEngine.Analyzers.Tests.Ci;

/// <summary>The request bodies under <c>docs/ci/github-rulesets</c>.</summary>
internal static class Rulesets
{
    public static JsonNode Load(string fileName) =>
        JsonNode.Parse(File.ReadAllText(Path.Join(Workflows.RepositoryRoot, "docs", "ci", "github-rulesets", fileName)))!;

    public static JsonNode Rule(JsonNode ruleset, string type) =>
        ruleset["rules"]!.AsArray().Single(rule => string.Equals((string?)rule!["type"], type, StringComparison.Ordinal))!;

    public static List<string> RequiredContexts(string fileName) =>
        [.. Rule(Load(fileName), "required_status_checks")["parameters"]!["required_status_checks"]!.AsArray().Select(check => (string)check!["context"]!)];
}
