namespace CohEngine.Core.Tests;

public sealed class EngineInfoTests
{
    [Fact]
    public void Version_is_a_non_empty_version_string()
    {
        Assert.False(string.IsNullOrWhiteSpace(EngineInfo.Version));
        Assert.True(char.IsAsciiDigit(EngineInfo.Version[0]), EngineInfo.Version);
    }
}
