namespace CohEngine.Core.BuildFile;

/// <summary>The <c>enhancements.json</c> fields the static index reads.</summary>
internal sealed record EnhancementIndexRecord
{
    public required int StaticIndex { get; init; }

    public required string UID { get; init; }

    public required string TypeName { get; init; }
}
