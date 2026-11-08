namespace CohEngine.Core.Enhancement;

/// <summary>How one eEnhance scalar aspect folds: the GlobalEnhance term it adds and whether it is capped.</summary>
/// <param name="Aspect">The eEnhance name.</param>
/// <param name="GlobalField">The GlobalEnhance term, by its Python field name.</param>
/// <param name="Capped">Whether the AT recharge cap clamps the fold.</param>
internal sealed record AspectHandler(string Aspect, string GlobalField, bool Capped);
