using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

// tests/fixtures/cli/chain holds the Python port's own output; the README beside it says how it was made.
public sealed class ChainParityTests
{
    private static readonly FixtureStore Store = FixtureStore.Locate(AppContext.BaseDirectory);

    public static TheoryData<string> Captures => [.. ChainCaptures.Names(Store)];

    [Theory]
    [MemberData(nameof(Captures))]
    public void Output_matches_the_python_capture(string name)
    {
        Assert.Equal(ChainCaptures.Expected(Store, name), ChainCaptures.Actual(Store, name));
    }
}
