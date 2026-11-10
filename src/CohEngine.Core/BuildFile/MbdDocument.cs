using System.Text.Json.Serialization;

namespace CohEngine.Core.BuildFile;

/// <summary>A <c>.mbd</c> file as JSON: property order is the key order the writer emits.</summary>
public sealed record MbdDocument
{
    /// <summary>Gets the provenance block; a file without one is refused.</summary>
    public MbdBuiltWithDoc? BuiltWith { get; set; }

    /// <summary>Gets the character level minus one, as text; a JSON number is accepted on read.</summary>
    [JsonConverter(typeof(LevelTextConverter))]
    public string? Level { get; set; }

    /// <summary>Gets the class name.</summary>
    public string? Class { get; set; }

    /// <summary>Gets the origin.</summary>
    public string Origin { get; set; } = string.Empty;

    /// <summary>Gets the alignment.</summary>
    public string Alignment { get; set; } = string.Empty;

    /// <summary>Gets the build name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets the comment.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Gets the power set names.</summary>
    public IReadOnlyList<string> PowerSets { get; set; } = [];

    /// <summary>Gets the last-power index.</summary>
    public int LastPower { get; set; }

    /// <summary>Gets the power entries.</summary>
    public IReadOnlyList<MbdPowerEntryDoc> PowerEntries { get; set; } = [];
}
