using CohEngine.Core.Registry.HardLimitRules;
using CohEngine.Core.Registry.LegalityRules;
using CohEngine.Core.Registry.ScoringMetrics;
using CohEngine.Core.Registry.SlottingRules;

namespace CohEngine.Core.Registry;

/// <summary>
/// The rule registries as explicit lists, in the order <c>python -m coh_engine rules</c> prints them.
/// A new rule is a new entry in the matching list at the bottom; nothing is discovered by reflection.
/// </summary>
public static class RuleRegistry
{
    /// <summary>Gets the legality rules, in registration order.</summary>
    public static IReadOnlyList<RuleInfo> Legality => [.. LegalityRules.Select(rule => rule.Info)];

    /// <summary>Gets the hard-limit rules, in registration order.</summary>
    public static IReadOnlyList<RuleInfo> HardLimits => [.. HardLimitRules.Select(rule => rule.Info)];

    /// <summary>Gets the slotting rules, in registration order; they run only under <c>validate --slotting</c>, so <c>rules</c> does not list them.</summary>
    public static IReadOnlyList<RuleInfo> Slotting => [.. SlottingRules.Select(rule => rule.Info)];

    /// <summary>Gets the enhancement aspect handlers, sorted by aspect name as the Python lists them.</summary>
    public static IReadOnlyList<RuleInfo> EnhancementAspects { get; } =
    [
        new("Accuracy", "GlobalEnhance.accuracy; uncapped"),
        new("EnduranceDiscount", "GlobalEnhance.end_discount; uncapped"),
        new("Interrupt", "GlobalEnhance.interrupt; uncapped"),
        new("Range", "GlobalEnhance.range; uncapped"),
        new("RechargeTime", "GlobalEnhance.recharge; capped"),
    ];

    /// <summary>Gets the scoring metrics, sorted by name as the Python lists them.</summary>
    public static IReadOnlyList<RuleInfo> Metrics => [.. ScoringMetrics.Select(metric => metric.Info)];

    /// <summary>Gets the scoring metrics with their bodies, sorted by name; a new metric is a new line here.</summary>
    internal static IReadOnlyList<RegisteredMetric> ScoringMetrics { get; } =
    [
        new(new("defense_positional_min", "Lowest of Melee/Ranged/AoE defense as a percentage (the positional softcap metric)."), DefensePositionalMinMetric.Evaluate),
        new(new("perma_dom", "Perma-Domination: Domination's buffed recharge <= its 90s duration."), PermaDominationMetric.Evaluate),
        new(new("perma_hasten", "Perma-Hasten: Hasten's buffed recharge <= its 120s duration."), PermaHastenMetric.Evaluate),
        new(new("resist_min", "Lowest typed resistance (Smashing..Psionic) as a percentage."), ResistMinMetric.Evaluate),
    ];

    /// <summary>Gets the legality rules with their bodies, in registration order; a new rule is a new line here.</summary>
    internal static IReadOnlyList<RegisteredRule<LegalityContext>> LegalityRules { get; } =
    [
        new(new("_rule_placement", "Per-slot placement (``classify_placement``): unknown-enh H-ENH-007, else the verdict."), PlacementRule.Run),
        new(new("_rule_global_unique", "H-ENH-004: a ``Unique`` IO slotted more than once build-wide."), GlobalUniqueRule.Run),
        new(new("_rule_mutex", "H-ENH-005: stealth procs, and superior/attuned versions of one set piece, are mutex."), MutexRule.Run),
        new(new("_rule_lone_set_piece", "P-SET-001: a single piece of a set in one power, earning no set bonus."), LoneSetPieceRule.Run),
        new(new("_rule_per_power_set_dup", "H-ENH-006: the same set piece slotted twice in one power."), PerPowerSetDuplicateRule.Run),
    ];

    /// <summary>Gets the slotting rules with their bodies, in registration order; a new rule is a new line here.</summary>
    internal static IReadOnlyList<RegisteredRule<SlottingContext>> SlottingRules { get; } =
    [
        new(new("P-ED-001", "A plain IO slotted past the last Enhancement Diversification threshold counts at 15% of its value."), EdOverSlotting.RunPastThreshold),
        new(new("P-ED-002", "Three or more copies of one plain IO that two boosted copies would match within five points."), EdOverSlotting.RunOverBoosting),
        new(new("P-SET-002", "A lone Luck of the Gambler recharge piece while the build holds slots that add little."), LoneGamblerRecharge.Run),
        new(new("P-IO-002", "A resistance toggle holding generic IOs while the resistance uniques are missing."), ResistanceMule.Run),
        new(new("P-IO-003", "Health with survivability uniques the build leaves out."), HealthUniques.Run),
        new(new("P-POWER-001", "A movement-and-recharge auto power picked after the level it opens at."), LateQualityOfLifePick.Run),
        new(new("P-SET-003", "A lower-tier set beside an unused rarer set of the same category."), RarerSetUnused.Run),
    ];

    /// <summary>Gets the hard-limit rules with their bodies, in registration order; a new rule is a new line here.</summary>
    internal static IReadOnlyList<RegisteredRule<HardLimitsContext>> HardLimitRules { get; } =
    [
        new(new("_rule_total_added_slots", "H-SLOT-001: total added slots across the build must not exceed the cap (67)."), TotalAddedSlotsRule.Run),
        new(new("_rule_per_power_added_slots", "H-SLOT-002: a power may hold at most 5 added slots (1 inherent + 5 = 6 total)."), PerPowerAddedSlotsRule.Run),
        new(new("_rule_slot_after_power", "H-SLOT-003: an added slot cannot be placed before its power is available (its pick level)."), SlotAfterPowerRule.Run),
        new(new("_rule_slot_on_grant_level", "H-SLOT-004: an added slot must land on a real slot-grant level in the schedule."), SlotOnGrantLevelRule.Run),
        new(new("_rule_slot_budget_over_time", "H-SLOT-005: a slot cannot be placed before it is earned (cumulative budget)."), SlotBudgetOverTimeRule.Run),
        new(new("_rule_power_pick_count", "H-POWER-001: a build may pick at most 24 powers (inherents excluded)."), PowerPickCountRule.Run),
        new(new("_rule_first_available_level", "H-POWER-002: a power is picked at or after its earliest level (DB ``Level``)."), FirstAvailableLevelRule.Run),
        new(new("_rule_pick_at_grant_level", "H-POWER-003: each pick sits on the schedule level of its own position among the 24."), PickAtGrantLevelRule.Run),
        new(new("_rule_pool_count", "H-POOL-001: a build may draw from at most 4 power pools (Ancillary/Patron excluded)."), PoolCountRule.Run),
        new(new("_rule_prerequisites", "Power prerequisites (``Build.MeetsRequirement`` steps C/D): required and forbidden picks."), PrerequisitesRule.Run),
        new(new("_rule_single_ancillary_pool", "H-POOL-002: at most one Ancillary/Patron (Epic) pool may be selected."), SingleAncillaryPoolRule.Run),
        new(new("_rule_mbd_powersets", "H-FMT-001: ``PowerSets`` must have 8 entries, or MidsReborn fails at ``Character.Reset``."), MbdPowersetsRule.Run),
        new(new("_rule_mbd_io_levels", "H-FMT-002: each IoLevel lies within its enhancement's bounds; MidsReborn clamps it on load otherwise."), MbdIoLevelsRule.Run),
        new(new("_rule_mbd_level_text", "H-FMT-003: the header ``Level`` is the character level minus one, as text from \"0\" to \"49\"."), MbdLevelTextRule.Run),
    ];
}
