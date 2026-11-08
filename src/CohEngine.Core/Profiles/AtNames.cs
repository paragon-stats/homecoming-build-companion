using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Profiles;

/// <summary>Archetype-name resolution: one canonical key from the key, the display name or the <c>Class_</c> name.</summary>
public static class AtNames
{
    /// <summary>
    /// Resolves an archetype named in any form to its canonical key among the valid keys: case, spaces and the
    /// <c>Class_</c> prefix do not matter.
    /// </summary>
    /// <param name="name">The name as given.</param>
    /// <param name="validKeys">The canonical keys.</param>
    /// <returns>The matching key.</returns>
    /// <exception cref="EngineException">E19: the name matches no key, or more than one.</exception>
    public static string ResolveAtKey(string name, IEnumerable<string> validKeys)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(validKeys);
        string[] keys = [.. validKeys];
        string normalized = Normalize(name);
        string[] matches = [.. keys.Where(key => string.Equals(Normalize(key), normalized, StringComparison.Ordinal))];
        if (matches.Length == 1)
        {
            return matches[0];
        }

        throw new EngineException(
            $"E19: archetype {PyText.Repr(name)} matches {matches.Length} of the {keys.Length} known keys {PyText.List(keys.Order(StringComparer.Ordinal))}; "
            + "pass a recognizable archetype name (canonical key, display name, or Class_ name)");
    }

    // shortcut: Python's casefold is approximated by the invariant lower case, which differs only for letters such as the German sharp s; upgrade if an archetype name ever uses one.
    private static string Normalize(string name)
    {
        string trimmed = name.Trim();
        return (trimmed.StartsWith("Class_", StringComparison.Ordinal) ? trimmed["Class_".Length..] : trimmed).Replace(' ', '_').ToLowerInvariant();
    }
}
