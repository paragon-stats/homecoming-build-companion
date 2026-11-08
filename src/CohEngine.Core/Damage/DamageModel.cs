using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>Where an attack is delivered from.</summary>
public enum Delivery
{
    /// <summary>Within melee reach.</summary>
    Melee,

    /// <summary>At range.</summary>
    Ranged,
}

/// <summary>The attack's area.</summary>
public enum Shape
{
    /// <summary>One target.</summary>
    SingleTarget,

    /// <summary>A cone.</summary>
    Cone,

    /// <summary>A sphere on a target.</summary>
    TargetedAoe,

    /// <summary>A sphere on self.</summary>
    Pbaoe,

    /// <summary>A placed location.</summary>
    Location,
}

/// <summary>How a power shapes the play cycle.</summary>
public enum Role
{
    /// <summary>In the chain.</summary>
    Chain,

    /// <summary>A damage aura.</summary>
    Aura,

    /// <summary>A location patch.</summary>
    Patch,

    /// <summary>A pet summon.</summary>
    PetSummon,

    /// <summary>A self damage-buff click.</summary>
    BuffClick,

    /// <summary>A click that buffs damage or puts -Res on foes.</summary>
    Support,

    /// <summary>A toggle or auto holding -Res on foes.</summary>
    PersistentDebuff,
}

/// <summary>Where the character stands while running the chain.</summary>
public enum Stance
{
    /// <summary>In melee.</summary>
    Melee,

    /// <summary>At range.</summary>
    Ranged,

    /// <summary>Switching between the two.</summary>
    Hybrid,
}

/// <summary>The Python enum values of the model enums.</summary>
public static class EnumText
{
    /// <summary>The Python value of a stance.</summary>
    /// <param name="stance">The stance.</param>
    /// <returns>Its text.</returns>
    public static string Value(this Stance stance)
    {
        return stance switch
        {
            Stance.Melee => "melee",
            Stance.Ranged => "ranged",
            _ => "hybrid",
        };
    }

    /// <summary>The Python value of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>Its text.</returns>
    public static string Value(this Role role)
    {
        return role switch
        {
            Role.Chain => "chain",
            Role.Aura => "aura",
            Role.Patch => "patch",
            Role.PetSummon => "pet_summon",
            Role.BuffClick => "buff_click",
            Role.Support => "support",
            _ => "persistent_debuff",
        };
    }

    /// <summary>The Python value of a delivery.</summary>
    /// <param name="delivery">The delivery.</param>
    /// <returns>Its text.</returns>
    public static string Value(this Delivery delivery)
    {
        return delivery == Delivery.Melee ? "melee" : "ranged";
    }

    /// <summary>Parses a stance value.</summary>
    /// <param name="text">The text.</param>
    /// <param name="stance">The stance.</param>
    /// <returns>Whether the text names a stance.</returns>
    public static bool TryParseStance(string text, out Stance stance)
    {
        foreach (Stance candidate in new[] { Stance.Melee, Stance.Ranged, Stance.Hybrid })
        {
            if (string.Equals(candidate.Value(), text, StringComparison.Ordinal))
            {
                stance = candidate;
                return true;
            }
        }

        stance = Stance.Melee;
        return false;
    }
}

/// <summary>One power's classification, computed once.</summary>
/// <param name="FullName">The power's full name.</param>
/// <param name="Delivery">Melee or ranged.</param>
/// <param name="Shape">The area.</param>
/// <param name="Role">The role.</param>
/// <param name="RangeFt">The range in feet.</param>
/// <param name="RadiusFt">The radius in feet.</param>
/// <param name="ArcRad">The arc.</param>
/// <param name="MaxTargets">The target cap, 0 for none.</param>
/// <param name="SetCategories">The allowed boost-set categories.</param>
/// <param name="ModesRequired">The modes the power needs.</param>
public sealed record AttackProfile(
    string FullName,
    Delivery Delivery,
    Shape Shape,
    Role Role,
    double RangeFt,
    double RadiusFt,
    double ArcRad,
    int MaxTargets,
    IReadOnlyList<string> SetCategories,
    IReadOnlyList<string> ModesRequired)
{
    /// <summary>Gets a value indicating whether the character must be in melee to use the power.</summary>
    public bool NeedsMeleePosition => Delivery == Delivery.Melee || Shape == Shape.Pbaoe;
}

/// <summary>The endurance a build has to spend: pool, income and committed toggle drain (per second).</summary>
/// <param name="MaxEnd">The pool.</param>
/// <param name="RecoveryPerSec">The income.</param>
/// <param name="ToggleDrainPerSec">The toggle drain.</param>
public sealed record EnduranceBudget(double MaxEnd, double RecoveryPerSec, double ToggleDrainPerSec)
{
    /// <summary>Recovery minus toggle drain minus extra drain.</summary>
    /// <param name="extraDrain">Chain, patch and summon drain.</param>
    /// <returns>The net rate.</returns>
    public double NetPerSec(double extraDrain = 0.0)
    {
        return F32.Round(RecoveryPerSec - ToggleDrainPerSec - extraDrain);
    }

