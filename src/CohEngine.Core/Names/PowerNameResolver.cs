using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Names;

/// <summary>Bidirectional internal/display lookup over the player powersets of the canonical data.</summary>
public sealed class PowerNameResolver
{
    private static readonly string[] ArchetypeCategoryFields = ["primary_category", "secondary_category", "power_pool_category"];

    private readonly Dictionary<string, PowerName> byInternal = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="PowerNameResolver"/> class.</summary>
    /// <param name="powersets">The powersets.</param>
    public PowerNameResolver(IReadOnlyList<PowersetName> powersets)
    {
        ArgumentNullException.ThrowIfNull(powersets);
        Powersets = powersets;
        foreach (PowerName power in powersets.SelectMany(set => set.Powers))
        {
            byInternal[power.Internal] = power;
        }
    }

    /// <summary>Gets every player powerset, sorted by internal name.</summary>
    public IReadOnlyList<PowersetName> Powersets { get; }

    /// <summary>Indexes every player powerset under the canonical directory.</summary>
    /// <param name="canonical">The <c>data/canonical</c> directory.</param>
    /// <returns>The resolver.</returns>
    public static PowerNameResolver FromCanonical(string canonical)
    {
        string powers = Path.Join(canonical, "powers");
        Dictionary<string, SortedSet<string>> sets = new(StringComparer.Ordinal);
        foreach (((string category, string? setDir), SortedSet<string> ats) in PlayerSetDirs(canonical))
        {
            IEnumerable<string> members = setDir is not null
                ? [Path.Join(powers, category, setDir)]
                : Directory.EnumerateDirectories(Path.Join(powers, category)).Order(StringComparer.Ordinal);
            foreach (string member in members)
            {
                sets.TryAdd(member, []);
                sets[member].UnionWith(ats);
            }
        }

        List<PowersetName> built = [];
        foreach ((string directory, SortedSet<string> ats) in sets)
        {
            string categoryName = Read(Path.Join(Path.GetDirectoryName(directory)!, "index.json")).ReqStr("name");
            JsonElement index = Read(Path.Join(directory, "index.json"));
            string internalName = $"{categoryName}.{index.ReqStr("name")}";
            string[] names = [.. index.Strings("power_names")];
            string[] displays = [.. index.Strings("power_display_names")];
            if (names.Length != displays.Length)
            {
                throw new EngineException($"{ErrorCodes.E22}: powerset {internalName} lists {names.Length.ToString(CultureInfo.InvariantCulture)} power names but {displays.Length.ToString(CultureInfo.InvariantCulture)} display names; re-harvest the canonical data");
            }

            built.Add(new PowersetName(internalName, index.ReqStr("display_name"), categoryName, [.. ats], [.. names.Select((name, position) => new PowerName(name, displays[position], internalName))]));
        }

        return new PowerNameResolver(PySort.Sorted(built, (left, right) => string.CompareOrdinal(left.Internal, right.Internal)));
    }

    /// <summary>The name the game shows for a power's full internal name.</summary>
    /// <param name="internalName">The internal name.</param>
    /// <returns>The display name.</returns>
    /// <exception cref="EngineException">E22 when the name is not a player power.</exception>
    public string DisplayName(string internalName)
    {
        ArgumentNullException.ThrowIfNull(internalName);
        return byInternal.TryGetValue(internalName, out PowerName? power)
            ? power.Display
            : throw new EngineException(
                $"{ErrorCodes.E22}: internal power name {PyText.Repr(internalName)} is not a player power among the {byInternal.Count.ToString(CultureInfo.InvariantCulture)} indexed; "
                + "pass a full name such as 'Pool.Teleportation.Long_Range_Teleport'");
    }

    /// <summary>Every player power shown as the display name (case-insensitive), optionally within one powerset.</summary>
    /// <param name="display">The display name.</param>
    /// <param name="powerset">The powerset, or null.</param>
    /// <returns>The internal names.</returns>
    public IReadOnlyList<string> InternalNames(string display, string? powerset = null)
    {
        ArgumentNullException.ThrowIfNull(display);
        return
        [
            .. byInternal.Values
                .Where(power => string.Equals(power.Display, display, StringComparison.OrdinalIgnoreCase) && (powerset is null || string.Equals(power.Powerset, powerset, StringComparison.Ordinal)))
                .Select(power => power.Internal),
        ];
    }

    /// <summary>The one internal name shown as the display name within the powerset.</summary>
    /// <param name="display">The display name.</param>
    /// <param name="powerset">The powerset's internal name.</param>
    /// <returns>The internal name.</returns>
    /// <exception cref="EngineException">E22 unless exactly one power matches.</exception>
    public string Resolve(string display, string powerset)
    {
        IReadOnlyList<string> matches = InternalNames(display, powerset);
        return matches.Count == 1
            ? matches[0]
            : throw new EngineException(
                $"{ErrorCodes.E22}: display name {PyText.Repr(display)} matches {matches.Count.ToString(CultureInfo.InvariantCulture)} powers in powerset {PyText.Repr(powerset)} {PyText.List(matches)}, expected 1; "
                + "pass the display name and the powerset's internal name, e.g. 'Fold Space' in 'Pool.Teleportation'");
    }

    /// <summary>A power as its display name, its internal name, or <c>Display (Internal)</c>.</summary>
    /// <param name="internalName">The internal name.</param>
    /// <param name="form"><c>display</c>, <c>internal</c> or <c>both</c>.</param>
    /// <returns>The text.</returns>
    public string Render(string internalName, string form = "display")
    {
        string display = DisplayName(internalName);
        if (string.Equals(form, "internal", StringComparison.Ordinal))
        {
            return internalName;
        }

        return string.Equals(form, "both", StringComparison.Ordinal) ? $"{display} ({internalName})" : display;
    }

    private static JsonElement Read(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static Dictionary<(string Category, string? SetDir), SortedSet<string>> PlayerSetDirs(string canonical)
    {
        Dictionary<(string, string?), SortedSet<string>> found = [];
        foreach (JsonProperty entry in Read(Path.Join(canonical, "powers_by_at.json")).EnumerateObject())
        {
            JsonElement record = Read(Path.Join(canonical, "archetypes", entry.Name + ".json"));
            foreach (string field in ArchetypeCategoryFields)
            {
                Add(found, (record.ReqStr(field).ToLowerInvariant(), null), entry.Name);
            }

            foreach (string[] parts in entry.Value.EnumerateArray().Select(pair => pair[1].GetString()!.Split('.')).Where(parts => string.Equals(parts[0], "epic", StringComparison.Ordinal)))
            {
                Add(found, (parts[0], parts[1]), entry.Name);
            }
        }

        return found;
    }

    private static void Add(Dictionary<(string Category, string? SetDir), SortedSet<string>> found, (string Category, string? SetDir) key, string archetype)
    {
        found.TryAdd(key, new SortedSet<string>(StringComparer.Ordinal));
        found[key].Add(archetype);
    }
}
