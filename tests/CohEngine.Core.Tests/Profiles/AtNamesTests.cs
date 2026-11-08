using CohEngine.Core.Diagnostics;
using CohEngine.Core.Profiles;

namespace CohEngine.Core.Tests.Profiles;

public sealed class AtNamesTests
{
    private static readonly string[] Keys = ["Tanker", "Scrapper", "Arachnos_Soldier", "Arachnos_Widow"];

    [Theory]
    [InlineData("Scrapper", "Scrapper")]
    [InlineData("scrapper", "Scrapper")]
    [InlineData("Arachnos Soldier", "Arachnos_Soldier")]
    [InlineData("Arachnos_Soldier", "Arachnos_Soldier")]
    [InlineData("Class_Arachnos_Widow", "Arachnos_Widow")]
    [InlineData("  Tanker  ", "Tanker")]
    public void Any_form_resolves_to_the_canonical_key(string name, string expected)
    {
        Assert.Equal(expected, AtNames.ResolveAtKey(name, Keys));
    }

    [Fact]
    public void An_unknown_archetype_is_e19()
    {
        EngineException exception = Assert.Throws<EngineException>(() => AtNames.ResolveAtKey("Blapper", Keys));

        Assert.StartsWith("E19: archetype 'Blapper' matches 0 of the 4 known keys ['Arachnos_Soldier', 'Arachnos_Widow', 'Scrapper', 'Tanker']; pass a recognizable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_keys_that_normalize_alike_are_refused()
    {
        Assert.Throws<EngineException>(() => AtNames.ResolveAtKey("scrapper", ["Scrapper", "SCRAPPER"]));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => AtNames.ResolveAtKey(null!, Keys));
        Assert.Throws<ArgumentNullException>(() => AtNames.ResolveAtKey("Tanker", null!));
    }
}
