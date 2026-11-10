using System.Globalization;
using System.Text.Json;

namespace CohEngine.TestSupport;

/// <summary>Builds canonical power JSON (the shape under <c>data/canonical/powers</c>) for the classifier and pet tests.</summary>
public static class CanonicalPowers
{
    /// <summary>One template as JSON.</summary>
    /// <param name="attribs">The attributes.</param>
    /// <param name="aspect">The aspect.</param>
    /// <param name="target">The target.</param>
    /// <param name="table">The modifier table.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="extra">More members, already in JSON form and starting with a comma.</param>
    /// <returns>The JSON text.</returns>
    public static string Template(string[] attribs, string aspect = "Absolute", string target = "AnyAffected", string table = "Melee_Damage", double scale = 1.0, string extra = "")
    {
        ArgumentNullException.ThrowIfNull(attribs);
        string names = string.Join(",", attribs.Select(attrib => $"\"{attrib}\""));
        return $"{{\"attribs\":[{names}],\"aspect\":\"{aspect}\",\"target\":\"{target}\",\"table\":\"{table}\",\"scale\":{scale.ToString("R", CultureInfo.InvariantCulture)}{extra}}}";
    }

    /// <summary>One effect group as JSON.</summary>
    /// <param name="templates">The template JSON texts.</param>
    /// <param name="extra">More members, already in JSON form and starting with a comma.</param>
    /// <returns>The JSON text.</returns>
    public static string Effect(string[] templates, string extra = "")
    {
        ArgumentNullException.ThrowIfNull(templates);
        return $"{{\"templates\":[{string.Join(",", templates)}]{extra}}}";
    }

    /// <summary>A canonical power as JSON text.</summary>
    /// <param name="name">The full name.</param>
    /// <param name="type">The power type.</param>
    /// <param name="effects">The effect group JSON texts.</param>
    /// <param name="area">The effect area.</param>
    /// <param name="range">The range.</param>
    /// <param name="categories">The allowed boost-set categories.</param>
    /// <param name="extra">More members, already in JSON form and starting with a comma.</param>
    /// <returns>The JSON text.</returns>
    public static string Text(string name = "Test.Set.Power", string type = "Click", string[]? effects = null, string area = "SingleTarget", double range = 7.0, string[]? categories = null, string extra = "")
    {
        string cats = string.Join(",", (categories ?? []).Select(category => $"\"{category}\""));
        return $"{{\"full_name\":\"{name}\",\"type\":\"{type}\",\"effect_area\":\"{area}\",\"range\":{range.ToString("R", CultureInfo.InvariantCulture)},\"radius\":0.0,\"arc\":0.0,"
            + $"\"max_targets_hit\":0,\"allowed_boostset_cats\":[{cats}],\"effects\":[{string.Join(",", effects ?? [])}]{extra}}}";
    }

    /// <summary>A canonical power parsed from its text.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The root element.</returns>
    public static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
