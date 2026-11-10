using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;

namespace CohEngine.Core.DamageLayers;

/// <summary>Archetype-inherent damage modifiers: expected-value approximations read from the assumptions.</summary>
public static class AtModifiers
{
    private const string Canon = "data/canonical/powers/inherent/inherent";
    private const string Approx = "approximate; verify in MidsReborn";

    private static readonly HashSet<string> Character = new(StringComparer.Ordinal) { DamageModel.SourceChain, DamageModel.SourceAura, DamageModel.SourcePatch };

    private static readonly (string Class, string Rule, Func<DamageContext, ModifierResult> Run)[] Rules =
    [
        ("Class_Brute", "brute_fury", BruteFury),
        ("Class_Controller", "controller_containment", ControllerContainment),
        ("Class_Corruptor", "corruptor_scourge", CorruptorScourge),
        ("Class_Blaster", "blaster_defiance", BlasterDefiance),
        ("Class_Defender", "defender_vigilance", DefenderVigilance),
        ("Class_Scrapper", "scrapper_critical_hit", ScrapperCriticalHit),
        ("Class_Stalker", "stalker_assassination", StalkerAssassination),
        ("Class_Sentinel", "sentinel_opportunity", SentinelOpportunity),
        ("Class_Mastermind", "mastermind_supremacy", MastermindSupremacy),
        ("Class_Peacebringer", "peacebringer_cosmic_balance", PeacebringerCosmicBalance),
        ("Class_Warshade", "warshade_dark_sustenance", WarshadeDarkSustenance),
        ("Class_Dominator", "dominator_domination", DominatorDomination),
        ("Class_Tanker", "tanker_gauntlet", TankerGauntlet),
        ("Class_Arachnos_Soldier", "arachnos_soldier_conditioning", ArachnosSoldierConditioning),
        ("Class_Arachnos_Widow", "arachnos_widow_conditioning", ArachnosWidowConditioning),
    ];

    /// <summary>Gets the archetype class name to the name of the rule that handles it.</summary>
    public static IReadOnlyDictionary<string, string> RuleByClass { get; } = Rules.ToDictionary(rule => rule.Class, rule => rule.Rule, StringComparer.Ordinal);

    /// <summary>Gets the registered rules in registration order; each returns null off its class.</summary>
    public static IReadOnlyList<Func<DamageContext, ModifierResult?>> Functions { get; } =
        [.. Rules.Select(rule => (Func<DamageContext, ModifierResult?>)(context => string.Equals(context.ArchetypeClass, rule.Class, StringComparison.Ordinal) ? rule.Run(context) : null))];

    private static double Param(DamageContext context, string cls, string name, double fallback, double low, double high)
    {
        string key = $"{cls}.{name}";
        double value = context.Assume(key, fallback);
        if (!(low <= value && value <= high))
        {
            throw new EngineException($"{ErrorCodes.E50}: assumption {key}={PyFloat.Repr(value)} is outside [{PyFloat.Repr(low)}, {PyFloat.Repr(high)}]; set it within that range");
        }

        return value;
    }

    private static ModifierResult Neutral(string rule, string cls, string why)
    {
        return new ModifierResult { Modifier = rule, Multiplier = 1.0, Assumption = $"{cls}: neutral, {why}" };
    }

    private static ModifierResult DominatorDomination(DamageContext context)
    {
        return Neutral(
            "dominator_domination",
            "Class_Dominator",
            $"Domination grants +25% to-hit, endurance refill and control magnitude, no damage effect ({Canon}/domination.json); to-hit and control are not modelled");
    }

    private static ModifierResult TankerGauntlet(DamageContext context)
    {
        return Neutral("tanker_gauntlet", "Class_Tanker", $"Gauntlet only grants/revokes the taunt power, no damage effect ({Canon}/gauntlet.json)");
    }

