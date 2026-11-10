namespace CohEngine.Core.BuildFile;

/// <summary>The <c>BuiltWith</c> object of a <c>.mbd</c> file.</summary>
public sealed record MbdBuiltWithDoc
{
    /// <summary>Gets the application name.</summary>
    public string App { get; set; } = string.Empty;

    /// <summary>Gets the application version.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Gets the database name.</summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>Gets the database version.</summary>
    public string DatabaseVersion { get; set; } = string.Empty;
}
