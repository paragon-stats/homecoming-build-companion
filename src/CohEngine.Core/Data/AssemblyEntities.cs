namespace CohEngine.Core.Data;

/// <summary>The entities and powersets pet absorption reads, by database index.</summary>
/// <param name="Entities">The entities by index.</param>
/// <param name="Powersets">The powersets by index.</param>
public sealed record AssemblyEntities(IReadOnlyDictionary<int, AssemblyEntity> Entities, IReadOnlyDictionary<int, AssemblyPowerset> Powersets);
