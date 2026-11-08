using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Profiles;

/// <summary>
/// The strategy layer above the calc engine: loads <c>build_profiles.json</c> and <c>breakpoints.json</c> and resolves
/// a named profile to concrete targets. A target <c>ref</c> is a dotted path into the breakpoints; <c>{at}</c> in it
/// is replaced by the build's archetype key. The JSON stays a <see cref="JsonElement"/> because the breakpoints are
/// walked by path and have no fixed shape.
/// </summary>
public static class ProfileResolver
{
    private static readonly string[] CanonicalNames =
    [
        "soft-cap-defense",
        "resist-cap",
        "perma-hasten",
        "procmonster",
        "farmer",
        "pvp",
        "theme-rp",
        "solo-99-difficulty",
        "speed-runner-tf",
    ];

    /// <summary>Gets the nine canonical build types.</summary>
    public static IReadOnlyList<string> CanonicalProfiles => CanonicalNames;

    /// <summary>Parses <c>breakpoints.json</c>, the numeric source the profile targets reference.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The root object.</returns>
    public static JsonElement LoadBreakpoints(string path)
    {
        return Load(path);
    }

    /// <summary>Parses <c>build_profiles.json</c> and returns its unresolved <c>profiles</c> object.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The profiles object.</returns>
    /// <exception cref="KeyNotFoundException">The file has no <c>profiles</c> key.</exception>
    public static JsonElement LoadBuildProfiles(string path)
    {
        return Load(path).GetProperty("profiles");
    }

    /// <summary>The profile keys defined in the file, in file order.</summary>
    /// <param name="rawProfiles">The profiles object.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlyList<string> ProfileNames(JsonElement rawProfiles)
    {
        return [.. rawProfiles.EnumerateObject().Select(property => property.Name)];
    }

    /// <summary>Resolves a named profile's targets against the breakpoints for an archetype.</summary>
    /// <param name="name">The profile key.</param>
    /// <param name="rawProfiles">The profiles object.</param>
    /// <param name="breakpoints">The breakpoints root.</param>
    /// <param name="atKey">The archetype, in any form <see cref="AtNames.ResolveAtKey"/> accepts.</param>
    /// <returns>The resolved profile.</returns>
    /// <exception cref="EngineException">P-GOAL-001: the profile is not defined; E16: a target reference does not resolve to a number.</exception>
    public static BuildProfile Resolve(string name, JsonElement rawProfiles, JsonElement breakpoints, string atKey)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(atKey);
        if (!rawProfiles.TryGetProperty(name, out JsonElement raw))
        {
            throw new EngineException(
                $"P-GOAL-001: unknown build profile {PyText.Repr(name)}; expected one of {PyText.List(ProfileNames(rawProfiles))} "
                + "(hard-mode / 4-star is a documented gap, not a profile)");
        }

        return new BuildProfile(
            name,
            raw.GetProperty("display_name").GetString()!,
            [.. raw.GetProperty("priority").EnumerateArray().Select(item => item.GetString()!)],
            [.. raw.GetProperty("targets").EnumerateArray().Select(target => ResolveTarget(target, breakpoints, atKey))]);
    }

    /// <summary>Selects and resolves the profile for a goal string.</summary>
    /// <param name="goal">The goal, which is a profile key.</param>
    /// <param name="rawProfiles">The profiles object.</param>
    /// <param name="breakpoints">The breakpoints root.</param>
    /// <param name="atKey">The archetype.</param>
    /// <returns>The resolved profile.</returns>
    public static BuildProfile Select(string goal, JsonElement rawProfiles, JsonElement breakpoints, string atKey)
    {
        return Resolve(goal, rawProfiles, breakpoints, atKey);
    }

    /// <summary>Python's <c>repr</c> of a JSON value, so messages quote a breakpoint node the way the Python engine does.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The repr text.</returns>
    internal static string Repr(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => "True",
            JsonValueKind.False => "False",
            JsonValueKind.String => PyText.Repr(value.GetString()!),
            JsonValueKind.Array => "[" + string.Join(", ", value.EnumerateArray().Select(Repr)) + "]",
            JsonValueKind.Object => "{" + string.Join(", ", value.EnumerateObject().Select(property => PyText.Repr(property.Name) + ": " + Repr(property.Value))) + "}",
            JsonValueKind.Number => IsInteger(value) ? value.GetRawText() : PyText.NumberRepr(value.GetDouble()),
            _ => "None",
        };
    }

    private static bool IsInteger(JsonElement number)
    {
        return number.GetRawText().IndexOfAny(['.', 'e', 'E']) < 0;
    }

    private static JsonElement Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private static Target ResolveTarget(JsonElement raw, JsonElement breakpoints, string atKey)
    {
        string metric = raw.GetProperty("metric").GetString()!;
        string comparison = raw.GetProperty("op").GetString()!;
        if (raw.TryGetProperty("value", out JsonElement literal))
        {
            MetricValue value = literal.ValueKind switch
            {
                JsonValueKind.True => MetricValue.Flag(true),
                JsonValueKind.False => MetricValue.Flag(false),
                _ => MetricValue.Of(literal.GetDouble()),
            };
            return new Target(metric, comparison, value);
        }

        return new Target(metric, comparison, MetricValue.Of(ResolveRef(raw.GetProperty("ref").GetString()!, breakpoints, atKey)));
    }

    private static double ResolveRef(string reference, JsonElement breakpoints, string atKey)
    {
        string key = atKey;
        if (reference.Contains("{at}", StringComparison.Ordinal))
        {
            IEnumerable<string> archetypes = breakpoints.TryGetProperty("archetypes", out JsonElement table) && table.ValueKind == JsonValueKind.Object
                ? table.EnumerateObject().Select(property => property.Name)
                : [];
            key = AtNames.ResolveAtKey(atKey, archetypes);
        }

        JsonElement node = breakpoints;
        foreach (string part in reference.Replace("{at}", key, StringComparison.Ordinal).Split('.'))
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(part, out JsonElement child))
            {
                throw new EngineException(
                    $"E16: profile target references breakpoint path {PyText.Repr(reference)}, but segment {PyText.Repr(part)} is not in "
                    + "breakpoints.json; fix the ref or add the breakpoint");
            }

            node = child;
        }

        if (node.ValueKind != JsonValueKind.Number)
        {
            throw new EngineException(
                $"E16: breakpoint path {PyText.Repr(reference)} resolved to a non-numeric value {Repr(node)}; point the ref at a "
                + "numeric leaf in breakpoints.json (not a sub-object)");
        }

        return node.GetDouble();
    }
}
