using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Data;

namespace CohEngine.Core.BuildFile;

/// <summary>
/// The <c>.mbd</c> reader, writer and the resolution of a build against the Mids dump.
/// A power's and a slot's stored <c>Level</c> is one more than the model's (character level = level + 1),
/// <c>LastPower</c> is assigned raw, and the header <c>Level</c> is the character level minus one as text.
/// A <c>.mbd</c> has no <c>Chosen</c> flag, so every entry resolves with <c>Chosen</c> true: a pool sub-power Mids grants
/// without a pick (Translocation from Mystic Flight, Double Jump) counts as one of the 24 picks, and a pick-count check
/// can over-count a build that holds one. The granted powers are listed in <c>docs/slotting-and-leveling.md</c>.
/// </summary>
public static partial class Mbd
{
    /// <summary>Gets the eEnhRelative ordinals as the <c>.mbd</c> and <c>.mxd</c> name them.</summary>
    public static IReadOnlyList<string> EnhRelative { get; } =
    [
        "None",
        "MinusThree",
        "MinusTwo",
        "MinusOne",
        "Even",
        "PlusOne",
        "PlusTwo",
        "PlusThree",
        "PlusFour",
        "PlusFive",
    ];

    /// <summary>Gets the eEnhGrade ordinals as the <c>.mbd</c> and <c>.mxd</c> name them.</summary>
    public static IReadOnlyList<string> EnhGrade { get; } = ["None", "TrainingO", "DualO", "SingleO"];

    /// <summary>Builds an <see cref="MbdBuild"/> from a decoded <c>.mbd</c> document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The build.</returns>
    /// <exception cref="InvalidDataException">E118: the <c>BuiltWith</c> block is missing or the <c>Level</c> is not an integer.</exception>
    public static MbdBuild ParseMbd(MbdDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        MbdBuiltWithDoc meta = document.BuiltWith
            ?? throw new InvalidDataException($"{BuildFileErrorCodes.DocumentUnreadable}: mbd build is missing the required BuiltWith block");
        string levelText = Need(document.Level, "Level");
        if (!int.TryParse(levelText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.DocumentUnreadable}: mbd Level '{levelText}' is not an integer");
        }

        return new MbdBuild(
            new BuildMetaData(meta.App, meta.Version, meta.Database, meta.DatabaseVersion),
            level,
            Need(document.Class, "Class"),
            document.Origin,
            document.Alignment,
            document.Name,
            document.Comment,
            document.PowerSets,
            document.LastPower,
            [.. document.PowerEntries.Select(ParsePowerEntry)]);
    }

    /// <summary>Reads and parses a <c>.mbd</c> file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The build.</returns>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="InvalidDataException">E118: the JSON is invalid, a required key is missing, or the <c>BuiltWith</c> block is missing.</exception>
    public static MbdBuild LoadMbd(string path)
    {
        return ParseMbd(ReadDocument(path));
    }

