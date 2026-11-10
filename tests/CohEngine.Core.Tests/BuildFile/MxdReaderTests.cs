using System.Buffers.Binary;
using System.Text;

using CohEngine.Core.BuildFile;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>Port of <c>test_mxd_reader.py</c>: the format variants and slot paths on synthetic buffers.</summary>
public sealed class MxdReaderTests
{
    private static readonly Dictionary<int, string> PowerIndex = new() { [100] = "Test.Set.Power", [101] = "Test.Set.SubPower" };

    private static readonly Dictionary<int, EnhIndexEntry> EnhIndex = new()
    {
        [5] = new("Enh_Normal", "Normal"),
        [6] = new("Enh_Set", "SetO"),
        [7] = new("Enh_None", "None"),
        [8] = new("Enh_Special", "SpecialO"),
    };

    private static Buf Header(double version, bool qualified, bool hasSub)
    {
        Buf buf = new Buf().Raw(Mxd.Magic).Single(version).Boolean(qualified).Boolean(hasSub).Text("Class_Scrapper").Text("Magic");
        if (version > 1)
        {
            buf.Int32(0);
        }

        return buf.Text("Synthetic").Int32(0).Text("Test.Set").Int32(0);
    }

    private static MxdBuild Read(Buf buf, ArchetypeDb? archetypes = null)
    {
        return Mxd.ReadMxdBuild(buf.ToArray(), PowerIndex, EnhIndex, archetypes);
    }

