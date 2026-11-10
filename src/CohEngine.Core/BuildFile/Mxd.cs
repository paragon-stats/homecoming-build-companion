using System.Globalization;
using System.IO.Compression;

using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.BuildFile;

/// <summary>The reader for the legacy <c>.mxd</c> build: the share-text envelope, the zlib payload and the binary stream.</summary>
public static class Mxd
{
    internal static readonly byte[] Magic = [0x4D, 0x78, 0x44, 0x0C];

    private static readonly double PriorVersion = F32.Round(3.10);
    private static readonly double ThisVersion = F32.Round(3.20);

    private static readonly Dictionary<string, string> PowersetMigrations = new(StringComparer.Ordinal)
    {
        ["Pool.Leadership_beta"] = "Pool.Leadership",
        ["Blaster_Support.Atomic_Manipulation"] = "Blaster_Support.Radiation_Manipulation",
        ["Pool.Fitness"] = "Pool.Invisibility",
    };

    /// <summary>Classifies a share block, tolerating a leading banner.</summary>
    /// <param name="text">The file text.</param>
    /// <returns><c>mxd</c>, <c>mbd</c>, <c>unkbase64</c>, or null.</returns>
    public static string? Classify(string text)
    {
        foreach (string line in Lines(text))
        {
            if (IsHeader(line, "MxDz", "HEX"))
            {
                return "mxd";
            }

            if (IsHeader(line, "MBD", "BASE64"))
            {
                return "mbd";
            }
        }

        string stripped = text.Trim();
        return stripped.Length > 0 && !stripped.Contains('\n', StringComparison.Ordinal) && IsUnkBase64(stripped) ? "unkbase64" : null;
    }

