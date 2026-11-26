using System.Globalization;
using System.Numerics;
using System.Text;

using CohEngine.Core.Maths;

namespace CohEngine.Core.Diagnostics;

/// <summary>Python's text forms (<c>repr</c>, <c>str</c>, float repr), so messages and JSON match the Python engine byte for byte.</summary>
public static class PyText
{
    /// <summary>The tries at replacing or deleting a file another process holds before the write fails.</summary>
    private const int FileAttempts = 3;

    private const int ErrorSharingViolation = 32;

    private const int ErrorLockViolation = 33;

    private static readonly TimeSpan FileRetryWait = TimeSpan.FromMilliseconds(100);

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
        if (SpecialRepr(value) is { } special)
        {
            return special;
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
            return sign + Scientific(digits, point);
        }

        if (point <= 0)
        {
            return sign + "0." + new string('0', -point) + digits;
        }

        return point >= digits.Length
            ? sign + digits + new string('0', point - digits.Length) + ".0"
            : sign + digits[..point] + "." + digits[point..];
    }

    /// <summary>Python's <c>str.isspace</c> for one character: .NET's white space plus the four separators U+001C to U+001F.</summary>
    /// <param name="letter">The character.</param>
    /// <returns>True when Python counts it as white space.</returns>
    public static bool IsSpace(char letter)
    {
        return char.IsWhiteSpace(letter) || letter is >= '\u001c' and <= '\u001f';
    }

    /// <summary>Python's <c>str.strip()</c>.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The text without leading and trailing white space.</returns>
    public static string Strip(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int start = 0;
        int end = value.Length;
        while (start < end && IsSpace(value[start]))
        {
            start++;
        }

        while (end > start && IsSpace(value[end - 1]))
        {
            end--;
        }

        return value[start..end];
    }

    /// <summary>Python's <c>" ".join(value.split())</c>: every run of white space folded to one space, none at the ends.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The folded text.</returns>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        StringBuilder builder = new(value.Length);
        bool gap = false;
        foreach (char letter in value)
        {
            if (IsSpace(letter))
            {
                gap = builder.Length > 0;
                continue;
            }

            if (gap)
            {
                builder.Append(' ');
                gap = false;
            }

            builder.Append(letter);
        }

        return builder.ToString();
    }

    /// <summary>Python's <c>str.splitlines()</c>: lines split at every line boundary Python knows, the boundaries dropped.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The lines; no trailing empty line for text that ends with a boundary.</returns>
    public static IReadOnlyList<string> SplitLines(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        List<string> lines = [];
        int start = 0;
        int index = 0;
        while (index < value.Length)
        {
            char letter = value[index];

            // \r\n is one boundary, two characters wide.
            int next = letter == '\r' && index + 1 < value.Length && value[index + 1] == '\n' ? index + 2 : index + 1;
            if (letter is '\n' or '\r' or '\v' or '\f' or '\u0085' or '\u2028' or '\u2029' or (>= '\u001c' and <= '\u001e'))
            {
                lines.Add(value[start..index]);
                start = next;
            }

            index = next;
        }

        if (start < value.Length)
        {
            lines.Add(value[start..]);
        }

        return lines;
    }

    /// <summary>Python's <c>Path.read_text(encoding="utf-8")</c>: strict UTF-8, a byte-order mark kept, and every <c>\r\n</c> and <c>\r</c> read as <c>\n</c>.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text.</returns>
    /// <exception cref="DecoderFallbackException">The file is not valid UTF-8.</exception>
    public static string ReadText(string path)
    {
        string text = StrictUtf8.GetString(File.ReadAllBytes(path));
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    /// <summary>Python's <c>int()</c> of a run of decimal digits, any script's, as a regex <c>\d+</c> captures them.</summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The number, held at <see cref="long.MaxValue"/> past its range.</returns>
    public static long Number(string digits)
    {
        ArgumentNullException.ThrowIfNull(digits);
        long number = 0;
        foreach (char digit in digits)
        {
            int value = (int)char.GetNumericValue(digit);
            number = number > (long.MaxValue - value) / 10 ? long.MaxValue : (number * 10) + value;
        }

        return number;
    }

    /// <summary>
    /// Python's <c>Path.write_text(text, encoding="utf-8", newline="\n")</c>: UTF-8 without a byte-order mark, the text as
    /// given. The text goes to a temporary file beside the target, which then replaces it, so a failed write leaves the
    /// old file whole.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text.</param>
    /// <exception cref="IOException">Another process held the file through every attempt to replace it.</exception>
    public static void WriteText(string path, string text)
    {
        WriteText(path, text, Thread.Sleep);
    }

    /// <summary>Appends text to a file as UTF-8, creating it when missing; the old bytes and the text replace the file whole, as <see cref="WriteText(string, string)"/> does.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text.</param>
    /// <exception cref="IOException">Another process held the file through every attempt to replace it.</exception>
    public static void AppendText(string path, string text)
    {
        AppendText(path, text, Thread.Sleep);
    }

    /// <summary>Deletes a file, retrying while another process holds it, as <see cref="WriteText(string, string)"/> does.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="IOException">Another process held the file through every attempt to delete it.</exception>
    public static void DeleteFile(string path)
    {
        Retried(Path.GetFullPath(path), () => File.Delete(path), Thread.Sleep);
    }

    /// <summary>As <see cref="WriteText(string, string)"/>, waiting between attempts with <paramref name="wait"/>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text.</param>
    /// <param name="wait">Waits the given time.</param>
    internal static void WriteText(string path, string text, Action<TimeSpan> wait)
    {
        byte[] content = StrictUtf8.GetBytes(text);
        Replace(Path.GetFullPath(path), () => content, wait);
    }

    /// <summary>As <see cref="AppendText(string, string)"/>, waiting between attempts with <paramref name="wait"/>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text.</param>
    /// <param name="wait">Waits the given time.</param>
    internal static void AppendText(string path, string text, Action<TimeSpan> wait)
    {
        string full = Path.GetFullPath(path);
        byte[] added = StrictUtf8.GetBytes(text);
        Replace(full, () => File.Exists(full) ? [.. File.ReadAllBytes(full), .. added] : added, wait);
    }

    /// <summary>
    /// Writes the content to a temporary file beside the target, which then replaces it, so a failed write leaves the old
    /// file whole; reading the content and replacing the file are retried while another process holds it.
    /// </summary>
    private static void Replace(string full, Func<byte[]> content, Action<TimeSpan> wait)
    {
        // A short name of its own, so a target name at the length limit still leaves room for it.
        string temporary = Path.Join(Path.GetDirectoryName(full), "." + Path.GetRandomFileName() + ".tmp");
        try
        {
            Retried(
                full,
                () =>
                {
                    File.WriteAllBytes(temporary, content());
                    File.Move(temporary, full, overwrite: true);
                },
                wait);
        }
        finally
        {
            RemoveTemporary(temporary);
        }
    }

    /// <summary>Deletes the temporary file when it can; a file someone else holds, or a folder gone, leaves it behind rather than replace the error already in flight.</summary>
    private static void RemoveTemporary(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: a stray .tmp file costs nothing, losing the caller's real error costs the diagnosis.
        }
    }

    /// <summary>Runs a replace or delete of <paramref name="path"/>, retrying while another process holds the file; the last refusal names it.</summary>
    private static void Retried(string path, Action change, Action<TimeSpan> wait)
    {
        for (int attempt = 1; attempt < FileAttempts; attempt++)
        {
            try
            {
                change();
                return;
            }
            catch (Exception exception) when (IsHeld(exception))
            {
                wait(FileRetryWait);
            }
        }

        try
        {
            change();
        }
        catch (Exception exception) when (IsHeld(exception))
        {
            throw new IOException(string.Create(CultureInfo.InvariantCulture, $"{path} is in use by another process (or access to it is denied); it was tried {FileAttempts} times; the system said: {exception.Message}"), exception);
        }
    }

    /// <summary>Whether a failure is a sharing or lock violation, or access denied, as Windows reports a file another handle holds.</summary>
    private static bool IsHeld(Exception exception)
    {
        return exception is UnauthorizedAccessException || (exception is IOException && (exception.HResult & 0xFFFF) is ErrorSharingViolation or ErrorLockViolation);
    }

    /// <summary>The repr of NaN, an infinity or a zero, or null for any other value.</summary>
    private static string? SpecialRepr(double value)
    {
        if (double.IsNaN(value))
        {
            return "nan";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "inf" : "-inf";
        }

        if (Exact.Same(value, 0.0))
        {
            return double.IsNegative(value) ? "-0.0" : "0.0";
        }

        return null;
    }

    private static string Scientific(string digits, int point)
    {
        string mantissa = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
        int shown = point - 1;
        return mantissa + "e" + (shown < 0 ? "-" : "+") + Math.Abs(shown).ToString("00", CultureInfo.InvariantCulture);
    }
}
