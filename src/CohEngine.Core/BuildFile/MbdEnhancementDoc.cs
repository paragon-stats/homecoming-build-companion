using System.Text.Json.Serialization;

namespace CohEngine.Core.BuildFile;

/// <summary>An <c>EnhancementData</c> object of a <c>.mbd</c> file; the legacy <c>Enhancement</c> key stands in for <c>Uid</c> on read.</summary>
public sealed record MbdEnhancementDoc
{
    /// <summary>Gets the enhancement UID.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uid { get; set; }

    /// <summary>Gets the eEnhGrade name.</summary>
    public string Grade { get; set; } = "None";

    /// <summary>Gets the IO level.</summary>
    public int IoLevel { get; set; } = 1;

    /// <summary>Gets the eEnhRelative name.</summary>
    public string RelativeLevel { get; set; } = "Even";

    /// <summary>Gets a value indicating whether the enhancement is marked obtained.</summary>
    public bool Obtained { get; set; }

    /// <summary>Gets the legacy key an old file uses for the UID; never written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Enhancement { get; set; }
}
