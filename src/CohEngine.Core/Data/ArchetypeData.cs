using CohEngine.Core.Maths;

namespace CohEngine.Core.Data;

/// <summary>Loaders for <c>archetypes.json</c> and <c>AttribMod.json</c>.</summary>
public static class ArchetypeData
{
    /// <summary>Parses <c>archetypes.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The class table.</returns>
    public static ArchetypeDb LoadArchetypes(string path)
    {
        return new ArchetypeDb(Json.Load(path, ArchetypeJsonContext.Default.IReadOnlyListArchetype));
    }

    /// <summary>Parses <c>AttribMod.json</c>, quantizing every cell.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The tables.</returns>
    public static AttribMods LoadAttribMod(string path)
    {
        return new AttribMods(
        [
            .. Json.Load(path, ArchetypeJsonContext.Default.AttribModFile).Modifier.Select(table => table with
            {
                Table = [.. table.Table.Select(row => (IReadOnlyList<double>)[.. row.Select(F32.Round)])],
            }),
        ]);
    }
}