    /// <summary>Seconds from full to empty at the net rate; infinity when sustainable.</summary>
    /// <param name="extraDrain">Extra drain.</param>
    /// <returns>The time.</returns>
    public double TimeToEmpty(double extraDrain = 0.0)
    {
        double net = NetPerSec(extraDrain);
        return net >= 0.0 ? double.PositiveInfinity : F32.Round(MaxEnd / -net);
    }
}

/// <summary>A damaging or damage-buffing power: cadence, damage per activation, profile, buff and debuff riders.</summary>
public sealed record ChainAttack
{
    /// <summary>Gets the full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the damage per activation.</summary>
    public required double Damage { get; init; }

    /// <summary>Gets the raw cast time.</summary>
    public required double CastTime { get; init; }

    /// <summary>Gets the recharge time.</summary>
    public required double RechargeTime { get; init; }

    /// <summary>Gets the endurance cost.</summary>
    public required double EndCost { get; init; }

    /// <summary>Gets the profile.</summary>
    public required AttackProfile Profile { get; init; }

    /// <summary>Gets the toggle tick period.</summary>
    public double ActivatePeriod { get; init; }

    /// <summary>Gets the self damage buff fraction.</summary>
    public double SelfDamageBuff { get; init; }

    /// <summary>Gets the self damage buff duration.</summary>
    public double SelfDamageBuffDuration { get; init; }

    /// <summary>Gets the target -Res fraction.</summary>
    public double TargetResDebuff { get; init; }

    /// <summary>Gets the target -Res duration.</summary>
    public double TargetResDebuffDuration { get; init; }
}

/// <summary>The fight being modelled: how many foes stand in reach of an area attack.</summary>
/// <param name="Targets">The foe count.</param>
public sealed record Scenario(int Targets = 1)
{
    /// <summary>Single-target powers hit one foe; area powers hit up to their cap.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The count hit.</returns>
    public int TargetsHit(AttackProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Shape == Shape.SingleTarget)
        {
            return 1;
        }

        return Math.Max(1, Math.Min(profile.MaxTargets != 0 ? profile.MaxTargets : Targets, Targets));
    }
}

/// <summary>How fast the character closes or opens range, and the standoff it keeps.</summary>
/// <param name="SpeedFtPerSec">The speed.</param>
/// <param name="StandoffFt">The standoff.</param>
public sealed record Movement(double SpeedFtPerSec, double StandoffFt)
{
    /// <summary>Gets the seconds to cross the standoff once.</summary>
    public double SwitchTime => SpeedFtPerSec > 0.0 ? F32.Round(StandoffFt / SpeedFtPerSec) : double.PositiveInfinity;
}

/// <summary>A summon power with its pets' attacks precomputed.</summary>
public sealed record PetSummon
{
    /// <summary>Gets the summon power name.</summary>
    public required string SummonName { get; init; }

    /// <summary>Gets the pet name.</summary>
    public required string PetName { get; init; }

    /// <summary>Gets one pet's attacks.</summary>
    public required IReadOnlyList<ChainAttack> PetAttacks { get; init; }

    /// <summary>Gets the pet count.</summary>
    public required int PetCount { get; init; }

    /// <summary>Gets the lifetime, infinity for a permanent pet.</summary>
    public required double Duration { get; init; }

    /// <summary>Gets the summon cast time.</summary>
    public required double SummonCastTime { get; init; }

    /// <summary>Gets the summon recharge.</summary>
    public required double SummonRecharge { get; init; }

    /// <summary>Gets the summon endurance cost.</summary>
    public required double SummonEndCost { get; init; }

    /// <summary>Gets 1 plus the pet's damage strength.</summary>
    public double DamageBuffBaseline { get; init; } = 1.0;

    /// <summary>Gets the label.</summary>
    public string Label { get; init; } = string.Empty;
}

/// <summary>Everything a source or modifier reads, built once per evaluation.</summary>
public sealed record DamageContext
{
    /// <summary>Gets the attacks.</summary>
    public required IReadOnlyList<ChainAttack> Attacks { get; init; }

    /// <summary>Gets the endurance budget.</summary>
    public required EnduranceBudget Budget { get; init; }

    /// <summary>Gets the stance.</summary>
    public required Stance Stance { get; init; }

    /// <summary>Gets the scenario.</summary>
    public required Scenario Scenario { get; init; }

    /// <summary>Gets the movement.</summary>
    public required Movement Movement { get; init; }

