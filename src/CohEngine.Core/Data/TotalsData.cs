namespace CohEngine.Core.Data;

/// <summary>Loaders for the totals family.</summary>
public static class TotalsData
{
    /// <summary>Parses a build's <c>totals.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>Mids' totals.</returns>
    public static MidsTotals LoadTotals(string path)
    {
        return Json.Load(path, TotalsJsonContext.Default.MidsTotals);
    }

    /// <summary>Parses a build's <c>enhanced_powers.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>Mids' enhanced powers in file order.</returns>
    public static IReadOnlyList<EnhancedPower> LoadEnhancedPowers(string path)
    {
        return Json.Load(path, TotalsJsonContext.Default.IReadOnlyListEnhancedPower);
    }

    /// <summary>Parses <c>server_data.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The server constants.</returns>
    public static ServerData LoadServerData(string path)
    {
        return Json.Load(path, TotalsJsonContext.Default.ServerData);
    }

    /// <summary>Parses <c>config.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The engine configuration.</returns>
    public static EngineConfig LoadEngineConfig(string path)
    {
        return Json.Load(path, TotalsJsonContext.Default.EngineConfig);
    }
}
