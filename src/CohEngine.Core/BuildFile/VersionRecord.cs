namespace CohEngine.Core.BuildFile;

/// <summary>The <c>version.json</c> fields the build database reads: the Mids release and database a dump was taken from.</summary>
internal sealed record VersionRecord
{
    public required string AppName { get; init; }

    public required string AppVersion { get; init; }

    public required string DatabaseName { get; init; }

    public required string DatabaseVersion { get; init; }
}
