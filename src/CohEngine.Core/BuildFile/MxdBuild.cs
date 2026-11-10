namespace CohEngine.Core.BuildFile;

/// <summary>A build parsed from a <c>.mxd</c> binary buffer; levels are the raw stored 0-based levels.</summary>
/// <param name="Format">The field-set format: legacy, prior or current.</param>
/// <param name="ClassName">The class name.</param>
/// <param name="OriginUid">The origin UID.</param>
/// <param name="Alignment">The alignment ordinal.</param>
/// <param name="Name">The build name.</param>
/// <param name="PowerSets">The power set names, migrated.</param>
/// <param name="LastPower">The last-power index.</param>
/// <param name="PowerEntries">The power entries.</param>
public sealed record MxdBuild(
    string Format,
    string ClassName,
    string OriginUid,
    int Alignment,
    string Name,
    IReadOnlyList<string> PowerSets,
    int LastPower,
    IReadOnlyList<BuildPowerEntry> PowerEntries);
