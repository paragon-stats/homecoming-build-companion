namespace CohEngine.Core.Data;

/// <summary>A summoned entity a power absorbs (the Python port's <c>assembly_data.AssemblyEntity</c>).</summary>
public sealed record AssemblyEntity
{
    /// <summary>Gets the entity index.</summary>
    public required int Nid { get; init; }

    /// <summary>Gets the entity UID.</summary>
    public required string Uid { get; init; }

    /// <summary>Gets the entity's class index.</summary>
    public required int ClassNid { get; init; }

    /// <summary>Gets the entity's powerset indexes in order.</summary>
    public required IReadOnlyList<int> PowersetNids { get; init; }

    /// <summary>Gets, per power of the first powerset, its effect types.</summary>
    public required IReadOnlyList<ISet<string>> FirstPowersetEffectTypes { get; init; }
}
