namespace CohEngine.Core.BuildFile;

/// <summary>A sub-power entry, such as a toggle's granted variant.</summary>
/// <param name="PowerName">The sub-power's full name.</param>
/// <param name="StatInclude">Whether it counts toward totals.</param>
public sealed record BuildSubPower(string PowerName, bool StatInclude);
