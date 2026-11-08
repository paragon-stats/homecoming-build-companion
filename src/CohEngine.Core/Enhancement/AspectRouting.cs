using CohEngine.Core.Maths;

namespace CohEngine.Core.Enhancement;

/// <summary>
/// The Pass 3/4 per-power scalar fold, <c>base / ((ED(sum slotted) + global) + 1)</c>, keyed on the eEnhance scalar
/// aspect: each aspect names its global <c>_selfEnhance</c> term and whether the AT recharge cap clamps it.
/// </summary>
internal static class AspectRouting
{
    /// <summary>Gets the recharge handler; its fold is clamped to the AT recharge cap.</summary>
    public static AspectHandler Recharge { get; } = new("RechargeTime", "recharge", Capped: true);

    /// <summary>Gets the endurance discount handler.</summary>
    public static AspectHandler End { get; } = new("EnduranceDiscount", "end_discount", Capped: false);

    /// <summary>Gets the interrupt handler.</summary>
    public static AspectHandler Interrupt { get; } = new("Interrupt", "interrupt", Capped: false);

    /// <summary>Gets the accuracy handler.</summary>
    public static AspectHandler Accuracy { get; } = new("Accuracy", "accuracy", Capped: false);

    /// <summary>Gets the range handler.</summary>
    public static AspectHandler Range { get; } = new("Range", "range", Capped: false);

    /// <summary>The global <c>_selfEnhance</c> term the aspect adds.</summary>
    /// <param name="handler">The aspect.</param>
    /// <param name="globalEnhance">The build's global terms.</param>
    /// <returns>The term.</returns>
    public static double GlobalTerm(AspectHandler handler, GlobalEnhance globalEnhance)
    {
        return handler.GlobalField switch
        {
            "recharge" => globalEnhance.Recharge,
            "end_discount" => globalEnhance.EndDiscount,
            "accuracy" => globalEnhance.Accuracy,
            "interrupt" => globalEnhance.Interrupt,
            _ => globalEnhance.Range,
        };
    }

    /// <summary>The fold cap: the AT recharge cap for recharge, none otherwise.</summary>
    /// <param name="handler">The aspect.</param>
    /// <param name="rechargeCap">The AT recharge cap.</param>
    /// <returns>The cap, or null.</returns>
    public static double? CapFor(AspectHandler handler, double rechargeCap)
    {
        return handler.Capped ? rechargeCap : null;
    }

    /// <summary>
    /// The fold multiplier for one scalar aspect over an already-ED aggregate. The global and post-ED terms are added
    /// one after the other, as Mids adds them, since float32 addition is not associative.
    /// </summary>
    /// <param name="handler">The aspect.</param>
    /// <param name="edAggregate">The ED-reduced slotted aggregate.</param>
    /// <param name="globalScalar">The aspect's global term.</param>
    /// <param name="ignoreEnh">The eEnhance names the power ignores.</param>
    /// <param name="rechargeCap">The AT recharge cap.</param>
    /// <param name="postEdExtra">The incarnate IgnoreED-true term.</param>
    /// <returns>The divisor.</returns>
    public static double FoldScalar(AspectHandler handler, double edAggregate, double globalScalar, IEnumerable<string> ignoreEnh, double rechargeCap, double postEdExtra = 0.0)
    {
        return FoldDivisor(edAggregate, globalScalar, handler.Aspect, ignoreEnh, CapFor(handler, rechargeCap), postEdExtra);
    }

    /// <summary>
    /// <c>(((aggregate + global) + postEdExtra) + 1)</c> in float order, clamped to the cap when one is given; 1 when the
    /// power ignores the aspect, whose modifiable base is then zero.
    /// </summary>
    /// <param name="aggregate">The ED-reduced aggregate.</param>
    /// <param name="globalTerm">The global term.</param>
    /// <param name="aspect">The eEnhance name.</param>
    /// <param name="ignoredAspects">The eEnhance names the power ignores.</param>
    /// <param name="cap">The cap, or null.</param>
    /// <param name="postEdExtra">The incarnate IgnoreED-true term.</param>
    /// <returns>The divisor.</returns>
    public static double FoldDivisor(double aggregate, double globalTerm, string aspect, IEnumerable<string>? ignoredAspects = null, double? cap = null, double postEdExtra = 0.0)
    {
        if (ignoredAspects is not null && ignoredAspects.Contains(aspect, StringComparer.Ordinal))
        {
            return 1.0;
        }

        double divisor = F32.Round(F32.Round(F32.Round(aggregate + globalTerm) + postEdExtra) + 1.0);
        if (cap is { } limit)
        {
            divisor = Math.Min(divisor, limit);
        }

        return divisor;
    }
}
