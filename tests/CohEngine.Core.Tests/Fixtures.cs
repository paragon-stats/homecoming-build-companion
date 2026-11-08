using CohEngine.Core.Data;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests;

/// <summary>The one fixture store the suite shares, so each dump file is parsed once per run.</summary>
internal static class Fixtures
{
    public static FixtureStore Store { get; } = FixtureStore.Locate(AppContext.BaseDirectory);

    public static TheoryData<string> Builds()
    {
        return [.. Store.BuildNames()];
    }
}
