namespace CohEngine.Core.Data;

/// <summary>A summoned entity's powerset (the Python port's <c>assembly_data.AssemblyPowerset</c>).</summary>
public sealed record AssemblyPowerset
{
    /// <summary>Gets the powerset index.</summary>
    public required int Nid { get; init; }

    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the eSetType name.</summary>
    public required string SetType { get; init; }

    /// <summary>Gets the powers' database indexes in order, -1 for an empty slot.</summary>
    public required IReadOnlyList<int> PowerNids { get; init; }
}
