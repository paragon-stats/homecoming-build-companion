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

        Assert.All(EngineToolCatalog.DumpTools, tool =>
        {
            using JsonDocument result = JsonDocument.Parse(EngineToolCatalog.Call(tool, buildDir, arguments));

            Assert.Equal(JsonValueKind.Object, result.RootElement.ValueKind);
        });
    }

    [Fact]
    public void Chain_is_offered_and_takes_its_optional_arguments_as_text()
    {
        EngineToolSpec chain = EngineToolCatalog.DumpTools.Single(tool => string.Equals(tool.Name, "chain", StringComparison.Ordinal));
        Dictionary<string, string> arguments = new(StringComparer.Ordinal) { ["stance"] = "melee", ["target"] = "spawn:2", ["move"] = "run", ["form"] = string.Empty, ["standoff"] = "30" };

        using JsonDocument result = JsonDocument.Parse(EngineToolCatalog.Call(chain, McpHarness.BuildDir(Slotted), arguments));

        Assert.Equal("melee", result.RootElement.GetProperty("stances")[0].GetProperty("stance").GetString());
    }

    [Fact]
    public void A_standoff_that_is_not_a_number_comes_back_as_error_text()
    {
        EngineToolSpec chain = EngineToolCatalog.DumpTools.Single(tool => string.Equals(tool.Name, "chain", StringComparison.Ordinal));
        Dictionary<string, string> arguments = new(StringComparer.Ordinal) { ["standoff"] = "far" };

        string result = EngineToolCatalog.Call(chain, McpHarness.BuildDir(Slotted), arguments);

        Assert.StartsWith("error: the argument standoff is not a number: 'far'", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_schema_is_a_json_object_schema()
    {
        Assert.All(EngineToolCatalog.DumpTools, tool =>
        {
            using JsonDocument schema = JsonDocument.Parse(tool.SchemaJson);

            Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        });
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
        EngineToolSpec totals = EngineToolCatalog.DumpTools.Single(tool => string.Equals(tool.Name, "totals", StringComparison.Ordinal));

        string result = EngineToolCatalog.Call(totals, Path.Join(McpHarness.Store.Root, "no-such-build"), None);

        Assert.StartsWith("error: E164", result, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_set_comes_back_as_an_unknown_result()
    {
        EngineToolSpec lookup = EngineToolCatalog.DumpTools.Single(tool => string.Equals(tool.Name, "set_bonus_lookup", StringComparison.Ordinal));
        Dictionary<string, string> arguments = new(StringComparer.Ordinal) { ["setName"] = "No Such Set" };

        using JsonDocument result = JsonDocument.Parse(EngineToolCatalog.Call(lookup, McpHarness.BuildDir(Slotted), arguments));

        Assert.StartsWith("E166", result.RootElement.GetProperty("unknown").GetString(), StringComparison.Ordinal);
    }
}