    /// <summary>Gets the archetype class name.</summary>
    public required string ArchetypeClass { get; init; }

    /// <summary>Gets the named assumptions.</summary>
    public IReadOnlyDictionary<string, double> Assumptions { get; init; } = new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>Gets 1 plus the damage strength already applied.</summary>
    public double DamageBuffBaseline { get; init; } = 1.0;

    /// <summary>Gets the pets.</summary>
    public IReadOnlyList<PetSummon> Pets { get; init; } = [];

    /// <summary>Gets the timeline share left for the chain.</summary>
    public double TimelineAvailable { get; init; } = 1.0;

    /// <summary>Gets the drain of the non-chain sources.</summary>
    public double OtherDrainPerSec { get; init; }

    /// <summary>Gets the form the chain runs in; empty is the base form.</summary>
    public string Form { get; init; } = string.Empty;

    /// <summary>The context's attacks with the given role, in input order.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The attacks.</returns>
    public IReadOnlyList<ChainAttack> ByRole(Role role)
    {
        return [.. Attacks.Where(attack => attack.Profile.Role == role)];
    }

    /// <summary>Reads an assumption.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The default.</param>
    /// <returns>The value.</returns>
    public double Assume(string key, double fallback)
    {
        return Assumptions.TryGetValue(key, out double value) ? value : fallback;
    }
}

/// <summary>One source's contribution: DPS, cast-time share, endurance drain, and a per-item breakdown.</summary>
public sealed record SourceResult
{
    /// <summary>Gets the source name.</summary>
    public required string Source { get; init; }

    /// <summary>Gets the DPS.</summary>
    public required double Dps { get; init; }

    /// <summary>Gets the timeline share.</summary>
    public double TimelineShare { get; init; }

    /// <summary>Gets the endurance per second.</summary>
    public double EndPerSec { get; init; }

    /// <summary>Gets the per-item breakdown.</summary>
    public IReadOnlyList<(string Name, double Dps)> Detail { get; init; } = [];

    /// <summary>Gets the label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the source's own damage baseline, or null for the context's.</summary>
    public double? DamageBuffBaseline { get; init; }
}

/// <summary>One modifier: its multiplier at full uptime, its uptime, and the assumption behind it.</summary>
public sealed record ModifierResult
{
    /// <summary>Gets the name.</summary>
    public required string Modifier { get; init; }

    /// <summary>Gets the multiplier at full uptime.</summary>
    public required double Multiplier { get; init; }

    /// <summary>Gets the uptime.</summary>
    public double Uptime { get; init; } = 1.0;

    /// <summary>Gets the sources it scales, or null for all.</summary>
    public IReadOnlySet<string>? AppliesTo { get; init; }

    /// <summary>Gets the assumption text.</summary>
    public string Assumption { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether this is a damage buff that joins the strength already applied.</summary>
    public bool Additive { get; init; }

    /// <summary>The uptime-weighted factor; an additive buff is divided by the baseline.</summary>
    /// <param name="baseline">1 plus the strength already applied.</param>
    /// <returns>The factor.</returns>
    public double Scale(double baseline = 1.0)
    {
        double bonus = F32.Round((Multiplier - 1.0) * Uptime);
        return Additive ? F32.Round(1.0 + (bonus / baseline)) : F32.Round(1.0 + bonus);
    }
}

/// <summary>The evaluated model.</summary>
/// <param name="Sources">Every source result.</param>
/// <param name="Modifiers">Every modifier result.</param>
/// <param name="ScaledDps">The scaled DPS per source, in source order.</param>
/// <param name="TotalDps">The scaled total.</param>
/// <param name="TotalDrainPerSec">The total drain.</param>
/// <param name="TimeToEmpty">Seconds to empty.</param>
/// <param name="RemainderContext">The context the remainder-filling sources ran on.</param>
public sealed record DamageReport(
    IReadOnlyList<SourceResult> Sources,
    IReadOnlyList<ModifierResult> Modifiers,
    IReadOnlyList<KeyValuePair<string, double>> ScaledDps,
    double TotalDps,
    double TotalDrainPerSec,
    double TimeToEmpty,
    DamageContext RemainderContext);

/// <summary>A registered damage source and whether it fills the remainder of the timeline.</summary>
/// <param name="Run">The source.</param>
/// <param name="FillsRemainder">Whether it runs last.</param>
public sealed record SourceRegistration(Func<DamageContext, SourceResult?> Run, bool FillsRemainder);

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
    /// <exception cref="ChainException">E30 when the non-chain sources claim more than the timeline.</exception>
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
            throw new ChainException(
                $"E30: non-chain damage sources claim {PyFloat.Fixed(share, 3)} of the timeline (> 1.0): [{claims}]; "
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

            scaled.Add(new(result.Source, F32.Round(result.Dps * factor)));
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
