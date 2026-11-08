"""Goal scoring: metrics, per-profile evaluation, and the cap / endurance warnings.

Scores the DM/Shield set-bonus fixture against the canonical profiles (it is a
survivability build, so it correctly falls short of the 45% positional softcap and of
perma-Hasten), and exercises the user-directed adherence warnings: over-cap totals
(game-clamped, so a warning not an error) and endurance drain exceeding recovery.
"""

from pathlib import Path

import pytest

from coh_engine.archetypes import ArchetypeDb, load_archetypes
from coh_engine.attribmod import AttribMods, load_attribmod
from coh_engine.base_totals import (
    BaseTotals,
    EngineConfig,
    EnumMaps,
    ServerData,
    TotalStatistics,
    compute_base_totals,
    load_engine_config,
    load_enum_maps,
    load_server_data,
)
from coh_engine.effect import Power, load_powers_effects
from coh_engine.enhancement import EnhancementRecord, SlotRef, load_build_slots, load_enhancement_effects
from coh_engine.maths import MathTables, load_maths
from coh_engine.profiles import BuildProfile, Target, load_breakpoints, load_build_profiles, resolve_profile
from coh_engine.scoring import (
    defense_positional_min,
    endurance_diagnostic,
    endurance_recovery_per_sec,
    perma_dom,
    perma_hasten,
    resist_min,
    score_build,
)
from coh_engine.set_bonuses import SetBonusDb, load_set_bonus_db
from coh_engine.statistics import PowerStats, StatsContext, compute_build_stats

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES.parents[3] / "tests" / "fixtures" / "mids"
STRATEGY = Path(__file__).parents[3] / "strategy"
BUILD = "shield_scrapper_set_bonuses"


@pytest.fixture(scope="module")
def classes() -> ArchetypeDb:
    return load_archetypes(MIDS / "archetypes.json")


@pytest.fixture(scope="module")
def scrapper_index(classes: ArchetypeDb) -> int:
    return classes.nid_from_uid_class("Class_Scrapper")


@pytest.fixture(scope="module")
def computed(classes: ArchetypeDb) -> tuple[BaseTotals, list[PowerStats]]:
    """The fixture build's totals + per-power stats (one real end-to-end compute)."""
    mods: AttribMods = load_attribmod(FIXTURES / "attribmod" / "AttribMod.json")
    enums: EnumMaps = load_enum_maps(MIDS / "enums.json")
    config: EngineConfig = load_engine_config(MIDS / "config.json")
    server: ServerData = load_server_data(MIDS / "server_data.json")
    tables: MathTables = load_maths(FIXTURES.parents[3] / "tests" / "fixtures" / "Maths.mhd")
    enh_db: dict[int, EnhancementRecord] = dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))
    set_db: SetBonusDb = load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json")
    powers: tuple[Power, ...] = load_powers_effects(MIDS / "builds" / BUILD / "powers_effects.json")
    slots: dict[int, tuple[SlotRef, ...]] = dict(load_build_slots(MIDS / "builds" / BUILD / "slots.json"))
    base = compute_base_totals(
        powers,
        class_name="Class_Scrapper",
        mods=mods,
        classes=classes,
        enums=enums,
        config=config,
        server=server,
        slots=slots,
        enh_db=enh_db,
        tables=tables,
        set_db=set_db,
    )
    at_index = classes.nid_from_uid_class("Class_Scrapper")
    ctx = StatsContext(
        mods=mods,
        classes=classes,
        archetype_index=at_index,
        config=config,
        tables=tables,
        enh_db=enh_db,
        global_enhance=base.global_enhance,
        recharge_cap=classes.classes[at_index].recharge_cap,
    )
    return base, compute_build_stats(powers, slots, ctx)


def test_positional_defense_min_is_the_lowest_of_melee_ranged_aoe(
    computed: tuple[BaseTotals, list[PowerStats]],
) -> None:
    """Positional softcap metric is min(Melee, Ranged, AoE) as a percentage."""
    base, _ = computed
    assert defense_positional_min(base.totals) == pytest.approx(14.47, abs=0.1)


