using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Totals;

/// <summary><c>Effect.Mag</c> and the <c>DatabaseAPI.GetModifier</c> lookups it reads.</summary>
internal static class EffectMagnitude
{
    /// <summary><c>MidsContext.MathLevelBase</c>: the 0-based AttribMod row every base magnitude reads, whatever the level.</summary>
    public const int MathLevelBase = 49;

    /// <summary>
    /// The raw per-effect magnitude. Damage reports a negative magnitude, since damage is stored as a negative buff,
    /// and every multiplication is quantized. GBPA_MultiplyVariable is folded in at read time: a VariableModified effect
    /// of a VariableEnabled power that does not IgnoreScaling has its scale multiplied by the entry's VariableValue.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <param name="power">The power that owns it.</param>
    /// <param name="mods">The AttribMod tables.</param>
    /// <param name="classes">The class table.</param>
    /// <param name="archetypeIndex">The build's class index.</param>
    /// <param name="expr">The build state expressions read, or null.</param>
    /// <returns>The magnitude.</returns>
    /// <exception cref="NotSupportedException">E08 for a magnitude expression the evaluator does not port.</exception>
    public static double Mag(Effect effect, Power power, AttribMods mods, ArchetypeDb classes, int archetypeIndex, ExpressionContext? expr = null)
    {
        double scale = effect.Scale;
        if (power.VariableEnabled && effect.VariableModified && !effect.IgnoreScaling)
        {
            scale = F32.Round(scale * power.VariableValue);
        }

        double sign = string.Equals(effect.EffectType, "Damage", StringComparison.Ordinal) ? -1.0 : 1.0;
        switch (effect.AttribType)
        {
            case "Magnitude":
                double modifier = ModifierForEffect(effect, power, mods, classes, archetypeIndex);
                return F32.Round(F32.Round(F32.Round(sign * scale) * effect.NMagnitude) * modifier);
            case "Duration":
                return F32.Round(sign * effect.NMagnitude);
            case "Expression":
                // A whitespace-only expression is blank to IsNullOrWhiteSpace and falls through to Scale * nMagnitude.
                if (effect.ExprMagnitude.Trim().Length != 0)
                {
                    double current = ModifierForEffect(effect, power, mods, classes, archetypeIndex);
                    return F32.Round(sign * Evaluator.EvaluateMagnitude(effect, power, expr, scale, current));
                }

                return F32.Round(F32.Round(sign * scale) * effect.NMagnitude);
            default:
                return 0.0;
        }
    }

    /// <summary>
    /// The public <c>GetModifier(IEffect)</c>: the class row is the power's ForcedClass, else the effect's absorbed
    /// class, else the build's class, and the level row is always <see cref="MathLevelBase"/>.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <param name="power">The power that owns it.</param>
    /// <param name="mods">The AttribMod tables.</param>
    /// <param name="classes">The class table.</param>
    /// <param name="archetypeIndex">The build's class index.</param>
    /// <returns>The modifier.</returns>
    public static double ModifierForEffect(Effect effect, Power power, AttribMods mods, ArchetypeDb classes, int archetypeIndex)
    {
        int classIndex;
        if (power.ForcedClass.Length != 0)
        {
            classIndex = classes.NidFromUidClass(power.ForcedClass);
        }
        else if (effect.AbsorbedClassNId > -1)
        {
            classIndex = effect.AbsorbedClassNId;
        }
        else
        {
            classIndex = archetypeIndex;
        }

        return Modifier(mods, classes.Classes, classIndex, effect.NModifierTable, MathLevelBase);
    }

    /// <summary>
    /// The private <c>GetModifier(iClass, iTable, iLevel)</c>: one table cell, 0 for any out-of-range index; the
    /// column is the class's stored <c>Column</c>, not its index.
    /// </summary>
    /// <param name="mods">The AttribMod tables.</param>
    /// <param name="classes">The class records.</param>
    /// <param name="classIndex">The class index.</param>
    /// <param name="table">The table index.</param>
    /// <param name="level">The 0-based level row.</param>
    /// <returns>The cell.</returns>
    public static double Modifier(AttribMods mods, IReadOnlyList<Archetype> classes, int classIndex, int table, int level)
    {
        if (classIndex < 0 || table < 0 || level < 0 || classIndex > classes.Count - 1)
        {
            return 0.0;
        }

        int column = classes[classIndex].Column;
        if (column < 0 || table > mods.Tables.Count - 1)
        {
            return 0.0;
        }

        IReadOnlyList<IReadOnlyList<double>> rows = mods.Tables[table].Table;
        if (level > rows.Count - 1)
        {
            return 0.0;
        }

        IReadOnlyList<double> row = rows[level];
        return column > row.Count - 1 ? 0.0 : row[column];
    }
}
