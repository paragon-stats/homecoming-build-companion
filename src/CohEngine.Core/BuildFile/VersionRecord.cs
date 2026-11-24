namespace CohEngine.Core.BuildFile;

/// <summary>The <c>version.json</c> fields the build database reads: the Mids release and database a dump was taken from.</summary>
/// <remarks>The fields are nullable so a missing, null and blank value all reach one check and one message.</remarks>
internal sealed record VersionRecord
{
    public string? AppName { get; init; }

    public string? AppVersion { get; init; }

    public string? DatabaseName { get; init; }

    public string? DatabaseVersion { get; init; }
}