def test_dm_shield_is_not_positionally_softcapped(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """The survivability fixture sits below the 45% positional softcap (Melee is the gap)."""
    base, _ = computed
    assert defense_positional_min(base.totals) < 45.0


def test_resist_min_is_below_the_scrapper_cap(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """Typed resistance min is well under the 75% Scrapper cap."""
    base, _ = computed
    assert resist_min(base.totals) < 75.0


def test_hasten_is_not_perma_in_this_build(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """Hasten recharge 315.789 > 120s duration, so perma-Hasten is not met."""
    _, stats = computed
    assert perma_hasten(stats) is False


def test_score_soft_cap_profile_reports_unmet_with_a_warning(
    computed: tuple[BaseTotals, list[PowerStats]],
) -> None:
    """Scoring soft-cap-defense on this build yields score 0 and an unmet-goal warning."""
    base, stats = computed
    raw = dict(load_build_profiles(STRATEGY / "build_profiles.json"))
    bp = dict(load_breakpoints(STRATEGY / "breakpoints.json"))
    profile = resolve_profile("soft-cap-defense", raw, bp, at_key="Scrapper")
    result = score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))
    assert result.score == 0.0
    assert result.met == {"defense_positional_min": False}
    assert any(d.rule_id == "P-GOAL-002" and d.severity == "warning" for d in result.diagnostics)


def test_theme_profile_scores_full_with_no_targets(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """A profile with no hard targets scores 1.0 (only the universal gate applies)."""
    base, stats = computed
    raw = dict(load_build_profiles(STRATEGY / "build_profiles.json"))
    bp = dict(load_breakpoints(STRATEGY / "breakpoints.json"))
    profile = resolve_profile("theme-rp", raw, bp, at_key="Scrapper")
    result = score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))
    assert result.score == 1.0
    assert result.met == {}


def test_endurance_is_sustainable_for_the_fixture(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """Recovery (~2.08/s) exceeds toggle usage (~1.61/s), so no endurance warning fires."""
    base, _ = computed
    recovery = endurance_recovery_per_sec(base.totals_capped, _scrapper(base))
    assert recovery > base.totals.end_use
    assert endurance_diagnostic(base.totals, base.totals_capped, _scrapper(base)) is None


def _scrapper(base: BaseTotals):  # type: ignore[no-untyped-def]
    from coh_engine.archetypes import load_archetypes

    classes = load_archetypes(MIDS / "archetypes.json")
    return classes.classes[classes.nid_from_uid_class("Class_Scrapper")]


def test_endurance_drain_exceeding_recovery_warns(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """A build whose toggle drain exceeds recovery gets a P-END-001 warning (not an error)."""
    base, _ = computed
    heavy = _replace_end_use(base.totals, 99.0)  # absurd drain
    diag = endurance_diagnostic(heavy, base.totals_capped, _scrapper(base))
    assert diag is not None
    assert diag.rule_id == "P-END-001" and diag.severity == "warning"
    assert "toggles" in diag.message  # no chain drain -> toggle-only detail


def test_attack_chain_drain_adds_to_toggle_drain(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """Toggles alone are sustainable, but toggles + a chain drain that tips over recovery warns."""
    base, _ = computed
    assert endurance_diagnostic(base.totals, base.totals_capped, _scrapper(base)) is None  # toggles OK
    diag = endurance_diagnostic(base.totals, base.totals_capped, _scrapper(base), chain_drain=1.0)
    assert diag is not None
    assert diag.rule_id == "P-END-001"
    assert "chain 1.000" in diag.message  # the composed toggle + chain detail


def test_score_build_sustainable_build_has_no_endurance_warning(
    computed: tuple[BaseTotals, list[PowerStats]],
) -> None:
    """With no attacks and sustainable toggles, score_build emits no P-END-001 (the None arm)."""
    base, _ = computed
    profile = BuildProfile(name="x", display_name="X", priority=(), targets=())
    result = score_build(profile, base.totals, base.totals_capped, [], _scrapper(base))  # no attacks -> no chain drain
    assert not any(d.rule_id == "P-END-001" for d in result.diagnostics)


def test_score_build_folds_the_attack_chain_into_endurance(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """score_build derives the chain from the power stats and includes it in the P-END-001 check."""
    base, stats = computed
    from coh_engine.attack_chain import build_attack_chain

    chain = build_attack_chain(stats)
    profile = BuildProfile(name="x", display_name="X", priority=(), targets=())
    result = score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))
    recovery = endurance_recovery_per_sec(base.totals_capped, _scrapper(base))
    fires = any(d.rule_id == "P-END-001" for d in result.diagnostics)
    assert fires == (base.totals.end_use + chain.end_per_sec > recovery)


def test_over_cap_total_warns_not_errors(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """Uncapped resistance above the AT cap yields a P-CAP-001 warning, never an error."""
    base, stats = computed
    raw = dict(load_build_profiles(STRATEGY / "build_profiles.json"))
    bp = dict(load_breakpoints(STRATEGY / "breakpoints.json"))
    profile = resolve_profile("resist-cap", raw, bp, at_key="Scrapper")
    over = _bump_first_res(base.totals, 0.99)  # 99% S/L resist, above the 75% cap
    result = score_build(profile, over, base.totals_capped, stats, _scrapper(base))
    caps = [d for d in result.diagnostics if d.rule_id == "P-CAP-001"]
    assert caps and all(d.severity == "warning" for d in caps)
    assert not any(d.severity == "error" for d in result.diagnostics)


def test_perma_dom_is_false_without_a_domination_power(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """A Scrapper build has no Domination, so perma-Dom is False (power not found)."""
    _, stats = computed
    assert perma_dom(stats) is False


def test_le_op_target_is_met(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """A ``<=`` target evaluates correctly (resist min <= 100 always holds)."""
    base, stats = computed
    profile = BuildProfile(
        name="x", display_name="X", priority=(), targets=(Target(metric="resist_min", op="<=", value=100.0),)
    )
    result = score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))
    assert result.met == {"resist_min": True}
    assert result.score == 1.0


def test_unknown_metric_fails_loud(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """A profile target naming an unimplemented metric is refused (E17)."""
    base, stats = computed
    profile = BuildProfile(
        name="x", display_name="X", priority=(), targets=(Target(metric="bogus", op=">=", value=1.0),)
    )
    with pytest.raises(ValueError, match="E17"):
        score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))


def test_unknown_op_fails_loud(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """A target with an unsupported comparison op is refused (E18)."""
    base, stats = computed
    profile = BuildProfile(
        name="x", display_name="X", priority=(), targets=(Target(metric="resist_min", op="!=", value=1.0),)
    )
    with pytest.raises(ValueError, match="E18"):
        score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))


def test_scalar_over_cap_warns(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """An uncapped scalar (BuffHaste) above the AT recharge cap yields a P-CAP-001 warning."""
    base, _ = computed
    from dataclasses import replace

    over = replace(base.totals, buff_haste=99.0)  # far above recharge_cap - 1
    diags = [d for d in _cap_only(over, base) if d.rule_id == "P-CAP-001"]
    assert diags and any("BuffHaste" in d.message for d in diags)


def test_score_perma_hasten_profile_reports_unmet(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """Scoring perma-hasten routes the boolean metric through score_build (unmet here)."""
    base, stats = computed
    raw = dict(load_build_profiles(STRATEGY / "build_profiles.json"))
    bp = dict(load_breakpoints(STRATEGY / "breakpoints.json"))
    profile = resolve_profile("perma-hasten", raw, bp, at_key="Scrapper")
    result = score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))
    assert result.met == {"perma_hasten": False}
    assert result.score == 0.0


def test_two_targets_on_one_metric_both_count_toward_score(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """Two targets on the same metric each count independently (score is over targets, not the met dict)."""
    base, stats = computed
    profile = BuildProfile(
        name="x",
        display_name="X",
        priority=(),
        targets=(
            Target(metric="resist_min", op=">=", value=0.0),  # met (resist_min ~12.75 >= 0)
            Target(metric="resist_min", op="<=", value=100.0),  # met (12.75 <= 100)
        ),
    )
    result = score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))
    assert result.score == 1.0  # both met — not 0.5 from a collided single dict entry


