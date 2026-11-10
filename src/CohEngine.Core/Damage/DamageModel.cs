using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>The layered model's source names and evaluator.</summary>
public static class DamageModel
{
    /// <summary>The chain source name.</summary>
    public const string SourceChain = "chain";

    /// <summary>The aura source name.</summary>
    public const string SourceAura = "aura";

    /// <summary>The patch source name.</summary>
    public const string SourcePatch = "patch";

    /// <summary>The pet source name.</summary>
    public const string SourcePet = "pet";

    /// <summary>Folds every source and modifier over a context.</summary>
    /// <param name="context">The context.</param>
    /// <param name="sources">The sources; the default registry when null.</param>
    /// <param name="modifiers">The modifiers; the default registry when null.</param>
    /// <returns>The report.</returns>
    /// <exception cref="EngineException">E30 when the non-chain sources claim more than the timeline.</exception>
    public static DamageReport Evaluate(
        DamageContext context,
        IReadOnlyList<SourceRegistration>? sources = null,
        IReadOnlyList<Func<DamageContext, ModifierResult?>>? modifiers = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        sources ??= DamagePlugins.Sources;
        modifiers ??= DamagePlugins.Modifiers;

        List<SourceResult> first = [];
        foreach (SourceRegistration registration in sources.Where(item => !item.FillsRemainder))
        {
            SourceResult? result = registration.Run(context);
            if (result is not null)
            {
                first.Add(result);
            }
        }

        double share = F32.Round(PyFloat.Sum(first.Select(result => result.TimelineShare)));
        if (share > 1.0)
        {
            string claims = string.Join(", ", first.Select(result => $"('{result.Source}', {PyFloat.Repr(result.TimelineShare)})"));
            throw new EngineException(
                $"{ErrorCodes.E30}: non-chain damage sources claim {PyFloat.Fixed(share, 3)} of the timeline (> 1.0): [{claims}]; "
                + "a summon, patch or click buff reports a cast share above its recharge cadence");
        }

        double drain = F32.Round(PyFloat.Sum(first.Select(result => result.EndPerSec)));
        DamageContext restContext = context with { TimelineAvailable = F32.Round(context.TimelineAvailable - share), OtherDrainPerSec = drain };
        List<SourceResult> results = [.. first];
        foreach (SourceRegistration registration in sources.Where(item => item.FillsRemainder))
        {
            SourceResult? result = registration.Run(restContext);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        List<ModifierResult> mods = [];
        foreach (Func<DamageContext, ModifierResult?> modifier in modifiers)
        {
            ModifierResult? result = modifier(restContext);
            if (result is not null)
            {
                mods.Add(result);
            }
        }

        List<KeyValuePair<string, double>> scaled = [];
        foreach (SourceResult result in results)
        {
            double baseline = result.DamageBuffBaseline ?? context.DamageBuffBaseline;
            List<ModifierResult> hit = [.. mods.Where(item => item.AppliesTo is null || item.AppliesTo.Contains(result.Source))];
            double bonus = F32.Round(PyFloat.Sum(hit.Where(item => item.Additive).Select(item => F32.Round((item.Multiplier - 1.0) * item.Uptime))));
            double factor = F32.Round(1.0 + (bonus / baseline));
            foreach (ModifierResult item in hit.Where(item => !item.Additive))
            {
                factor = F32.Round(factor * item.Scale());
            }

            int existing = scaled.FindIndex(pair => string.Equals(pair.Key, result.Source, StringComparison.Ordinal));
            KeyValuePair<string, double> entry = new(result.Source, F32.Round(result.Dps * factor));
            if (existing >= 0)
            {
                scaled[existing] = entry;
            }
            else
            {
                scaled.Add(entry);
            }
        }

        double totalDrain = F32.Round(PyFloat.Sum(results.Select(result => result.EndPerSec)));
        return new DamageReport(
            results,
            mods,
            scaled,
            F32.Round(PyFloat.Sum(scaled.Select(entry => entry.Value))),
            totalDrain,
            context.Budget.TimeToEmpty(totalDrain),
            restContext);
    }
}
