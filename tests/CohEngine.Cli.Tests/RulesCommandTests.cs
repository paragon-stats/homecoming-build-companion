using CohEngine.Core;
using CohEngine.Core.Data;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

// tests/fixtures/rules-golden.txt is the output of `python -m coh_engine rules`, with line endings made LF.
public sealed class RulesCommandTests
{
    [Fact]
    public void Rules_prints_the_python_listing()
    {
        string golden = File.ReadAllText(Path.Combine(FixtureStore.Locate(AppContext.BaseDirectory).Root, "rules-golden.txt"));
        using StringWriter output = new();
        using StringWriter error = new();

        int code = CliRunner.Run(["rules"], output, error);

        Assert.Equal(0, code);
        Assert.Equal(golden.ReplaceLineEndings("\n"), output.ToString().ReplaceLineEndings("\n"));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Every_rule_carries_a_summary()
    {
        RuleInfo[] rules = [.. RuleRegistry.Legality, .. RuleRegistry.HardLimits, .. RuleRegistry.EnhancementAspects, .. RuleRegistry.Metrics];

        Assert.All(rules, rule => Assert.False(string.IsNullOrWhiteSpace(rule.Summary), rule.Id));
        Assert.Equal(rules.Length, rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count());
    }
}
