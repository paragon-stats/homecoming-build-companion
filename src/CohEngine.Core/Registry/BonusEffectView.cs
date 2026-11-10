namespace CohEngine.Core.Registry;

/// <summary>One effect of a set-bonus power, resolved through the modifier tables.</summary>
/// <param name="Power">The bonus power's full name.</param>
/// <param name="EffectType">The effect type, for example <c>Defense</c>.</param>
/// <param name="Aspect">The damage or mez type the effect applies to, or <c>None</c>.</param>
/// <param name="Magnitude">The resolved magnitude as the game stores it (a fraction for a percentage effect).</param>
/// <param name="Percent">The magnitude as a percentage, for an effect the game displays as one; otherwise null.</param>
public sealed record BonusEffectView(string Power, string EffectType, string Aspect, double Magnitude, double? Percent);
