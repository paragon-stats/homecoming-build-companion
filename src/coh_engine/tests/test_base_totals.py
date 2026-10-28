"""Tests for coh_engine.base_totals — filtering, bucket routing, GBD_Totals."""

import math
from collections.abc import Callable
from dataclasses import replace
from pathlib import Path

import pytest

from coh_engine.archetypes import ArchetypeDb
from coh_engine.attribmod import AttribMods
from coh_engine.base_totals import (
    EngineConfig,
    EnumMaps,
    ServerData,
    _apply_buff_effects,
    _apply_enhance_effects,
    _BuffsX,
    _compute_enh_multipliers,
    _enhance_names,
    _gbd_totals,
    _get_effect_mag_sum,
    _get_enhancement_mag_sum,
    _is_global_acc_source,
    _MagContext,
    _new_buffs,
    calculate_pvp_dr,
    compute_base_totals,
    load_enum_maps,
    load_server_data,
)
from coh_engine.ed import Schedule
from coh_engine.effect import Effect, Power
from coh_engine.enhancement import EnhancementRecord, EnhEffect, SlotRef
from coh_engine.maths import MathTables, f32
from coh_engine.set_bonuses import SET_BONUS_BUILD_INDEX, SetBonusDb

MakeEffect = Callable[..., Effect]
MakePower = Callable[..., Power]

FIXTURES = Path(__file__).parent / "fixtures"
PVE = EngineConfig(suppression=0, disable_pve=False, force_level=50, scaling_to_hit=0.75)
PVP = EngineConfig(suppression=0, disable_pve=True, force_level=50, scaling_to_hit=0.75)
# The eEnhance name-identity route set, loaded once for the plain _ctx helper (not a fixture).
_ENUMS = load_enum_maps(FIXTURES / "mids" / "enums.json")
_ENHANCE_NAMES = _enhance_names(_ENUMS)


@pytest.fixture(scope="module")
def enums() -> EnumMaps:
    return load_enum_maps(FIXTURES / "mids" / "enums.json")


@pytest.fixture(scope="module")
def server() -> ServerData:
    return load_server_data(FIXTURES / "mids" / "server_data.json")


def _ctx(tiny_mods: AttribMods, tiny_classes: ArchetypeDb, config: EngineConfig = PVE) -> _MagContext:
    return _MagContext(
        mods=tiny_mods,
        classes=tiny_classes,
        archetype_index=0,
        config=config,
        enhance_names=_ENHANCE_NAMES,
        enums=_ENUMS,
    )


def _mag_sum(
    power: Power,
    i_effect: str,
    tiny_mods: AttribMods,
    tiny_classes: ArchetypeDb,
    config: EngineConfig = PVE,
    **flags: bool,
) -> list[tuple[Effect, float]]:
    return _get_effect_mag_sum(power, i_effect, _ctx(tiny_mods, tiny_classes, config), **flags)


def _run_buff_pass(
    power: Power,
    tiny_mods: AttribMods,
    tiny_classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig = PVE,
    prev_hp_max: float = 0.0,
    global_acc_source: bool = False,
) -> _BuffsX:
    buffs = _new_buffs(enums)
    _apply_buff_effects(
        power,
        buffs,
        _ctx(tiny_mods, tiny_classes, config),
        enums=enums,
        prev_hp_max=prev_hp_max,
        global_acc_source=global_acc_source,
    )
    return buffs


