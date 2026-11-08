using System.Globalization;

using CohEngine.Core.Data;

namespace CohEngine.Core.Expressions;

/// <summary><c>Effect.ValidateConditional</c> and <c>Effect.CanInclude</c> over an effect's ActiveConditionals.</summary>
internal static class Conditionals
{
    /// <summary>
    /// <c>Effect.ValidateConditional()</c>: the conditions chained left to right, AND binding tighter than OR. Only a
    /// condition led by <c>OR </c>, and not the first, starts a new group; every other one is ANDed on.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <param name="context">The build state.</param>
    /// <returns>Whether the conditions hold.</returns>
    /// <exception cref="NotSupportedException">E92 for a condition type other than Active, Taken and Stacks.</exception>
    /// <exception cref="InvalidDataException">E93 for a condition value that does not read as that type's value.</exception>
    public static bool ConditionalsMet(Effect effect, ExpressionContext context)
    {
        List<List<bool>> groups = [];
        for (int position = 0; position < effect.Conditionals.Count; position++)
        {
            (string key, string value) = effect.Conditionals[position];
            bool held = ConditionHolds(effect, key, value, context);
            if (groups.Count > 0 && !(position > 0 && key.StartsWith("OR ", StringComparison.Ordinal)))
            {
                groups[^1].Add(held);
            }
            else
            {
                groups.Add([held]);
            }
        }

        return groups.Count == 0 || groups.Any(group => group.All(held => held));
    }

    /// <summary>
    /// <c>Effect.CanInclude</c>: an effect with a special case is out; one with conditions needs every one met.
    /// Without a context only an unconditional effect is in.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <param name="context">The build state, or null.</param>
    /// <returns>Whether the effect counts.</returns>
    /// <exception cref="InvalidDataException">E94 for an effect that counts conditions but carries none, given a context.</exception>
    public static bool EffectIncluded(Effect effect, ExpressionContext? context)
    {
        if (!string.Equals(effect.SpecialCase, "None", StringComparison.Ordinal))
        {
            return false;
        }

        if (context is null)
        {
            return effect.ActiveConditionalsCount == 0 && effect.Conditionals.Count == 0;
        }

        if (effect.ActiveConditionalsCount != 0 && effect.Conditionals.Count == 0)
        {
            throw new InvalidDataException(
                $"E94: effect {effect.Index} counts {effect.ActiveConditionalsCount} conditions but carries none: "
                + "expected the conditions loaded with the effect, found an empty list; "
                + "load the effect from a record that holds ActiveConditionals");
        }

        return effect.Conditionals.All(condition => IncludedByCondition(effect, condition.Key, condition.Value, context));
    }

    /// <summary>
    /// One condition as <c>CanInclude</c> reads it: no AND/OR lead is stripped, so a led key is not met, and a Team
    /// condition is met only with team members configured, which the reference dumps never are.
    /// </summary>
    private static bool IncludedByCondition(Effect effect, string key, string value, ExpressionContext context)
    {
        string kind = key.Split(':', 2)[0];
        if (string.Equals(kind, "Team", StringComparison.Ordinal) || kind.StartsWith("AND ", StringComparison.Ordinal) || kind.StartsWith("OR ", StringComparison.Ordinal))
        {
            return false;
        }

        return ConditionHolds(effect, key, value, context);
    }

    /// <summary>One ActiveConditionals entry; the key reads <c>&lt;type&gt;:&lt;power full name&gt;</c>, optionally led by AND or OR.</summary>
    private static bool ConditionHolds(Effect effect, string key, string value, ExpressionContext context)
    {
        string[] parts = key.Replace("AND ", string.Empty, StringComparison.Ordinal).Replace("OR ", string.Empty, StringComparison.Ordinal).Split(':', 2);
        string kind = parts[0];
        string powerName = parts.Length > 1 ? parts[1] : string.Empty;
        switch (kind)
        {
            case "Active":
                return context.ActivePowerNames.Contains(powerName) == Boolean(effect, key, value);
            case "Taken":
                return context.BuildPowerNames.Contains(powerName) == Boolean(effect, key, value);
            case "Stacks":
                string[] split = value.Split(' ', 2);
                string count = split.Length > 1 ? split[1] : string.Empty;
                if (!int.TryParse(count, NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out int wanted))
                {
                    throw new InvalidDataException(
                        $"E93: effect {effect.Index} condition '{key}': expected a Stacks value of the form "
                        + $"'<op> <integer>', found '{value}'; fix the condition record");
                }

                int stacks = context.VariableValues.GetValueOrDefault(powerName, 0);
                return split[0] switch
                {
                    "=" => stacks == wanted,
                    ">" => stacks > wanted,
                    "<" => stacks < wanted,
                    _ => false,
                };
            default:
                throw new NotSupportedException(
                    $"E92: effect {effect.Index} condition '{key}': expected a condition type of Active, Taken or Stacks, "
                    + $"found '{kind}'; port that condition type against a Mids reference dump that exercises it");
        }
    }

    /// <summary><c>Convert.ToBoolean</c> on a condition value: True or False in any case.</summary>
    private static bool Boolean(Effect effect, string key, string value)
    {
        string text = value.Trim().ToLowerInvariant();
        if (text is not ("true" or "false"))
        {
            throw new InvalidDataException(
                $"E93: effect {effect.Index} condition '{key}': expected the value 'True' or 'False', "
                + $"found '{value}'; fix the condition record");
        }

        return string.Equals(text, "true", StringComparison.Ordinal);
    }
}
