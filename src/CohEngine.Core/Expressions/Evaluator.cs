using System.Globalization;
using System.Text.RegularExpressions;

using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Expressions;

/// <summary>
/// <c>Expressions.Parse</c> for a magnitude: the keywords are substituted as text, in order, and the arithmetic
/// left is evaluated. Floats are rendered as .NET renders them, and constants are replaced before the functions.
/// </summary>
internal static class Evaluator
{
    private const double DoubleEpsilon = 5e-324;

    private static readonly Regex VariableVal = new(@"(?<name>[a-zA-Z\-_\.]+)>variableVal", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex PowerGroupIn = new(@"powerGroupIn\((?<group>[a-zA-Z0-9_\-\.]+)\)", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex PowerGroupNotIn = new(@"powerGroupNotIn\((?<group>[a-zA-Z0-9_\-\.]+)\)", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex AndOrCall = new(@"\b(?<word>and|or)\s*\(", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Evaluates an effect's magnitude expression. Only the keywords and functions the committed builds use are
    /// substituted; anything else is refused rather than evaluated to the 0 Mids returns on a parse failure.
    /// </summary>
    /// <param name="effect">The effect whose <c>ExprMagnitude</c> is read.</param>
    /// <param name="power">The power that owns the effect once assembled.</param>
    /// <param name="context">The build state, or null.</param>
    /// <param name="scale">The <c>@StdResult</c> value.</param>
    /// <param name="modifier">The <c>modifier&gt;current</c> value.</param>
    /// <returns>The magnitude, single precision.</returns>
    /// <exception cref="NotSupportedException">E08 for an expression outside the ported keywords and grammar, or one that needs a context when none was given.</exception>
    public static double EvaluateMagnitude(Effect effect, Power power, ExpressionContext? context, double scale, double modifier)
    {
        ExpressionContext NeedContext(string what)
        {
            return context ?? throw Unported(effect, power, $"{what}, which needs the build context");
        }

        (string Keyword, Func<string> Text)[] constants =
        [
            ("power.base>activateperiod", () => FloatText(power.ActivatePeriod)),
            ("power.base>activatetime", () => FloatText(power.CastTime)),
            ("power.base>areafactor", () => FloatText(power.AoeModifier)),
            ("power.base>rechargetime", () => FloatText(power.BaseRechargeTime)),
            ("@StdResult", () => FloatText(scale)),
            ("modifier>current", () => FloatText(modifier)),
            ("source>Max.kHitPoints", () => FloatText(NeedContext("source>Max.kHitPoints").PrevHpMax)),
            ("cfg>player>hp", () => (context?.PlayerHpPercent ?? ExpressionContext.DefaultPlayerHpPercent).ToString(CultureInfo.InvariantCulture)),
        ];
        string expr = effect.ExprMagnitude;
        foreach ((string keyword, Func<string> text) in constants)
        {
            if (expr.Contains(keyword, StringComparison.Ordinal))
            {
                expr = expr.Replace(keyword, text(), StringComparison.Ordinal);
            }
        }

        expr = VariableVal.Replace(expr, match =>
        {
            string name = match.Groups["name"].Value;
            if (string.Equals(name, power.FullName, StringComparison.Ordinal) && (context is null || !context.VariableValues.ContainsKey(name)))
            {
                return power.VariableValue.ToString(CultureInfo.InvariantCulture);
            }

            return NeedContext($"the variable value of another power ({name})").VariableValues.GetValueOrDefault(name, 0).ToString(CultureInfo.InvariantCulture);
        });
        expr = PowerGroupIn.Replace(expr, match => power.FullName.StartsWith(match.Groups["group"].Value, StringComparison.Ordinal) ? "1" : "0");
        expr = PowerGroupNotIn.Replace(expr, match => power.FullName.StartsWith(match.Groups["group"].Value, StringComparison.Ordinal) ? "0" : "1");
        expr = AndOrCall.Replace(expr, "${word}_(");
        Parser.Node tree = Parser.Parse(expr) ?? throw Unported(effect, power, $"text that is not plain arithmetic ('{expr}')");
        return F32.Round(Evaluate(tree, effect, power));
    }

    /// <summary>A float32 as .NET formats it into a string: the shortest text that reads back to the same value.</summary>
    /// <param name="value">The value, quantized first.</param>
    /// <returns>The shortest round-trip text.</returns>
    internal static string FloatText(double value)
    {
        value = F32.Round(value);
        for (int digits = 1; ; digits++)
        {
            string text = value.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            if (Exact.Same(F32.Round(double.Parse(text, CultureInfo.InvariantCulture)), value))
            {
                return text;
            }
        }
    }

    /// <summary>Double division as .NET does it: a zero divisor gives an infinity or NaN.</summary>
    private static double Divide(double dividend, double divisor)
    {
        if (!Exact.Same(divisor, 0))
        {
            return dividend / divisor;
        }

        return Exact.Same(dividend, 0) ? double.NaN : double.CopySign(double.PositiveInfinity, dividend);
    }

    private static double PyMin(double first, double second)
    {
        return second < first ? second : first;
    }

    private static double PyMax(double first, double second)
    {
        return second > first ? second : first;
    }

    private static double? Function(string name, double[] args)
    {
        return (name, args.Length) switch
        {
            ("eq", 2) => Math.Abs(args[0] - args[1]) < DoubleEpsilon ? 1 : 0,
            ("ne", 2) => Math.Abs(args[0] - args[1]) > DoubleEpsilon ? 1 : 0,
            ("gt", 2) => args[0] > args[1] ? 1 : 0,
            ("gte", 2) => args[0] >= args[1] ? 1 : 0,
            ("lt", 2) => args[0] < args[1] ? 1 : 0,
            ("lte", 2) => args[0] <= args[1] ? 1 : 0,
            ("minmax", 3) => PyMin(args[1] > args[2] ? args[1] : args[2], PyMax(args[1] > args[2] ? args[2] : args[1], args[0])),
            ("and_", 2) => !Exact.Same(args[0], 0) && !Exact.Same(args[1], 0) ? 1 : 0,
            ("or_", 2) => !Exact.Same(args[0], 0) || !Exact.Same(args[1], 0) ? 1 : 0,
            _ => null,
        };
    }

    private static double Evaluate(Parser.Node node, Effect effect, Power power)
    {
        switch (node)
        {
            case Parser.Node.Number number:
                return number.Value;
            case Parser.Node.Binary { Operator: "+" or "-" or "*" or "/" } binary:
                double left = Evaluate(binary.Left, effect, power);
                double right = Evaluate(binary.Right, effect, power);
                return binary.Operator switch
                {
                    "+" => left + right,
                    "-" => left - right,
                    "*" => left * right,
                    _ => Divide(left, right),
                };
            case Parser.Node.Unary { Operator: "-" or "+" } unary:
                double operand = Evaluate(unary.Operand, effect, power);
                return string.Equals(unary.Operator, "-", StringComparison.Ordinal) ? -operand : operand;
            case Parser.Node.Call { Function: Parser.Node.Name name } call:
                double[] args = [.. call.Args.Select(arg => Evaluate(arg, effect, power))];
                return Function(name.Identifier, args) ?? throw Unported(effect, power, $"the term '{node}'");
            default:
                throw Unported(effect, power, $"the term '{node}'");
        }
    }

    private static NotSupportedException Unported(Effect effect, Power power, string what)
    {
        return new NotSupportedException(
            $"E08: cannot compute Mag for {power.FullName} effect {effect.Index}: the magnitude expression "
            + $"'{effect.ExprMagnitude}' uses {what}, which the expression evaluator does not port; "
            + "port that keyword or function against a Mids reference dump that exercises it");
    }
}