    /// <summary>Parses a share-text envelope.</summary>
    /// <param name="text">The file text.</param>
    /// <returns>The header sizes and the joined payload.</returns>
    /// <exception cref="InvalidDataException">E112: no header, no payload line, not five items, or non-integer sizes.</exception>
    public static ShareBlock DecodeShareBlock(string text)
    {
        string[] lines = Lines(text);
        int headerIndex = Array.FindIndex(lines, line => line.StartsWith("|MxDz;", StringComparison.Ordinal) || line.StartsWith("|MBD;", StringComparison.Ordinal));
        if (headerIndex < 0)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.ShareEnvelope}: no |MxDz;…| or |MBD;…| header line found; copy the whole share block, header line included");
        }

        if (headerIndex == lines.Length - 1)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.ShareEnvelope}: share block needs a header line and at least one payload line; copy the payload lines under the header");
        }

        string[] items = lines[headerIndex].Trim('|').Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (items.Length != 5)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.ShareEnvelope}: share header must have exactly 5 items, got {items.Length}; copy the header line unchanged from the Mids export");
        }

        if (!int.TryParse(items[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int uncompressed)
            || !int.TryParse(items[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int compressed)
            || !int.TryParse(items[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int encoded))
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.ShareEnvelope}: share header sizes must be integers; copy the header line unchanged from the Mids export");
        }

        List<string> parts = [];
        foreach (string line in lines.Skip(headerIndex + 1))
        {
            string token = line.Replace("|", string.Empty, StringComparison.Ordinal).Trim();
            if (token.Length == 0)
            {
                continue;
            }

            if (!token.All(symbol => IsBase64Symbol(symbol) || symbol == '='))
            {
                break;
            }

            parts.Add(token);
        }

        return new ShareBlock(items[0], uncompressed, compressed, encoded, items[4], string.Concat(parts));
    }

    /// <summary>Decodes a hex string to bytes.</summary>
    /// <param name="data">Upper or lower case hex.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidDataException">E113: not valid hex.</exception>
    public static byte[] HexDecode(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || data.Length % 2 != 0 || !data.All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.SharePayload}: mxd payload is not valid hex; copy the payload lines unchanged from the Mids export");
        }

        return Convert.FromHexString(data);
    }

    /// <summary>Decodes a <c>.mxd</c> share block to its decompressed binary buffer.</summary>
    /// <param name="text">The file text.</param>
    /// <returns>The buffer, cut to the declared uncompressed size.</returns>
    /// <exception cref="InvalidDataException">E113: wrong format, bad hex, a compressed-size mismatch or a zlib failure.</exception>
    public static byte[] DecodeMxd(string text)
    {
        if (!string.Equals(Classify(text), "mxd", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.SharePayload}: not an mxd share block; pass a MxDz block, not another format");
        }

        ShareBlock block = DecodeShareBlock(text);
        byte[] raw = HexDecode(block.Payload);
        if (raw.Length != block.CompressedSize)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.SharePayload}: mxd compressed size mismatch: header {block.CompressedSize}, actual {raw.Length}; the payload is truncated or edited, re-export it from Mids");
        }

        byte[] buffer = Inflate(raw);

        return buffer[..Math.Min(block.UncompressedSize, buffer.Length)];
    }

    /// <summary>Parses a decompressed <c>.mxd</c> binary buffer.</summary>
    /// <param name="buffer">The decompressed buffer.</param>
    /// <param name="powerIndex">StaticIndex to power full name.</param>
    /// <param name="enhIndex">StaticIndex to enhancement identity.</param>
    /// <param name="archetypes">When given, an unknown class UID is rejected.</param>
    /// <returns>The build.</returns>
    /// <exception cref="InvalidDataException">E114 magic absent, E115 version too new, E116 qualified names or invalid class, E117 corrupt ordinal.</exception>
    public static MxdBuild ReadMxdBuild(
        byte[] buffer,
        IReadOnlyDictionary<int, string> powerIndex,
        IReadOnlyDictionary<int, EnhIndexEntry> enhIndex,
        ArchetypeDb? archetypes = null)
    {
        ArgumentNullException.ThrowIfNull(powerIndex);
        BinaryCursor cursor = new(buffer, ScanMagic(buffer));
        double version = cursor.ReadSingle();
        string format = FormatForVersion(version);

        if (cursor.ReadBoolean())
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.Unsupported}: qualified-names .mxd builds are not supported; re-save the build from Mids as a .mbd");
        }

        bool hasSubPower = cursor.ReadBoolean();
        string className = cursor.ReadString();
        if (archetypes is not null && archetypes.NidFromUidClass(className) < 0)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.Unsupported}: invalid class UID: '{className}'; re-dump the Mids data or check the build's class");
        }

        string originUid = cursor.ReadString();
        int alignment = version > 1 ? cursor.ReadInt32() : 0;
        string name = cursor.ReadString();

        List<string> powerSets = [];
        for (long count = cursor.ReadArrayCount(); count > 0; count--)
        {
            string powerSet = cursor.ReadString();
            powerSets.Add(PowersetMigrations.GetValueOrDefault(powerSet, powerSet));
        }

        int lastPower = cursor.ReadInt32() - 1;

        List<BuildPowerEntry> entries = [];
        long powerCount = cursor.ReadArrayCount();
        for (long index = 0; index < powerCount; index++)
        {
            entries.Add(ReadPowerEntry(cursor, powerIndex, enhIndex, format, version, hasSubPower, index));
        }

        return new MxdBuild(format, className, originUid, alignment, name, powerSets, lastPower, entries);
    }

    /// <summary>Decodes a <c>.mxd</c> share block and parses it.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="powerIndex">StaticIndex to power full name.</param>
    /// <param name="enhIndex">StaticIndex to enhancement identity.</param>
    /// <param name="archetypes">When given, an unknown class UID is rejected.</param>
    /// <returns>The build.</returns>
    public static MxdBuild ReadMxd(
        string text,
        IReadOnlyDictionary<int, string> powerIndex,
        IReadOnlyDictionary<int, EnhIndexEntry> enhIndex,
        ArchetypeDb? archetypes = null)
    {
        return ReadMxdBuild(DecodeMxd(text), powerIndex, enhIndex, archetypes);
    }

    internal static string FormatForVersion(double version)
    {
        if (version > ThisVersion)
        {
            throw new InvalidDataException(FormattableString.Invariant($"{BuildFileErrorCodes.VersionTooNew}: mxd saved by a newer version ({version}); cannot read; update this tool or re-save the build from an older Mids"));
        }

        if (version < PriorVersion)
        {
            return "legacy";
        }

        return version < ThisVersion ? "prior" : "current";
    }

    internal static int ScanMagic(byte[] buffer)
    {
        int position = buffer.AsSpan().IndexOf(Magic);
        if (position < 0)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.MagicMissing}: mxd magic number MxD\\x0c not found; the file is not a decoded .mxd, re-export it from Mids");
        }

        return position + Magic.Length;
    }

    internal static SlottedEnhancement? ReadSlotData(BinaryCursor cursor, IReadOnlyDictionary<int, EnhIndexEntry> enhIndex, double version)
    {
        if (!enhIndex.TryGetValue(cursor.ReadInt32(), out EnhIndexEntry? entry))
        {
            return null;
        }

        if (entry.TypeId is "Normal" or "SpecialO")
        {
            string relative = EnumName(Mbd.EnhRelative, cursor.ReadSByte(), "relative level");
            string grade = EnumName(Mbd.EnhGrade, cursor.ReadSByte(), "grade");
            return new SlottedEnhancement(entry.Uid, grade, 1, relative);
        }

        if (entry.TypeId is "InventO" or "SetO")
        {
            int ioLevel = Math.Min((int)cursor.ReadSByte(), 49);
            string relative = version > 1.0 ? EnumName(Mbd.EnhRelative, cursor.ReadSByte(), "relative level") : "Even";
            return new SlottedEnhancement(entry.Uid, "None", ioLevel, relative);
        }

        return new SlottedEnhancement(entry.Uid);
    }

    internal static string MigratePowerName(string name, long index)
    {
        if (string.Equals(name, "Pool.Flight.Afterburner", StringComparison.Ordinal))
        {
            return "Pool.Flight.Evasive_Maneuvers";
        }

        return string.Equals(name, "Inherent.Inherent.Afterburner", StringComparison.Ordinal) && index < 24 ? "Pool.Flight.Evasive_Maneuvers" : name;
    }

    private static BuildPowerEntry ReadPowerEntry(
        BinaryCursor cursor,
        IReadOnlyDictionary<int, string> powerIndex,
        IReadOnlyDictionary<int, EnhIndexEntry> enhIndex,
        string format,
        double version,
        bool hasSubPower,
        long index)
    {
        int staticIndex = cursor.ReadInt32();
        string powerName = MigratePowerName(powerIndex.GetValueOrDefault(staticIndex, string.Empty), index);
        int level = 0;
        bool statInclude = false;
        bool procInclude = false;
        int variableValue = 0;
        int inherentSlotsUsed = 0;
        List<BuildSubPower> subPowers = [];
        if (staticIndex > -1 || powerName.Length > 0)
        {
            level = cursor.ReadSByte();
            statInclude = cursor.ReadBoolean();
            if (format is "current" or "prior")
            {
                procInclude = cursor.ReadBoolean();
            }

            variableValue = cursor.ReadInt32();
            if (string.Equals(format, "current", StringComparison.Ordinal))
            {
                inherentSlotsUsed = cursor.ReadInt32();
            }

            if (hasSubPower)
            {
                for (int subCount = cursor.ReadSByte() + 1; subCount > 0; subCount--)
                {
                    string subName = powerIndex.GetValueOrDefault(cursor.ReadInt32(), string.Empty);
                    subPowers.Add(new BuildSubPower(subName, cursor.ReadBoolean()));
                }
            }
        }

        List<BuildSlot> slots = [];
        for (int slotCount = cursor.ReadSByte() + 1; slotCount > 0; slotCount--)
        {
            int slotLevel = cursor.ReadSByte();
            bool isInherent = string.Equals(format, "current", StringComparison.Ordinal) && cursor.ReadBoolean();
            SlottedEnhancement? enhancement = ReadSlotData(cursor, enhIndex, version);
            SlottedEnhancement? flipped = cursor.ReadBoolean() ? ReadSlotData(cursor, enhIndex, version) : null;
            slots.Add(new BuildSlot(slotLevel, isInherent, enhancement, flipped));
        }

        return new BuildPowerEntry(powerName, level, statInclude, procInclude, variableValue, inherentSlotsUsed, subPowers, slots);
    }

    private static string EnumName(IReadOnlyList<string> table, int ordinal, string kind)
    {
        if (ordinal >= 0 && ordinal < table.Count)
        {
            return table[ordinal];
        }

        throw new InvalidDataException($"{BuildFileErrorCodes.EnumOutOfRange}: corrupt .mxd: {kind} ordinal {ordinal} out of range; the file is corrupt, re-export it from Mids");
    }

    private static byte[] Inflate(byte[] raw)
    {
        try
        {
            using MemoryStream source = new(raw);
            using ZLibStream zlib = new(source, CompressionMode.Decompress);
            using MemoryStream output = new();
            zlib.CopyTo(output);
            byte[] inflated = output.ToArray();

            // ZLibStream accepts a truncated stream and ignores the trailer; Python's zlib.decompress does neither,
            // and accepts bytes after the trailer. The Adler-32 of the inflated bytes must follow the deflate data.
            if (!HasTrailer(raw, Adler32(inflated)))
            {
                throw new InvalidDataException("zlib stream is truncated or fails its checksum");
            }

            return inflated;
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.SharePayload}: mxd zlib inflate failed; the payload is truncated or corrupt, re-export it from Mids", exception);
        }
    }

    private static uint Adler32(byte[] data)
    {
        uint low = 1;
        uint high = 0;
        foreach (byte value in data)
        {
            low = (low + value) % 65521;
            high = (high + low) % 65521;
        }

        return (high << 16) | low;
    }

    private static bool HasTrailer(byte[] raw, uint checksum)
    {
        byte[] trailer = [(byte)(checksum >> 24), (byte)(checksum >> 16), (byte)(checksum >> 8), (byte)checksum];
        return raw.Length > 2 && raw.AsSpan(2).IndexOf(trailer) >= 0;
    }

    // `||` marks a line break in single-line files.
    private static string[] Lines(string text)
    {
        return text.Replace("||", "|\n|", StringComparison.Ordinal).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsHeader(string line, string magic, string encoding)
    {
        string prefix = $"|{magic};";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> rest = line.AsSpan(prefix.Length);
        for (int size = 0; size < 3; size++)
        {
            int digits = rest.IndexOfAnyExceptInRange('0', '9');
            if (digits <= 0 || rest[digits] != ';')
            {
                return false;
            }

            rest = rest[(digits + 1)..];
        }

        return rest.StartsWith($"{encoding};|", StringComparison.Ordinal);
    }

    private static bool IsBase64Symbol(char symbol)
    {
        return char.IsAsciiLetterOrDigit(symbol) || symbol is '+' or '/';
    }

    private static bool IsUnkBase64(string text)
    {
        string body = text.TrimEnd('=');
        return body.Length > 0 && text.Length - body.Length <= 2 && body.All(IsBase64Symbol);
    }
}
