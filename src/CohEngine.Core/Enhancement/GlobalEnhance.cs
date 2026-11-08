namespace CohEngine.Core.Enhancement;

/// <summary>
/// The global <c>_selfEnhance</c> scalar terms GBPA_Pass3_EnhancePostED adds to the matching per-power multiplier
/// before the +1 and the divide. Accuracy and range are populated though no fold reads them yet.
/// </summary>
/// <param name="Recharge"><c>_selfEnhance.Effect[Haste]</c>.</param>
/// <param name="EndDiscount">The global endurance discount.</param>
/// <param name="Accuracy">The global accuracy.</param>
/// <param name="Interrupt">The global interrupt reduction.</param>
/// <param name="Range">The global range.</param>
internal sealed record GlobalEnhance(double Recharge, double EndDiscount, double Accuracy, double Interrupt, double Range);
