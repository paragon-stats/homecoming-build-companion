using System.ComponentModel;

using CohEngine.Core;
using CohEngine.Core.Registry;

using ModelContextProtocol.Server;

namespace CohEngine.Mcp;

/// <summary>
/// The engine's MCP tools. Each is a thin call into Core that wraps the answer with the engine build it came from.
/// </summary>
[McpServerToolType]
public sealed class EngineTools
{
    private static readonly string DataVersion = EngineInfo.Version;

    /// <summary>Lists every registry.</summary>
    /// <returns>The legality rules, hard-limit rules, enhancement aspect handlers and scoring metrics.</returns>
    [McpServerTool(Name = "rules", ReadOnly = true)]
    [Description("List the engine's rule registries: legality rules, hard-limit rules, enhancement aspect handlers and scoring metrics.")]
    public static RulesResult Rules()
    {
        return new RulesResult(DataVersion, View(RuleRegistry.Legality), View(RuleRegistry.HardLimits), View(RuleRegistry.EnhancementAspects), View(RuleRegistry.Metrics));
    }

    private static List<RuleView> View(IReadOnlyList<RuleInfo> rules)
    {
        return [.. rules.Select(rule => new RuleView(rule.Id, rule.Summary))];
    }
}
