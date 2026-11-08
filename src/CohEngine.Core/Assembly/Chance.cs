using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Assembly;

/// <summary><c>Effect.Probability</c> as a recompute leaves it: the base probability plus the build's chance modifier.</summary>
internal static class Chance
{
    /// <summary>Gets the empty modifiers each power is assembled with; the cross-power pass fills the build's.</summary>
    public static IReadOnlyDictionary<string, double> None { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary><c>Effect.Probability</c>.</summary>
    /// <param name="effect">The effect.</param>
    /// <param name="mods">The build's chance modifiers by effect id.</param>
    /// <returns>The probability.</returns>
    public static double Probability(Effect effect, IReadOnlyDictionary<string, double> mods)
    {
        if (Computed(effect))
        {
            return effect.Probability;
        }

        double sum = effect.BaseProbability + Mod(effect, mods);
        double capped = sum < 1.0 ? sum : 1.0;
        return capped > 0.0 ? capped : 0.0;
    }

    /// <summary><c>Effect.EffectiveProbability</c>: the base probability that makes the probability read <paramref name="value"/>.</summary>
    /// <param name="effect">The effect.</param>
    /// <param name="value">The probability wanted.</param>
    /// <param name="mods">The build's chance modifiers.</param>
    /// <returns>The effect with that base probability and its probability recomputed.</returns>
    public static Effect WithProbability(Effect effect, double value, IReadOnlyDictionary<string, double> mods)
    {
        effect = effect with { BaseProbability = F32.Round(value - Mod(effect, mods)) };
        return effect with { Probability = Probability(effect, mods) };
    }

    /// <summary>Whether Mids computes the probability from PPM or an expression, so the stored value stands.</summary>
    private static bool Computed(Effect effect)
    {
        return effect.ProcsPerMinute > 0 || (string.Equals(effect.AttribType, "Expression", StringComparison.Ordinal) && effect.ExprProbability.Trim().Length != 0);
    }

    private static double Mod(Effect effect, IReadOnlyDictionary<string, double> mods)
    {
        return effect.EffectId.Length != 0 ? mods.GetValueOrDefault(effect.EffectId, 0.0) : 0.0;
    }
}