    private static ModifierResult ArachnosSoldierConditioning(DamageContext context)
    {
        return Neutral("arachnos_soldier_conditioning", "Class_Arachnos_Soldier", $"Conditioning adds regeneration and endurance, no damage effect ({Canon}/spider_conditioning.json)");
    }

    private static ModifierResult ArachnosWidowConditioning(DamageContext context)
    {
        return Neutral("arachnos_widow_conditioning", "Class_Arachnos_Widow", $"Conditioning adds regeneration and endurance, no damage effect ({Canon}/widow_conditioning.json)");
    }

    private static ModifierResult BruteFury(DamageContext context)
    {
        double level = Param(context, "Class_Brute", "fury_level", 75.0, 0.0, 100.0);
        return new ModifierResult
        {
            Modifier = "brute_fury",
            AppliesTo = Character,
            Additive = true,
            Multiplier = F32.Round(1.0 + (0.02 * level)),
            Assumption = $"Class_Brute.fury_level={PyFloat.Repr(level)} of 0..100 (default 75: {Approx}); +2% per point from {Canon}/rage_buff.json, meter range 0..100 assumed",
        };
    }

    private static ModifierResult ControllerContainment(DamageContext context)
    {
        double share = Param(context, "Class_Controller", "contained_share", 0.5, 0.0, 1.0);
        return new ModifierResult
        {
            Modifier = "controller_containment",
            AppliesTo = Character,
            Multiplier = F32.Round(1.0 + share),
            Assumption = $"Class_Controller.contained_share={PyFloat.Repr(share)} of damage lands on controlled foes (default 0.5, {Approx}); "
                + $"containment adds +100% ({Approx}; {Canon}/containment.json has no magnitude)",
        };
    }

    private static ModifierResult CorruptorScourge(DamageContext context)
    {
        double chance = Param(context, "Class_Corruptor", "scourge_chance", 0.10, 0.0, 1.0);
        return new ModifierResult
        {
            Modifier = "corruptor_scourge",
            AppliesTo = Character,
            Multiplier = F32.Round(1.0 + chance),
            Assumption = $"Class_Corruptor.scourge_chance={PyFloat.Repr(chance)} averaged over the fight (default 0.10, {Approx}); "
                + $"scourge hits deal up to double damage per {Canon}/scourge.json help text",
        };
    }

    private static ModifierResult BlasterDefiance(DamageContext context)
    {
        double bonus = Param(context, "Class_Blaster", "damage_bonus", 0.30, 0.0, 5.0);
        return new ModifierResult
        {
            Modifier = "blaster_defiance",
            AppliesTo = Character,
            Additive = true,
            Multiplier = F32.Round(1.0 + bonus),
            Assumption = $"Class_Blaster.damage_bonus={PyFloat.Repr(bonus)} average stacked bonus (default 0.30, {Approx}); "
                + $"{Canon}/defiance.json gives only the 7.5 s stacking description",
        };
    }

    private static ModifierResult DefenderVigilance(DamageContext context)
    {
        double team = Param(context, "Class_Defender", "team_size", 1.0, 1.0, 8.0);
        double bonus = Math.Max(0.0, 0.3 - (0.1 * Math.Min(team - 1.0, 3.0)));
        return new ModifierResult
        {
            Modifier = "defender_vigilance",
            AppliesTo = Character,
            Additive = true,
            Multiplier = F32.Round(1.0 + bonus),
            Assumption = $"Class_Defender.team_size={PyFloat.Repr(team)} (default 1, solo); +30% less 10% per extra teammate up to 3, from {Canon}/vigilance.json",
        };
    }

    private static ModifierResult ScrapperCriticalHit(DamageContext context)
    {
        double chance = Param(context, "Class_Scrapper", "crit_chance", 0.10, 0.0, 1.0);
        double mult = Param(context, "Class_Scrapper", "crit_damage_mult", 2.0, 1.0, 4.0);
        return new ModifierResult
        {
            Modifier = "scrapper_critical_hit",
            AppliesTo = Character,
            Multiplier = F32.Round(1.0 + (chance * (mult - 1.0))),
            Assumption = $"Class_Scrapper.crit_chance={PyFloat.Repr(chance)} (default 0.10, {Approx}; rank-dependent), "
                + $"crit_damage_mult={PyFloat.Repr(mult)} (double damage per {Canon}/critical_hit.json help text)",
        };
    }

