namespace CohEngine.Core.Data;

/// <summary>Loader for a build's <c>incarnates.json</c>.</summary>
public static class IncarnateData
{
    /// <summary>Parses <c>incarnates.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The incarnate powers in file order.</returns>
    public static IReadOnlyList<Incarnate> LoadIncarnates(string path)
    {
        return Json.Load(path, IncarnateJsonContext.Default.IReadOnlyListIncarnate);
    }
}
