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
        Assert.Contains("ask --build BUILD --model NAME [--provider anthropic|openai|mistral|local] [--base-url URL] [--api-key-env NAME] [--] <question>", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Extract_canonical_dispatches_to_its_command()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int code = CliRunner.Run(["extract-canonical", "--bogus"], output, error);

        Assert.Equal(2, code);
        Assert.StartsWith("usage: coh-engine extract-canonical", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("patch-notes")]
    [InlineData("source-manifest")]
    public void Patch_notes_and_source_manifest_dispatch_to_their_commands(string command)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int code = CliRunner.Run([command, "--bogus"], output, error);

        Assert.Equal(2, code);
        Assert.StartsWith("usage: coh-engine " + command, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_source_root_is_one_error_line()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        string absent = Path.Join(AppContext.BaseDirectory, "no-such-source-root");

        int code = CliRunner.Run(["source-manifest", "--source-root", absent], output, error);

        Assert.Equal(1, code);
        Assert.Equal($"error: E210: expected a directory at {absent}, which was not found; set --source-root or COH_SOURCE_ROOT" + Environment.NewLine, error.ToString());
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
