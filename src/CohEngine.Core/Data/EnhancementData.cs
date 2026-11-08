namespace CohEngine.Core.Data;

/// <summary>Loaders for <c>enhancement_effects.json</c> and a build's <c>slots.json</c>.</summary>
public static class EnhancementData
{
    /// <summary>Parses <c>enhancement_effects.json</c>, keyed by enhancement index.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The records by index.</returns>
    public static IReadOnlyDictionary<int, EnhancementRecord> LoadEnhancementEffects(string path)
    {
        return Json.Load(path, EnhancementJsonContext.Default.IReadOnlyListEnhancementRecord).ToLastWinsDictionary(record => record.Nid);
    }

    /// <summary>Parses a build's <c>slots.json</c>, keyed by the power's build index.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The slots by build index.</returns>
    public static IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> LoadBuildSlots(string path)
    {
        return LoadSlotEntries(path).ToLastWinsDictionary(entry => entry.BuildIndex, entry => entry.Slots);
    }

    /// <summary>Parses a build's <c>slots.json</c> entries in file order.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<BuildSlots> LoadSlotEntries(string path)
    {
        return Json.Load(path, EnhancementJsonContext.Default.IReadOnlyListBuildSlots);
    }
}
