using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>One power's classification, computed once.</summary>
/// <param name="FullName">The power's full name.</param>
/// <param name="Delivery">Melee or ranged.</param>
/// <param name="Shape">The area.</param>
/// <param name="Role">The role.</param>
/// <param name="RangeFt">The range in feet.</param>
/// <param name="RadiusFt">The radius in feet.</param>
/// <param name="ArcRad">The arc.</param>
/// <param name="MaxTargets">The target cap, 0 for none.</param>
/// <param name="SetCategories">The allowed boost-set categories.</param>
/// <param name="ModesRequired">The modes the power needs.</param>
public sealed record AttackProfile(
    string FullName,
    Delivery Delivery,
    Shape Shape,
    Role Role,
    double RangeFt,
    double RadiusFt,
    double ArcRad,
    int MaxTargets,
    IReadOnlyList<string> SetCategories,
    IReadOnlyList<string> ModesRequired)
{
    /// <summary>Gets a value indicating whether the character must be in melee to use the power.</summary>
    public bool NeedsMeleePosition => Delivery == Delivery.Melee || Shape == Shape.Pbaoe;
}
