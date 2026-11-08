using System.Globalization;

namespace CohEngine.Core.Maths;

/// <summary>
/// The Maths.mhd enhancement-multiplier tables, indexed exactly as MidsReborn stores them
/// (the Python port's <c>maths.MathTables</c>). Every cell is single-precision quantized.
/// </summary>
/// <param name="MultEd"><c>MultED[schedule][threshold]</c>: ED thresholds per schedule A..D (0..3).</param>
/// <param name="MultTo"><c>MultTO[0][schedule]</c>: Training Origin effectiveness per schedule.</param>
/// <param name="MultDo"><c>MultDO[0][schedule]</c>: Dual Origin effectiveness per schedule.</param>
/// <param name="MultSo"><c>MultSO[0][schedule]</c>: Single Origin effectiveness per schedule (also SpecialO and Hami-O).</param>
/// <param name="MultHo"><c>MultHO[0][schedule]</c>: loaded but unused by <c>GetScheduleMult</c>.</param>
/// <param name="MultIo"><c>MultIO[ioLevel0Based][schedule]</c>: 53 rows; a level-50 IO reads row 49.</param>
public sealed record MathTables(
    IReadOnlyList<IReadOnlyList<double>> MultEd,
    IReadOnlyList<double> MultTo,
    IReadOnlyList<double> MultDo,
    IReadOnlyList<double> MultSo,
    IReadOnlyList<double> MultHo,
    IReadOnlyList<IReadOnlyList<double>> MultIo)
{
    private const int IoRows = 53;

    /// <summary>Parses a Maths.mhd file.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The parsed tables.</returns>
    /// <exception cref="InvalidDataException">The version header or a section header is missing, or a row is short.</exception>
    public static MathTables Load(string path)
    {
        using StreamReader reader = File.OpenText(path);
        return Parse(reader);
    }

    /// <summary>Parses Maths.mhd text the way <c>DatabaseAPI.LoadMaths</c> and <c>FileIO.IOGrab</c>/<c>IOSeek</c> read it.</summary>
    /// <param name="reader">The tab-delimited text.</param>
    /// <returns>The parsed tables.</returns>
    /// <exception cref="InvalidDataException">The version header or a section header is missing, or a row is short.</exception>
    public static MathTables Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        Seek(reader, "Version:");
        Seek(reader, "EDRT");

        // File rows are thresholds 1..3 and columns schedules A..D; Mids stores the transpose.
        string[][] thresholdRows = [Grab(reader), Grab(reader), Grab(reader)];
        IReadOnlyList<double>[] multEd = new IReadOnlyList<double>[4];
        for (int schedule = 0; schedule < 4; schedule++)
        {
            multEd[schedule] = [.. thresholdRows.Select(row => TryParseFloat(Cell(row, schedule + 1)))];
        }

        Seek(reader, "EGE");
        IReadOnlyList<double> multTo = Row4(reader);
        IReadOnlyList<double> multDo = Row4(reader);
        IReadOnlyList<double> multSo = Row4(reader);
        IReadOnlyList<double> multHo = Row4(reader);
        Seek(reader, "LBIOE");
        IReadOnlyList<double>[] multIo = new IReadOnlyList<double>[IoRows];
        for (int row = 0; row < IoRows; row++)
        {
            multIo[row] = Row4(reader);
        }

        return new MathTables(multEd, multTo, multDo, multSo, multHo, multIo);
    }

    /// <summary>
    /// Mirrors <c>FileIO.IOStrip</c> including its quirk: the leading-strip result feeds only the
    /// trailing-space test, so a leading apostrophe or space is never removed. Reproduce, do not fix.
    /// </summary>
    /// <param name="token">One tab-separated cell.</param>
    /// <returns>The cell as Mids returns it.</returns>
    internal static string Strip(string token)
    {
        string probe = token.StartsWith('\'') || token.StartsWith(' ') ? token[1..] : token;
        if (probe.EndsWith(' '))
        {
            token = token[..^1];
        }

        return token.Replace("\"", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Mirrors <c>float.TryParse</c> as the loader uses it: a parse failure yields 0.</summary>
    /// <param name="token">The cell text.</param>
    /// <returns>The quantized value, or 0.</returns>
    internal static double TryParseFloat(string token)
    {
        // shortcut: .NET's invariant Float style stands in for Python's float(); they differ only on
        // exotic spellings (underscores, "infinity" casing) Maths.mhd does not hold. Upgrade if one appears.
        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? F32.Round(value)
            : 0.0;
    }

    private static string[] Grab(TextReader reader)
    {
        string line = reader.ReadLine()
            ?? throw new InvalidDataException("unexpected end of file while reading Maths.mhd tables");
        return [.. line.Split('\t').Select(Strip)];
    }

    private static void Seek(TextReader reader, string label)
    {
        while (reader.ReadLine() is { } line)
        {
            if (string.Equals(Strip(line.Split('\t')[0]), label, StringComparison.Ordinal))
            {
                return;
            }
        }

        throw new InvalidDataException($"section header '{label}' not found in Maths.mhd");
    }

    private static double[] Row4(TextReader reader)
    {
        string[] tokens = Grab(reader);
        return [.. Enumerable.Range(1, 4).Select(column => TryParseFloat(Cell(tokens, column)))];
    }

    private static string Cell(string[] tokens, int column)
    {
        return column < tokens.Length
            ? tokens[column]
            : throw new InvalidDataException($"Maths.mhd row has {tokens.Length} cells; expected at least {column + 1}");
    }
}
