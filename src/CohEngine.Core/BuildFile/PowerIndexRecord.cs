namespace CohEngine.Core.BuildFile;

/// <summary>One <c>power_static_index.json</c> record.</summary>
internal sealed record PowerIndexRecord
{
    public required int StaticIndex { get; init; }

    public required string FullName { get; init; }
}
