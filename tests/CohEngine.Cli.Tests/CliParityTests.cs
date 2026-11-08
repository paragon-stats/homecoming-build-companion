using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

/// <summary>
/// The CLI's output against the Python engine's, byte for byte, for every sample build. The captures under
/// <c>tests/fixtures/cli</c> are described in that folder's README.
/// </summary>
public sealed class CliParityTests
{
    private static readonly CliCapture Capture = new(FixtureStore.Locate(AppContext.BaseDirectory));

    public static TheoryData<string> Builds()
    {
        return [.. Capture.Names("validate")];
    }

    [Fact]
    public void Rules_matches_the_python_listing()
    {
        Assert.Equal((Capture.Output("rules", "rules", "txt"), Capture.ExitCode("rules", "rules")), CliCapture.Run("rules"));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Validate_text_matches_the_python_output(string name)
    {
        Assert.Equal((Capture.Output("validate", name, "txt"), Capture.ExitCode("validate", name)), CliCapture.Run("validate", Capture.BuildDirectory(name), "--format", "text"));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Validate_json_matches_the_python_output(string name)
    {
        Assert.Equal((Capture.Output("validate", name, "json"), Capture.ExitCode("validate", name)), CliCapture.Run("validate", Capture.BuildDirectory(name), "--format", "json"));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Validate_defaults_to_text(string name)
    {
        Assert.Equal((Capture.Output("validate", name, "txt"), Capture.ExitCode("validate", name)), CliCapture.Run("validate", Capture.BuildDirectory(name)));
    }

    [Fact]
    public void Every_sample_build_has_a_capture()
    {
        Assert.Equal(Capture.Store.BuildNames(), Capture.Names("validate"));
    }
}