    [Fact]
    public void Format_follows_the_version()
    {
        Assert.Equal("legacy", Mxd.FormatForVersion(F32.Round(3.0)));
        Assert.Equal("prior", Mxd.FormatForVersion(F32.Round(3.15)));
        Assert.Equal("current", Mxd.FormatForVersion(F32.Round(3.20)));
        Assert.StartsWith("E115:", Assert.Throws<InvalidDataException>(() => Mxd.FormatForVersion(F32.Round(9.0))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scanning_skips_leading_garbage()
    {
        Assert.Equal(6, Mxd.ScanMagic([0, 1, .. Mxd.Magic, 9]));
        Assert.StartsWith("E114:", Assert.Throws<InvalidDataException>(() => Mxd.ScanMagic(new byte[10])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_magic_is_E114()
    {
        Assert.StartsWith("E114:", Assert.Throws<InvalidDataException>(() => Mxd.ReadMxdBuild(new byte[8], PowerIndex, EnhIndex)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_newer_version_is_E115()
    {
        byte[] buffer = [.. Mxd.Magic, .. new Buf().Single(9.99).ToArray()];

        Assert.StartsWith("E115:", Assert.Throws<InvalidDataException>(() => Mxd.ReadMxdBuild(buffer, PowerIndex, EnhIndex)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5, "Normal", "Enh_Normal", "PlusOne")]
    [InlineData(8, "SpecialO", "Enh_Special", "PlusOne")]
    [InlineData(7, "None", "Enh_None", "Even")]
    public void Slot_data_follows_the_enhancement_type(int staticIndex, string typeId, string uid, string relative)
    {
        Buf buf = new Buf().Int32(staticIndex);
        if (!string.Equals(typeId, "None", StringComparison.Ordinal))
        {
            buf.SByte(5).SByte(1);
        }

        SlottedEnhancement? enhancement = Mxd.ReadSlotData(new BinaryCursor(buf.ToArray()), EnhIndex, F32.Round(3.20));

        Assert.Equal(uid, enhancement?.Uid);
        Assert.Equal(relative, enhancement?.RelativeLevel);
    }

    [Fact]
    public void A_set_piece_clamps_its_io_level()
    {
        SlottedEnhancement? enhancement = Mxd.ReadSlotData(new BinaryCursor(new Buf().Int32(6).SByte(53).SByte(9).ToArray()), EnhIndex, F32.Round(3.20));

        Assert.Equal(new SlottedEnhancement("Enh_Set", "None", 49, "PlusFive"), enhancement);
    }

    [Fact]
    public void An_empty_slot_reference_is_null()
    {
        Assert.Null(Mxd.ReadSlotData(new BinaryCursor(new Buf().Int32(-1).ToArray()), EnhIndex, F32.Round(3.20)));
    }

    [Fact]
    public void An_old_version_stores_no_relative_level_after_a_set_piece()
    {
        SlottedEnhancement? enhancement = Mxd.ReadSlotData(new BinaryCursor(new Buf().Int32(6).SByte(40).ToArray()), EnhIndex, 1.0);

        Assert.Equal(new SlottedEnhancement("Enh_Set", "None", 40, "Even"), enhancement);
    }

    [Fact]
    public void The_current_format_reads_an_empty_entry_and_a_subpower()
    {
        Buf buf = Header(F32.Round(3.20), qualified: false, hasSub: true).Int32(1);
        buf.Int32(-1).SByte(-1);
        buf.Int32(100).SByte(5).Boolean(true).Boolean(false).Int32(2).Int32(1);
        buf.SByte(0).Int32(101).Boolean(true);
        buf.SByte(0);
        buf.SByte(5).Boolean(false).Int32(5).SByte(5).SByte(0).Boolean(false);

        MxdBuild build = Read(buf);

        BuildPowerEntry power = Assert.Single(build.PowerEntries, entry => entry.PowerName.Length > 0);
        Assert.Equal("current", build.Format);
        Assert.Equal(2, build.PowerEntries.Count);
        Assert.Equal(("Test.Set.Power", 5, 2, 1), (power.PowerName, power.Level, power.VariableValue, power.InherentSlotsUsed));
        Assert.Equal(new BuildSubPower("Test.Set.SubPower", true), Assert.Single(power.SubPowerEntries));
        Assert.Equal("Enh_Normal", power.SlotEntries[0].Enhancement?.Uid);
    }

    [Fact]
    public void A_flipped_enhancement_follows_its_flag()
    {
        Buf buf = Header(F32.Round(3.20), qualified: false, hasSub: false).Int32(0);
        buf.Int32(100).SByte(5).Boolean(true).Boolean(false).Int32(0).Int32(0);
        buf.SByte(0).SByte(5).Boolean(true).Int32(7).Boolean(true).Int32(7);

        BuildSlot slot = Assert.Single(Assert.Single(Read(buf).PowerEntries).SlotEntries);

        Assert.True(slot.IsInherent);
        Assert.Equal("Enh_None", slot.Enhancement?.Uid);
        Assert.Equal("Enh_None", slot.FlippedEnhancement?.Uid);
    }

    [Fact]
    public void The_prior_format_reads_proc_but_not_inherent_slots()
    {
        Buf buf = Header(F32.Round(3.15), qualified: false, hasSub: false).Int32(0);
        buf.Int32(100).SByte(3).Boolean(true).Boolean(true).Int32(0).SByte(-1);

        MxdBuild build = Read(buf);

        Assert.Equal("prior", build.Format);
        Assert.True(build.PowerEntries[0].ProcInclude);
        Assert.Equal(0, build.PowerEntries[0].InherentSlotsUsed);
    }

    [Fact]
    public void The_legacy_format_reads_no_proc_and_no_inherent()
    {
        Buf buf = Header(F32.Round(3.0), qualified: false, hasSub: false).Int32(0);
        buf.Int32(100).SByte(2).Boolean(true).Int32(0).SByte(-1);

        MxdBuild build = Read(buf);

        Assert.Equal("legacy", build.Format);
        Assert.False(build.PowerEntries[0].ProcInclude);
        Assert.True(build.PowerEntries[0].StatInclude);
    }

    [Fact]
    public void A_version_of_one_has_no_alignment()
    {
        Buf buf = new Buf().Raw(Mxd.Magic).Single(1.0).Boolean(false).Boolean(false).Text("Class_Scrapper").Text("Magic").Text("Synthetic");
        buf.Int32(0).Text("Pool.Fitness").Int32(1).Int32(-1);

        MxdBuild build = Read(buf);

        Assert.Equal(0, build.Alignment);
        Assert.Equal(["Pool.Invisibility"], build.PowerSets);
        Assert.Equal(0, build.LastPower);
    }

    [Fact]
    public void Qualified_names_are_E116()
    {
        Buf buf = Header(F32.Round(3.20), qualified: true, hasSub: false);

        Assert.StartsWith("E116:", Assert.Throws<InvalidDataException>(() => Read(buf)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void An_out_of_range_relative_level_is_E117(int badByte)
    {
        BinaryCursor cursor = new(new Buf().Int32(5).SByte((sbyte)badByte).SByte(0).ToArray());

        Assert.Contains("relative level ordinal", Assert.Throws<InvalidDataException>(() => Mxd.ReadSlotData(cursor, EnhIndex, F32.Round(3.20))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_out_of_range_grade_is_E117()
    {
        BinaryCursor cursor = new(new Buf().Int32(5).SByte(4).SByte(9).ToArray());

        Assert.Contains("grade ordinal", Assert.Throws<InvalidDataException>(() => Mxd.ReadSlotData(cursor, EnhIndex, F32.Round(3.20))).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Pool.Flight.Afterburner", 5, "Pool.Flight.Evasive_Maneuvers")]
    [InlineData("Inherent.Inherent.Afterburner", 10, "Pool.Flight.Evasive_Maneuvers")]
    [InlineData("Inherent.Inherent.Afterburner", 30, "Inherent.Inherent.Afterburner")]
    [InlineData("Pool.Flight.Fly", 0, "Pool.Flight.Fly")]
    public void The_afterburner_migration(string name, int index, string expected)
    {
        Assert.Equal(expected, Mxd.MigratePowerName(name, index));
    }

    [Fact]
    public void A_known_class_is_accepted()
    {
        Buf buf = Header(F32.Round(3.20), qualified: false, hasSub: false).Int32(-1);

        Assert.Equal("Class_Scrapper", Read(buf, BuildFileFixtures.Archetypes).ClassName);
    }

    [Fact]
    public void An_unknown_class_is_E116()
    {
        Buf buf = Header(F32.Round(3.20), qualified: false, hasSub: false).Int32(-1);

        Assert.Contains("invalid class UID", Assert.Throws<InvalidDataException>(() => Read(buf, new ArchetypeDb([]))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadMxd_decodes_then_parses()
    {
        Buf buf = Header(F32.Round(3.20), qualified: false, hasSub: false).Int32(-1);

        Assert.Equal("Synthetic", Mxd.ReadMxd(MxdTests.MakeShare(buf.ToArray()), PowerIndex, EnhIndex).Name);
    }

    private sealed class Buf
    {
        private readonly List<byte> bytes = [];

        public byte[] ToArray()
        {
            return [.. bytes];
        }

        public Buf Raw(byte[] data)
        {
            bytes.AddRange(data);
            return this;
        }

        public Buf Int32(int value)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(data, value);
            return Raw(data);
        }

        public Buf Single(double value)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(data, (float)value);
            return Raw(data);
        }

        public Buf Boolean(bool value)
        {
            bytes.Add(value ? (byte)1 : (byte)0);
            return this;
        }

        public Buf SByte(sbyte value)
        {
            bytes.Add((byte)value);
            return this;
        }

        public Buf Text(string value)
        {
            byte[] raw = Encoding.UTF8.GetBytes(value);
            bytes.Add((byte)raw.Length);
            return Raw(raw);
        }
    }
}
