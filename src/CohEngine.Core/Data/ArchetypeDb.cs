namespace CohEngine.Core.Data;

/// <summary>The class table, indexed as <c>Database.Classes</c> is.</summary>
/// <param name="Classes">The class records in index order.</param>
public sealed record ArchetypeDb(IReadOnlyList<Archetype> Classes)
{
    /// <summary>Case-insensitive <c>ClassName</c> to index lookup (<c>DatabaseAPI.NidFromUidClass</c>).</summary>
    /// <param name="name">The class name.</param>
    /// <returns>The class index, or -1.</returns>
    public int NidFromUidClass(string name)
    {
        return Classes.FirstOrDefault(archetype => string.Equals(archetype.ClassName, name, StringComparison.OrdinalIgnoreCase))?.Index ?? -1;
    }
}
