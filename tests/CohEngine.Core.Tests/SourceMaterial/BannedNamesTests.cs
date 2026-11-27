using System.Text.RegularExpressions;

using CohEngine.Core.Chain;
using CohEngine.Core.SourceMaterial;

namespace CohEngine.Core.Tests.SourceMaterial;

// Invented names only: a real name must never be committed.
public sealed class BannedNamesTests
{
    private const string Names = " Zorvex Quill , ,Brannoc ";

    [Fact]
    public void The_names_are_split_at_commas_and_trimmed()
    {
        Assert.Equal(["Zorvex Quill", "Brannoc"], BannedNames.Parse(Names));
        Assert.Empty(BannedNames.Parse(null));
        Assert.Null(BannedNames.Pattern([]));
    }

    [Fact]
    public void A_row_is_judged_on_its_json_and_only_the_count_is_reported()
    {
        Regex pattern = BannedNames.Pattern(BannedNames.Parse(Names))!;
        PyObject named = new() { { "title", "Brannoc's \"guide\"" } };
        PyObject clean = new() { { "title", "Fire guide" } };

        (IReadOnlyList<PyObject> kept, int withheld) = BannedNames.Withhold([named, clean], pattern);

        Assert.Equal((1, 1), (kept.Count, withheld));
        Assert.Same(clean, kept[0]);
        Assert.Equal("1 row(s) withheld by the name filter", BannedNames.Report(withheld));
    }
}
