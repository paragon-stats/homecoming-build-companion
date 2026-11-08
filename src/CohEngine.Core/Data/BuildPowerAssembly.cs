namespace CohEngine.Core.Data;

/// <summary>One build power ready for assembly (the Python port's <c>assembly_data.BuildPowerAssembly</c>).</summary>
/// <param name="BuildIndex">The power's index in the build.</param>
/// <param name="ProcInclude">The entry's ProcInclude flag.</param>
/// <param name="SubPowers">The entry's sub-powers.</param>
/// <param name="Power">The power's own assembly record.</param>
public sealed record BuildPowerAssembly(int BuildIndex, bool ProcInclude, IReadOnlyList<SubPower> SubPowers, AssemblyPower Power);
