using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CohEngine.Core.Chain;

/// <summary>Python <c>repr</c> and truthiness helpers for strings and lists.</summary>
public static class PyText
{
    /// <summary>Python <c>repr(str)</c>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The quoted text.</returns>
    public static string Repr(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        char quote = text.Contains('\'', StringComparison.Ordinal) && !text.Contains('"', StringComparison.Ordinal) ? '"' : '\'';
        StringBuilder builder = new();
        builder.Append(quote);
        foreach (char letter in text)
        {
            builder.Append(letter == quote || letter == '\\' ? "\\" + letter : letter == '\n' ? "\\n" : letter == '\t' ? "\\t" : letter == '\r' ? "\\r" : letter.ToString());
        }

        return builder.Append(quote).ToString();
    }

    /// <summary>Python <c>repr(list[str])</c>.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The list text.</returns>
    public static string Repr(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return "[" + string.Join(", ", items.Select(Repr)) + "]";
    }

    /// <summary>Python <c>repr(int)</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Repr(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