def test_score_perma_dom_metric(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """The perma_dom metric routes through score_build (False for a Scrapper build)."""
    base, stats = computed
    profile = BuildProfile(
        name="x", display_name="X", priority=(), targets=(Target(metric="perma_dom", op="==", value=True),)
    )
    result = score_build(profile, base.totals, base.totals_capped, stats, _scrapper(base))
    assert result.met == {"perma_dom": False}


def test_score_build_appends_endurance_warning(computed: tuple[BaseTotals, list[PowerStats]]) -> None:
    """score_build folds the endurance shortfall warning into its diagnostics."""
    base, stats = computed
    profile = BuildProfile(name="x", display_name="X", priority=(), targets=())
    heavy = _replace_end_use(base.totals, 99.0)
    result = score_build(profile, heavy, base.totals_capped, stats, _scrapper(base))
    assert any(d.rule_id == "P-END-001" for d in result.diagnostics)


def _cap_only(totals: TotalStatistics, base: BaseTotals) -> list:  # type: ignore[type-arg]
    from coh_engine.scoring import cap_adherence_diagnostics

    return cap_adherence_diagnostics(totals, base.totals_capped)


def _replace_end_use(totals: TotalStatistics, value: float) -> TotalStatistics:
    from dataclasses import replace

    return replace(totals, end_use=value)


def _bump_first_res(totals: TotalStatistics, value: float) -> TotalStatistics:
    from dataclasses import replace

    res = list(totals.res)
    res[1] = value
    return replace(totals, res=res)
