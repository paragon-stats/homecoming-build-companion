namespace CohEngine.Core.Data;

/// <summary>Loader for a build's <c>powers_effects.json</c> (the Python port's <c>effect.load_powers_effects</c>).</summary>
public static class EffectData
{
    /// <summary>Parses <c>powers_effects.json</c>, dropping blank names and empty groups from the requirement lists.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The build's powers in file order.</returns>
    public static IReadOnlyList<Power> LoadPowersEffects(string path)
    {
        return
        [
            .. Json.Load(path, EffectJsonContext.Default.IReadOnlyListPower).Select(power => power with
            {
                RequiresPowers = RequirementGroups(power.RequiresPowers),
                RequiresPowersNot = RequirementGroups(power.RequiresPowersNot),
            }),
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> RequirementGroups(IReadOnlyList<IReadOnlyList<string>> groups)
    {
        // Single-power requirements leave the second name of an AND pair blank.
        return
        [
            .. groups
                .Select<IReadOnlyList<string>, IReadOnlyList<string>>(group => [.. group.Where(name => !string.IsNullOrEmpty(name))])
                .Where(group => group.Count > 0),
        ];
    }
}
