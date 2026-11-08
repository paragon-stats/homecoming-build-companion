using CohEngine.Core.BuildFile;
using CohEngine.Core.Data;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>Port of <c>test_mbd.py</c>: the <c>.mbd</c> reader and the resolution of a build against the Mids dump.</summary>
public sealed class MbdTests : IDisposable
{
    private static readonly string DarkShield = Path.Combine(BuildFileFixtures.Samples, "community", "Scrapper - Dark-Shield.mbd");

    private static readonly string Bane = Path.Combine(BuildFileFixtures.Samples, "operator-builds", "Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training).mbd");

    private readonly string directory = Directory.CreateTempSubdirectory("coh-engine-").FullName;

    public static TheoryData<string> Samples()
    {
        return [.. BuildFileFixtures.SampleMbd()];
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }

    internal static MbdDocument Document(params MbdPowerEntryDoc[] entries)
    {
        return new MbdDocument
        {
            BuiltWith = new MbdBuiltWithDoc { App = "x", Version = "1.0.0", Database = "Homecoming", DatabaseVersion = "1.0.0" },
            Level = "50",
            Class = "Class_Blaster",
            Origin = "Magic",
            Alignment = "Hero",
            Name = "T",
            PowerSets = ["A"],
            PowerEntries = entries,
        };
    }

