using System.Globalization;
using System.Numerics;
using System.Text;

namespace CohEngine.Core.Diagnostics;

/// <summary>Python's text forms (<c>repr</c>, <c>str</c>, float repr), so messages and JSON match the Python engine byte for byte.</summary>
public static class PyText
{
    /// <summary>Python's <c>repr</c> of a string: single quotes unless the text holds one and no double quote.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The quoted form.</returns>
    public static string Repr(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        char quote = value.Contains('\'', StringComparison.Ordinal) && !value.Contains('"', StringComparison.Ordinal) ? '"' : '\'';
        StringBuilder builder = new();
        builder.Append(quote);
        foreach (char letter in value)
        {
            builder.Append(letter switch
            {
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when letter == quote => "\\" + quote,
                _ when letter < ' ' || letter == '\u007f' => "\\x" + ((int)letter).ToString("x2", CultureInfo.InvariantCulture),
                _ => letter.ToString(),
            });
        }

        return builder.Append(quote).ToString();
    }

    /// <summary>Python's <c>repr</c> of a JSON-shaped value: null, bool, integer, float or string.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The repr text.</returns>
    public static string Repr(object? value)
    {
        return value is string text ? Repr(text) : Str(value);
    }

    /// <summary>Python's <c>str</c> of a JSON-shaped value: null, bool, integer, float or string.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Str(object? value)
    {
        return value switch
        {
            null => "None",
            bool flag => flag ? "True" : "False",
            string text => text,
            double number => NumberRepr(number),
            int or long or BigInteger => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            _ => throw new ArgumentException($"no Python text form for {value.GetType().Name}", nameof(value)),
        };
    }

    /// <summary>Python's list <c>repr</c> of integers.</summary>
    /// <param name="values">The values.</param>
    /// <returns>For example <c>[1, 2]</c>.</returns>
    public static string List(IEnumerable<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return "[" + string.Join(", ", values.Select(item => item.ToString(CultureInfo.InvariantCulture))) + "]";
    }

    /// <summary>Python's list <c>repr</c> of strings.</summary>
    /// <param name="values">The values.</param>
    /// <returns>For example <c>['a', 'b']</c>.</returns>
    public static string List(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return "[" + string.Join(", ", values.Select(Repr)) + "]";
    }

    /// <summary>Python's float <c>repr</c>: the shortest round-trip digits, <c>1.0</c> for a whole number, an exponent past 1e16 or below 1e-4.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The repr text.</returns>
    public static string NumberRepr(double value)
    {
        if (double.IsNaN(value))
        {
            return "nan";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "inf" : "-inf";
        }

        if (value == 0.0)
        {
            return double.IsNegative(value) ? "-0.0" : "0.0";
        }

        string sign = value < 0 ? "-" : string.Empty;
        string[] parts = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture).Split('E');
        int exponent = parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        string[] halves = parts[0].Split('.');
        string digits = halves[0] + (halves.Length == 2 ? halves[1] : string.Empty);
        int point = halves[0].Length + exponent;
        string trimmed = digits.TrimStart('0');
        point -= digits.Length - trimmed.Length;
        digits = trimmed.TrimEnd('0');
        if (point <= -4 || point > 16)
        {
            string mantissa = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
            int shown = point - 1;
            return sign + mantissa + "e" + (shown < 0 ? "-" : "+") + Math.Abs(shown).ToString("00", CultureInfo.InvariantCulture);
        }

        if (point <= 0)
        {
            return sign + "0." + new string('0', -point) + digits;
        }

        return point >= digits.Length
            ? sign + digits + new string('0', point - digits.Length) + ".0"
            : sign + digits[..point] + "." + digits[point..];
    }
}
