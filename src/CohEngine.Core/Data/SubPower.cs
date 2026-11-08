namespace CohEngine.Core.Data;

/// <summary>A sub-power of a build entry.</summary>
/// <param name="NidPower">The sub-power's database index.</param>
/// <param name="StatInclude">Whether the sub-power counts toward totals.</param>
public sealed record SubPower(int NidPower, bool StatInclude);
