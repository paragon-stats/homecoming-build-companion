using CohEngine.Core;

namespace CohEngine.Cli.Tests;

public sealed class CliRunnerTests
{
    [Fact]
    public void Version_prints_the_engine_version()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int code = CliRunner.Run(["--version"], output, error);

        Assert.Equal(0, code);
        Assert.Equal("coh-engine " + EngineInfo.Version + Environment.NewLine, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_prints_one_line_of_usage(string flag)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int code = CliRunner.Run([flag], output, error);

        Assert.Equal(0, code);
        Assert.StartsWith("usage: coh-engine", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("ask <question> --build BUILD --model NAME [--provider anthropic|openai|mistral|local] [--base-url URL] [--api-key-env NAME]", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Unknown_arguments_print_usage_to_error_and_fail()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int code = CliRunner.Run(["--bogus"], output, error);

        Assert.Equal(2, code);
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith("usage: coh-engine", error.ToString(), StringComparison.Ordinal);
    }
}
