namespace CohEngine.Core.Data;

/// <summary>All AttribMod tables, indexed as <c>AttribMods.Modifier</c> is.</summary>
/// <param name="Tables">The tables in index order.</param>
public sealed record AttribMods(IReadOnlyList<ModifierTable> Tables)
{
    /// <summary>Case-insensitive table name to index lookup (<c>DatabaseAPI.NidFromUidAttribMod</c>).</summary>
    /// <param name="name">The table name.</param>
    /// <returns>The table index, or -1.</returns>
    public int NidFromUidAttribMod(string name)
    {
        for (int i = 0; i < Tables.Count; i++)
        {
            if (string.Equals(Tables[i].Id, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
