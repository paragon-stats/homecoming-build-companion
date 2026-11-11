using System.Text.Json.Nodes;

using CohEngine.Core;

using ModelContextProtocol.Client;

namespace CohEngine.Mcp.Tests;

public sealed class HandshakeTests
{
    [Fact]
    public async Task Initialize_reports_the_server_and_its_tools_capability()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        Assert.Equal("coh-engine", harness.Client.ServerInfo?.Name);
        Assert.Equal(EngineInfo.Version, harness.Client.ServerInfo?.Version);
        Assert.NotNull(harness.Client.ServerCapabilities.Tools);
    }

    [Fact]
    public async Task The_mcp_command_takes_no_arguments()
    {
        using StringWriter error = new();

        int code = await McpCommand.RunAsync(["--port", "1"], null, null, error, TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Equal(2, code);
        Assert.Equal("usage: coh-engine mcp" + Environment.NewLine + "coh-engine mcp: error: unrecognized arguments: --port 1" + Environment.NewLine, error.ToString());
    }

    [Fact]
    public async Task The_handshake_lists_the_chain_tool_and_calls_it_and_an_unknown_lookup()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        IList<McpClientTool> tools = await harness.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir("shield_scrapper_set_bonuses");

        JsonNode plan = await harness.CallJsonAsync("chain", ("buildDir", buildDir), ("stance", "melee")).ConfigureAwait(true);
        JsonNode unknown = await harness.CallJsonAsync("power_lookup", ("fullName", "No.Such.Power"), ("buildDir", buildDir)).ConfigureAwait(true);

        Assert.True(tools.Single(tool => string.Equals(tool.Name, "chain", StringComparison.Ordinal)).JsonSchema.GetProperty("properties").TryGetProperty("buildDir", out _));
        Assert.Equal("melee", plan["stances"]![0]!["stance"]!.GetValue<string>());
        Assert.StartsWith("E167", unknown["unknown"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tools_list_names_every_tool()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        IList<McpClientTool> tools = await harness.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        string[] expected = ["chain", "check_hard_limits", "check_legality", "power_lookup", "rules", "score", "set_bonus_lookup", "totals"];
        Assert.Equal(expected, tools.Select(tool => tool.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
