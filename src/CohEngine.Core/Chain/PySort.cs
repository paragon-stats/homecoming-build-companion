namespace CohEngine.Core.Chain;

/// <summary>A stable sort, as Python's <c>sorted</c> is.</summary>
internal static class PySort
{
    /// <summary>Sorts a copy of the items, keeping the input order for ties.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="compare">The comparison.</param>
    /// <returns>The sorted list.</returns>
    public static List<T> Sorted<T>(IEnumerable<T> items, Comparison<T> compare)
    {
        (T Item, int Index)[] indexed = [.. items.Select((item, index) => (item, index))];
        Array.Sort(indexed, (left, right) =>
        {
            int result = compare(left.Item, right.Item);
            return result != 0 ? result : left.Index.CompareTo(right.Index);
        });
        return [.. indexed.Select(entry => entry.Item)];
    }
}
