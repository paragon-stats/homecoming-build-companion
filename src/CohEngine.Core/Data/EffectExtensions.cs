namespace CohEngine.Core.Data;

/// <summary>
/// The extended keys of one power's effects, read in a second pass over an extended-record file. Every key of
/// <see cref="EffectExtension"/> is required, so a stale dump fails with E85 instead of loading defaults.
/// </summary>
public sealed record EffectExtensions
{
    /// <summary>Gets the extended keys of the power's effects.</summary>
    public required IReadOnlyList<EffectExtension> Effects { get; init; }
}
