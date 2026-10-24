"""Damage sources: chain, aura, patch, buff cast; plus the end-to-end fold with the modifiers."""

from dataclasses import replace

import pytest

from coh_engine.attack_chain import arcana_cast_time
from coh_engine.damage_model import (
    AttackProfile,
    ChainAttack,
    DamageContext,
    Delivery,
    EnduranceBudget,
    Movement,
    Role,
    Scenario,
    Shape,
    SourceResult,
    Stance,
    evaluate_damage,
)
from coh_engine.damage_modifiers import click_buff_modifier, target_debuff_modifier
from coh_engine.damage_sources import (
    attacks_for_form,
    aura_source,
    buff_cast_source,
    chain_source,
    chosen_chain,
    melee_share,
    patch_source,
)


def _attack(
    name: str,
    role: Role = Role.CHAIN,
    delivery: Delivery = Delivery.RANGED,
    shape: Shape = Shape.SINGLE,
    damage: float = 100.0,
    cast: float = 1.0,
    recharge: float = 1.0,
    end: float = 5.0,
    **kw: float,
) -> ChainAttack:
    profile = AttackProfile(name, delivery, shape, role, 80.0, 0.0, 0.0, 0)
    return ChainAttack(name, damage, cast, recharge, end, profile, **kw)


def _ctx(*attacks: ChainAttack, **kw: object) -> DamageContext:
    base: dict[str, object] = {
        "attacks": attacks,
        "budget": EnduranceBudget(max_end=100.0, recovery_per_sec=10.0, toggle_drain_per_sec=0.0),
        "stance": Stance.MELEE,
        "scenario": Scenario(targets=3),
        "movement": Movement(speed_ft_per_sec=20.0, standoff_ft=40.0),
        "archetype_class": "Class_Test",
    }
    base.update(kw)
    return DamageContext(**base)  # type: ignore[arg-type]


def _aura(period: float = 2.0) -> ChainAttack:
    return _attack("Aura", Role.AURA, Delivery.MELEE, Shape.PBAOE, damage=10.0, activate_period=period)


def test_none_without_matching_attacks() -> None:
    """Every source returns None when its role is absent."""
    ctx = _ctx(_attack("Bolt", Role.PET_SUMMON))
    assert chain_source(ctx) is None
    assert chosen_chain(ctx) is None
    assert aura_source(ctx) is None
    assert patch_source(ctx) is None
    assert buff_cast_source(ctx) is None


def test_chain_source_sustainable_pick() -> None:
    """The chain reports the planner's rotation, per-attack DPS in rotation order, and a sustainable label."""
    a, b = _attack("A", damage=100.0), _attack("B", damage=60.0, recharge=3.0)
    ctx = _ctx(a, b, timeline_available=0.5)
    option = chosen_chain(ctx)
    assert option is not None
    assert option.sustainable
    result = chain_source(ctx)
    assert result is not None
    assert result.source == "chain"
    assert result.dps == option.dps
    assert result.end_per_sec == option.end_per_sec
    assert [n for n, _ in result.detail] == list(option.rotation)
    by_name = dict(result.detail)
    assert by_name["A"] == pytest.approx(100.0 / option.cycle_time * 0.5, rel=1e-5)
    assert sum(by_name.values()) == pytest.approx(option.dps, rel=1e-5)
    assert "sustainable" in result.label
    assert "not sustainable" not in result.label
    assert "#31" in result.label


def test_chain_falls_back_to_best_overall() -> None:
    """When no option lasts its minimum cycles the pick is the highest-DPS option, labelled as such."""
    ctx = _ctx(
        _attack("Heavy", end=500.0),
        budget=EnduranceBudget(max_end=100.0, recovery_per_sec=1.0, toggle_drain_per_sec=0.0),
    )
    option = chosen_chain(ctx)
    assert option is not None
    assert not option.sustainable
    result = chain_source(ctx)
    assert result is not None
    assert "not affordable (best overall)" in result.label


def test_chain_label_names_an_affordable_draining_pick() -> None:
    """A draining pick that lasts its minimum cycles is labelled affordable, with its cycle count."""
    # Cycle 2.188 s drains 5 / 2.188 = 2.29/s against 2.0 recovery: empty in ~351 s, about 160 cycles.
    ctx = _ctx(_attack("A"), budget=EnduranceBudget(max_end=100.0, recovery_per_sec=2.0, toggle_drain_per_sec=0.0))
    result = chain_source(ctx)
    assert result is not None
    assert "affordable: empties in" in result.label
    assert "cycles" in result.label


_AURA_FULL = 10.0 * 3 / 2.0  # damage x foes in reach / tick period


def _melee_hit(name: str = "M") -> ChainAttack:
    return _attack(name, delivery=Delivery.MELEE, recharge=10.0)


def _ranged_hit(name: str = "R") -> ChainAttack:
    return _attack(name, recharge=10.0)