    /// <summary>Reads a <c>.mbd</c> file as its JSON document.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The document.</returns>
    /// <exception cref="InvalidDataException">E118: the JSON is invalid or a required key is missing.</exception>
    public static MbdDocument ReadDocument(string path)
    {
        try
        {
            return Json.Load(path, BuildFileJsonContext.Default.MbdDocument);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.DocumentUnreadable}: {path} is not a readable .mbd: {exception.Message}", exception);
        }
    }

    /// <summary>The <c>.mbd</c> document for a parsed build: the inverse of <see cref="ParseMbd"/>.</summary>
    /// <param name="build">The build.</param>
    /// <returns>The document.</returns>
    public static MbdDocument DumpMbd(MbdBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return new MbdDocument
        {
            BuiltWith = new MbdBuiltWithDoc
            {
                App = build.Meta.App,
                Version = build.Meta.Version,
                Database = build.Meta.Database,
                DatabaseVersion = build.Meta.DatabaseVersion,
            },
            Level = build.Level.ToString(CultureInfo.InvariantCulture),
            Class = build.ClassName,
            Origin = build.Origin,
            Alignment = build.Alignment,
            Name = build.Name,
            Comment = build.Comment,
            PowerSets = build.PowerSets,
            LastPower = build.LastPower,
            PowerEntries = [.. build.PowerEntries.Select(DumpEntry)],
        };
    }

    /// <summary>Reads the reference tables a <c>.mbd</c> is checked and resolved against.</summary>
    /// <param name="midsDir">The Mids dump directory.</param>
    /// <returns>The bundle.</returns>
    /// <exception cref="FileNotFoundException">One of the four files is missing.</exception>
    /// <exception cref="InvalidDataException">E118: a file lacks a field; re-dump with the current harness.</exception>
    public static MbdDb LoadMbdDb(string midsDir)
    {
        try
        {
            return new MbdDb(
                Json.Load(Path.Combine(midsDir, "enhancements.json"), BuildFileJsonContext.Default.IReadOnlyListEnhancementDbRecord)
                    .ToLastWinsDictionary(record => record.UID, record => new EnhInfo(record.Nid, record.TypeName, record.SetNid, record.LevelMin, record.LevelMax)),
                Json.Load(Path.Combine(midsDir, "enhancement_sets.json"), BuildFileJsonContext.Default.IReadOnlyListEnhancementSetRecord)
                    .ToLastWinsDictionary(record => record.Nid, record => (record.LevelMin, record.LevelMax)),
                ArchetypeData.LoadArchetypes(Path.Combine(midsDir, "archetypes.json")),
                Json.Load(Path.Combine(midsDir, "powers.json"), BuildFileJsonContext.Default.IReadOnlyListPowerDbRecord)
                    .ToLastWinsDictionary(record => record.FullName, ToTemplate));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.DocumentUnreadable}: a Mids dump file lacks a field; re-dump with the current harness: {exception.Message}", exception);
        }
    }

    /// <summary>The 0-based <c>IoLevel</c> range MidsReborn keeps for an enhancement.</summary>
    /// <param name="uid">The enhancement UID.</param>
    /// <param name="database">The reference tables.</param>
    /// <returns>The bounds, or null for a UID not in the table.</returns>
    public static (int Min, int Max)? IoLevelBounds(string uid, MbdDb database)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (!database.Enhancements.TryGetValue(uid, out EnhInfo? info))
        {
            return null;
        }

        return info.TypeName switch
        {
            "InventO" => (info.LevelMin, info.LevelMax),
            "SetO" => database.SetBounds.TryGetValue(info.SetNid, out (int Min, int Max) bounds) ? bounds : (9, 52),
            _ => (1, 1),
        };
    }

    /// <summary>The IoLevel bounds of every enhancement, keyed by nID.</summary>
    /// <param name="database">The reference tables.</param>
    /// <returns>The bounds by nID; a repeated nID keeps the last.</returns>
    public static IReadOnlyDictionary<int, (int Min, int Max)> IoBoundsByNid(MbdDb database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.Enhancements
            .Select(pair => (pair.Value.Nid, Bounds: IoLevelBounds(pair.Key, database)))
            .Where(pair => pair.Bounds is not null)
            .ToLastWinsDictionary(pair => pair.Nid, pair => pair.Bounds!.Value);
    }

    /// <summary>Each named entry of a build as a <see cref="Power"/>, its <c>BuildIndex</c> the entry's position.</summary>
    /// <param name="build">The build.</param>
    /// <param name="database">The reference tables.</param>
    /// <returns>The powers in entry order.</returns>
    /// <exception cref="InvalidDataException">E77: a power name the powers table lacks; re-dump the Mids data.</exception>
    public static IReadOnlyList<Power> PowersFromBuild(MbdBuild build, MbdDb database)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(database);
        List<Power> powers = [];
        for (int position = 0; position < build.PowerEntries.Count; position++)
        {
            BuildPowerEntry entry = build.PowerEntries[position];
            if (entry.PowerName.Length == 0)
            {
                continue;
            }

            if (!database.Powers.TryGetValue(entry.PowerName, out PowerTemplate? template))
            {
                throw new InvalidDataException($"E77: entry {position + 1} names {entry.PowerName}, which powers.json lacks; re-dump the Mids data or replace the entry");
            }

            powers.Add(new Power(template.RequiresPowers, template.RequiresPowersNot)
            {
                BuildIndex = position,
                NidPower = template.Nid,
                FullName = template.FullName,
                StaticIndex = template.StaticIndex,
                PowerType = template.PowerType,
                ForcedClass = string.Empty,
                ClickBuff = false,
                Level = template.Level,
                EndCost = 0.0,
                ActivatePeriod = 0.0,
                ToggleCost = 0.0,
                VariableEnabled = false,
                StatInclude = entry.StatInclude,
                VariableValue = entry.VariableValue,
                Effects = [],
                SetTypes = template.SetTypes,
                Enhancements = template.Enhancements,
                PickLevel = entry.Level,
                RechargeTime = 0.0,
                CastTime = 0.0,
                InterruptTime = 0.0,
                IgnoreEnh = [],
            });
        }

        return powers;
    }

    /// <summary>Each named entry's slots as <see cref="SlotRef"/>, keyed by the entry's position (its <c>BuildIndex</c>).</summary>
    /// <param name="build">The build.</param>
    /// <param name="database">The reference tables.</param>
    /// <returns>The slots by entry position.</returns>
    /// <exception cref="InvalidDataException">E78: an enhancement the table lacks; E117: a grade or relative level that is not a known name.</exception>
    public static IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> SlotsFromBuild(MbdBuild build, MbdDb database)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(database);
        Dictionary<int, IReadOnlyList<SlotRef>> slots = [];
        for (int position = 0; position < build.PowerEntries.Count; position++)
        {
            BuildPowerEntry entry = build.PowerEntries[position];
            if (entry.PowerName.Length > 0)
            {
                slots[position] = [.. entry.SlotEntries.Select(slot => ToSlotRef(slot, entry, database))];
            }
        }

        return slots;
    }

    private static BuildPowerEntry ParsePowerEntry(MbdPowerEntryDoc entry)
    {
        string name = Need(entry.PowerName, "PowerName");
        int level = Need(entry.Level, "Level");
        return new BuildPowerEntry(
            name,
            name.Length > 0 ? level - 1 : level,
            entry.StatInclude,
            entry.ProcInclude,
            entry.VariableValue,
            entry.InherentSlotsUsed,
            [.. entry.SubPowerEntries.Select(sub => new BuildSubPower(Need(sub.PowerName, "PowerName"), sub.StatInclude))],
            [.. entry.SlotEntries.Select(slot => new BuildSlot(Need(slot.Level, "Level") - 1, slot.IsInherent, ParseEnhancement(slot.Enhancement), ParseEnhancement(slot.FlippedEnhancement)))]);
    }

    private static T Need<T>(T? value, string key)
        where T : class
    {
        return value ?? throw new InvalidDataException($"{BuildFileErrorCodes.DocumentUnreadable}: a required key '{key}' is missing or null");
    }

    private static T Need<T>(T? value, string key)
        where T : struct
    {
        return value ?? throw new InvalidDataException($"{BuildFileErrorCodes.DocumentUnreadable}: a required key '{key}' is missing or null");
    }

    private static SlottedEnhancement? ParseEnhancement(MbdEnhancementDoc? enhancement)
    {
        string? uid = enhancement?.Uid ?? enhancement?.Enhancement;
        return enhancement is null || uid is null
            ? null
            : new SlottedEnhancement(uid, enhancement.Grade, enhancement.IoLevel, enhancement.RelativeLevel, enhancement.Obtained);
    }

    private static MbdPowerEntryDoc DumpEntry(BuildPowerEntry entry)
    {
        return new MbdPowerEntryDoc
        {
            PowerName = entry.PowerName,
            Level = entry.PowerName.Length > 0 ? entry.Level + 1 : entry.Level,
            StatInclude = entry.StatInclude,
            ProcInclude = entry.ProcInclude,
            VariableValue = entry.VariableValue,
            InherentSlotsUsed = entry.InherentSlotsUsed,
            SubPowerEntries = [.. entry.SubPowerEntries.Select(sub => new MbdSubPowerDoc { PowerName = sub.PowerName, StatInclude = sub.StatInclude })],
            SlotEntries =
            [
                .. entry.SlotEntries.Select(slot => new MbdSlotDoc
                {
                    Level = slot.Level + 1,
                    IsInherent = slot.IsInherent,
                    Enhancement = DumpEnhancement(slot.Enhancement),
                    FlippedEnhancement = DumpEnhancement(slot.FlippedEnhancement),
                }),
            ],
        };
    }

    private static MbdEnhancementDoc? DumpEnhancement(SlottedEnhancement? enhancement)
    {
        return enhancement is null
            ? null
            : new MbdEnhancementDoc
            {
                Uid = enhancement.Uid,
                Grade = enhancement.Grade,
                IoLevel = enhancement.IoLevel,
                RelativeLevel = enhancement.RelativeLevel,
                Obtained = enhancement.Obtained,
            };
    }

    private static PowerTemplate ToTemplate(PowerDbRecord record)
    {
        return new PowerTemplate(
            record.FullName,
            record.NIDPower,
            record.StaticIndex,
            record.PowerType,
            record.Level,
            record.SetTypes,
            record.Enhancements,
            RequirementGroups(record.RequiresPowers),
            RequirementGroups(record.RequiresPowersNot),
            record.RequiresClass,
            record.RequiresClassNot,
            record.PowerSetType);
    }

    // A Requires OR-list of AND-pairs, dropping blank names and empty groups.
    private static IReadOnlyList<IReadOnlyList<string>> RequirementGroups(IReadOnlyList<IReadOnlyList<string>> groups)
    {
        return
        [
            .. groups
                .Select(group => (IReadOnlyList<string>)[.. group.Where(name => !string.IsNullOrEmpty(name))])
                .Where(group => group.Count > 0),
        ];
    }

    private static SlotRef ToSlotRef(BuildSlot slot, BuildPowerEntry entry, MbdDb database)
    {
        SlottedEnhancement? enhancement = slot.Enhancement;
        if (enhancement is null)
        {
            return new SlotRef { Level = slot.Level, IsInherent = slot.IsInherent, Enh = -1, Grade = 0, IoLevel = 1, RelativeLevel = OrdinalOf(EnhRelative, "Even", "relative level") };
        }

        if (!database.Enhancements.TryGetValue(enhancement.Uid, out EnhInfo? info))
        {
            throw new InvalidDataException($"E78: {entry.PowerName} slot at level {slot.Level + 1} holds {enhancement.Uid}, which enhancements.json lacks; re-dump the Mids data or replace the enhancement");
        }

        return new SlotRef
        {
            Level = slot.Level,
            IsInherent = slot.IsInherent,
            Enh = info.Nid,
            Grade = OrdinalOf(EnhGrade, enhancement.Grade, "grade"),
            IoLevel = enhancement.IoLevel,
            RelativeLevel = OrdinalOf(EnhRelative, enhancement.RelativeLevel, "relative level"),
        };
    }

    private static int OrdinalOf(IReadOnlyList<string> table, string name, string kind)
    {
        for (int ordinal = 0; ordinal < table.Count; ordinal++)
        {
            if (string.Equals(table[ordinal], name, StringComparison.Ordinal))
            {
                return ordinal;
            }
        }

        throw new InvalidDataException($"{BuildFileErrorCodes.EnumOutOfRange}: {kind} '{name}' is not one of {string.Join(", ", table)}");
    }
}
