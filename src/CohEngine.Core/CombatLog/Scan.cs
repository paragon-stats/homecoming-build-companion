using System.Globalization;

namespace CohEngine.Core.CombatLog;

/// <summary>
/// The span-slicing primitives the classifier is built from. No regular expressions: the rules stay
/// literal, ahead-of-time safe, and readable as the line shapes they match.
/// </summary>
internal static class Scan
{
    /// <summary>Cuts <paramref name="text"/> at the first <paramref name="marker"/>.</summary>
    public static bool Split(ReadOnlySpan<char> text, string marker, out ReadOnlySpan<char> head, out ReadOnlySpan<char> tail)
    {
        int index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            head = default;
            tail = default;
            return false;
        }

        head = text[..index];
        tail = text[(index + marker.Length)..];
        return true;
    }

    /// <summary>Cuts <paramref name="text"/> at the last <paramref name="marker"/>.</summary>
    public static bool SplitLast(ReadOnlySpan<char> text, string marker, out ReadOnlySpan<char> head, out ReadOnlySpan<char> tail)
    {
        int index = text.LastIndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            head = default;
            tail = default;
            return false;
        }

        head = text[..index];
        tail = text[(index + marker.Length)..];
        return true;
    }

    /// <summary>Gives what follows <paramref name="prefix"/> when <paramref name="text"/> starts with it.</summary>
    public static bool After(ReadOnlySpan<char> text, string prefix, out ReadOnlySpan<char> rest)
    {
        rest = text;
        return TrimStart(ref rest, prefix);
    }

    /// <summary>Gives what follows the first of <paramref name="prefixes"/> that <paramref name="text"/> starts with.</summary>
    public static bool AfterAny(ReadOnlySpan<char> text, string[] prefixes, out ReadOnlySpan<char> rest)
    {
        for (int index = 0; index < prefixes.Length; index++)
        {
            if (After(text, prefixes[index], out rest))
            {
                return true;
            }
        }

        rest = default;
        return false;
    }

    /// <summary>Removes <paramref name="prefix"/> when <paramref name="text"/> starts with it.</summary>
    public static bool TrimStart(ref ReadOnlySpan<char> text, string prefix)
    {
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        text = text[prefix.Length..];
        return true;
    }

    /// <summary>Removes <paramref name="suffix"/> when <paramref name="text"/> ends with it.</summary>
    public static bool TrimEnd(ref ReadOnlySpan<char> text, string suffix)
    {
        if (!text.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        text = text[..^suffix.Length];
        return true;
    }

    /// <summary>Parses a number the game wrote: a dot for decimals, commas for thousands.</summary>
    public static bool Number(ReadOnlySpan<char> text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Parses a whole number the game wrote, commas for thousands allowed.</summary>
    public static bool Whole(ReadOnlySpan<char> text, out long value)
    {
        return long.TryParse(text, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
    }
}
