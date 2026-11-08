namespace CohEngine.Core.Totals.Incarnate;

/// <summary>
/// The raw pre-ED and post-ED incarnate contributions the numeric core co-EDs and folds in. The consumer feeds the
/// pre-ED term through the slotted aggregate before its one ApplyED, then adds the post-ED term; no ED happens here.
/// </summary>
/// <param name="Scalar">By (build index, eEnhance aspect): the per-power recharge, end, interrupt, accuracy and range fold.</param>
/// <param name="Effect">By (build index, effect index): the per-effect enhancement.</param>
internal sealed record IncarnateAddends(
    IReadOnlyDictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)> Scalar,
    IReadOnlyDictionary<(int BuildIndex, int EffectIndex), (double PreEd, double PostEd)> Effect);
