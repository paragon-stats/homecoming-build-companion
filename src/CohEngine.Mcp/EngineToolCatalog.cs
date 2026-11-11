using System.Text.Json;

using ModelContextProtocol;

namespace CohEngine.Mcp;

/// <summary>The <see cref="EngineTools"/> as a list of specs; each spec is a call into the same method the MCP server runs.</summary>
public static class EngineToolCatalog
{
    private const string NoArguments = """{"type":"object","properties":{}}""";

    private const string SetNameSchema = """{"type":"object","properties":{"setName":{"type":"string","description":"The set's UID, as the Mids database spells it."}},"required":["setName"]}""";

    private const string FullNameSchema = """{"type":"object","properties":{"fullName":{"type":"string","description":"The power's full name, for example Scrapper_Melee.Dark_Melee.Shadow_Punch."}},"required":["fullName"]}""";

    /// <summary>Gets the tools that work on a Mids dump directory; <c>chain</c> is left out because the MCP tool of that name is a placeholder.</summary>
    public static IReadOnlyList<EngineToolSpec> DumpTools { get; } =
    [
        new("rules", "List the engine's rule registries: legality rules, hard-limit rules, enhancement aspect handlers and scoring metrics.", NoArguments, (_, _) => JsonSerializer.Serialize(EngineTools.Rules(), McpJsonContext.Default.RulesResult)),
        new("check_legality", "Check the build's enhancement legality (placement, unique, mutex, lone set piece, duplicate set piece).", NoArguments, (build, _) => JsonSerializer.Serialize(EngineTools.CheckLegality(build), McpJsonContext.Default.DiagnosticsResult)),
        new("check_hard_limits", "Check the build against the game's hard limits (slots, picks, pools, levels).", NoArguments, (build, _) => JsonSerializer.Serialize(EngineTools.CheckHardLimits(build), McpJsonContext.Default.DiagnosticsResult)),
        new("score", "Compute the registered scoring metrics (positional defense minimum, minimum resistance, perma-Hasten, perma-Domination) for the build.", NoArguments, (build, _) => JsonSerializer.Serialize(EngineTools.Score(build), McpJsonContext.Default.ScoreResult)),
        new("totals", "Compute the build's base totals (defense, resistance, recovery, recharge and the rest), uncapped and capped, with defense and resistance as percentages.", NoArguments, (build, _) => JsonSerializer.Serialize(EngineTools.Totals(build), McpJsonContext.Default.TotalsResult)),
        new("set_bonus_lookup", "Look up an enhancement set's bonuses by set name (UID), with every magnitude resolved.", SetNameSchema, (build, arguments) => JsonSerializer.Serialize(EngineTools.SetBonusLookup(Required(arguments, "setName"), build), McpJsonContext.Default.SetBonusResult)),
        new("power_lookup", "Look up a power by full name in the build's powers data. Levels are game levels (1-based).", FullNameSchema, (build, arguments) => JsonSerializer.Serialize(EngineTools.PowerLookup(Required(arguments, "fullName"), build), McpJsonContext.Default.PowerResult)),
    ];

    /// <summary>Runs a tool; an engine failure comes back as text for the model instead of an exception.</summary>
    /// <param name="spec">The tool.</param>
    /// <param name="buildDirectory">The build's Mids dump directory.</param>
    /// <param name="arguments">The model's arguments by name.</param>
    /// <returns>The tool's JSON result, or <c>error: </c> and the engine's message.</returns>
    public static string Call(EngineToolSpec spec, string buildDirectory, IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentNullException.ThrowIfNull(spec);
        try
        {
            return spec.Run(buildDirectory, arguments);
        }
        catch (Exception exception) when (exception is McpException or ArgumentException)
        {
            return "error: " + exception.Message;
        }
    }

    private static string Required(IReadOnlyDictionary<string, string> arguments, string name)
    {
        return arguments.TryGetValue(name, out string? value) ? value : throw new ArgumentException($"the tool needs the argument {name}", nameof(arguments));
    }
}
