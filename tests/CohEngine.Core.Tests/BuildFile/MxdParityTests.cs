using CohEngine.Core.BuildFile;
using CohEngine.Core.Tests.Parity;
using CohEngine.TestSupport;

using Xunit.Sdk;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>
/// Port of <c>test_mxd_parity.py</c>: each <c>.mxd</c> fixture was saved by Mids from a sample <c>.mbd</c>, so reading it
/// must give the build that <c>.mbd</c> describes.
/// </summary>
[Trait("Category", "Parity")]
public sealed class MxdParityTests
{
    // Mids injects this virtual power to carry set bonuses; a community .mbd may lack it.
    private const string VirtualPower = "Inherent.Inherent.Special_Set_Bonuses";

    public static TheoryData<string> MxdFixtures()
    {
        return [.. Directory.GetFiles(BuildFileFixtures.Root + "/mxd", "*.mxd").Order(StringComparer.Ordinal)];
    }

    public static TheoryData<string> LegacyFixtures()
    {
        return [.. BuildFileFixtures.Files(BuildFileFixtures.Root + "/mxd/legacy", "*.mxd")];
    }

    private static MxdBuild ReadMxd(string path)
    {
        return Mxd.ReadMxd(File.ReadAllText(path), BuildFileFixtures.PowerIndex, BuildFileFixtures.EnhIndex, BuildFileFixtures.Archetypes);
    }

    private static List<BuildPowerEntry> Named(IReadOnlyList<BuildPowerEntry> entries)
    {
        return [.. entries.Where(entry => entry.PowerName.Length > 0 && !string.Equals(entry.PowerName, VirtualPower, StringComparison.Ordinal))];
    }

    private static void Same<T>(T expected, T actual, string path)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new XunitException($"{path}: expected {expected}, got {actual}");
        }
    }

    private static void SameEnhancement(SlottedEnhancement? expected, SlottedEnhancement? actual, string path)
    {
        Same(expected is null, actual is null, $"{path}: enhancement presence");
        if (expected is null || actual is null)
        {
            return;
        }

        Same(expected.Uid, actual.Uid, $"{path}: uid");
        Same(expected.RelativeLevel, actual.RelativeLevel, $"{path}: relative_level");
        Same(expected.Grade, actual.Grade, $"{path}: grade");
        ParityAssert.Equal(expected.IoLevel, actual.IoLevel, $"{path}: io_level");
    }

    private static void SameEntry(BuildPowerEntry expected, BuildPowerEntry actual)
    {
        string power = actual.PowerName;
        Same(expected.PowerName, power, "power name");
        ParityAssert.Equal(expected.Level, actual.Level, $"{power}: level");
        Same(expected.StatInclude, actual.StatInclude, $"{power}: stat_include");
        ParityAssert.Count(expected.SlotEntries.Count, actual.SlotEntries.Count, $"{power}: slot count");
        for (int slot = 0; slot < expected.SlotEntries.Count; slot++)
        {
            ParityAssert.Equal(expected.SlotEntries[slot].Level, actual.SlotEntries[slot].Level, $"{power} slot {slot}: level");
            SameEnhancement(expected.SlotEntries[slot].Enhancement, actual.SlotEntries[slot].Enhancement, $"{power} slot {slot}");
        }
    }

    [Theory]
    [MemberData(nameof(MxdFixtures))]
    public void The_mxd_matches_its_mbd(string path)
    {
        MxdBuild mxd = ReadMxd(path);
        string stem = Path.GetFileNameWithoutExtension(path);
        string mbdPath = Assert.Single(BuildFileFixtures.Files(BuildFileFixtures.Samples, stem + ".mbd"));
        MbdBuild mbd = Mbd.LoadMbd(mbdPath);

        Same(mbd.ClassName, mxd.ClassName, $"{stem}: class");
        Same(mbd.Name, mxd.Name, $"{stem}: name");
        ParityAssert.Equal(mbd.LastPower, mxd.LastPower, $"{stem}: last_power");
        List<BuildPowerEntry> expected = Named(mbd.PowerEntries);
        List<BuildPowerEntry> actual = Named(mxd.PowerEntries);
        ParityAssert.Count(expected.Count, actual.Count, $"{stem}: named power entries");
        for (int index = 0; index < expected.Count; index++)
        {
            SameEntry(expected[index], actual[index]);
        }
    }

    [Theory]
    [MemberData(nameof(LegacyFixtures))]
    public void Every_legacy_mxd_reads(string path)
    {
        Assert.NotEmpty(ReadMxd(path).PowerEntries);
    }

    [Fact]
    public void There_are_five_current_and_131_legacy_fixtures()
    {
        Assert.Equal((5, 131), (MxdFixtures().Count, LegacyFixtures().Count));
    }

    [Fact]
    public void The_scrapper_reads_in_the_current_format()
    {
        MxdBuild mxd = ReadMxd(BuildFileFixtures.Root + "/mxd/Scrapper - Dark-Shield.mxd");

        BuildPowerEntry smite = Named(mxd.PowerEntries)[0];
        Assert.Equal(("current", "Class_Scrapper", "Scrapper_Melee.Dark_Melee.Smite", 0), (mxd.Format, mxd.ClassName, smite.PowerName, smite.Level));
        Assert.Equal(new SlottedEnhancement("Crafted_Hecatomb_A", "None", 49, "PlusFive"), smite.SlotEntries[0].Enhancement);
    }
}