def test_aura_melee_counts_fully_and_ranged_drops_it() -> None:
    """MELEE counts the aura fully; RANGED drops it."""
    full = aura_source(_ctx(_aura()))
    assert full is not None
    assert full.dps == pytest.approx(_AURA_FULL)
    assert full.end_per_sec == 0.0
    assert full.timeline_share == 0.0
    assert full.detail == (("Aura", full.dps),)
    assert aura_source(_ctx(_aura(), stance=Stance.RANGED)) is None


def test_hybrid_aura_share_is_zero_for_an_all_ranged_rotation() -> None:
    """A rotation with no melee-position attack never stands where the aura reaches."""
    assert aura_source(_ctx(_aura(), _ranged_hit(), stance=Stance.HYBRID)) is None


def test_hybrid_aura_share_is_zero_without_any_chain_attack() -> None:
    """With no chain attack there is no rotation to stand in melee for."""
    assert aura_source(_ctx(_aura(), stance=Stance.HYBRID)) is None


def test_hybrid_aura_share_is_one_for_an_all_melee_rotation() -> None:
    """A rotation of melee-position attacks only keeps the character in melee all cycle."""
    result = aura_source(_ctx(_aura(), _melee_hit(), stance=Stance.HYBRID))
    assert result is not None
    assert result.dps == pytest.approx(_AURA_FULL)


def test_hybrid_aura_share_of_a_mixed_rotation_is_melee_time_over_cycle() -> None:
    """Melee casts plus half the move time, over the cycle."""
    ctx = _ctx(_aura(), _melee_hit(), _ranged_hit(), stance=Stance.HYBRID)
    chain = chosen_chain(ctx)
    assert chain is not None
    assert chain.rotation == ("M", "R")
    cast = arcana_cast_time(1.0)
    expected = (cast + chain.move_time / 2.0) / chain.cycle_time
    assert 0.0 < expected < 1.0
    result = aura_source(ctx)
    assert result is not None
    assert result.dps == pytest.approx(_AURA_FULL * expected, rel=1e-4)


def test_hybrid_melee_share_assumption_overrides_the_rotation() -> None:
    """``hybrid.melee_share`` wins when present, even over an all-ranged rotation."""
    ctx = _ctx(_aura(), _ranged_hit(), stance=Stance.HYBRID, assumptions={"hybrid.melee_share": 0.25})
    result = aura_source(ctx)
    assert result is not None
    assert result.dps == pytest.approx(_AURA_FULL * 0.25)


def test_aura_errors() -> None:
    """A zero tick period is E55; a melee share outside 0..1 is E56."""
    with pytest.raises(ValueError, match="E55"):
        aura_source(_ctx(_aura(period=0.0)))
    with pytest.raises(ValueError, match="E56"):
        aura_source(_ctx(_aura(), stance=Stance.HYBRID, assumptions={"hybrid.melee_share": 1.5}))


def test_patch_source_cadence() -> None:
    """A patch casts on cooldown: DPS over the full cycle, cast share of the cycle, end over the cycle."""
    patch = _attack(
        "Patch", Role.PATCH, Delivery.RANGED, Shape.LOCATION, damage=50.0, cast=1.0, recharge=10.0, end=12.0
    )
    result = patch_source(_ctx(patch, stance=Stance.RANGED))
    cast = arcana_cast_time(1.0)
    cycle = cast + 10.0
    assert result is not None
    assert result.source == "patch"
    assert result.dps == pytest.approx(50.0 * 3 / cycle, rel=1e-5)
    assert result.timeline_share == pytest.approx(cast / cycle, rel=1e-5)
    assert result.end_per_sec == pytest.approx(12.0 / cycle, rel=1e-5)
    assert result.detail[0][0] == "Patch"


def test_patch_without_damage_is_not_charged_timeline_or_endurance() -> None:
    """A decoy or placed device deals nothing here, so it takes no cast share or endurance."""
    decoy = _attack("Decoy", Role.PATCH, Delivery.RANGED, Shape.LOCATION, damage=0.0, recharge=10.0, end=12.0)
    assert patch_source(_ctx(decoy)) is None
    real = _attack("Patch", Role.PATCH, Delivery.RANGED, Shape.LOCATION, damage=50.0, recharge=10.0, end=12.0)
    both = patch_source(_ctx(decoy, real))
    alone = patch_source(_ctx(real))
    assert both is not None
    assert alone is not None
    assert both == alone
    assert [n for n, _ in both.detail] == ["Patch"]


def test_buff_cast_source() -> None:
    """A buff click deals no damage but takes its cast share and endurance."""
    buff = _attack("Buff", Role.BUFF_CLICK, end=8.0, cast=1.0, recharge=10.0)
    result = buff_cast_source(_ctx(buff))
    cast = arcana_cast_time(1.0)
    assert result is not None
    assert result.source == "buff_cast"
    assert result.dps == 0.0
    assert result.timeline_share == pytest.approx(cast / (cast + 10.0), rel=1e-5)
    assert result.end_per_sec == pytest.approx(8.0 / (cast + 10.0), rel=1e-5)


