using System.Text.Json.Serialization;

namespace CohEngine.Core.BuildFile;

/// <summary>The <c>enhancements.json</c> fields the build database reads.</summary>
internal sealed record EnhancementDbRecord
{
    public required string UID { get; init; }

    public required int Nid { get; init; }

    public required string TypeName { get; init; }

    [JsonPropertyName("nIDSet")]
    public required int SetNid { get; init; }

    public required int LevelMin { get; init; }

    public required int LevelMax { get; init; }
}
