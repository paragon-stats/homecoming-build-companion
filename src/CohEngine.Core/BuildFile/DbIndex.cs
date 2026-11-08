using CohEngine.Core.Data;

namespace CohEngine.Core.BuildFile;

/// <summary>
/// The <c>StaticIndex</c> maps a <c>.mxd</c> resolves its power and enhancement references through. Entries with a
/// negative <c>StaticIndex</c> are dropped: -1 is the <c>.mxd</c> sentinel for an empty reference.
/// </summary>
public static class DbIndex
{
    /// <summary>Loads the <c>StaticIndex</c> to power <c>FullName</c> map.</summary>
    /// <param name="path">The <c>power_static_index.json</c> file.</param>
    /// <returns>The map; a repeated index keeps the last record.</returns>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    public static IReadOnlyDictionary<int, string> LoadPowerIndex(string path)
    {
        return Json.Load(path, BuildFileJsonContext.Default.IReadOnlyListPowerIndexRecord)
            .Where(record => record.StaticIndex >= 0)
            .ToLastWinsDictionary(record => record.StaticIndex, record => record.FullName);
    }

    /// <summary>Loads the <c>StaticIndex</c> to <see cref="EnhIndexEntry"/> map.</summary>
    /// <param name="path">The <c>enhancements.json</c> file.</param>
    /// <returns>The map; a repeated index keeps the last record.</returns>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    public static IReadOnlyDictionary<int, EnhIndexEntry> LoadEnhancementIndex(string path)
    {
        return Json.Load(path, BuildFileJsonContext.Default.IReadOnlyListEnhancementIndexRecord)
            .Where(record => record.StaticIndex >= 0)
            .ToLastWinsDictionary(record => record.StaticIndex, record => new EnhIndexEntry(record.UID, record.TypeName));
    }
}