def test_buff_cast_source_covers_support_powers() -> None:
    """A SUPPORT power takes the same cooldown cast share and endurance, in the one buff_cast source."""
    cast = arcana_cast_time(1.5)
    melt = _attack("Melt", Role.SUPPORT, cast=1.5, recharge=100.0, end=10.0)
    fury = _attack("Fury", Role.BUFF_CLICK, cast=1.0, recharge=10.0, end=8.0)
    result = buff_cast_source(_ctx(melt, fury))
    fcast = arcana_cast_time(1.0)
    assert result is not None
    assert result.source == "buff_cast"
    assert result.timeline_share == pytest.approx(cast / (cast + 100.0) + fcast / (fcast + 10.0), rel=1e-5)
    assert result.end_per_sec == pytest.approx(10.0 / (cast + 100.0) + 8.0 / (fcast + 10.0), rel=1e-5)


def _form_attack(name: str, *modes: str, damage: float = 100.0) -> ChainAttack:
    a = _attack(name, damage=damage)
    return replace(a, profile=replace(a.profile, modes_required=modes))


def test_attacks_for_form_filters_on_modes_required() -> None:
    """The base form keeps mode-free attacks; a named form keeps attacks requiring that mode."""
    human, nova = _form_attack("Blast"), _form_attack("Nova Bolt", "Nova")
    assert attacks_for_form(_ctx(human, nova)) == (human,)
    assert attacks_for_form(_ctx(human, nova, form="Nova")) == (nova,)


def test_chain_plans_only_the_forms_attacks() -> None:
    """Each form's chain picks only its own attacks, though the other form's hits harder."""
    human = _form_attack("Blast", damage=100.0)
    nova = _form_attack("Nova Bolt", "Nova", damage=500.0)
    ctx = _ctx(human, nova)
    chosen = chosen_chain(ctx)
    assert chosen is not None
    assert chosen.rotation == ("Blast",)
    nova_ctx = replace(ctx, form="Nova")
    chosen = chosen_chain(nova_ctx)
    assert chosen is not None
    assert chosen.rotation == ("Nova Bolt",)
    assert chain_source(nova_ctx) is not None


def test_melee_share_is_public() -> None:
    """The melee share is the public helper other layers reuse."""
    assert melee_share(_ctx(stance=Stance.MELEE)) == 1.0


def test_end_to_end_fold() -> None:
    """Chain sees the timeline left by patch and buff; click buff skips pets; debuff reaches pets."""
    chain = _attack("Smite", damage=120.0, cast=1.0, recharge=1.0, end=5.0, target_res_debuff=0.2)
    chain = replace(chain, target_res_debuff_duration=4.0)
    patch = _attack(
        "Patch", Role.PATCH, Delivery.RANGED, Shape.LOCATION, damage=50.0, cast=1.0, recharge=10.0, end=12.0
    )
    buff = _attack(
        "Fury",
        Role.BUFF_CLICK,
        cast=1.0,
        recharge=10.0,
        end=8.0,
        self_damage_buff=0.5,
        self_damage_buff_duration=5.5,
    )
    ctx = _ctx(chain, patch, buff, scenario=Scenario(targets=1))

    def pet_source(_: DamageContext) -> SourceResult:
        return SourceResult(source="pet", dps=100.0)

    report = evaluate_damage(
        ctx,
        sources=[
            (chain_source, True),
            (aura_source, False),
            (patch_source, False),
            (buff_cast_source, False),
            (pet_source, False),
        ],
        modifiers=[click_buff_modifier, target_debuff_modifier],
    )
    cast = arcana_cast_time(1.0)
    cycle = cast + 10.0
    share = 2 * cast / cycle
    chain_cycle = max(cast, cast + 1.0)
    chain_dps = 120.0 / chain_cycle * (1.0 - share)
    click = 1.0 + 0.5 * (5.5 / cycle)
    debuff = 1.2
    by_source = {s.source: s for s in report.sources}
    assert by_source["chain"].dps == pytest.approx(chain_dps, rel=1e-4)
    assert report.scaled_dps["chain"] == pytest.approx(chain_dps * click * debuff, rel=1e-4)
    assert report.scaled_dps["patch"] == pytest.approx(50.0 / cycle * click * debuff, rel=1e-4)
    assert report.scaled_dps["pet"] == pytest.approx(100.0 * debuff, rel=1e-4)
    assert report.scaled_dps["buff_cast"] == 0.0
    assert report.total_drain_per_sec == pytest.approx(by_source["chain"].end_per_sec + (12.0 + 8.0) / cycle, rel=1e-4)