    private static ModifierResult StalkerAssassination(DamageContext context)
    {
        double team = Param(context, "Class_Stalker", "team_size", 1.0, 1.0, 8.0);
        double mult = Param(context, "Class_Stalker", "crit_damage_mult", 2.0, 1.0, 4.0);
        double chance = Math.Min(1.0, 0.10 + (0.03 * team));
        return new ModifierResult
        {
            Modifier = "stalker_assassination",
            AppliesTo = Character,
            Multiplier = F32.Round(1.0 + (chance * (mult - 1.0))),
            Assumption = $"Class_Stalker.team_size={PyFloat.Repr(team)} (default 1) gives crit chance {PyFloat.Fixed(chance, 2)} = 0.10 + 0.03 per member "
                + $"from {Canon}/assassination.json; crit_damage_mult={PyFloat.Repr(mult)} (default 2.0, {Approx}); "
                + "Hide guarantees crits and is not modelled",
        };
    }

    private static ModifierResult SentinelOpportunity(DamageContext context)
    {
        double sentinels = Param(context, "Class_Sentinel", "sentinels", 1.0, 0.0, 8.0);
        return new ModifierResult
        {
            Modifier = "sentinel_opportunity",
            AppliesTo = Character,
            Additive = true,
            Multiplier = F32.Round(1.0 + (0.05 * sentinels)),
            Assumption = $"Class_Sentinel.sentinels={PyFloat.Repr(sentinels)} focusing the target (default 1); +5% each from {Canon}/scrutiny.json help text, full uptime assumed",
        };
    }

    private static ModifierResult MastermindSupremacy(DamageContext context)
    {
        double share = Param(context, "Class_Mastermind", "pets_in_range", 1.0, 0.0, 1.0);
        return new ModifierResult
        {
            Modifier = "mastermind_supremacy",
            Additive = true,
            Multiplier = F32.Round(1.0 + (0.25 * share)),
            AppliesTo = new HashSet<string>(StringComparer.Ordinal) { DamageModel.SourcePet },
            Assumption = $"Class_Mastermind.pets_in_range={PyFloat.Repr(share)} share of pets within range (default 1); +25% damage from {Canon}/supremacy.json",
        };
    }

    private static ModifierResult PeacebringerCosmicBalance(DamageContext context)
    {
        double allies = Param(context, "Class_Peacebringer", "damage_allies", 0.0, 0.0, 7.0);
        return new ModifierResult
        {
            Modifier = "peacebringer_cosmic_balance",
            AppliesTo = Character,
            Additive = true,
            Multiplier = F32.Round(1.0 + (0.2 * allies)),
            Assumption = $"Class_Peacebringer.damage_allies={PyFloat.Repr(allies)} nearby Tanker/Mastermind/Corruptor/Defender (default 0, solo); +20% each from {Canon}/cosmic_balance.json",
        };
    }

    private static ModifierResult WarshadeDarkSustenance(DamageContext context)
    {
        double allies = Param(context, "Class_Warshade", "damage_allies", 0.0, 0.0, 7.0);
        return new ModifierResult
        {
            Modifier = "warshade_dark_sustenance",
            AppliesTo = Character,
            Additive = true,
            Multiplier = F32.Round(1.0 + (0.2 * allies)),
            Assumption = $"Class_Warshade.damage_allies={PyFloat.Repr(allies)} nearby Scrapper/Sentinel/Stalker/Brute/Blaster (default 0, solo); "
                + $"+20% each from {Canon}/dark_sustenance.json (the Warshade's Cosmic Balance counterpart)",
        };
    }
}
