using System.Globalization;
using System.Numerics;

namespace CohEngine.Core.Chain;

/// <summary>Python's float rendering and <c>sum</c>, so printed numbers match the Python port byte for byte.</summary>
public static class PyFloat
{
    /// <summary>Python 3.12+ <c>sum</c> over floats: the first item, then Neumaier-compensated addition.</summary>
    /// <param name="values">The items.</param>
    /// <returns>The sum; 0 when empty.</returns>
    public static double Sum(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        bool first = true;
        double total = 0.0;
        double compensation = 0.0;
        foreach (double value in values)
        {
            if (first)
            {
                total = value + 0.0;
                first = false;
                continue;
            }

            double next = total + value;
            compensation += Math.Abs(total) >= Math.Abs(value) ? (total - next) + value : (value - next) + total;
            total = next;
        }

        return compensation != 0.0 && double.IsFinite(compensation) ? total + compensation : total;
    }

    /// <summary><c>format(value, ".Nf")</c>, or <c>"+.Nf"</c> when <paramref name="plus"/> is set.</summary>
    /// <param name="value">The value.</param>
    /// <param name="digits">Digits after the point.</param>
    /// <param name="plus">Whether a positive value gets a plus sign.</param>
    /// <returns>The text.</returns>
    public static string Fixed(double value, int digits, bool plus = false)
    {
        if (!double.IsFinite(value))
        {
            return NonFinite(value);
        }

        BigInteger numerator = Exact(Math.Abs(value), out BigInteger denominator);
        BigInteger scaled = RoundHalfEven(numerator * BigInteger.Pow(10, digits), denominator);
        string text = scaled.ToString(CultureInfo.InvariantCulture).PadLeft(digits + 1, '0');
        string body = digits == 0 ? text : text[..^digits] + "." + text[^digits..];
        return (double.IsNegative(value) ? "-" : plus ? "+" : string.Empty) + body;
    }

    /// <summary>The value times 100 as a double, fixed to <paramref name="digits"/>, then a percent sign.</summary>
    /// <param name="value">The fraction.</param>
    /// <param name="digits">Digits after the point.</param>
    /// <returns>The text.</returns>
    public static string Percent(double value, int digits)
    {
        return Fixed(value * 100.0, digits) + "%";
    }

    /// <summary><c>format(value, "g")</c>: six significant digits, trailing zeros dropped.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string General(double value)
    {
        if (!double.IsFinite(value))
        {
            return NonFinite(value);
        }

        if (value == 0.0)
        {
            return double.IsNegative(value) ? "-0" : "0";
        }

        (string digits, int exponent) = Significant(Math.Abs(value), 6);
        string sign = value < 0.0 ? "-" : string.Empty;
        string trimmed = digits.TrimEnd('0');
        if (exponent < -4 || exponent >= 6)
        {
            return sign + Scientific(trimmed, exponent);
        }

        if (exponent < 0)
        {
            return sign + "0." + new string('0', -exponent - 1) + trimmed;
        }

        string whole = trimmed.Length > exponent + 1 ? trimmed[..(exponent + 1)] + "." + trimmed[(exponent + 1)..] : trimmed.PadRight(exponent + 1, '0');
        return sign + whole;
    }

    /// <summary>Python <c>repr(float)</c>: the shortest text that reads back to the same value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Repr(double value)
    {
        if (!double.IsFinite(value))
        {
            return NonFinite(value);
        }

        if (value == 0.0)
        {
            return double.IsNegative(value) ? "-0.0" : "0.0";
        }

        (string digits, int exponent) = ShortestDigits(Math.Abs(value).ToString("R", CultureInfo.InvariantCulture));
        string sign = value < 0.0 ? "-" : string.Empty;
        if (exponent < -4 || exponent >= 16)
        {
            return sign + Scientific(digits, exponent);
        }

        if (exponent < 0)
        {
            return sign + "0." + new string('0', -exponent - 1) + digits;
        }

        if (digits.Length <= exponent + 1)
        {
            return sign + digits.PadRight(exponent + 1, '0') + ".0";
        }

        return sign + digits[..(exponent + 1)] + "." + digits[(exponent + 1)..];
    }

    private static string Scientific(string digits, int exponent)
    {
        string mantissa = digits.Length > 1 ? digits[..1] + "." + digits[1..] : digits;
        return mantissa + "e" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
    }

    private static string NonFinite(double value)
    {
        return double.IsNaN(value) ? "nan" : value > 0 ? "inf" : "-inf";
    }

    /// <summary>The significant digits and decimal exponent of a "R" rendering such as 1.5E-05, 0.001 or 123456.</summary>
    private static (string Digits, int Exponent) ShortestDigits(string shortest)
    {
        int exponent = 0;
        string mantissa = shortest;
        int marker = shortest.IndexOf('E', StringComparison.Ordinal);
        if (marker >= 0)
        {
            mantissa = shortest[..marker];
            exponent = int.Parse(shortest[(marker + 1)..], CultureInfo.InvariantCulture);
        }

        int point = mantissa.IndexOf('.', StringComparison.Ordinal);
        string whole = point < 0 ? mantissa : mantissa[..point];
        string all = whole + (point < 0 ? string.Empty : mantissa[(point + 1)..]);
        int lead = all.Length - all.TrimStart('0').Length;
        return (all.TrimStart('0').TrimEnd('0'), exponent + whole.Length - 1 - lead);
    }

    private static BigInteger Exact(double value, out BigInteger denominator)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0)
        {
            exponent++;
        }
        else
        {
            mantissa |= 1L << 52;
        }

        exponent -= 1075;
        denominator = exponent >= 0 ? BigInteger.One : BigInteger.Pow(2, -exponent);
        return exponent >= 0 ? mantissa * BigInteger.Pow(2, exponent) : mantissa;
    }

    private static BigInteger RoundHalfEven(BigInteger numerator, BigInteger denominator)
    {
        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        int compare = (remainder * 2).CompareTo(denominator);
        return compare > 0 || (compare == 0 && !quotient.IsEven) ? quotient + 1 : quotient;
    }

    private static (string Digits, int Exponent) Significant(double value, int count)
    {
        BigInteger numerator = Exact(value, out BigInteger denominator);
        int exponent = (int)Math.Floor(Math.Log10(value));
        while (true)
        {
            int shift = count - 1 - exponent;
            BigInteger scaledNumerator = shift >= 0 ? numerator * BigInteger.Pow(10, shift) : numerator;
            BigInteger scaledDenominator = shift >= 0 ? denominator : denominator * BigInteger.Pow(10, -shift);
            string text = RoundHalfEven(scaledNumerator, scaledDenominator).ToString(CultureInfo.InvariantCulture);
            if (text.Length == count)
            {
                return (text, exponent);
            }

            exponent += text.Length > count ? 1 : -1;
        }
    }
}
