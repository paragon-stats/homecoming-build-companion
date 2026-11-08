using System.Globalization;
using System.Numerics;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry;

/// <summary>
/// The <c>.mbd</c> file facts the <c>H-FMT</c> rules check: the header fields and each enhancement's IoLevel bounds.
/// Present only when a bare <c>.mbd</c> is validated; a Mids dump directory has no such file, so the rules stay silent.
/// </summary>
/// <param name="PowerSets">The <c>PowerSets</c> entries as written.</param>
/// <param name="LevelText">The header <c>Level</c> as written: a string, a number, a bool or null.</param>
/// <param name="IoBoundsByNid">Each enhancement index's inclusive IoLevel range.</param>
public sealed record MbdFormat(IReadOnlyList<string> PowerSets, object? LevelText, IReadOnlyDictionary<int, (int Min, int Max)> IoBoundsByNid)
{
    /// <summary>The number of <c>PowerSets</c> entries MidsReborn indexes (primary, secondary, blank, four pools, ancillary).</summary>
    public const int PowerSetsExpected = 8;

    private const int MaxLevelText = 49;

    /// <summary>Why a header <c>Level</c> is not one MidsReborn loads: a string of an integer from 0 to 49.</summary>
    /// <param name="value">The header value as written.</param>
    /// <returns>The reason, or null when the value loads.</returns>
    public static string? LevelTextProblem(object? value)
    {
        if (value is string text)
        {
            string digits = text.TrimStart('-');
            bool signed = text.Length - digits.Length <= 1;
            if (signed && digits.Length > 0 && digits.All(char.IsAsciiDigit)
                && BigInteger.Parse(text, CultureInfo.InvariantCulture) is { } number && number >= 0 && number <= MaxLevelText)
            {
                return null;
            }
        }

        return $"{PyText.Repr(value)} is not a string of an integer from 0 to {MaxLevelText}";
    }
}
