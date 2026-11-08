namespace CohEngine.Core.Data;

/// <summary>The <c>assembly_entities.json</c> root.</summary>
public sealed record AssemblyEntitiesFile
{
    /// <summary>Gets the entities.</summary>
    public required IReadOnlyList<AssemblyEntity> Entities { get; init; }

    /// <summary>Gets the powersets.</summary>
    public required IReadOnlyList<AssemblyPowerset> Powersets { get; init; }
}
