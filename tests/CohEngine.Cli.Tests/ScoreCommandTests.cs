using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using CohEngine.Core;
using CohEngine.Core.Data;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

public sealed partial class ScoreCommandTests
{
    private static readonly FixtureStore Store = FixtureStore.Locate(AppContext.BaseDirectory);

    private static string Build => Path.Join(Store.MidsDirectory, "builds", "shield_scrapper_slotted");

    [GeneratedRegex("(?:case \\[|args is \\[)\"(?<name>[a-z][a-z-]*)\"", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex DispatchedCommand { get; }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int code = CliRunner.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void Score_prints_the_metrics_the_scoring_pipeline_computes()
    {
        ComputedBuild expected = DumpScoring.Compute(Build, Store.MidsDirectory);

        (int code, string output, string error) = Run("score", Build);

        Assert.Equal((0, string.Empty), (code, error));
        JsonNode result = JsonNode.Parse(output)!;
        Assert.Equal(expected.ClassName, result["class_name"]!.GetValue<string>());
        Assert.StartsWith("sha256:", result["data_version"]!.GetValue<string>(), StringComparison.Ordinal);
        JsonArray metrics = result["metrics"]!.AsArray();
        Assert.Equal(expected.Metrics.Select(metric => metric.Name), metrics.Select(metric => metric!["name"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(
            expected.Metrics.Select(metric => metric.Value.IsFlag ? (object)metric.Value.IsTrue : metric.Value.Number),
            metrics.Select(metric => metric!["flag"] is { } flag ? (object)flag.GetValue<bool>() : metric["number"]!.GetValue<double>()));
    }

    [Fact]
    public void Explicit_paths_give_the_same_answer_as_the_default_layout()
    {
        string attribMod = Path.Join(Store.Root, "attribmod", "AttribMod.json");
        string maths = Path.Join(Store.Root, "Maths.mhd");

        Assert.Equal(Run("score", Build), Run("score", Build, "--mids-dir", Store.MidsDirectory, "--attribmod", attribMod, "--maths", maths));
    }

    [Fact]
    public void An_option_may_be_written_with_an_equals_sign()
    {
        Assert.Equal(Run("score", Build, "--mids-dir", Store.MidsDirectory), Run("score", Build, "--mids-dir=" + Store.MidsDirectory));
    }

    [Theory]
    [InlineData("score")]
    [InlineData("score", "--bogus")]
    [InlineData("score", "build", "--mids-dir")]
    [InlineData("score", "build", "other")]
    public void A_bad_command_line_prints_usage_and_exits_2(params string[] args)
    {
        (int code, string output, string error) = Run(args);

        Assert.Equal((2, string.Empty), (code, output));
        Assert.StartsWith("usage: coh-engine", error, StringComparison.Ordinal);
        Assert.Contains("coh-engine score: error:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mbd_file_is_refused_because_it_holds_no_power_effects()
    {
        (int code, _, string error) = Run("score", "build.mbd");

        Assert.Equal(1, code);
        Assert.StartsWith("error: E180:", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--mids-dir")]
    [InlineData("--attribmod")]
    [InlineData("--maths")]
    public void A_missing_path_is_a_named_error(string option)
    {
        string missing = Path.Join(Store.Root, "no-such-path");

        (int code, _, string error) = Run("score", Build, option, missing);

        Assert.Equal(1, code);
        Assert.StartsWith("error: E164:", error, StringComparison.Ordinal);
        Assert.Contains(missing, error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_build_directory_is_a_named_error()
    {
        (int code, _, string error) = Run("score", Path.Join(Store.Root, "no-such-build"));

        Assert.Equal(1, code);
        Assert.StartsWith("error: E164:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_lists_every_command_the_runner_dispatches()
    {
        string sources = File.ReadAllText(Path.Join(Store.RepositoryRoot, "src", "CohEngine.Core", "CliRunner.cs"))
            + File.ReadAllText(Path.Join(Store.RepositoryRoot, "src", "CohEngine.Cli", "Program.cs"));
        IEnumerable<string> dispatched = DispatchedCommand.Matches(sources)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal);
        string[] commands = [.. dispatched];

        (_, string help, _) = Run("--help");

        Assert.Equal(["ask", "chain", "extract-canonical", "mcp", "patch-notes", "rules", "score", "source-manifest", "validate"], commands.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(commands, command => Assert.Contains(command, help, StringComparison.Ordinal));
    }
}