class TestGetEffectMagSum:
    """Filtering rules of ``Power.GetEffectMagSum`` (``Power.cs:1464-1522``)."""

    def test_unspecified_to_who_is_excluded(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(to_who="Unspecified"))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []

    def test_only_self_drops_target_effects(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(to_who="Target"))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes, only_self=True) == []
        assert len(_mag_sum(power, "Defense", tiny_mods, tiny_classes)) == 1

    def test_only_target_drops_self_effects(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(to_who="Self"))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes, only_target=True) == []

    def test_max_aspect_speed_effects_need_max_mode(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="SpeedRunning", aspect="Max"))
        assert _mag_sum(power, "SpeedRunning", tiny_mods, tiny_classes) == []
        assert len(_mag_sum(power, "SpeedRunning", tiny_mods, tiny_classes, max_mode=True)) == 1

    def test_max_mode_drops_non_max_aspects(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="SpeedRunning", aspect="Cur"))
        assert _mag_sum(power, "SpeedRunning", tiny_mods, tiny_classes, max_mode=True) == []

    def test_active_suppression_bits_exclude(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(suppression=7))
        suppressed = EngineConfig(suppression=4, disable_pve=False, force_level=50, scaling_to_hit=0.75)
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes, config=suppressed) == []
        assert len(_mag_sum(power, "Defense", tiny_mods, tiny_classes)) == 1

    def test_zero_probability_excludes(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(probability=0.0))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []

    def test_effect_type_mismatch_excludes(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="Resistance"))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []

    @pytest.mark.parametrize("effect_class", ["Ignored", "Special"])
    def test_ignored_and_special_classes_excluded(
        self,
        effect_class: str,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
    ) -> None:
        power = make_power(make_effect(effect_class=effect_class))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []

    def test_delayed_effects_excluded_unless_requested(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(delayed_time=10.0))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []
        assert len(_mag_sum(power, "Defense", tiny_mods, tiny_classes, include_delayed=True)) == 1

    def test_conditional_effects_excluded(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(active_conditionals_count=1))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []

    def test_special_case_effects_excluded(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(special_case="Hidden"))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []

    def test_pvp_only_effects_dropped_in_pve(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(pv_mode="PvP"))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []

    def test_pve_only_effects_dropped_in_pvp(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(pv_mode="PvE"), make_effect(index=1, pv_mode="PvP"))
        pairs = _mag_sum(power, "Defense", tiny_mods, tiny_classes, config=PVP)
        assert [fx.index for fx, _ in pairs] == [1]

    def test_class_restricted_effects_gate_on_archetype(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(n_id_class_name=1))
        assert _mag_sum(power, "Defense", tiny_mods, tiny_classes) == []
        matching = make_power(make_effect(n_id_class_name=0))
        assert len(_mag_sum(matching, "Defense", tiny_mods, tiny_classes)) == 1

    def test_stacking_dot_multiplies_by_ticks(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(
            make_effect(scale=2.0, ticks=5, stacking="Yes"),
            make_effect(index=1, scale=2.0, ticks=5, stacking="No"),
        )
        pairs = _mag_sum(power, "Defense", tiny_mods, tiny_classes)
        assert [v for _, v in pairs] == [f32(10.0), f32(2.0)]


class TestBuffRouting:
    """Bucket routing of ``CalculateAndApplyEffects`` (``clsToonX.cs:507-832``)."""

    def test_global_boost_powers_are_skipped(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(), power_type="GlobalBoost")
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert not any(buffs.defense)

    def test_target_effects_do_not_route(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(to_who="Target"))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert not any(buffs.defense)

    def test_enhancement_effects_route_to_boosts(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="Enhancement", et_modifies="RechargeTime", scale=0.2),
            make_effect(index=1, effect_type="Enhancement", et_modifies="Mez", mez_type="Held", scale=0.3),
            make_effect(index=2, effect_type="Enhancement", et_modifies="None", scale=0.4),
        )
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        e_type = enums.maps["eEffectType"]
        e_mez = enums.maps["eMez"]
        assert buffs.boosts[e_type["RechargeTime"]] == f32(0.2)
        assert buffs.boosts_mez[e_mez["Held"]] == f32(0.3)
        assert not any(v == f32(0.4) for v in buffs.boosts)

    def test_enhancement_range_and_heal_redirect_to_effect_buckets(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="Enhancement", et_modifies="Range", scale=0.25),
            make_effect(index=1, effect_type="Enhancement", et_modifies="Heal", scale=0.15),
        )
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        e_type = enums.maps["eEffectType"]
        assert buffs.effect[e_type["Range"]] == f32(0.25)
        assert buffs.effect[e_type["Heal"]] == f32(0.15)

    def test_absorbed_global_boost_effects_skip_buckets(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(absorbed_effect=True, absorbed_power_type="GlobalBoost"))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert not any(buffs.defense)

    def test_heal_effects_are_not_summed(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Heal", scale=2.0))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.effect[enums.maps["eEffectType"]["Heal"]] == 0.0

    def test_max_endurance_accumulates(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Endurance", aspect="Max", scale=10.0))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.max_end == f32(10.0)

    def test_et_modifies_mez_routes_to_mez_bucket(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="MovementControl", et_modifies="Mez", mez_type="Held", scale=2.0),
            make_effect(index=1, effect_type="MovementControl", et_modifies="MezResist", mez_type="Sleep", scale=3.0),
        )
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        e_mez = enums.maps["eMez"]
        assert buffs.mez[e_mez["Held"]] == f32(2.0)
        assert buffs.mez_res[e_mez["Sleep"]] == f32(3.0)

    def test_typed_elusivity_routes(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Elusivity", damage_type="Melee", scale=0.2))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.elusivity[enums.maps["eDamage"]["Melee"]] == f32(0.2)

    def test_accuracy_et_modifies_routes_to_tohit(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="MovementControl", et_modifies="Accuracy", scale=0.05))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.effect[enums.maps["eStatType"]["ToHit"]] == f32(0.05)

    @pytest.mark.parametrize(
        "speed,stat_name",
        [("SpeedRunning", "MaxRunSpeed"), ("SpeedJumping", "MaxJumpSpeed"), ("SpeedFlying", "MaxFlySpeed")],
    )
    def test_max_aspect_speed_effects_raise_the_speed_cap(
        self,
        speed: str,
        stat_name: str,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type=speed, et_modifies=speed, aspect="Max", scale=0.5),
            make_effect(index=1, effect_type=stat_name, to_who="Self", scale=0.25),
        )
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        # The Max-aspect speed effect routes to the cap stat; the scalar
        # MaxXSpeed self effect is added by the gather special case.
        assert buffs.effect[enums.maps["eStatType"][stat_name]] == f32(0.75)

    def test_tertiary_enhancement_tohit_is_ignored(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="ToHit", is_enhancement_effect=True, effect_class="Tertiary", scale=0.2),
        )
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.effect[enums.maps["eStatType"]["ToHit"]] == 0.0

    def test_percentage_absorb_scales_by_previous_hp_max(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Absorb", display_percentage=True, scale=0.1))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums, prev_hp_max=1000.0)
        assert buffs.effect[enums.maps["eStatType"]["Absorb"]] == f32(100.0)

    def test_click_powers_subtract_their_base_magnitude(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        # A stacking DoT on a click: value = 3 * mag is added, raw mag is
        # subtracted -> net 2 * mag survives (the C# mirrored-removal quirk).
        power = make_power(
            make_effect(effect_type="Recovery", scale=1.0, ticks=3, stacking="Yes"),
            power_type="Click",
            click_buff=False,
        )
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.effect[enums.maps["eStatType"]["EndRec"]] == f32(2.0)


class TestGbdTotals:
    """``GBD_Totals`` folds, scalars, and cap application (``clsToonX.cs:839-1002``)."""

    def test_unknown_class_name_raises(
        self,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
    ) -> None:
        with pytest.raises(ValueError, match="E09"):
            compute_base_totals(
                [],
                class_name="Class_Nope",
                mods=tiny_mods,
                classes=tiny_classes,
                enums=enums,
                config=PVE,
                server=server,
            )

    def test_toggle_end_use_and_fly_gating(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
    ) -> None:
        # A Toggle with no ActivatePeriod: ToggleCost == EndCost (Power.cs:388).
        flyer = make_power(
            make_effect(effect_type="Fly", scale=1.0),
            make_effect(index=1, effect_type="SpeedFlying", scale=0.5),
            end_cost=0.26,
        )
        result = compute_base_totals(
            [flyer],
            class_name="Class_Test",
            mods=tiny_mods,
            classes=tiny_classes,
            enums=enums,
            config=PVE,
            server=server,
        )
        assert result.totals.end_use == f32(0.26)
        # (1 + 0.5) * BaseFlySpeed 31.5 = 47.25 — kept because a Fly effect is up.
        assert result.totals.fly_spd == f32(47.25)

    def test_fly_speed_zeroed_without_fly_effect(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
    ) -> None:
        power = make_power(make_effect(effect_type="SpeedFlying", scale=0.5))
        result = compute_base_totals(
            [power],
            class_name="Class_Test",
            mods=tiny_mods,
            classes=tiny_classes,
            enums=enums,
            config=PVE,
            server=server,
        )
        assert result.totals.fly_spd == 0.0

    def test_speed_buff_floor_and_maxmax_clamp(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
    ) -> None:
        power = make_power(
            make_effect(effect_type="SpeedRunning", scale=-5.0),
            make_effect(index=1, effect_type="SpeedJumping", scale=50.0),
        )
        result = compute_base_totals(
            [power],
            class_name="Class_Test",
            mods=tiny_mods,
            classes=tiny_classes,
            enums=enums,
            config=PVE,
            server=server,
        )
        # Floor at -90%: (1 - 0.9) * 21 = 2.1; jump clamps to MaxMax 166.257.
        assert result.totals.run_spd == pytest.approx(2.1, abs=1e-5)
        assert result.totals.jump_spd == server.max_max_jump_speed

    def test_defense_index0_folds_into_typed_channels(
        self, tiny_mods: AttribMods, tiny_classes: ArchetypeDb, enums: EnumMaps, server: ServerData
    ) -> None:
        buffs = _new_buffs(enums)
        buffs.defense[0] = f32(0.05)
        buffs.defense[1] = f32(0.10)
        result = _gbd_totals(
            [],
            buffs,
            _new_buffs(enums),
            archetype=tiny_classes.classes[0],
            ctx=_ctx(tiny_mods, tiny_classes),
            enums=enums,
            server=server,
            toggle_end_agg={},
        )
        assert result.totals.def_[0] == f32(0.05)
        assert result.totals.def_[1] == f32(f32(0.10) + f32(0.05))
        assert result.totals.def_[2] == f32(0.05)

    def test_buff_dam_picks_max_when_one_low_outlier(
        self, tiny_mods: AttribMods, tiny_classes: ArchetypeDb, enums: EnumMaps, server: ServerData
    ) -> None:
        # avg sits near the top, so max-avg < avg-min -> report the max.
        buffs = _new_buffs(enums)
        buffs.damage[1:8] = [f32(v) for v in (0.1, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0)]
        result = _gbd_totals(
            [],
            buffs,
            _new_buffs(enums),
            archetype=tiny_classes.classes[0],
            ctx=_ctx(tiny_mods, tiny_classes),
            enums=enums,
            server=server,
            toggle_end_agg={},
        )
        assert result.totals.buff_dam == f32(1.0)

    def test_buff_dam_picks_positive_min_when_one_high_outlier(
        self, tiny_mods: AttribMods, tiny_classes: ArchetypeDb, enums: EnumMaps, server: ServerData
    ) -> None:
        # avg sits near the bottom, so max-avg > avg-min and min > 0 -> min.
        buffs = _new_buffs(enums)
        buffs.damage[1:8] = [f32(v) for v in (0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 1.0)]
        result = _gbd_totals(
            [],
            buffs,
            _new_buffs(enums),
            archetype=tiny_classes.classes[0],
            ctx=_ctx(tiny_mods, tiny_classes),
            enums=enums,
            server=server,
            toggle_end_agg={},
        )
        assert result.totals.buff_dam == f32(0.1)

    def test_buff_dam_falls_back_to_max_when_min_not_positive(
        self, tiny_mods: AttribMods, tiny_classes: ArchetypeDb, enums: EnumMaps, server: ServerData
    ) -> None:
        buffs = _new_buffs(enums)
        buffs.damage[1:8] = [f32(v) for v in (-0.5, 0.1, 0.1, 0.1, 0.1, 0.1, 1.0)]
        result = _gbd_totals(
            [],
            buffs,
            _new_buffs(enums),
            archetype=tiny_classes.classes[0],
            ctx=_ctx(tiny_mods, tiny_classes),
            enums=enums,
            server=server,
            toggle_end_agg={},
        )
        assert result.totals.buff_dam == f32(1.0)

    def test_caps_clamp_res_hp_regen_and_perception(
        self, tiny_mods: AttribMods, tiny_classes: ArchetypeDb, enums: EnumMaps, server: ServerData
    ) -> None:
        buffs = _new_buffs(enums)
        stat = enums.maps["eStatType"]
        buffs.resistance[1] = f32(0.9)
        buffs.effect[stat["HPMax"]] = 900.0
        buffs.effect[stat["HPRegen"]] = 25.0
        buffs.effect[stat["EndRec"]] = 9.0
        buffs.effect[stat["Haste"]] = 9.0
        buffs.effect[stat["Perception"]] = 5.0
        buffs.effect[stat["JumpHeight"]] = 100.0
        result = _gbd_totals(
            [],
            buffs,
            _new_buffs(enums),
            archetype=tiny_classes.classes[0],
            ctx=_ctx(tiny_mods, tiny_classes),
            enums=enums,
            server=server,
            toggle_end_agg={},
        )
        capped = result.totals_capped
        assert result.totals.res[1] == f32(0.9)
        assert capped.res[1] == f32(0.75)
        assert result.totals.hp_max == f32(1900.0)
        assert capped.hp_max == f32(1500.0)
        assert capped.hp_regen == f32(19.0)
        assert capped.end_rec == f32(4.0)
        assert capped.buff_haste == f32(4.0)
        assert capped.perception == f32(1153.0)
        assert capped.jump_height == server.max_jump_height
        # Verbatim Assign quirk: Max*Spd are not copied into TotalsCapped.
        assert capped.max_run_spd == 0.0
        assert capped.max_jump_spd == 0.0
        assert capped.max_fly_spd == 0.0

    def test_hp_cap_zero_skips_hp_clamp(
        self, tiny_mods: AttribMods, tiny_classes: ArchetypeDb, enums: EnumMaps, server: ServerData
    ) -> None:
        uncapped_at = replace(tiny_classes.classes[0], hp_cap=0.0)
        buffs = _new_buffs(enums)
        buffs.effect[enums.maps["eStatType"]["HPMax"]] = 9000.0
        result = _gbd_totals(
            [],
            buffs,
            _new_buffs(enums),
            archetype=uncapped_at,
            ctx=_ctx(tiny_mods, tiny_classes),
            enums=enums,
            server=server,
            toggle_end_agg={},
        )
        assert result.totals_capped.hp_max == f32(10000.0)

    def test_pvp_mode_applies_defense_diminishing_returns(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
    ) -> None:
        power = make_power(make_effect(damage_type="Smashing", scale=0.4))
        result = compute_base_totals(
            [power],
            class_name="Class_Test",
            mods=tiny_mods,
            classes=tiny_classes,
            enums=enums,
            config=PVP,
            server=server,
        )
        assert result.totals.def_[1] == calculate_pvp_dr(f32(0.4))

    def test_calculate_pvp_dr_formula(self) -> None:
        # Faithful to C# CalculatePvpDr: a = 1.2f, the bracket is (float)-narrowed
        # before the final float multiply (two f32 rounding steps).
        value = f32(0.45)
        factor = f32(1.0 - abs(math.atan(f32(1.2) * value)) * (2.0 / math.pi) * f32(1.0))
        assert calculate_pvp_dr(value) == f32(value * factor)

    def test_stat_exclude_powers_contribute_nothing(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
    ) -> None:
        power = make_power(make_effect(scale=1.0), stat_include=False, toggle_cost=0.5)
        result = compute_base_totals(
            [power],
            class_name="Class_Test",
            mods=tiny_mods,
            classes=tiny_classes,
            enums=enums,
            config=PVE,
            server=server,
        )
        assert result.totals.end_use == 0.0
        assert not any(result.totals.def_)


def test_enum_maps_size(enums: EnumMaps) -> None:
    assert enums.size("eDamage") == 16
    assert enums.size("eMez") == 20


def _res_rech_record() -> EnhancementRecord:
    """An InventO enhancing both Resistance (effect-borne) and RechargeTime (scalar)."""
    return EnhancementRecord(
        nid=1,
        static_index=1,
        type_name="InventO",
        superior=False,
        effects=(
            EnhEffect(
                enhance="Resistance", enhance_sub_id=-1, schedule=int(Schedule.B), multiplier=1.0, buff_mode="Any"
            ),
            EnhEffect(
                enhance="RechargeTime", enhance_sub_id=-1, schedule=int(Schedule.A), multiplier=1.0, buff_mode="Any"
            ),
        ),
    )


def _record(*aspects: tuple[str, int]) -> EnhancementRecord:
    """An InventO granting each ``(aspect, sub id)`` on schedule A."""
    return EnhancementRecord(
        nid=1,
        static_index=1,
        type_name="InventO",
        superior=False,
        effects=tuple(
            EnhEffect(enhance=aspect, enhance_sub_id=sub, schedule=int(Schedule.A), multiplier=1.0, buff_mode="Any")
            for aspect, sub in aspects
        ),
    )


_IO50 = SlotRef(level=0, is_inherent=False, enh=1, grade=0, io_level=49, relative_level=4)


def _multiplier(
    fx: Effect,
    record: EnhancementRecord,
    slot_count: int,
    make_power: MakePower,
    ctx: _MagContext,
    tables: MathTables,
    self_enhance: _BuffsX | None = None,
) -> float | None:
    power = make_power(fx, build_index=0)
    mults = _compute_enh_multipliers(
        [power],
        slots={0: (_IO50,) * slot_count},
        enh_db={1: record},
        tables=tables,
        force_level=50,
        ctx=ctx,
        self_enhance=self_enhance,
    )
    return mults.get((0, fx.index))


class TestEffectRouting:
    """Which slotted aspect enhances which effect, and whether ED applies to it."""

    def test_granted_aspect_without_a_matching_effect_is_inert(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        tables: MathTables,
    ) -> None:
        fx = make_effect(effect_type="Recovery", buffable=True)
        assert _multiplier(fx, _res_rech_record(), 1, make_power, _ctx(tiny_mods, tiny_classes), tables) is None

    def test_regeneration_debuff_resistance_takes_heal_without_ed(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        tables: MathTables,
    ) -> None:
        # Only the Heal enhancement's allowed-effects list reaches this effect, and ED skips it.
        fx = make_effect(effect_type="ResEffect", et_modifies="Regeneration", buffable=True)
        single = tables.mult_io[49][int(Schedule.A)]
        raw = f32(f32(f32(single + single) + single) + single)
        got = _multiplier(fx, _record(("Heal", -1)), 4, make_power, _ctx(tiny_mods, tiny_classes), tables)
        assert got == f32(1.0 + raw)

    @pytest.mark.parametrize(("mez_type", "enhanced"), [("Held", True), ("Stunned", False)])
    def test_mez_effect_takes_only_its_own_subtype(
        self,
        mez_type: str,
        enhanced: bool,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        tables: MathTables,
        enums: EnumMaps,
    ) -> None:
        held = enums.maps["eMez"]["Held"]
        fx = make_effect(effect_type="Mez", mez_type=mez_type, buffable=True)
        ctx = _ctx(tiny_mods, tiny_classes)
        got = _multiplier(fx, _record(("Mez", held)), 1, make_power, ctx, tables)
        assert (got is not None) is enhanced

    @pytest.mark.parametrize(
        ("effect_type", "fields"),
        [("Mez", {"mez_type": "Held", "attrib_type": "Duration"}), ("Damage", {"damage_type": "Special"})],
    )
    def test_effects_whose_magnitude_mids_leaves_unenhanced(
        self,
        effect_type: str,
        fields: dict[str, str],
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        tables: MathTables,
        enums: EnumMaps,
    ) -> None:
        held = enums.maps["eMez"]["Held"]
        fx = make_effect(effect_type=effect_type, buffable=True, **fields)
        ctx = _ctx(tiny_mods, tiny_classes)
        record = _record(("Mez", held), ("Damage", -1))
        assert _multiplier(fx, record, 1, make_power, ctx, tables) is None


class TestGlobalEnhancementFold:
    """The build-wide enhancement buffs folded into every buffable effect after ED."""

    @pytest.mark.parametrize(
        ("fields", "bucket", "key"),
        [
            ({"effect_type": "Regeneration"}, "effect", ("eEffectType", "Regeneration")),
            ({"effect_type": "Defense", "damage_type": "Cold"}, "defense", ("eDamage", "Cold")),
            ({"effect_type": "Resistance", "damage_type": "Cold"}, "resistance", ("eDamage", "Cold")),
            ({"effect_type": "Damage", "damage_type": "Cold"}, "damage", ("eDamage", "Cold")),
            ({"effect_type": "Mez", "mez_type": "Held"}, "mez", ("eMez", "Held")),
            ({"effect_type": "SpeedFlying", "scale": 1.0}, "effect", ("eEffectType", "SpeedFlying")),
            ({"effect_type": "SpeedFlying", "scale": -1.0}, "effect_aux", ("eEffectType", "SpeedFlying")),
            (
                {"effect_type": "Enhancement", "et_modifies": "SpeedJumping", "scale": -1.0},
                "effect_aux",
                ("eEffectType", "SpeedJumping"),
            ),
            (
                {"effect_type": "Enhancement", "et_modifies": "SpeedJumping", "scale": 1.0},
                "effect",
                ("eEffectType", "SpeedJumping"),
            ),
        ],
    )
    def test_global_term_is_added_after_ed(
        self,
        fields: dict[str, object],
        bucket: str,
        key: tuple[str, str],
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        tables: MathTables,
        enums: EnumMaps,
    ) -> None:
        self_enhance = _new_buffs(enums)
        getattr(self_enhance, bucket)[enums.maps[key[0]][key[1]]] = f32(0.25)
        fx = make_effect(buffable=True, **fields)
        ctx = _ctx(tiny_mods, tiny_classes)
        got = _multiplier(fx, _record(), 0, make_power, ctx, tables, self_enhance)
        assert got == f32(1.25)

    @pytest.mark.parametrize(
        "fields",
        [
            {"effect_type": "RechargeTime"},  # a scalar aspect: folded into the power, not the effect
            {"effect_type": "Regeneration", "buffable": False},
            {"effect_type": "SpeedFlying", "scale": 0.0},
            {"effect_type": "Mez", "mez_type": "Held", "attrib_type": "Duration"},
        ],
    )
    def test_global_term_skips(
        self,
        fields: dict[str, object],
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        tables: MathTables,
        enums: EnumMaps,
    ) -> None:
        self_enhance = _new_buffs(enums)
        for name in ("RechargeTime", "Regeneration", "SpeedFlying"):
            self_enhance.effect[enums.maps["eEffectType"][name]] = f32(0.25)
            self_enhance.effect_aux[enums.maps["eEffectType"][name]] = f32(0.25)
        self_enhance.mez[enums.maps["eMez"]["Held"]] = f32(0.25)
        fx = make_effect(**{"buffable": True, **fields})
        ctx = _ctx(tiny_mods, tiny_classes)
        assert _multiplier(fx, _record(), 0, make_power, ctx, tables, self_enhance) is None

    def test_set_bonus_power_takes_no_global_term(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        tables: MathTables,
        enums: EnumMaps,
    ) -> None:
        self_enhance = _new_buffs(enums)
        self_enhance.effect[enums.maps["eEffectType"]["Regeneration"]] = f32(0.25)
        power = make_power(make_effect(effect_type="Regeneration", buffable=True), build_index=SET_BONUS_BUILD_INDEX)
        mults = _compute_enh_multipliers(
            [power],
            slots={},
            enh_db={},
            tables=tables,
            force_level=50,
            ctx=_ctx(tiny_mods, tiny_classes),
            self_enhance=self_enhance,
        )
        assert mults == {}

    def test_global_speed_buff_reaches_fly_speed(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
        tables: MathTables,
    ) -> None:
        """A self Enhancement(SpeedFlying) in one power raises another power's fly speed."""
        booster = make_power(
            make_effect(effect_type="Enhancement", et_modifies="SpeedFlying", to_who="Self", scale=0.5), build_index=0
        )
        flight = make_power(
            make_effect(effect_type="SpeedFlying", to_who="Self", buffable=True),
            make_effect(index=1, effect_type="Fly", to_who="Self"),
            build_index=1,
            power_type="Toggle",
        )
        result = compute_base_totals(
            [booster, flight],
            class_name="Class_Test",
            mods=tiny_mods,
            classes=tiny_classes,
            enums=enums,
            config=PVE,
            server=server,
            slots={},
            enh_db={},
            tables=tables,
        )
        assert result.totals.fly_spd == f32(f32(1 + f32(1.5)) * server.base_fly_speed)


class TestEnhancementGuards:
    """The fail-loud guards that refuse builds the enhancement port cannot compute."""

    def test_partial_enhancement_inputs_raise(
        self, tiny_mods: AttribMods, tiny_classes: ArchetypeDb, enums: EnumMaps, server: ServerData
    ) -> None:
        with pytest.raises(ValueError, match="E11"):
            compute_base_totals(
                [],
                class_name="Class_Test",
                mods=tiny_mods,
                classes=tiny_classes,
                enums=enums,
                config=PVE,
                server=server,
                slots={},  # enh_db / tables left None -> partial
            )

    def test_self_damage_buff_feeds_buff_dam(
        self,
        make_power: MakePower,
        make_effect: MakeEffect,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
    ) -> None:
        """A self DamageBuff (what E12 used to refuse) now folds into BuffDam via _selfEnhance.

        With the enhancement pass ported, the DamageBuff gather feeds
        ``_selfEnhance.Damage[Smashing]``; ``_gbd_buff_damage`` seeds the empty
        ``_selfBuffs.Damage`` from it and the min/avg/max heuristic reports it.
        """
        power = make_power(
            make_effect(effect_type="DamageBuff", to_who="Self", damage_type="Smashing", modifier_table="Ones")
        )
        result = compute_base_totals(
            [power],
            class_name="Class_Test",
            mods=tiny_mods,
            classes=tiny_classes,
            enums=enums,
            config=PVE,
            server=server,
        )
        assert result.totals.buff_dam == f32(1.0)


def _run_enhance_pass(
    power: Power,
    tiny_mods: AttribMods,
    tiny_classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig = PVE,
) -> _BuffsX:
    buffs = _new_buffs(enums)
    _apply_enhance_effects(power, buffs, _ctx(tiny_mods, tiny_classes, config), enums=enums)
    return buffs


class TestEnhancePass:
    """The enhancement pass (``enhancementPass=true``) feeding ``_selfEnhance``."""

    def test_recharge_routes_to_boosts_and_effect_fallback(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="RechargeTime", to_who="Self"))
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        etype = enums.maps["eEffectType"]
        assert buffs.boosts[etype["RechargeTime"]] == f32(1.0)
        assert buffs.effect[etype["RechargeTime"]] == f32(1.0)

    def test_mez_enhancement_routes_to_boosts_mez_and_mez(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Mez", mez_type="Held", to_who="Self"))
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        emez = enums.maps["eMez"]
        assert buffs.boosts_mez[emez["Held"]] == f32(1.0)
        assert buffs.mez[emez["Held"]] == f32(1.0)

    def test_heal_enhancement_routes_to_boosts_and_effect_heal(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Heal", to_who="Self"))
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        etype = enums.maps["eEffectType"]
        assert buffs.boosts[etype["Heal"]] == f32(1.0)
        assert buffs.effect[etype["Heal"]] == f32(1.0)

    def test_typed_defense_enhancement_routes_to_defense_vector(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="Enhancement", et_modifies="Defense", damage_type="Fire", to_who="Self")
        )
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.defense[enums.maps["eDamage"]["Fire"]] == f32(1.0)

    def test_generic_defense_enhancement_routes_to_effect_bucket(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="Enhancement", et_modifies="Defense", damage_type="None", to_who="Self")
        )
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.effect[enums.maps["eEffectType"]["Defense"]] == f32(1.0)

    def test_generic_resistance_enhancement_routes_to_effect_bucket(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="Enhancement", et_modifies="Resistance", damage_type="None", to_who="Self")
        )
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.effect[enums.maps["eEffectType"]["Resistance"]] == f32(1.0)

    def test_typed_resistance_enhancement_routes_to_resistance_vector(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="Enhancement", et_modifies="Resistance", damage_type="Cold", to_who="Self")
        )
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.resistance[enums.maps["eDamage"]["Cold"]] == f32(1.0)

    def test_damage_buff_routes_to_damage_vector(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="DamageBuff", damage_type="Fire", to_who="Self"))
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.damage[enums.maps["eDamage"]["Fire"]] == f32(1.0)

    def test_defiance_damage_buff_is_excluded(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        fx = make_effect(effect_type="DamageBuff", damage_type="Fire", to_who="Self", effect_class="Tertiary")
        power = make_power(replace(fx, is_enhancement_effect=True))
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.damage[enums.maps["eDamage"]["Fire"]] == 0.0

    def test_accuracy_enhancement_is_dropped_from_effect(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Accuracy", to_who="Self"))
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        etype = enums.maps["eEffectType"]
        assert buffs.boosts[etype["Accuracy"]] == f32(1.0)  # boost head still fires...
        assert buffs.effect[etype["Accuracy"]] == 0.0  # ...but the main routing drops Accuracy.

    @pytest.mark.parametrize(("buff_mode", "bucket"), [("Normal", "effect"), ("Debuff", "effect_aux")])
    def test_speed_scalar_enhancement_splits_on_buff_mode(
        self,
        buff_mode: str,
        bucket: str,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        fx = make_effect(effect_type="Enhancement", et_modifies="SpeedRunning", to_who="Self", buff_mode=buff_mode)
        buffs = _run_enhance_pass(make_power(fx), tiny_mods, tiny_classes, enums)
        index = enums.maps["eEffectType"]["SpeedRunning"]
        assert getattr(buffs, bucket)[index] == f32(1.0)
        other = "effect_aux" if bucket == "effect" else "effect"
        assert getattr(buffs, other)[index] == 0.0

    def test_mez_none_subtype_takes_boost_elif_and_mez_zero(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        # ETModifies==Mez with MezType None: the boost head's Mez branch is False
        # (mez_type == None) and the elif is False (Mez is in the skip tuple), so no
        # boost; the main routing still folds it into Mez[None] (index 0).
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Mez", mez_type="None", to_who="Self"))
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert buffs.mez[enums.maps["eMez"]["None"]] == f32(1.0)
        assert all(v == 0.0 for v in buffs.boosts_mez)

    def test_unspecified_to_who_gathered_but_not_routed(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        # GetEnhancementMagSum includes ToWho != Target (so Unspecified passes), but
        # the apply loop routes only Self/All — the effect is gathered then dropped.
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="RechargeTime", to_who="Unspecified"))
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert all(v == 0.0 for v in buffs.effect)
        assert all(v == 0.0 for v in buffs.boosts)

    def test_global_boost_power_is_skipped(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(
            make_effect(effect_type="Enhancement", et_modifies="RechargeTime", to_who="Self"), power_type="GlobalBoost"
        )
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        assert all(v == 0.0 for v in buffs.effect)

    def test_absorbed_global_boost_effect_skips_main_routing(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        # absorbed_effect False keeps it past the gather filter; the route-level
        # GlobalBoost skip then bars the main routing (boost head still runs).
        power = make_power(
            make_effect(
                effect_type="Enhancement", et_modifies="RechargeTime", to_who="Self", absorbed_power_type="GlobalBoost"
            )
        )
        buffs = _run_enhance_pass(power, tiny_mods, tiny_classes, enums)
        etype = enums.maps["eEffectType"]
        assert buffs.boosts[etype["RechargeTime"]] == f32(1.0)
        assert buffs.effect[etype["RechargeTime"]] == 0.0


class TestGetEnhancementMagSum:
    """Filtering rules of ``Power.GetEnhancementMagSum`` (``Power.cs:1433-1462``)."""

    def _sum(
        self, power: Power, i_effect: str, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> list[tuple[Effect, float]]:
        return _get_enhancement_mag_sum(power, i_effect, _ctx(tiny_mods, tiny_classes))

    def test_zero_probability_excluded(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Haste", probability=0.0))
        assert self._sum(power, "Haste", tiny_mods, tiny_classes) == []

    def test_non_enhancement_effect_type_excluded(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="Defense", et_modifies="Haste"))
        assert self._sum(power, "Haste", tiny_mods, tiny_classes) == []

    def test_target_directed_excluded(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Haste", to_who="Target"))
        assert self._sum(power, "Haste", tiny_mods, tiny_classes) == []

    def test_conditional_effect_excluded(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Haste", special_case="Domination"))
        assert self._sum(power, "Haste", tiny_mods, tiny_classes) == []

    def test_pvp_only_effect_excluded_in_pve(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Haste", pv_mode="PvP"))
        assert self._sum(power, "Haste", tiny_mods, tiny_classes) == []

    def test_absorbed_global_boost_excluded(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(
            make_effect(
                effect_type="Enhancement",
                et_modifies="Haste",
                absorbed_effect=True,
                absorbed_power_type="GlobalBoost",
            )
        )
        assert self._sum(power, "Haste", tiny_mods, tiny_classes) == []

    def test_matching_effect_included(
        self, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Haste", to_who="Self"))
        result = self._sum(power, "Haste", tiny_mods, tiny_classes)
        assert len(result) == 1
        assert result[0][1] == f32(1.0)


class TestIsGlobalAccuracySource:
    def test_set_bonus_virtual_power_is_global(self, make_power: MakePower) -> None:
        assert _is_global_acc_source(make_power(build_index=SET_BONUS_BUILD_INDEX)) is True

    def test_global_boost_power_is_global(self, make_power: MakePower) -> None:
        assert _is_global_acc_source(make_power(build_index=0, power_type="GlobalBoost")) is True

    def test_ordinary_power_is_not_global(self, make_power: MakePower) -> None:
        assert _is_global_acc_source(make_power(build_index=0)) is False

    def test_global_accuracy_routes_to_buff_acc(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Accuracy", to_who="Self"))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums, global_acc_source=True)
        stat = enums.maps["eStatType"]
        assert buffs.effect[stat["BuffAcc"]] == f32(1.0)
        assert buffs.effect[stat["ToHit"]] == 0.0

    def test_ordinary_accuracy_routes_to_to_hit(
        self,
        make_effect: MakeEffect,
        make_power: MakePower,
        tiny_mods: AttribMods,
        tiny_classes: ArchetypeDb,
        enums: EnumMaps,
    ) -> None:
        power = make_power(make_effect(effect_type="Enhancement", et_modifies="Accuracy", to_who="Self"))
        buffs = _run_buff_pass(power, tiny_mods, tiny_classes, enums, global_acc_source=False)
        stat = enums.maps["eStatType"]
        assert buffs.effect[stat["ToHit"]] == f32(1.0)
        assert buffs.effect[stat["BuffAcc"]] == 0.0


def test_set_db_without_slots_raises(
    tiny_mods: AttribMods, tiny_classes: ArchetypeDb, enums: EnumMaps, server: ServerData
) -> None:
    empty_db = SetBonusDb(sets={}, powers={})
    with pytest.raises(ValueError, match="E14"):
        compute_base_totals(
            [],
            class_name="Class_Test",
            mods=tiny_mods,
            classes=tiny_classes,
            enums=enums,
            config=PVE,
            server=server,
            set_db=empty_db,
        )
