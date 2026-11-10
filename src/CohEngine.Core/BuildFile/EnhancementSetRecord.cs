namespace CohEngine.Core.BuildFile;

/// <summary>The <c>enhancement_sets.json</c> fields the build database reads.</summary>
internal sealed record EnhancementSetRecord
{
    public required int Nid { get; init; }

    public required int LevelMin { get; init; }

    public required int LevelMax { get; init; }
}
