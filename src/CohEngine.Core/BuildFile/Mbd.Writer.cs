using System.Globalization;
using System.Text;

using CohEngine.Core.Chain;
using CohEngine.Core.Data;

namespace CohEngine.Core.BuildFile;

/// <summary>The writer half of <see cref="Mbd"/>: a build walked level by level, as the game grants it.</summary>
public static partial class Mbd
{
    /// <summary>The number of power sets MidsReborn indexes: primary, secondary, a blank, four pools, an ancillary.</summary>
    public const int PowerSetsExpected = 8;

    private const int MaxLevelText = 49;
    private const int ExemplarLevel = 15;
    private const int ExemplarSlots = 2;

    /// <summary>Gets the provenance block of the database the Homecoming builds are written against.</summary>
    public static BuildMetaData Homecoming { get; } = new("Mids Reborn", "3.8.6.0", "Homecoming", "2026.1.1242");

    /// <summary>
    /// The bytes of a <c>.mbd</c> file: indented two spaces, LF line endings, every character past 0x7E as a lowercase
    /// <c>\uXXXX</c> escape, and a final newline, with UTF-8 and no BOM (the Python <c>json.dumps(indent=2)</c> form).
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The file's bytes.</returns>
    public static byte[] Serialize(MbdDocument document)
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(PyJson.Dumps(ToTree(document)) + "\n");
    }

    /// <summary>The 0-based <c>PowerSets</c> arity check input: how many entries MidsReborn would index.</summary>
    /// <param name="powerSets">The power set names.</param>
    /// <returns>The count.</returns>
    public static int PowersetsArity(IReadOnlyCollection<string> powerSets)
    {
        ArgumentNullException.ThrowIfNull(powerSets);
        return powerSets.Count;
    }

    /// <summary>Why a header <c>Level</c> is not one MidsReborn loads: a string of an integer from 0 to 49.</summary>
    /// <param name="value">The stored level text.</param>
    /// <returns>The reason, or null when the value loads.</returns>
    public static string? LevelTextProblem(string? value)
    {
        string digits = value?.TrimStart('-') ?? string.Empty;
        bool loads = digits.Length > 0
            && digits.All(char.IsAsciiDigit)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
            && level is >= 0 and <= MaxLevelText;
        return loads ? null : $"{(value is null ? "null" : $"'{value}'")} is not a string of an integer from 0 to {MaxLevelText}";
    }

    /// <summary>The inherent powers Mids adds to a character of <paramref name="level"/> that <paramref name="entryNames"/> lacks.</summary>
    /// <param name="entryNames">The power names the build holds.</param>
    /// <param name="className">The class name.</param>
    /// <param name="level">The character level.</param>
    /// <param name="database">The reference tables.</param>
    /// <returns>The missing inherents' full names, in the class's order.</returns>
    /// <exception cref="InvalidDataException">E76: the class is not in the archetype table.</exception>
    public static IReadOnlyList<string> MissingInherents(IReadOnlyCollection<string> entryNames, string className, int level, MbdDb database)
    {
        ArgumentNullException.ThrowIfNull(entryNames);
        ArgumentNullException.ThrowIfNull(database);
        int nid = database.Archetypes.NidFromUidClass(className);
        if (nid < 0)
        {
            throw new InvalidDataException($"E76: class {className} is not in the archetype table; re-dump the Mids data");
        }

        HashSet<string> present = new(entryNames, StringComparer.Ordinal);
        return
        [
            .. database.Archetypes.Classes[nid].InherentPowers
                .Where(power => power.Level >= 1 && power.Level <= level && !present.Contains(power.FullName))
                .Select(power => power.FullName),
        ];
    }

    /// <summary>Refuses a document MidsReborn would crash on or load wrongly.</summary>
    /// <param name="document">The document.</param>
    /// <param name="database">The reference tables.</param>
    /// <exception cref="InvalidDataException">
    /// E73 for a <c>PowerSets</c> list not 8 long, E75 for a <c>Level</c> that is not a string of 0 to 49, E76 for a missing
    /// inherent power, E74 for an <c>IoLevel</c> outside the bounds of its enhancement.
    /// </exception>
    public static void CheckMbdInvariants(MbdDocument document, MbdDb database)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(database);
        int arity = PowersetsArity(document.PowerSets);
        if (arity != PowerSetsExpected)
        {
            throw new InvalidDataException($"E73: PowerSets has {arity} entries; MidsReborn needs {PowerSetsExpected}; pad with empty strings");
        }

        string? problem = LevelTextProblem(document.Level);
        if (problem is not null)
        {
            throw new InvalidDataException($"E75: Level {problem}; write the character level minus one as text");
        }

        int level = int.Parse(document.Level!, CultureInfo.InvariantCulture) + 1;
        IReadOnlyList<string> missing = MissingInherents([.. document.PowerEntries.Select(entry => entry.PowerName ?? string.Empty)], document.Class ?? string.Empty, level, database);
        if (missing.Count > 0)
        {
            throw new InvalidDataException($"E76: missing inherent powers {string.Join(", ", missing)}; add them as unslotted entries");
        }

        foreach (MbdPowerEntryDoc entry in document.PowerEntries)
        {
            foreach (MbdSlotDoc slot in entry.SlotEntries)
            {
                CheckIoLevel(entry, slot, slot.Enhancement, database);
                CheckIoLevel(entry, slot, slot.FlippedEnhancement, database);
            }
        }
    }

    /// <summary>The document for a spec, picks and slots walked in the order the game grants them.</summary>
    /// <param name="spec">The build spec.</param>
    /// <param name="schedule">The level schedule.</param>
    /// <param name="database">The reference tables.</param>
    /// <returns>The document, checked against <see cref="CheckMbdInvariants"/>.</returns>
    /// <exception cref="InvalidDataException">
    /// E46 for a pick off its schedule level, E47 for a pinned slot before its power, E48 for a pinned slot off a grant level
    /// or a short pin list, E49 for added slots the schedule cannot place, E77 for an inherent power the powers table lacks,
    /// E78 for an enhancement the table lacks, and E73 to E76 from the invariants.
    /// </exception>
    public static MbdDocument WriteMbd(BuildSpec spec, LevelSchedule schedule, MbdDb database)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(database);
        List<PickSpec> picks = OrderedPicks(spec.Picks, schedule);
        List<PickSpec> powers = [.. picks, .. spec.Inherents, .. OmittedInherents(spec, database)];
        Dictionary<int, List<int>> placed = PlaceSlots(powers, schedule);
        List<MbdPowerEntryDoc> entries = [.. powers.Select((pick, index) => Entry(pick, placed.GetValueOrDefault(index, []), database))];
        entries.AddRange(spec.Incarnates.Select(name => Entry(new PickSpec { Power = name, Level = 50, Enhancements = [], StatInclude = true }, [], database)));
        MbdDocument document = new()
        {
            BuiltWith = new MbdBuiltWithDoc { App = spec.Meta.App, Version = spec.Meta.Version, Database = spec.Meta.Database, DatabaseVersion = spec.Meta.DatabaseVersion },
            Level = (spec.Level - 1).ToString(CultureInfo.InvariantCulture),
            Class = spec.ClassName,
            Origin = spec.Origin,
            Alignment = spec.Alignment,
            Name = spec.Name,
            Comment = spec.Comment,
            PowerSets = spec.PowerSets,
            LastPower = picks.Count,
            PowerEntries = entries,
        };
        CheckMbdInvariants(document, database);
        return document;
    }

    internal static void FillSlots(IReadOnlyList<PickSpec> powers, IReadOnlyDictionary<int, int> remaining, IReadOnlyList<int> openGrants, Dictionary<int, List<int>> placed)
    {
        Dictionary<int, int> taken = remaining.Keys.ToDictionary(index => index, _ => 0);
        for (int position = 0; position < openGrants.Count; position++)
        {
            int level = openGrants[position];
            List<int> ranked =
            [
                .. remaining.Keys
                    .Where(index => taken[index] < remaining[index] && powers[index].Level <= level)
                    .OrderBy(index => !(powers[index].Essential && taken[index] < ExemplarSlots && level <= ExemplarLevel))
                    .ThenBy(index => taken[index] / (double)remaining[index])
                    .ThenBy(index => index),
            ];
            foreach (int index in ranked)
            {
                taken[index]++;
                if (Feasible([.. openGrants.Skip(position + 1)], remaining.ToDictionary(pair => pair.Key, pair => pair.Value - taken[pair.Key]), powers))
                {
                    placed[index].Add(level);
                    break;
                }

                taken[index]--;
            }
        }
    }

    private static void CheckIoLevel(MbdPowerEntryDoc entry, MbdSlotDoc slot, MbdEnhancementDoc? enhancement, MbdDb database)
    {
        string? uid = enhancement?.Uid ?? enhancement?.Enhancement;
        (int Min, int Max)? bounds = string.IsNullOrEmpty(uid) ? null : IoLevelBounds(uid, database);
        if (enhancement is not null && bounds is { } range && (enhancement.IoLevel < range.Min || enhancement.IoLevel > range.Max))
        {
            throw new InvalidDataException(
                $"E74: {entry.PowerName} slot at level {slot.Level}: {uid} has IoLevel {enhancement.IoLevel}, outside {range.Min} to {range.Max}; write at most {range.Max}");
        }
    }

    private static List<PickSpec> OrderedPicks(IReadOnlyList<PickSpec> picks, LevelSchedule schedule)
    {
        IReadOnlyList<int> order = schedule.PowerPickLevels;
        if (picks.Count > order.Count)
        {
            throw new InvalidDataException($"E46: {picks.Count} picks; the schedule grants {order.Count}");
        }

        List<PickSpec> ordered = [.. picks.OrderBy(pick => pick.Level)];
        for (int position = 0; position < ordered.Count; position++)
        {
            int granted = order[position] + 1;
            if (ordered[position].Level != granted)
            {
                throw new InvalidDataException(
                    $"E46: pick {position + 1} of {order.Count} ({ordered[position].Power}) is at level {ordered[position].Level}; the schedule grants it at level {granted}");
            }
        }

        return ordered;
    }

    private static Dictionary<int, List<int>> PlaceSlots(List<PickSpec> powers, LevelSchedule schedule)
    {
        List<(int Level, int Count)> grants = [.. schedule.SlotsPerLevel.OrderBy(pair => pair.Key).Select(pair => (pair.Key + 1, pair.Value))];
        Dictionary<int, int> capacity = grants.ToLastWinsDictionary(grant => grant.Level, grant => grant.Count);
        SortedDictionary<int, int> need = new(Enumerable.Range(0, powers.Count).Where(index => powers[index].Enhancements.Count > 1).ToDictionary(index => index, index => powers[index].Enhancements.Count - 1));
        int total = need.Values.Sum();
        if (total > schedule.TotalSlotsGranted)
        {
            throw new InvalidDataException($"E49: {total} added slots; the schedule grants {schedule.TotalSlotsGranted}");
        }

        Dictionary<int, List<int>> placed = PinSlots(powers, need, capacity);
        List<int> openGrants = [.. grants.SelectMany(grant => Enumerable.Repeat(grant.Level, capacity[grant.Level]))];
        SortedDictionary<int, int> remaining = new(need.Where(pair => pair.Value > placed[pair.Key].Count).ToDictionary(pair => pair.Key, pair => pair.Value - placed[pair.Key].Count));
        foreach ((int index, int count) in remaining)
        {
            int reachable = openGrants.Count(level => level >= powers[index].Level);
            if (count > reachable)
            {
                throw new InvalidDataException(
                    $"E49: {powers[index].Power} picked at level {powers[index].Level} needs {count} added slots; {reachable} can be placed from level {powers[index].Level}");
            }
        }

        if (!Feasible(openGrants, remaining, powers))
        {
            throw new InvalidDataException("E49: the powers picked late need more added slots than the grants after their picks");
        }

        FillSlots(powers, remaining, openGrants, placed);
        return placed;
    }

    private static Dictionary<int, List<int>> PinSlots(List<PickSpec> powers, SortedDictionary<int, int> need, Dictionary<int, int> capacity)
    {
        Dictionary<int, List<int>> placed = need.Keys.ToDictionary(index => index, _ => new List<int>());
        for (int index = 0; index < powers.Count; index++)
        {
            PickSpec pick = powers[index];
            if (pick.SlotLevels is null || !need.TryGetValue(index, out int needed))
            {
                continue;
            }

            if (pick.SlotLevels.Count != needed)
            {
                string plural = pick.SlotLevels.Count != 1 ? "s" : string.Empty;
                throw new InvalidDataException($"E48: {pick.Power} names {pick.SlotLevels.Count} slot level{plural} for {needed} added slots");
            }

            foreach (int level in pick.SlotLevels)
            {
                if (level < pick.Level)
                {
                    throw new InvalidDataException($"E47: {pick.Power} places a slot at level {level}, before it is picked at level {pick.Level}");
                }

                if (capacity.GetValueOrDefault(level, 0) < 1)
                {
                    throw new InvalidDataException($"E48: {pick.Power} places a slot at level {level}, which grants no slot");
                }

                capacity[level]--;
                placed[index].Add(level);
            }
        }

        return placed;
    }

    // Whether the grants can still cover every remaining need: the latest grant goes to the latest pick.
    private static bool Feasible(IReadOnlyList<int> grants, IReadOnlyDictionary<int, int> left, IReadOnlyList<PickSpec> powers)
    {
        SortedDictionary<int, int> counts = new(left.ToDictionary(pair => pair.Key, pair => pair.Value));
        foreach (int level in grants.Reverse())
        {
            int? latest = null;
            foreach (int index in counts.Keys.Where(index => counts[index] > 0 && powers[index].Level <= level && (latest is null || powers[index].Level > powers[latest.Value].Level)))
            {
                latest = index;
            }

            if (latest is { } chosen)
            {
                counts[chosen]--;
            }
        }

        return counts.Values.All(count => count == 0);
    }

    private static MbdEnhancementDoc? EnhancementDoc(string? uid, MbdDb database)
    {
        if (uid is null)
        {
            return null;
        }

        (int Min, int Max) bounds = IoLevelBounds(uid, database)
            ?? throw new InvalidDataException($"E78: enhancement {uid} is not in enhancements.json; check the uid or re-dump the Mids data");
        return new MbdEnhancementDoc { Uid = uid, Grade = "None", IoLevel = bounds.Max, RelativeLevel = "Even", Obtained = false };
    }

    private static MbdPowerEntryDoc Entry(PickSpec pick, IReadOnlyList<int> added, MbdDb database)
    {
        List<int> levels = pick.Enhancements.Count > 0 ? [pick.Level, .. added.Order()] : [];
        return new MbdPowerEntryDoc
        {
            PowerName = pick.Power,
            Level = pick.Level,
            StatInclude = pick.StatInclude,
            SlotEntries = [.. pick.Enhancements.Select((uid, index) => new MbdSlotDoc { Level = levels[index], IsInherent = false, Enhancement = EnhancementDoc(uid, database) })],
        };
    }

    private static List<PickSpec> OmittedInherents(BuildSpec spec, MbdDb database)
    {
        List<PickSpec> added = [];
        foreach (string name in MissingInherents([.. spec.Inherents.Select(pick => pick.Power)], spec.ClassName, spec.Level, database))
        {
            PowerTemplate template = database.Powers.GetValueOrDefault(name)
                ?? throw new InvalidDataException($"E77: inherent power {name} is not in powers.json; re-dump the Mids data");
            added.Add(new PickSpec
            {
                Power = name,
                Level = template.Level,
                Enhancements = template.Enhancements.Count > 0 ? [null] : [],
                StatInclude = string.Equals(template.PowerType, "Auto_", StringComparison.Ordinal),
            });
        }

        return added;
    }
}