    private static MbdPowerEntryDoc Entry(string name, int level, params MbdSlotDoc[] slots)
    {
        return new MbdPowerEntryDoc { PowerName = name, Level = level, SlotEntries = slots };
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_sample_build_parses(string path)
    {
        Assert.NotEmpty(Mbd.LoadMbd(path).PowerEntries);
    }

    [Fact]
    public void Meta_and_header_fields_are_read()
    {
        MbdBuild build = Mbd.LoadMbd(DarkShield);

        Assert.Equal(("Homecoming", "Class_Scrapper", "Technology", "Hero", 49, 25), (build.Meta.Database, build.ClassName, build.Origin, build.Alignment, build.Level, build.LastPower));
        Assert.Contains("Scrapper_Melee.Dark_Melee", build.PowerSets, StringComparer.Ordinal);
    }

    [Fact]
    public void Power_and_slot_levels_lose_one()
    {
        BuildPowerEntry smite = Mbd.LoadMbd(DarkShield).PowerEntries[0];

        Assert.Equal(("Scrapper_Melee.Dark_Melee.Smite", 0, 0), (smite.PowerName, smite.Level, smite.SlotEntries[0].Level));
        Assert.Equal(new SlottedEnhancement("Crafted_Hecatomb_A", "None", 49, "PlusFive"), smite.SlotEntries[0].Enhancement);
    }

    [Fact]
    public void An_empty_slot_enhancement_is_null()
    {
        MbdDocument document = Document(Entry("A.B.C", 2, new MbdSlotDoc { Level = 2 }));

        BuildSlot slot = Assert.Single(Mbd.ParseMbd(document).PowerEntries[0].SlotEntries);

        Assert.Equal((null, null, 1), (slot.Enhancement, slot.FlippedEnhancement, slot.Level));
    }

    [Fact]
    public void A_legacy_enhancement_key_resolves_to_the_uid()
    {
        MbdDocument document = Document(Entry("A.B.C", 1, new MbdSlotDoc { Level = 1, Enhancement = new MbdEnhancementDoc { Enhancement = "Legacy_Uid_X" } }));

        Assert.Equal(new SlottedEnhancement("Legacy_Uid_X"), Mbd.ParseMbd(document).PowerEntries[0].SlotEntries[0].Enhancement);
    }

    [Fact]
    public void An_enhancement_object_without_a_uid_is_an_empty_slot()
    {
        MbdDocument document = Document(Entry("A.B.C", 1, new MbdSlotDoc { Level = 1, Enhancement = new MbdEnhancementDoc { Grade = "None" } }));

        Assert.Null(Mbd.ParseMbd(document).PowerEntries[0].SlotEntries[0].Enhancement);
    }

    [Fact]
    public void Sub_power_entries_are_read()
    {
        MbdPowerEntryDoc entry = new()
        {
            PowerName = "A.B.C",
            Level = 1,
            ProcInclude = true,
            VariableValue = 3,
            InherentSlotsUsed = 1,
            SubPowerEntries = [new MbdSubPowerDoc { PowerName = "Sub1", StatInclude = true }],
        };

        BuildPowerEntry parsed = Mbd.ParseMbd(Document(entry)).PowerEntries[0];

        Assert.Equal((3, 1, true), (parsed.VariableValue, parsed.InherentSlotsUsed, parsed.ProcInclude));
        Assert.Equal(new BuildSubPower("Sub1", true), Assert.Single(parsed.SubPowerEntries));
    }

    [Fact]
    public void An_empty_power_entry_keeps_its_raw_level()
    {
        BuildPowerEntry entry = Mbd.ParseMbd(Document(Entry(string.Empty, 5))).PowerEntries[0];

        Assert.Equal((string.Empty, 5), (entry.PowerName, entry.Level));
    }

    [Fact]
    public void A_missing_builtwith_block_is_E118()
    {
        MbdDocument document = Document() with { BuiltWith = null };

        Assert.StartsWith("E118:", Assert.Throws<InvalidDataException>(() => Mbd.ParseMbd(document)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_that_is_not_an_integer_is_E118()
    {
        MbdDocument document = Document() with { Level = "high" };

        Assert.Contains("'high'", Assert.Throws<InvalidDataException>(() => Mbd.ParseMbd(document)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_file_raises()
    {
        Assert.Throws<FileNotFoundException>(() => Mbd.LoadMbd(Path.Combine(directory, "nope.mbd")));
    }

    [Theory]
    [InlineData("""{"BuiltWith":{},"Level":49,"Class":"Class_Blaster"}""", 49)]
    [InlineData("""{"BuiltWith":{},"Level":"7","Class":"Class_Blaster"}""", 7)]
    public void The_header_level_may_be_a_string_or_a_number(string json, int expected)
    {
        string path = Path.Combine(directory, "level.mbd");
        File.WriteAllText(path, json);

        Assert.Equal(expected, Mbd.LoadMbd(path).Level);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"BuiltWith":{},"Level":"1"}""")]
    [InlineData("""{"BuiltWith":{},"Level":null,"Class":"Class_Blaster"}""")]
    public void Unreadable_json_is_E118(string json)
    {
        string path = Path.Combine(directory, "bad.mbd");
        File.WriteAllText(path, json);

        Assert.StartsWith("E118:", Assert.Throws<InvalidDataException>(() => Mbd.LoadMbd(path)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dump_missing_a_field_is_E118()
    {
        foreach (string file in new[] { "enhancements.json", "enhancement_sets.json", "powers.json" })
        {
            File.WriteAllText(Path.Combine(directory, file), "[{}]");
        }

        File.Copy(Path.Combine(Fixtures.Store.MidsDirectory, "archetypes.json"), Path.Combine(directory, "archetypes.json"));

        Assert.StartsWith("E118:", Assert.Throws<InvalidDataException>(() => Mbd.LoadMbdDb(directory)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bane_build_resolves_to_24_picks_and_resolved_slots()
    {
        MbdBuild build = Mbd.LoadMbd(Bane);

        IReadOnlyList<Power> powers = Mbd.PowersFromBuild(build, BuildFileFixtures.Database);
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = Mbd.SlotsFromBuild(build, BuildFileFixtures.Database);

        Assert.Equal(24, powers.Count(power => !power.FullName.StartsWith("Inherent.", StringComparison.Ordinal)));
        Assert.Equal(Enumerable.Range(0, build.PowerEntries.Count), powers.Select(power => power.BuildIndex));
        Assert.True(powers[0].Chosen);
        Assert.Equal(build.PowerEntries[0].Level, powers[0].PickLevel);
        Assert.True(powers[0].Level >= 1);
        Assert.DoesNotContain(slots.SelectMany(pair => pair.Value.Zip(build.PowerEntries[pair.Key].SlotEntries)), pair => pair.Second.Enhancement is not null && pair.First.Enh < 0);
        Assert.True(slots.Values.Sum(refs => refs.Count) > 50);
    }

    [Fact]
    public void A_slot_keeps_its_grade_relative_level_and_io_level()
    {
        MbdDocument document = Document(Entry(
            "Inherent.Inherent.Brawl",
            1,
            new MbdSlotDoc
            {
                Level = 1,
                IsInherent = true,
                Enhancement = new MbdEnhancementDoc { Uid = "Crafted_Accuracy", Grade = "SingleO", IoLevel = 30, RelativeLevel = "PlusOne" },
            },
            new MbdSlotDoc { Level = 3 }));

        IReadOnlyList<SlotRef> refs = Mbd.SlotsFromBuild(Mbd.ParseMbd(document), BuildFileFixtures.Database)[0];

        Assert.Equal((3, 5, 30, 0, true), (refs[0].Grade, refs[0].RelativeLevel, refs[0].IoLevel, refs[0].Level, refs[0].IsInherent));
        Assert.Equal(BuildFileFixtures.Database.Enhancements["Crafted_Accuracy"].Nid, refs[0].Enh);
        Assert.Equal((-1, 2), (refs[1].Enh, refs[1].Level));
    }

    [Fact]
    public void An_enhancement_the_dump_lacks_is_E78()
    {
        MbdDocument document = Document(Entry("A.B.C", 1, new MbdSlotDoc { Level = 1, Enhancement = new MbdEnhancementDoc { Uid = "Crafted_No_Such_Thing" } }));

        string message = Assert.Throws<InvalidDataException>(() => Mbd.SlotsFromBuild(Mbd.ParseMbd(document), BuildFileFixtures.Database)).Message;

        Assert.Matches("^E78: .*Crafted_No_Such_Thing.*enhancements.json", message);
    }

    [Theory]
    [InlineData("Grade")]
    [InlineData("RelativeLevel")]
    public void An_unknown_grade_or_relative_level_is_E117(string field)
    {
        MbdEnhancementDoc enhancement = new() { Uid = "Crafted_Accuracy" };
        enhancement = string.Equals(field, "Grade", StringComparison.Ordinal) ? enhancement with { Grade = "Bogus" } : enhancement with { RelativeLevel = "Bogus" };
        MbdDocument document = Document(Entry("A.B.C", 1, new MbdSlotDoc { Level = 1, Enhancement = enhancement }));

        Assert.StartsWith("E117:", Assert.Throws<InvalidDataException>(() => Mbd.SlotsFromBuild(Mbd.ParseMbd(document), BuildFileFixtures.Database)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_power_the_dump_lacks_is_E77()
    {
        MbdDocument document = Document(Entry("Nope.Nope.Nope", 1));

        string message = Assert.Throws<InvalidDataException>(() => Mbd.PowersFromBuild(Mbd.ParseMbd(document), BuildFileFixtures.Database)).Message;

        Assert.Matches("^E77: .*Nope.Nope.Nope.*powers.json", message);
    }

    [Fact]
    public void An_empty_placeholder_entry_resolves_to_no_power()
    {
        MbdDocument source = Mbd.DumpMbd(Mbd.LoadMbd(Bane));
        MbdBuild build = Mbd.ParseMbd(source with { PowerEntries = [source.PowerEntries[0], Entry(string.Empty, 5), .. source.PowerEntries.Skip(1)] });

        Assert.Equal([0, 2, 3], Mbd.PowersFromBuild(build, BuildFileFixtures.Database).Take(3).Select(power => power.BuildIndex));
        Assert.DoesNotContain(1, Mbd.SlotsFromBuild(build, BuildFileFixtures.Database).Keys);
    }

    [Fact]
    public void Io_bounds_follow_the_enhancement_type()
    {
        MbdDb database = BuildFileFixtures.Database with
        {
            Enhancements = new Dictionary<string, EnhInfo>(StringComparer.Ordinal)
            {
                ["inv"] = new(1, "InventO", -1, 9, 49),
                ["so"] = new(2, "SingleO", -1, 0, 52),
                ["orphan"] = new(3, "SetO", -1, 0, 0),
                ["twin"] = new(3, "SingleO", -1, 0, 0),
            },
        };

        Assert.Equal((9, 49), Mbd.IoLevelBounds("inv", database));
        Assert.Equal((1, 1), Mbd.IoLevelBounds("so", database));
        Assert.Equal((9, 52), Mbd.IoLevelBounds("orphan", database));
        Assert.Null(Mbd.IoLevelBounds("missing", database));
        Assert.Equal([KeyValuePair.Create(1, (9, 49)), KeyValuePair.Create(2, (1, 1)), KeyValuePair.Create(3, (1, 1))], Mbd.IoBoundsByNid(database));
    }

    [Fact]
    public void A_build_is_equal_to_its_round_trip_and_hashes_alike()
    {
        MbdBuild build = Mbd.LoadMbd(DarkShield);
        MbdBuild again = Mbd.ParseMbd(Mbd.DumpMbd(build));

        Assert.Equal(build, again);
        Assert.Equal(build.GetHashCode(), again.GetHashCode());
        Assert.Equal(build.PowerEntries[0].GetHashCode(), again.PowerEntries[0].GetHashCode());
        Assert.NotEqual(build, again with { PowerSets = [] });
        Assert.NotEqual(build.PowerEntries[0], again.PowerEntries[1]);
        Assert.False(build.Equals(null));
        Assert.False(build.PowerEntries[0].Equals(null));
    }

    [Fact]
    public void A_placeholder_entry_and_a_null_slot_survive_the_round_trip()
    {
        MbdPowerEntryDoc named = new()
        {
            PowerName = "A.B.C",
            Level = 3,
            SubPowerEntries = [new MbdSubPowerDoc { PowerName = "A.B.D", StatInclude = true }],
            SlotEntries = [new MbdSlotDoc { Level = 3, IsInherent = true }],
        };
        MbdBuild build = Mbd.ParseMbd(Document(Entry(string.Empty, 7), named));

        Assert.Equal(build, Mbd.ParseMbd(Mbd.DumpMbd(build)));
        Assert.Equal(7, Mbd.DumpMbd(build).PowerEntries[0].Level);
    }
}
