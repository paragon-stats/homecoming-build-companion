"""Tests for coh_engine.assembly on hand-made records in the contract's shapes."""

from collections.abc import Callable, Mapping, Sequence
from typing import Any

import pytest

from coh_engine.assembly import AssembledPower, Conditions, apply_assembly, assemble_build, assemble_power
from coh_engine.assembly_data import (
    AssemblyEntities,
    AssemblyEntity,
    AssemblyPower,
    AssemblyPowerset,
    BuildPowerAssembly,
)
from coh_engine.effect import Effect, Power
from coh_engine.enhancement import EnhancementRecord, EnhEffect, SlotRef
from coh_engine.maths import f32
from coh_engine.set_bonuses import BonusItem, EnhancementSetDef, SetBonusDb

MakeEffect = Callable[..., Effect]
MakePower = Callable[..., Power]
MakeAssemblyPower = Callable[..., AssemblyPower]
MakeBuildPower = Callable[..., BuildPowerAssembly]

AT = 3
_NO_BONUS = BonusItem(slotted=0, pv_mode=0, index=())
_ENH_EFFECT = EnhEffect(enhance="Damage", enhance_sub_id=0, schedule=0, multiplier=1.0, buff_mode="Any")
SET_DB = SetBonusDb(
    sets={
        7: EnhancementSetDef(
            nid=7,
            uid="Test_Set",
            set_type=0,
            set_type_name="Defense",
            enhancements=(50, 51, 52, 56),
            bonus=(),
            special_bonus=(_NO_BONUS, BonusItem(slotted=0, pv_mode=0, index=(900,)), _NO_BONUS, _NO_BONUS),
        ),
        8: EnhancementSetDef(
            nid=8,
            uid="Pet_Set",
            set_type=1,
            set_type_name="PetDamage",
            enhancements=(60,),
            bonus=(),
            special_bonus=(_NO_BONUS,),
        ),
    },
    powers={},
)


def _enh(
    nid: int, power_nid: int, nid_set: int = 7, *, is_proc: bool = False, effects: bool = False
) -> EnhancementRecord:
    return EnhancementRecord(
        nid=nid,
        static_index=nid,
        type_name="SetO",
        superior=False,
        effects=(_ENH_EFFECT,) if effects else (),
        nid_set=nid_set,
        is_proc=is_proc,
        power_nid=power_nid,
    )


ENH_DB = {
    50: _enh(50, 500),
    51: _enh(51, 500),
    52: _enh(52, 500, is_proc=True, effects=True),
    53: _enh(53, -1),
    54: _enh(54, 500, nid_set=-1),
    55: _enh(55, 500),
    56: _enh(56, 501),
    57: _enh(57, 500, nid_set=99),
    58: _enh(58, 404),
    60: _enh(60, 500, nid_set=8),
}
NO_ENTITIES = AssemblyEntities(entities={}, powersets={})


def _entities(*records: AssemblyEntity | AssemblyPowerset) -> AssemblyEntities:
    return AssemblyEntities(
        entities={r.nid: r for r in records if isinstance(r, AssemblyEntity)},
        powersets={r.nid: r for r in records if isinstance(r, AssemblyPowerset)},
    )


def _entity(nid: int, *powersets: int, class_nid: int = 18) -> AssemblyEntity:
    return AssemblyEntity(nid=nid, uid=f"Pets_{nid}", class_nid=class_nid, powerset_nids=powersets)


def _powerset(nid: int, *powers: int, set_type: str = "Pet") -> AssemblyPowerset:
    return AssemblyPowerset(nid=nid, full_name=f"Pets.Set_{nid}", set_type=set_type, power_nids=powers)


def _slot(enh: int) -> SlotRef:
    return SlotRef(level=0, is_inherent=False, enh=enh, grade=0, io_level=49, relative_level=4)


def _never(fx: Effect) -> bool:
    raise AssertionError(f"conditions called for effect {fx.index}")


def _assemble(
    build_power: BuildPowerAssembly,
    powers: Sequence[AssemblyPower] = (),
    slots: Sequence[SlotRef] = (),
    conditions: Conditions = _never,
    entities: AssemblyEntities = NO_ENTITIES,
) -> tuple[Effect, ...]:
    return assemble_power(
        build_power,
        slots,
        enh_db=ENH_DB,
        set_db=SET_DB,
        powers={p.nid: p for p in powers},
        entities=entities,
        archetype_index=AT,
        conditions=conditions,
    ).effects


def _types(effects: Sequence[Effect]) -> list[str]:
    return [fx.effect_type for fx in effects]


# --- the raw power and re-indexing --------------------------------------------------------------------


