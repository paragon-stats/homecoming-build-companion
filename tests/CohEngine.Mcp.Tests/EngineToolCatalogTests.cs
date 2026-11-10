using System.Text.Json;

using CohEngine.TestSupport;

namespace CohEngine.Mcp.Tests;

public sealed class EngineToolCatalogTests
{
    private const string Slotted = "shield_scrapper_set_bonuses";

    private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>(StringComparer.Ordinal);

    [Fact]
    public void Every_tool_runs_on_a_build_and_returns_json()
    {
        string buildDir = McpHarness.BuildDir(Slotted);
        string power = McpHarness.Store.Build(Slotted).Powers.First(candidate => candidate.PickLevel > 0).FullName;
        Dictionary<string, string> arguments = new(StringComparer.Ordinal) { ["setName"] = "blessing_of_the_zephyr", ["fullName"] = power };

        foreach (EngineToolSpec tool in EngineToolCatalog.DumpTools)
        {
            using JsonDocument result = JsonDocument.Parse(EngineToolCatalog.Call(tool, buildDir, arguments));

            Assert.Equal(JsonValueKind.Object, result.RootElement.ValueKind);
        }
    }

    [Fact]
    public void Chain_is_not_offered_because_the_mcp_tool_is_a_placeholder()
    {
        Assert.DoesNotContain(EngineToolCatalog.DumpTools, tool => string.Equals(tool.Name, "chain", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_schema_is_a_json_object_schema()
    {
        foreach (EngineToolSpec tool in EngineToolCatalog.DumpTools)
        {
            using JsonDocument schema = JsonDocument.Parse(tool.SchemaJson);

            Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        }
    }

    [Fact]
    public void A_missing_argument_comes_back_as_error_text()
    {
        EngineToolSpec lookup = EngineToolCatalog.DumpTools.Single(tool => string.Equals(tool.Name, "power_lookup", StringComparison.Ordinal));

        string result = EngineToolCatalog.Call(lookup, McpHarness.BuildDir(Slotted), None);

        Assert.StartsWith("error: the tool needs the argument fullName", result, StringComparison.Ordinal);
    }

    [Fact]
    public void An_engine_failure_comes_back_as_error_text_with_its_code()
    {
        EngineToolSpec lookup = EngineToolCatalog.DumpTools.Single(tool => string.Equals(tool.Name, "set_bonus_lookup", StringComparison.Ordinal));
        Dictionary<string, string> arguments = new(StringComparer.Ordinal) { ["setName"] = "No Such Set" };

        string result = EngineToolCatalog.Call(lookup, McpHarness.BuildDir(Slotted), arguments);

        Assert.StartsWith("error: E166", result, StringComparison.Ordinal);
    }
}
