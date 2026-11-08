namespace CohEngine.Core.Data;

/// <summary>
/// Name to ordinal maps for the enums Mids indexes arrays by (the Python port's <c>base_totals.EnumMaps</c>),
/// with the ordinal to name inverse built once.
/// </summary>
public sealed record EnumMaps
{
    /// <summary>Initializes a new instance of the <see cref="EnumMaps"/> class.</summary>
    /// <param name="maps">Each enum's member names and ordinals, by enum name.</param>
    public EnumMaps(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        Maps = maps;
        Inverse = maps.ToDictionary(
            map => map.Key,
            map => (IReadOnlyDictionary<int, string>)map.Value.GroupBy(member => member.Value).ToDictionary(group => group.Key, group => group.Last().Key),
            StringComparer.Ordinal);
    }

    /// <summary>Gets each enum's member names and ordinals, by enum name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Maps { get; }

    /// <summary>Gets each enum's ordinal to name map; an ordinal two names share maps to the later one, as the Python inverse does.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> Inverse { get; }

    /// <summary>The array length for an enum: C# <c>Enum.GetValues&lt;T&gt;().Length</c>.</summary>
    /// <param name="enumName">The enum name, for example <c>eDamage</c>.</param>
    /// <returns>The member count.</returns>
    public int Size(string enumName)
    {
        return Maps[enumName].Count;
    }
}