def test_plain_power_keeps_its_effects_reindexed(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    bp = make_build_power(make_effect(index=4, effect_type="Defense"), make_effect(index=9, effect_type="Resistance"))
    out = _assemble(bp)
    assert _types(out) == ["Defense", "Resistance"]
    assert [fx.index for fx in out] == [0, 1]


# --- step 1: power override ----------------------------------------------------------------------------


def test_met_override_refuses(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    bp = make_build_power(make_effect(effect_type="PowerRedirect"), has_power_override_effect=True)
    with pytest.raises(ValueError, match="E88"):
        _assemble(bp)


@pytest.mark.parametrize(
    ("flag", "over"),
    [
        (False, {}),
        (True, {"probability": 0.5}),
        (True, {"special_case": "Domination"}),
        (True, {"effect_type": "Defense"}),
    ],
)
def test_unmet_override_is_ignored(
    make_effect: MakeEffect, make_build_power: MakeBuildPower, flag: bool, over: dict[str, Any]
) -> None:
    fx = make_effect(**{"effect_type": "PowerRedirect", **over})
    assert len(_assemble(make_build_power(fx, has_power_override_effect=flag))) == 1


# --- step 2: enhancement effects -----------------------------------------------------------------------


def _enh_powers(make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower) -> list[AssemblyPower]:
    return [
        make_assembly_power(
            make_effect(
                effect_type="Recovery",
                modifier_table="Melee_Boosts_Rec",
                to_who="Target",
                ticks=3,
                absorbed_power_nid=4,
            ),
            make_effect(effect_type="Regeneration", modifier_table="Melee_Ones"),
            nid=500,
            full_name="Boosts.Test.Test",
        ),
        make_assembly_power(make_effect(effect_type="GrantPower", n_summon=700), nid=501),
        make_assembly_power(make_effect(effect_type="Heal"), nid=700, power_type="Click"),
    ]


def test_enhancement_effects_are_copied(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    out = _assemble(make_build_power(make_effect()), _enh_powers(make_effect, make_assembly_power), [_slot(50)])
    assert _types(out) == ["Defense", "Recovery", "Regeneration"]
    rec = out[1]
    assert (rec.is_enhancement_effect, rec.ignore_scaling, rec.absorbed_effect, rec.buffable) == (
        True,
        False,
        True,
        False,
    )
    # Mids' copy of the effect does not carry Absorbed_Power_nID.
    assert (rec.to_who, rec.ticks, rec.absorbed_power_nid) == ("Target", 3, -1)


def test_proc_enhancement_with_boosts_drops_boost_tables_and_ignores_scaling(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    out = _assemble(make_build_power(), _enh_powers(make_effect, make_assembly_power), [_slot(52)])
    assert _types(out) == ["Regeneration"]
    assert out[0].ignore_scaling


@pytest.mark.parametrize("enh", [-1, 51, 53, 54, 55])
def test_enhancements_that_add_nothing(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower, enh: int
) -> None:
    assert _assemble(make_build_power(), _enh_powers(make_effect, make_assembly_power), [_slot(enh)]) == ()


def test_proc_include_skips_proc_enhancements(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    bp = make_build_power(proc_include=True)
    assert _assemble(bp, _enh_powers(make_effect, make_assembly_power), [_slot(52)]) == ()


@pytest.mark.parametrize("enh", [57, 58, 59])
def test_enhancement_pointing_at_a_missing_record_refuses(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower, enh: int
) -> None:
    with pytest.raises(ValueError, match="E90"):
        _assemble(make_build_power(), _enh_powers(make_effect, make_assembly_power), [_slot(enh)])


def test_enhancement_grant_runs_the_grant_step(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    out = _assemble(make_build_power(), _enh_powers(make_effect, make_assembly_power), [_slot(56)])
    assert _types(out) == ["GrantPower", "Heal"]
    assert out[0].effect_class == "Ignored"


@pytest.mark.parametrize(
    ("pet_powers", "copies"),
    [
        # One copy per pet power with an effect of the same type, and one more when the last pet power has one.
        ((frozenset({"Heal"}), frozenset({"Damage"}), frozenset({"Heal", "Damage"})), 3),
        ((frozenset({"Heal"}), frozenset({"Heal"}), frozenset({"Damage"})), 2),
        ((), 0),
    ],
)
def test_pets_only_enhancement_on_a_summon_copies_per_matching_pet_power(
    pet_powers: tuple[frozenset[str], ...],
    copies: int,
    make_effect: MakeEffect,
    make_assembly_power: MakeAssemblyPower,
    make_build_power: MakeBuildPower,
) -> None:
    powers = [make_assembly_power(make_effect(effect_type="Heal", reward="Set_Bonus.Pet"), nid=500)]
    pet = AssemblyEntity(
        nid=41, uid="Pets_Decoy", class_nid=18, powerset_nids=(), first_powerset_effect_types=pet_powers
    )
    summon = make_build_power(make_effect(effect_type="EntCreate", probability=0.5, summon="Pets_Decoy"))
    out = _assemble(summon, powers, [_slot(60)], entities=_entities(pet))
    assert _types(out) == ["EntCreate"] + ["Heal"] * copies
    assert all(fx.is_enhancement_effect and not fx.buffable for fx in out[1:])


def test_pets_only_enhancement_with_an_unknown_entity_refuses(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    powers = [make_assembly_power(make_effect(reward="Set_Bonus.Pet"), nid=500)]
    summon = make_build_power(make_effect(effect_type="EntCreate", probability=0.5, summon="Pets_Missing"))
    with pytest.raises(NotImplementedError, match="E89"):
        _assemble(summon, powers, [_slot(60)])


def test_pets_only_grant_runs_the_grant_step(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    grant = make_effect(effect_type="GrantPower", reward="Set_Bonus.Pet", n_summon=600)
    powers = [make_assembly_power(grant, nid=500), make_assembly_power(make_effect(effect_type="Heal"), nid=600)]
    pet = AssemblyEntity(
        nid=41,
        uid="Pets_Decoy",
        class_nid=18,
        powerset_nids=(),
        first_powerset_effect_types=(frozenset({"GrantPower"}),),
    )
    summon = make_build_power(make_effect(effect_type="EntCreate", probability=0.5, summon="Pets_Decoy"))
    out = _assemble(summon, powers, [_slot(60)], entities=_entities(pet))
    assert _types(out) == ["EntCreate", "GrantPower", "GrantPower", "Heal", "Heal"]


@pytest.mark.parametrize(("summon", "enh"), [(True, 50), (False, 60)])
def test_set_bonus_reward_without_pets_only_is_copied(
    make_effect: MakeEffect,
    make_assembly_power: MakeAssemblyPower,
    make_build_power: MakeBuildPower,
    summon: bool,
    enh: int,
) -> None:
    powers = [make_assembly_power(make_effect(effect_type="Heal", reward="Set_Bonus.Pet"), nid=500)]
    own = (make_effect(effect_type="EntCreate", probability=0.5),) if summon else ()
    assert _types(_assemble(make_build_power(*own), powers, [_slot(enh)]))[-1] == "Heal"


# --- step 3: pet absorption ----------------------------------------------------------------------------


def _create(make_effect: MakeEffect, entity: int = 40, **over: Any) -> Effect:
    return make_effect(**{"effect_type": "EntCreate", "n_summon": entity, "duration": 30.0, **over})


def test_pet_effects_are_absorbed(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    pets = [
        make_assembly_power(
            make_effect(effect_type="Damage", to_who="Target"),
            make_effect(effect_type="Heal", delayed_time=0.5),
            nid=600,
            power_type="Auto_",
            activate_period=2.0,
            entities_affected=frozenset({"Foe"}),
            entities_auto_hit=frozenset({"Foe"}),
        ),
        make_assembly_power(make_effect(effect_type="Resistance"), nid=601),
        make_assembly_power(make_effect(effect_type="Endurance"), nid=602, entities_affected=frozenset({"Foe"})),
    ]
    entities = _entities(_entity(40, 70, -1, 71, class_nid=18), _powerset(70, 600, 601), _powerset(71, 602))
    bp = make_build_power(_create(make_effect, delayed_time=1.5), absorb_summon_effects=True)
    out = _assemble(bp, pets, entities=entities)
    # 601 hits only its caster, so only its EntCreate effects would cross; it has none.
    assert _types(out) == ["EntCreate", "Damage", "Heal", "Endurance"]
    damage = out[1]
    assert (damage.absorbed_class_n_id, damage.absorbed_power_nid, damage.absorbed_power_type) == (18, 600, "Auto_")
    assert (damage.ticks, damage.delayed_time, damage.to_who) == (16, 1.5, "Target")


def test_pet_creates_are_followed_one_level(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    pets = [
        make_assembly_power(
            _create(make_effect, 41),
            _create(make_effect, 42),
            _create(make_effect, 43),
            make_effect(effect_type="EntCreate", n_summon=-1),
            nid=600,
        ),
        make_assembly_power(make_effect(effect_type="Heal"), nid=610, entities_affected=frozenset({"Foe"})),
    ]
    entities = _entities(
        _entity(40, 70), _entity(41, 71), _entity(42), _entity(43, -1), _powerset(70, 600), _powerset(71, 610)
    )
    bp = make_build_power(_create(make_effect), absorb_summon_effects=True)
    out = _assemble(bp, pets, entities=entities)
    assert _types(out) == ["EntCreate"] * 5 + ["Heal"]
    assert out[-1].absorbed_class_n_id == 18


@pytest.mark.parametrize("over", [{"n_summon": -1}, {"probability": 0.5}, {"effect_type": "Defense"}])
def test_creates_that_are_not_absorbed(
    make_effect: MakeEffect, make_build_power: MakeBuildPower, over: dict[str, Any]
) -> None:
    bp = make_build_power(_create(make_effect, **over), absorb_summon_effects=True)
    assert len(_assemble(bp)) == 1


def test_absorption_needs_a_flag(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    assert len(_assemble(make_build_power(_create(make_effect)))) == 1


def test_entity_without_powersets_adds_nothing(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    bp = make_build_power(_create(make_effect), absorb_summon_effects=True, absorb_summon_attributes=True)
    assert len(_assemble(bp, entities=_entities(_entity(40)))) == 1


@pytest.mark.parametrize("entities", [NO_ENTITIES, AssemblyEntities(entities={40: _entity(40, 70)}, powersets={})])
def test_missing_entity_or_powerset_refuses(
    make_effect: MakeEffect, make_build_power: MakeBuildPower, entities: AssemblyEntities
) -> None:
    bp = make_build_power(_create(make_effect), absorb_summon_effects=True)
    with pytest.raises(NotImplementedError, match="E89"):
        _assemble(bp, entities=entities)


def _all_grant(make_effect: MakeEffect) -> Effect:
    return make_effect(effect_type="GrantPower", n_summon=700, to_who="All")


@pytest.mark.parametrize(
    ("auto_hit", "to_who"),
    [
        # Hitting only its caster, the power copies every pet power in turn; the last one affects only foes.
        (frozenset({"Caster"}), "Target"),
        (frozenset(), "Target"),
        # Hitting others, it stops at the first, which affects everyone.
        (frozenset({"Foe"}), "All"),
    ],
)
def test_pet_attributes_are_copied(
    make_effect: MakeEffect,
    make_assembly_power: MakeAssemblyPower,
    make_build_power: MakeBuildPower,
    auto_hit: frozenset[str],
    to_who: str,
) -> None:
    everyone = frozenset({"Caster", "Friend", "Foe"})
    pets = [
        make_assembly_power(nid=600, entities_affected=everyone, ignore_enh=frozenset({"Range"})),
        make_assembly_power(nid=601, entities_affected=frozenset({"Foe"})),
        make_assembly_power(make_effect(effect_type="Heal", to_who="Target"), nid=700),
    ]
    bp = make_build_power(
        _create(make_effect),
        _all_grant(make_effect),
        absorb_summon_attributes=True,
        has_grant_power_effect=True,
        entities_auto_hit=auto_hit,
    )
    out = _assemble(bp, pets, entities=_entities(_entity(40, 70), _powerset(70, 600, 601)))
    assert out[-1].to_who == to_who


def test_attributes_skip_an_empty_first_powerset(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    pets = [make_assembly_power(make_effect(effect_type="Heal", to_who="Target"), nid=700)]
    bp = make_build_power(
        _create(make_effect), _all_grant(make_effect), absorb_summon_attributes=True, has_grant_power_effect=True
    )
    assert _assemble(bp, pets, entities=_entities(_entity(40, -1)))[-1].to_who == "Target"


def test_pet_duration_rule(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    pets = [
        make_assembly_power(
            make_effect(effect_type="Heal", n_duration=99.0),
            nid=600,
            power_set_type="Pet",
            entities_affected=frozenset(),
        ),
        make_assembly_power(
            make_effect(effect_type="SilentKill", to_who="Self", delayed_time=20.0),
            make_effect(effect_type="SilentKill", to_who="Target", delayed_time=50.0),
            make_effect(effect_type="SilentKill", to_who="Self"),
            make_effect(effect_type="Defense", to_who="Self", delayed_time=60.0),
            nid=601,
            power_set_type="Pet",
            entities_affected=frozenset(),
        ),
    ]
    entities = _entities(_entity(40, 70), _powerset(70, -1, 600, 601))
    # The create's 30 s is longer than the pet's 20 s life, so the absorbed effects last 20 s.
    out = _assemble(make_build_power(_create(make_effect), absorb_summon_effects=True), pets, entities=entities)
    assert [fx.n_duration for fx in out[1:]] == [20.0] * 5


@pytest.mark.parametrize(("create_duration", "expected"), [(0.0, 20.0), (10.0, 10.0)])
def test_pet_duration_rule_keeps_a_shorter_create(
    make_effect: MakeEffect,
    make_assembly_power: MakeAssemblyPower,
    make_build_power: MakeBuildPower,
    create_duration: float,
    expected: float,
) -> None:
    pets = [
        make_assembly_power(
            make_effect(effect_type="Heal", n_duration=99.0),
            make_effect(effect_type="SilentKill", to_who="Self", delayed_time=20.0),
            nid=600,
            power_set_type="Pet",
            entities_affected=frozenset(),
        ),
    ]
    entities = _entities(_entity(40, 70), _powerset(70, 600))
    bp = make_build_power(_create(make_effect, duration=create_duration), absorb_summon_effects=True)
    assert _assemble(bp, pets, entities=entities)[1].n_duration == expected


# --- step 4: GrantPower replacement --------------------------------------------------------------------


def _grant(make_effect: MakeEffect, **over: Any) -> Effect:
    return make_effect(**{"effect_type": "GrantPower", "n_summon": 700, "summon": "Granted", "duration": 5.0, **over})


def test_grant_is_replaced_by_the_granted_effects(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(
        make_effect(effect_type="Heal", to_who="Target", ticks=9),
        make_effect(effect_type="Endurance", to_who="Target"),
        nid=700,
        power_type="Click",
    )
    bp = make_build_power(_grant(make_effect, to_who="Self", is_enhancement_effect=True), has_grant_power_effect=True)
    out = _assemble(bp, [granted])
    assert _types(out) == ["GrantPower", "Heal", "Endurance"]
    assert out[0].effect_class == "Ignored"
    heal, endurance = out[1], out[2]
    assert (heal.absorbed_effect, heal.absorbed_power_type, heal.absorbed_power_nid) == (True, "Click", 700)
    assert (heal.absorbed_class_n_id, heal.to_who, heal.ticks, heal.is_enhancement_effect) == (AT, "Target", 9, False)
    # Only the last absorbed effect takes the grant's target and enhancement flag.
    assert (endurance.to_who, endurance.is_enhancement_effect) == ("Self", True)


def test_grant_needs_the_power_flag(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(make_effect(effect_type="Heal"), nid=700)
    assert _types(_assemble(make_build_power(_grant(make_effect)), [granted])) == ["GrantPower"]


@pytest.mark.parametrize("over", [{"effect_class": "Ignored"}, {"n_summon": -1}, {"effect_type": "Defense"}])
def test_effects_that_grant_nothing(
    make_effect: MakeEffect, make_build_power: MakeBuildPower, over: dict[str, Any]
) -> None:
    out = _assemble(make_build_power(_grant(make_effect, **over), has_grant_power_effect=True))
    assert len(out) == 1


@pytest.mark.parametrize(
    ("auto_hit", "power_type", "to_who", "ticks"),
    [
        (frozenset({"Friend"}), "Click", "Target", 0),
        (frozenset({"MyPet"}), "Click", "Target", 0),
        (frozenset({"Friend", "Caster"}), "Click", "Self", 0),
        (frozenset({"Caster"}), "Auto_", "Self", 3),
        (frozenset({"Caster"}), "Toggle", "Self", 3),
    ],
)
def test_granted_power_target_and_ticks(
    make_effect: MakeEffect,
    make_assembly_power: MakeAssemblyPower,
    make_build_power: MakeBuildPower,
    auto_hit: frozenset[str],
    power_type: str,
    to_who: str,
    ticks: int,
) -> None:
    granted = make_assembly_power(
        make_effect(effect_type="Heal"),
        make_effect(effect_type="Heal"),
        nid=700,
        entities_auto_hit=auto_hit,
        power_type=power_type,
        activate_period=2.0,
    )
    out = _assemble(make_build_power(_grant(make_effect), has_grant_power_effect=True), [granted])
    assert (out[1].to_who, out[1].ticks) == (to_who, ticks)


def test_granted_activate_period_zero_gives_no_ticks(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(make_effect(effect_type="Heal", ticks=4), nid=700, power_type="Auto_")
    out = _assemble(make_build_power(_grant(make_effect), has_grant_power_effect=True), [granted])
    assert out[1].ticks == 0


def test_last_effect_rewrites(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(make_effect(effect_type="Heal", to_who="Target"), nid=700, power_type="Click")
    grant = _grant(make_effect, to_who="All", absorbed_power_nid=12, absorbed_power_type="Boost")
    out = _assemble(make_build_power(grant, has_grant_power_effect=True), [granted])
    assert (out[1].absorbed_power_type, out[1].to_who) == ("Boost", "Target")


def test_all_stays_all_when_the_power_affects_everyone(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(make_effect(effect_type="Heal"), nid=700)
    bp = make_build_power(
        _grant(make_effect, to_who="All"),
        has_grant_power_effect=True,
        entities_affected=frozenset({"Caster", "Friend", "Foe"}),
    )
    assert _assemble(bp, [granted])[1].to_who == "All"


def test_nested_grants_run_in_rounds(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    middle = make_assembly_power(
        make_effect(effect_type="Heal"), _grant(make_effect, n_summon=701, to_who="Target"), nid=700
    )
    inner = make_assembly_power(make_effect(effect_type="Endurance"), nid=701)
    out = _assemble(make_build_power(_grant(make_effect), has_grant_power_effect=True), [middle, inner])
    assert _types(out) == ["GrantPower", "Heal", "GrantPower", "Endurance"]
    # A granted GrantPower keeps its own target.
    assert out[2].to_who == "Target"
    assert [fx.effect_class for fx in out if fx.effect_type == "GrantPower"] == ["Ignored", "Ignored"]


def test_grant_cycle_stops_after_a_hundred_rounds(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    loop = make_assembly_power(_grant(make_effect), nid=700)
    out = _assemble(make_build_power(_grant(make_effect), has_grant_power_effect=True), [loop])
    assert len(out) == 101
    assert out[-1].effect_class == "Primary"


def test_grant_probability_below_one_multiplies(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(make_effect(effect_type="Heal", probability=0.5, base_probability=0.5), nid=700)
    grant = _grant(make_effect, probability=0.4, base_probability=0.4)
    out = _assemble(make_build_power(grant, has_grant_power_effect=True), [granted])
    assert out[1].probability == f32(0.5 * 0.4)
    assert out[1].base_probability == out[1].probability


def test_ppm_and_expression_probability_are_not_overwritten(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(
        make_effect(effect_type="Heal", probability=0.5, procs_per_minute=3.0),
        make_effect(effect_type="Heal", probability=0.5, attrib_type="Expression", expr_probability="1"),
        nid=700,
    )
    grant = _grant(make_effect, probability=0.4, base_probability=0.4)
    out = _assemble(make_build_power(grant, grant, has_grant_power_effect=True), [granted])
    assert [fx.probability for fx in out[2:]] == [0.5] * 4
    assert out[3].base_probability == f32(0.5 * 0.4)


@pytest.mark.parametrize("met", [True, False])
def test_grant_conditions_are_asked(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower, met: bool
) -> None:
    granted = make_assembly_power(make_effect(effect_type="Heal"), nid=700)
    grant = _grant(make_effect, conditionals=(("Active:Pool.Fighting.Boxing", "True"),), active_conditionals_count=1)
    asked: list[Effect] = []

    def conditions(fx: Effect) -> bool:
        asked.append(fx)
        return met

    out = _assemble(make_build_power(grant, has_grant_power_effect=True), [granted], conditions=conditions)
    assert asked[0] == grant
    assert len(out) == (2 if met else 1)


def test_grant_special_case_refuses(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    bp = make_build_power(_grant(make_effect, special_case="Domination"), has_grant_power_effect=True)
    with pytest.raises(NotImplementedError, match="E91"):
        _assemble(bp)


def test_missing_granted_power_refuses(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    with pytest.raises(ValueError, match="E90"):
        _assemble(make_build_power(_grant(make_effect), has_grant_power_effect=True))


def test_pet_power_without_its_powerset_refuses(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(make_effect(), nid=700, power_set_type="Pet")
    with pytest.raises(NotImplementedError, match="E89"):
        _assemble(make_build_power(_grant(make_effect), has_grant_power_effect=True), [granted])


# --- step 5: ExecutePower expansion --------------------------------------------------------------------


def _execute(make_effect: MakeEffect, summon: str = "Executed", **over: Any) -> Effect:
    return make_effect(**{"effect_type": "ExecutePower", "summon": summon, **over})


def test_execute_is_replaced_in_place(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    executed = make_assembly_power(
        make_effect(effect_type="Heal"), make_effect(effect_type="Endurance"), nid=800, full_name="Executed"
    )
    bp = make_build_power(
        make_effect(effect_type="Defense"), _execute(make_effect), make_effect(effect_type="Recovery")
    )
    assert _types(_assemble(bp, [executed])) == ["Defense", "Heal", "Endurance", "Recovery"]


@pytest.mark.parametrize("summon", ["", "Unknown"])
def test_execute_without_a_power_is_dropped(
    make_effect: MakeEffect, make_build_power: MakeBuildPower, summon: str
) -> None:
    assert _assemble(make_build_power(_execute(make_effect, summon))) == ()


def test_executed_effects_inherit_from_the_caller(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    executed = make_assembly_power(
        make_effect(
            effect_type="Heal",
            n_duration=3.0,
            delayed_time=0.25,
            base_probability=0.9,
            conditionals=(("Active:A", "True"),),
            active_conditionals_count=1,
        ),
        make_effect(effect_type="Heal", base_probability=0.3, ticks=2),
        nid=800,
        full_name="Executed",
    )
    caller = _execute(
        make_effect,
        n_duration=10.0,
        delayed_time=0.5,
        base_probability=0.6,
        conditionals=(("Active:B", "True"),),
        active_conditionals_count=1,
        ticks=7,
    )
    first, second = _assemble(make_build_power(caller), [executed])
    assert (first.n_duration, first.delayed_time, first.probability, first.base_probability) == (10.0, 0.75, 0.6, 0.6)
    assert first.conditionals == (("Active:A", "True"), ("Active:B", "True"))
    assert (first.active_conditionals_count, first.ticks) == (2, 7)
    assert (second.probability, second.ticks) == (0.3, 2)


@pytest.mark.parametrize("caller_probability", [0.0, 1.0])
def test_executed_probability_kept_for_a_certain_or_zero_caller(
    make_effect: MakeEffect,
    make_assembly_power: MakeAssemblyPower,
    make_build_power: MakeBuildPower,
    caller_probability: float,
) -> None:
    executed = make_assembly_power(
        make_effect(effect_type="Heal", n_duration=3.0, base_probability=0.4), nid=800, full_name="Executed"
    )
    caller = _execute(make_effect, base_probability=caller_probability)
    (out,) = _assemble(make_build_power(caller), [executed])
    assert (out.probability, out.n_duration) == (0.4, 3.0)


def test_execute_cycle_stops_at_depth_five(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    loop = make_assembly_power(make_effect(effect_type="Heal"), _execute(make_effect), nid=800, full_name="Executed")
    out = _assemble(make_build_power(_execute(make_effect)), [loop])
    assert _types(out) == ["Heal"] * 5


# --- step 6: sub-powers --------------------------------------------------------------------------------


def test_stat_include_sub_powers_are_appended(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    sub = make_assembly_power(make_effect(effect_type="Heal"), nid=900, power_type="Toggle")
    bp = make_build_power(make_effect(), sub_powers=((900, True), (900, False), (-1, True)))
    out = _assemble(bp, [sub])
    assert _types(out) == ["Defense", "Heal"]
    assert (out[1].absorbed_effect, out[1].absorbed_power_nid, out[1].absorbed_power_type) == (True, 900, "Toggle")


def test_missing_sub_power_refuses(make_build_power: MakeBuildPower) -> None:
    with pytest.raises(ValueError, match="E90"):
        _assemble(make_build_power(sub_powers=((900, True),)))


# --- the cross-power GrantPower pass -------------------------------------------------------------------


def _build(
    entries: Mapping[int, BuildPowerAssembly],
    powers: Sequence[AssemblyPower] = (),
    stat_include: Mapping[int, bool] | None = None,
    set_bonus_effects: Sequence[Effect] = (),
) -> dict[int, tuple[Effect, ...]]:
    out = assemble_build(
        entries,
        {},
        stat_include=stat_include or dict.fromkeys(entries, True),
        enh_db=ENH_DB,
        set_db=SET_DB,
        powers={p.nid: p for p in powers},
        entities=NO_ENTITIES,
        archetype_index=AT,
        conditions=_never,
        set_bonus_effects=set_bonus_effects,
    )
    return {i: assembled.effects for i, assembled in out.items()}


def _target(make_build_power: MakeBuildPower, make_effect: MakeEffect, **over: Any) -> BuildPowerAssembly:
    return make_build_power(
        make_effect(effect_type="Defense"), **{"build_index": 0, "nid": 10, "enhancements": frozenset({1, 2}), **over}
    )


def _global(make_build_power: MakeBuildPower, *effects: Effect, **over: Any) -> BuildPowerAssembly:
    return make_build_power(
        *effects,
        **{
            "build_index": 1,
            "nid": 20,
            "power_type": "GlobalBoost",
            "power_set_type": "Boost",
            "enhancements": frozenset({2}),
            "entities_auto_hit": frozenset({"Friend"}),
            **over,
        },
    )


def test_global_boost_effect_is_absorbed_with_no_grant_probability(
    make_effect: MakeEffect, make_build_power: MakeBuildPower
) -> None:
    source = _global(make_build_power, make_effect(effect_type="Recovery", ticks=4, is_enhancement_effect=True))
    out = _build({0: _target(make_build_power, make_effect), 1: source})
    assert _types(out[0]) == ["Defense", "Recovery"]
    absorbed = out[0][1]
    assert (absorbed.index, absorbed.to_who, absorbed.absorbed_effect, absorbed.is_enhancement_effect) == (
        1,
        "Target",
        True,
        True,
    )
    assert (absorbed.absorbed_power_type, absorbed.absorbed_power_nid, absorbed.ticks) == ("GlobalBoost", 20, 4)
    # No grant of the source covers the position, so Mids reads a grant probability of 0.
    assert absorbed.probability == 0.0
    assert _types(out[1]) == ["Recovery"]


def test_absorbed_effect_takes_the_grant_probability_it_falls_under(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(
        make_effect(effect_type="Recovery", base_probability=0.5),
        nid=700,
        power_type="GlobalBoost",
        enhancements=frozenset({2}),
    )
    source = _global(make_build_power, _grant(make_effect, base_probability=0.8), has_grant_power_effect=True)
    out = _build({0: _target(make_build_power, make_effect), 1: source}, [granted])
    absorbed = out[0][1]
    assert absorbed.effect_type == "Recovery"
    # The source's own grant step left 0.5 * 0.8; the cross pass multiplies by the grant once more.
    assert absorbed.probability == f32(f32(0.5 * 0.8) * 0.8)


def test_grant_probability_map_skips_a_grant_without_a_power(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    source = _global(
        make_build_power,
        make_effect(effect_type="Recovery"),
        make_effect(effect_type="GrantPower", n_summon=-1, effect_class="Ignored"),
        make_effect(effect_type="GrantPower", n_summon=701, base_probability=0.25, effect_class="Ignored"),
    )
    out = _build({0: _target(make_build_power, make_effect), 1: source}, [make_assembly_power(make_effect(), nid=701)])
    # The one absorbed position sits past the source's three database effects, inside the second grant.
    assert out[0][1].probability == 0.25


@pytest.mark.parametrize(
    "over",
    [
        {"effect_class": "Ignored"},
        {"effect_type": "Enhancement"},
        {"effect_type": "DamageBuff"},
        {"ignore_ed": True},
        {"effect_type": "GrantPower", "absorbed_effect": True, "absorbed_power_type": "GlobalBoost"},
    ],
)
def test_source_effects_that_do_not_cross(
    make_effect: MakeEffect, make_build_power: MakeBuildPower, over: dict[str, Any]
) -> None:
    source = _global(make_build_power, make_effect(**{"effect_type": "Recovery", **over}))
    assert len(_build({0: _target(make_build_power, make_effect), 1: source})[0]) == 1


def test_non_global_source_crosses_only_global_absorbed_effects(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    owner = make_assembly_power(nid=30, enhancements=frozenset({1}))
    source = _global(
        make_build_power,
        make_effect(effect_type="Recovery"),
        make_effect(effect_type="Heal", absorbed_effect=True, absorbed_power_type="GlobalBoost", absorbed_power_nid=30),
        make_effect(effect_type="Endurance", absorbed_effect=True, absorbed_power_type="GlobalBoost"),
        power_type="Auto_",
        enhancements=frozenset({9}),
    )
    out = _build({0: _target(make_build_power, make_effect), 1: source}, [owner])
    # Heal is allowed through its owner's enhancements; Endurance's owner is the source, which shares none.
    assert _types(out[0]) == ["Defense", "Heal"]
    assert out[0][1].ticks == 0


@pytest.mark.parametrize(
    "over", [{"enhancements": frozenset()}, {"power_set_type": "Pet"}, {"enhancements": frozenset({7})}]
)
def test_targets_that_take_nothing(
    make_effect: MakeEffect, make_build_power: MakeBuildPower, over: dict[str, Any]
) -> None:
    source = _global(make_build_power, make_effect(effect_type="Recovery"))
    assert len(_build({0: _target(make_build_power, make_effect, **over), 1: source})[0]) == 1


def test_empty_or_inactive_sources_give_nothing(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    target = _target(make_build_power, make_effect)
    assert len(_build({0: target, 1: _global(make_build_power)})[0]) == 1
    source = _global(make_build_power, make_effect(effect_type="Recovery"))
    assert len(_build({0: target, 1: source}, stat_include={0: True, 1: False})[0]) == 1


def test_global_grant_rewrites_every_target_effect(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(make_effect(effect_type="Heal", base_probability=0.5), nid=700)
    grant = _grant(make_effect, base_probability=0.5, ticks=6, is_enhancement_effect=True)
    source = _global(make_build_power, grant)
    target = _target(make_build_power, make_effect)
    out = _build({0: target, 1: source}, [granted])[0]
    assert _types(out) == ["Defense", "Heal"]
    assert all(
        (fx.to_who, fx.absorbed_effect, fx.is_enhancement_effect, fx.ticks) == ("Target", True, True, 6) for fx in out
    )
    # The target's own effect: base 1 * 0.5, then * 0.5. The absorbed one: base 0.5 * 0.5, then * 0.5, then the
    # grant it falls under (0.5) once more.
    assert out[0].probability == 0.25
    assert out[1].probability == 0.0625


def test_pet_powerset_source_refuses(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    source = _global(make_build_power, make_effect(effect_type="Recovery"), power_set_type="Pet")
    with pytest.raises(NotImplementedError, match="E89"):
        _build({0: _target(make_build_power, make_effect), 1: source})


def test_sources_carry_what_they_absorbed(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    first = _target(make_build_power, make_effect, build_index=0, nid=10)
    source = _global(make_build_power, make_effect(effect_type="Recovery"))
    last = _target(make_build_power, make_effect, build_index=2, nid=11)
    out = _build({0: first, 1: source, 2: last})
    # Power 0 absorbed Recovery from 1, so power 2 takes it from both 0 and 1.
    assert _types(out[0]) == ["Defense", "Recovery"]
    assert _types(out[2]) == ["Defense", "Recovery", "Recovery"]
    assert [fx.absorbed_power_nid for fx in out[2][1:]] == [10, 20]


# --- the build's chance modifiers ----------------------------------------------------------------------


def test_absorbed_chance_ids_take_the_source_rewards(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    granted = make_assembly_power(
        make_effect(effect_type="Damage", base_probability=0.0, effect_id="DoT"),
        make_effect(effect_type="Heal", base_probability=0.0, effect_id="  "),
        nid=700,
        power_type="GlobalBoost",
    )
    source = _global(
        make_build_power,
        _grant(make_effect, effect_id="Root"),
        make_effect(effect_type="GlobalChanceMod", reward="DoT", scale=0.5, effect_class="Ignored"),
        make_effect(effect_type="GlobalChanceMod", reward="DoT", scale=0.25, effect_class="Ignored"),
    )
    other = make_build_power(
        make_effect(effect_type="GlobalChanceMod", reward="Root", scale=0.1), build_index=2, nid=30
    )
    second = _target(make_build_power, make_effect, build_index=3, nid=11)
    out = _build({0: _target(make_build_power, make_effect), 1: source, 2: other, 3: second}, [granted])
    # The ids enter the table at the sum of the source's rewards for them, before any GlobalChanceMod: DoT at
    # 0.75 and Root at 0, so the grant it falls under reads 1, and Root keeps 0 though power 2 rewards it.
    # The second target finds both ids already entered.
    for target in (0, 3):
        damage, heal = out[target][1], out[target][2]
        assert (damage.effect_type, damage.probability) == ("Damage", 0.75)
        assert (heal.effect_type, heal.probability) == ("Heal", 0.0)


def test_global_chance_mods_raise_probability(make_effect: MakeEffect, make_build_power: MakeBuildPower) -> None:
    raised = make_build_power(make_effect(effect_type="Regeneration", base_probability=0.0, effect_id="Regen"))
    rewards = make_build_power(
        make_effect(effect_type="GlobalChanceMod", reward="Regen", scale=1.0),
        make_effect(effect_type="GlobalChanceMod", reward="Regen", scale=-1.0),
        make_effect(effect_type="GlobalChanceMod", reward="", scale=1.0),
        make_effect(effect_type="Defense", reward="Regen", scale=-1.0),
        build_index=1,
    )
    inactive = make_build_power(make_effect(effect_type="GlobalChanceMod", reward="Regen", scale=-1.0), build_index=2)
    out = _build({0: raised, 1: rewards, 2: inactive}, stat_include={0: True, 1: True, 2: False})
    assert out[0][0].probability == 1.0


def test_set_bonus_chance_mods_follow_the_build_powers(
    make_effect: MakeEffect, make_build_power: MakeBuildPower
) -> None:
    raised = make_build_power(make_effect(effect_type="Regeneration", base_probability=0.0, effect_id="Regen"))
    other = make_build_power(make_effect(effect_type="Heal", base_probability=0.0, effect_id="Other"), build_index=1)
    rewards = make_build_power(make_effect(effect_type="GlobalChanceMod", reward="Regen", scale=1.0), build_index=2)
    bonus = (
        make_effect(effect_type="GlobalChanceMod", reward="Regen", scale=-1.0),
        make_effect(effect_type="GlobalChanceMod", reward="Other", scale=0.25),
        make_effect(effect_type="GlobalChanceMod", reward="Other", scale=0.5),
        make_effect(effect_type="Defense", reward="Other", scale=1.0),
    )
    out = _build({0: raised, 1: other, 2: rewards}, set_bonus_effects=bonus)
    assert (out[0][0].probability, out[1][0].probability) == (1.0, 0.25)


def test_pet_attributes_reach_the_assembled_power(
    make_effect: MakeEffect, make_assembly_power: MakeAssemblyPower, make_build_power: MakeBuildPower
) -> None:
    pet = make_assembly_power(nid=300, ignore_enh=frozenset({"RechargeTime"}))
    entities = _entities(_entity(40, 30), _powerset(30, 300))
    absorber = make_build_power(_create(make_effect, probability=1.0), absorb_summon_attributes=True)
    assembled = assemble_power(
        absorber,
        (),
        enh_db=ENH_DB,
        set_db=SET_DB,
        powers={300: pet},
        entities=entities,
        archetype_index=AT,
        conditions=_never,
    )
    assert assembled.ignore_enh == frozenset({"RechargeTime"})


@pytest.mark.parametrize(
    ("own", "absorbed", "want"),
    [
        (("Range", "Accuracy"), frozenset({"Accuracy", "Range"}), ("Range", "Accuracy")),
        ((), frozenset({"B", "A"}), ("A", "B")),
    ],
)
def test_apply_assembly_hands_the_powers_their_assembled_state(
    own: tuple[str, ...],
    absorbed: frozenset[str],
    want: tuple[str, ...],
    make_effect: MakeEffect,
    make_power: MakePower,
    make_build_power: MakeBuildPower,
) -> None:
    power = make_power(make_effect(effect_type="Defense"), build_index=2, ignore_enh=own)
    entry = make_build_power(build_index=2, aoe_modifier=1.5, base_recharge_time=8.0)
    effects = (make_effect(effect_type="Heal"),)
    (out,) = apply_assembly([power], {2: entry}, {2: AssembledPower(effects=effects, ignore_enh=absorbed)})
    assert (out.effects, out.ignore_enh, out.aoe_modifier, out.base_recharge_time) == (effects, want, 1.5, 8.0)
