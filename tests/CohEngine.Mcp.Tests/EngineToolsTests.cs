namespace CohEngine.Mcp.Tests;

public sealed class EngineToolsTests
{
    [Fact]
    public void The_tool_type_has_only_a_private_constructor()
    {
        Assert.Empty(typeof(EngineTools).GetConstructors());
        Assert.IsType<EngineTools>(Activator.CreateInstance(typeof(EngineTools), nonPublic: true));
    }
}
