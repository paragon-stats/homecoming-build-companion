using CohEngine.Core.Data;

namespace CohEngine.Core.Assembly;

/// <summary>The records assembly reaches: powers by database index, the summoned entities, and the build's class.</summary>
/// <param name="Powers">The powers, in the order an executed power is looked up by name.</param>
/// <param name="Entities">The summoned entities and their powersets.</param>
/// <param name="ArchetypeIndex">The build's class index.</param>
internal sealed record AssemblyInputs(IReadOnlyDictionary<int, AssemblyPower> Powers, AssemblyEntities Entities, int ArchetypeIndex)
{
    /// <summary>A record the inputs must hold.</summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="table">The table to read.</param>
    /// <param name="key">The record's index.</param>
    /// <param name="what">What the record is, for the message.</param>
    /// <param name="where">What points at it, for the message.</param>
    /// <returns>The record.</returns>
    /// <exception cref="InvalidDataException">E90 when the table does not hold the record.</exception>
    public static T Record<T>(IReadOnlyDictionary<int, T> table, int key, string what, string where)
    {
        if (table.TryGetValue(key, out T? value))
        {
            return value;
        }

        throw new InvalidDataException(
            $"E90: {where} points at {what} {key}, which the assembly inputs do not hold; expected every record "
            + "assembly reaches; re-run the Mids dump harness so the export carries it");
    }
}
