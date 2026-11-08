namespace CohEngine.Core.Data;

/// <summary>Loader for <c>levels.json</c>.</summary>
public static class LevelData
{
    /// <summary>Parses <c>levels.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The schedule.</returns>
    public static LevelSchedule LoadLevelSchedule(string path)
    {
        LevelsFile raw = Json.Load(path, LevelsJsonContext.Default.LevelsFile);
        return new LevelSchedule(
            raw.SlotGrantLevels.ToDictionary(entry => entry.LevelIndex, entry => entry.Slots),
            [.. raw.PowerPickLevels.Select(entry => entry.LevelIndex)]);
    }
}
