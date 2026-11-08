using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>One power's Mids-computed damage inputs.</summary>
/// <param name="FullName">The power's full name.</param>
/// <param name="DamagePerActivation">Mids <c>DamagePerActivation</c>.</param>
/// <param name="CastTime">The buffed cast time.</param>
/// <param name="RechargeTime">The buffed recharge time.</param>
/// <param name="EndCost">The buffed endurance cost.</param>
/// <param name="ActivatePeriod">The toggle or auto tick period.</param>
/// <param name="PowerType">The Mids power type.</param>
/// <param name="StatInclude">Whether the power's buffs feed the build totals.</param>
/// <param name="SelfDamageBuff">The self damage buff fraction.</param>
/// <param name="SelfDamageBuffDuration">The buff duration.</param>
/// <param name="TargetResDebuff">The fraction of target resistance removed.</param>
/// <param name="TargetResDebuffDuration">The debuff duration.</param>
public sealed record PowerDamageRef(
    string FullName,
    double DamagePerActivation,
    double CastTime,
    double RechargeTime,
    double EndCost,
    double ActivatePeriod,
    string PowerType,
    bool StatInclude,
    double SelfDamageBuff,
    double SelfDamageBuffDuration,
    double TargetResDebuff,
    double TargetResDebuffDuration);
