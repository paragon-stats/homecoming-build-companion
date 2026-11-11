namespace CohEngine.Mcp;

/// <summary>A power's record from the dump.</summary>
/// <param name="FullName">The power's full name.</param>
/// <param name="PowerType">The power type.</param>
/// <param name="AvailableLevel">The earliest game level (1-based) the power can be taken.</param>
/// <param name="PickLevel">The game level (1-based) the build took it at.</param>
/// <param name="RechargeTime">The base recharge, in seconds.</param>
/// <param name="CastTime">The base cast time, in seconds.</param>
/// <param name="EndCost">The base endurance cost.</param>
/// <param name="ActivatePeriod">The toggle tick period, in seconds.</param>
/// <param name="StatInclude">Whether the power counts toward totals.</param>
/// <param name="EffectTypes">The distinct effect types the power carries.</param>
/// <param name="RequiresPowers">The prerequisite groups (an OR of AND groups); absent when the dump predates the field, which is not the same as none.</param>
/// <param name="Chosen">Whether a pick was spent on the power; absent when the dump predates the field.</param>
public sealed record PowerView(
    string FullName,
    string PowerType,
    int AvailableLevel,
    int PickLevel,
    double RechargeTime,
    double CastTime,
    double EndCost,
    double ActivatePeriod,
    bool StatInclude,
    IReadOnlyList<string> EffectTypes,
    IReadOnlyList<IReadOnlyList<string>>? RequiresPowers,
    bool? Chosen);
