"""Chain report: context building, the chain-rule registry, and the text and JSON views."""

import json
import shutil
import subprocess
import sys
from dataclasses import replace
from pathlib import Path
from typing import Any

import pytest

from coh_engine import chain_report
from coh_engine.archetypes import load_archetypes
from coh_engine.attack_chain import ChainOption, arcana_cast_time
from coh_engine.attack_profile import canonical_resolver, classify_attack
from coh_engine.attribmod import get_modifier, load_attribmod
from coh_engine.chain_report import (
    ChainSetup,
    build_chain_setup,
    build_damage_context,
    parse_assumptions,
    parse_target,
    registered_chain_rules,
    run_chain,
    stance_to_json,
    stance_to_text,
)
from coh_engine.damage_model import (
    AttackProfile,
    ChainAttack,
    DamageContext,
    Delivery,
    EnduranceBudget,
    Role,
    Shape,
    Stance,
)
from coh_engine.damage_reference import PowerDamageRef
from coh_engine.maths import f32

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"
BUILD = MIDS / "builds" / "shield_scrapper_set_bonuses"
ALL_STANCES = list(Stance)


def _setup(build: Path = BUILD, **kwargs: Any) -> ChainSetup:
    return build_chain_setup(build, mids_dir=MIDS, **kwargs)


def _rules(results: list[chain_report.StanceReport]) -> list[str]:
    return [d.rule_id for r in results for d in r.diagnostics]


def test_plugins_are_registered_by_importing_chain_report() -> None:
    """In a fresh interpreter, importing only ``chain_report`` populates the default registries."""
    src = Path(__file__).resolve().parents[2]
    code = (
        f"import sys; sys.path.insert(0, {str(src)!r}); "
        "import coh_engine.chain_report; "
        "from coh_engine.damage_model import registered_damage_modifiers as m, registered_damage_sources as s; "
        "assert 'pet_source' in s() and 'chain_source' in s(), s(); "
        "assert 'target_debuff_modifier' in m() and 'click_buff_modifier' in m(), m()"
    )
    done = subprocess.run([sys.executable, "-I", "-c", code], capture_output=True, text=True, check=False, cwd=src)
    assert done.returncode == 0, done.stderr


def test_reported_chain_is_the_one_the_chain_source_ran_on() -> None:
    """With a patch and a buff click beside the chain, the reported rotation and DPS are the chain source's."""

    def attack(name: str, role: Role, **kw: float) -> ChainAttack:
        profile = AttackProfile(name, Delivery.RANGED, Shape.SINGLE, role, 80.0, 0.0, 0.0, 0)
        return ChainAttack(name, 100.0, 1.0, 5.0, 4.0, profile, **kw)

    setup = _setup()
    attacks = (
        attack("Bolt", Role.CHAIN),
        attack("Patch", Role.PATCH),
        attack("Buff", Role.BUFF_CLICK, self_damage_buff=0.5, self_damage_buff_duration=5.0),
    )
    setup = replace(setup, context=replace(setup.context, attacks=attacks))
    result = run_chain(setup, [Stance.RANGED])[0]
    (source,) = [s for s in result.report.sources if s.source == "chain"]
    assert result.chain is not None
    assert result.chain.dps == source.dps
    assert list(result.chain.rotation) == [n for n, _ in source.detail]


def test_registered_chain_rules() -> None:
    """The diagnostics are registered rules."""
    assert registered_chain_rules() == [
        "_rule_end_002",
        "_rule_chain_001",
        "_rule_chain_002",
        "_rule_chain_002_skipped",
    ]


def test_fixture_reports_every_stance_with_positive_dps() -> None:
    """A real dump yields a report per stance; melee and hybrid have a chain with positive DPS."""
    setup = _setup()
    results = run_chain(setup, ALL_STANCES)
    assert [r.stance for r in results] == ALL_STANCES
    melee, ranged, hybrid = results
    assert melee.report.scaled_dps["chain"] > 0.0
    assert hybrid.report.scaled_dps["chain"] > 0.0
    assert melee.chain is not None
    assert ranged.chain is not None and ranged.chain.rotation == ("Epic.Stalker_Soul_Mastery.Moonbeam",)
    assert all(r.report.total_dps > 0.0 for r in results)


def test_fixture_without_clicksoff_dump_emits_p_chain_002() -> None:
    """No clicks-off dump -> an info diagnostic says click buffs count as always on."""
    setup = _setup()
    assert setup.clicks_always_on
    results = run_chain(setup, [Stance.MELEE])
    messages = [d.message for d in results[0].diagnostics if d.rule_id == "P-CHAIN-002"]
    assert any("click buffs are counted always-on" in m for m in messages)
    assert any("Shield_Charge" in m for m in messages)  # a category_disagreement message


def test_default_clicksoff_dir_is_used_and_silences_the_fallback(tmp_path: Path) -> None:
    """``<build_dir>_clicksoff`` is picked up by default; no fallback note is emitted."""
    build = tmp_path / "b"
    shutil.copytree(BUILD, build)
    shutil.copytree(BUILD, tmp_path / "b_clicksoff")
    path = tmp_path / "b_clicksoff" / "enhanced_powers.json"
    powers = json.loads(path.read_text(encoding="utf-8-sig"))
    (smite,) = [p for p in powers if p["FullName"].endswith(".Smite")]
    smite["DamagePerActivation"] = 1234.5
    path.write_text(json.dumps(powers), encoding="utf-8")
    setup = _setup(build)
    assert not setup.clicks_always_on
    assert setup.source.endswith("b_clicksoff")
    (loaded,) = [a for a in setup.context.attacks if a.full_name.endswith(".Smite")]
    assert loaded.damage == 1234.5  # the edited clicks-off value, not the build dump's
    messages = [d.message for r in run_chain(setup, [Stance.MELEE]) for d in r.diagnostics]
    assert not any("always-on" in m for m in messages)


def test_explicit_clicksoff_dir_and_context_wrapper(tmp_path: Path) -> None:
    """An explicit clicks-off dir wins; ``build_damage_context`` returns the melee context."""
    off = tmp_path / "off"
    shutil.copytree(BUILD, off)
    ctx = build_damage_context(BUILD, mids_dir=MIDS, clicksoff_dir=off)
    assert isinstance(ctx, DamageContext)
    assert ctx.stance is Stance.MELEE
    assert ctx.archetype_class == "Class_Scrapper"
    assert ctx.assumptions["chain.min_cycles"] == 4.0


def test_context_inputs_scenario_movement_and_assumptions() -> None:
    """Movement, standoff, scenario and assumptions flow into the context; baseline uses the 0.95 average."""
    setup = _setup(
        scenario=parse_target("spawn:5"), standoff_ft=30.0, assumptions={"damage.enhancement_avg": 0.5, "x.y": 1.0}
    )
    ctx = setup.context
    assert ctx.scenario.targets == 5
    assert ctx.movement.standoff_ft == 30.0
    assert ctx.movement.speed_ft_per_sec == setup.capped.run_spd
    assert ctx.assumptions["x.y"] == 1.0
    assert ctx.damage_buff_baseline == pytest.approx(1.0 + setup.capped.buff_dam + 0.5, abs=1e-5)
    default = _setup().context
    assert default.movement.standoff_ft == 40.0
    assert default.damage_buff_baseline == pytest.approx(1.0 + setup.capped.buff_dam + 0.95, abs=1e-5)


def test_jump_speed_is_read_from_capped_totals() -> None:
    """The jump mode reads ``jump_spd`` in ft/s."""
    setup = _setup(move="jump")
    assert setup.context.movement.speed_ft_per_sec == setup.capped.jump_spd > 0.0


def test_fly_without_flight_is_e69() -> None:
    """A build with no flight power has fly speed 0."""
    with pytest.raises(ValueError, match="E69"):
        _setup(move="fly")


def test_non_positive_standoff_is_e70() -> None:
    """A standoff of zero or less is rejected."""
    with pytest.raises(ValueError, match="E70"):
        _setup(standoff_ft=0.0)


def test_missing_explicit_clicksoff_dir_is_e65(tmp_path: Path) -> None:
    """A named clicks-off dir that does not exist is an error, not a silent fallback."""
    with pytest.raises(ValueError, match="E65"):
        _setup(clicksoff_dir=tmp_path / "nope")


def test_missing_totals_and_archetypes_are_e65(tmp_path: Path) -> None:
    """A dump without ``totals.json`` or a shared dir without ``archetypes.json``."""
    with pytest.raises(ValueError, match=r"E65.*totals"):
        build_chain_setup(tmp_path, mids_dir=MIDS)
    with pytest.raises(ValueError, match=r"E65.*archetype table"):
        build_chain_setup(BUILD, mids_dir=tmp_path)


def test_unknown_class_is_e65(tmp_path: Path) -> None:
    """A ``Class`` absent from ``archetypes.json``."""
    build = tmp_path / "b"
    shutil.copytree(BUILD, build)
    totals = json.loads((build / "totals.json").read_text(encoding="utf-8-sig"))
    totals["Class"] = "Class_Nope"
    (build / "totals.json").write_text(json.dumps(totals), encoding="utf-8")
    with pytest.raises(ValueError, match=r"E65.*Class_Nope"):
        _setup(build)


def _force_role(monkeypatch: pytest.MonkeyPatch, role: Role, name: str = "Inherent.Inherent.Brawl") -> None:
    def classify(power: dict[str, Any], **kwargs: Any) -> AttackProfile:
        profile = classify_attack(power, **kwargs)
        return replace(profile, role=role) if power["full_name"] == name else profile

    monkeypatch.setattr(chain_report, "classify_attack", classify)


def test_pet_build_without_mids_db_is_e66(monkeypatch: pytest.MonkeyPatch) -> None:
    """A summon with no ``--mids-db`` names the flag."""
    _force_role(monkeypatch, Role.PET_SUMMON)
    with pytest.raises(ValueError, match=r"E66.*--mids-db"):
        _setup()


def test_pet_build_with_mids_db_builds_pets(monkeypatch: pytest.MonkeyPatch) -> None:
    """With the DB dir the summons go through ``build_pet_summons`` and land in the context."""
    _force_role(monkeypatch, Role.PET_SUMMON)
    seen: dict[str, Any] = {}

    def fake(summons: list[Any], **kwargs: Any) -> tuple[str, ...]:
        seen["names"] = [s.full_name for s in summons]
        seen["attribmod"] = kwargs["attribmod"]
        return ("pet",)

    monkeypatch.setattr(chain_report, "build_pet_summons", fake)
    setup = _setup(mids_db=FIXTURES / "attribmod")
    assert setup.context.pets == ("pet",)  # type: ignore[comparison-overlap]
    assert seen["names"] == ["Inherent.Inherent.Brawl"]
    assert seen["attribmod"].tables


CANONICAL_POWERS = Path(__file__).parents[3] / "data" / "canonical" / "powers"
ACID_MORTAR = "Mastermind_Buff.Traps.Acid_Mortar"


def _table_value(mods: Any, classes: Any, class_name: str, table: str) -> float:
    index = classes.nid_from_uid_class(class_name)
    return abs(get_modifier(mods, classes.classes, index, mods.nid_from_uid_attribmod(table), 49))


def test_linked_res_debuff_reads_a_patch_entitys_minus_res() -> None:
    """Acid Mortar: -2.0 x Ranged_Res_Dmg on every type, from its mortar entity, which lives 60 s."""
    mods = load_attribmod(FIXTURES / "attribmod" / "AttribMod.json")
    classes = load_archetypes(MIDS / "archetypes.json")
    resolve = canonical_resolver(CANONICAL_POWERS)
    power = resolve(ACID_MORTAR)
    assert power is not None
    index = classes.nid_from_uid_class("Class_Mastermind")
    magnitude, duration = chain_report.linked_res_debuff(power, resolve, mods, classes, index)
    assert magnitude == pytest.approx(2.0 * _table_value(mods, classes, "Class_Mastermind", "Ranged_Res_Dmg"), rel=1e-5)
    assert duration == 60.0


def test_linked_res_debuff_counts_freezing_rain_once() -> None:
    """Freezing Rain's two identical templates are a PvP/PvE pair; the PvE one counts once."""
    mods = load_attribmod(FIXTURES / "attribmod" / "AttribMod.json")
    classes = load_archetypes(MIDS / "archetypes.json")
    resolve = canonical_resolver(CANONICAL_POWERS)
    power = resolve("Defender_Buff.Storm_Summoning.Fog")
    assert power is not None
    index = classes.nid_from_uid_class("Class_Defender")
    magnitude, _ = chain_report.linked_res_debuff(power, resolve, mods, classes, index)
    table = _table_value(mods, classes, "Class_Defender", "Ranged_Debuff_Res_Dmg")
    assert magnitude == pytest.approx(4.0 * table, rel=1e-5)
    melt = resolve("Defender_Buff.Thermal_Radiation.Melt_Armor")
    assert melt is not None
    melt_magnitude, melt_duration = chain_report.linked_res_debuff(melt, resolve, mods, classes, index)
    assert melt_magnitude > 0.0 and melt_duration > 0.0  # its own templates, no summoned entity


def test_linked_res_debuff_with_an_unknown_table_is_e61() -> None:
    """A -Res template naming a table the AttribMod data lacks fails loud instead of counting 0."""
    mods = load_attribmod(FIXTURES / "attribmod" / "AttribMod.json")
    classes = load_archetypes(MIDS / "archetypes.json")
    template = {
        "attribs": ["Fire_Dmg"],
        "aspect": "Resistance",
        "target": "Foe",
        "table": "No_Such_Debuff_Res_Dmg",
        "scale": -1.0,
        "duration": "10 seconds",
    }
    power = {"full_name": "Tst.Set.Melt", "effects": [{"templates": [template]}]}
    with pytest.raises(ValueError, match=r"E61.*No_Such_Debuff_Res_Dmg"):
        chain_report.linked_res_debuff(power, lambda _name: None, mods, classes, 0)


def _with_patch(monkeypatch: pytest.MonkeyPatch) -> None:
    """Add a dump-zero Acid Mortar PATCH to the fixture build's attacks."""
    real = chain_report._attacks

    def attacks(*args: Any, **kwargs: Any) -> Any:
        found, skipped = real(*args, **kwargs)
        profile = AttackProfile(ACID_MORTAR, Delivery.RANGED, Shape.LOCATION, Role.PATCH, 60.0, 0.0, 0.0, 0)
        mortar = ChainAttack(ACID_MORTAR, 50.0, 1.0, 60.0, 10.0, profile)
        return (*found, mortar), skipped

    monkeypatch.setattr(chain_report, "_attacks", attacks)


def test_patch_minus_res_is_filled_from_canonical_with_the_mids_db(monkeypatch: pytest.MonkeyPatch) -> None:
    """A dump-zero patch debuff is read from its linked templates for the build's own AT."""
    _with_patch(monkeypatch)
    setup = _setup(mids_db=FIXTURES / "attribmod")
    mortar = next(a for a in setup.context.attacks if a.full_name == ACID_MORTAR)
    assert mortar.target_res_debuff > 0.0
    assert mortar.target_res_debuff_duration == 60.0


def test_patch_minus_res_without_the_mids_db_is_reported_not_modelled(monkeypatch: pytest.MonkeyPatch) -> None:
    """Without the table data the gap is reported, never silently left at zero."""
    _with_patch(monkeypatch)
    setup = _setup()
    mortar = next(a for a in setup.context.attacks if a.full_name == ACID_MORTAR)
    assert mortar.target_res_debuff == 0.0
    (skip,) = [s for s in setup.skipped if s[0] == ACID_MORTAR]
    assert "--mids-db" in skip[1]


def test_a_pet_data_gap_skips_that_summon_and_reports_it(monkeypatch: pytest.MonkeyPatch) -> None:
    """E60-E62 (entity, class table or duration missing) skip one summon with a reason; the run carries on."""
    _force_role(monkeypatch, Role.PET_SUMMON)

    def gap(summons: list[Any], **kwargs: Any) -> tuple[str, ...]:
        raise ValueError("E61: entity X has class 'minion_controllerpets' with no class in the archetype table")

    monkeypatch.setattr(chain_report, "build_pet_summons", gap)
    setup = _setup(mids_db=FIXTURES / "attribmod")
    assert setup.context.pets == ()
    (skip,) = [s for s in setup.skipped if s[0] == "Inherent.Inherent.Brawl"]
    assert "E61" in skip[1] and "pet" in skip[1]


def test_a_non_data_gap_pet_error_still_propagates(monkeypatch: pytest.MonkeyPatch) -> None:
    """Only the pet data-gap codes are tolerated; anything else fails loud."""
    _force_role(monkeypatch, Role.PET_SUMMON)

    def boom(summons: list[Any], **kwargs: Any) -> tuple[str, ...]:
        raise ValueError("E36: unknown effect_area")

    monkeypatch.setattr(chain_report, "build_pet_summons", boom)
    with pytest.raises(ValueError, match="E36"):
        _setup(mids_db=FIXTURES / "attribmod")


def test_only_e37_is_skipped_when_classifying(monkeypatch: pytest.MonkeyPatch) -> None:
    """E37 drops a power; any other error propagates."""

    def non_damaging(power: dict[str, Any], **kwargs: Any) -> AttackProfile:
        raise ValueError("E37: not damaging")

    monkeypatch.setattr(chain_report, "classify_attack", non_damaging)
    with pytest.raises(ValueError, match="E71"):
        _setup()

    def broken(power: dict[str, Any], **kwargs: Any) -> AttackProfile:
        raise ValueError("E36: bad area")

    monkeypatch.setattr(chain_report, "classify_attack", broken)
    with pytest.raises(ValueError, match="E36"):
        _setup()


def test_a_power_with_signal_that_fails_e37_is_recorded_as_skipped(monkeypatch: pytest.MonkeyPatch) -> None:
    """A damaging power the classifier rejects is dropped from the chain but named, with its reason."""
    target = "Scrapper_Melee.Dark_Melee.Smite"

    def classify(power: dict[str, Any], **kwargs: Any) -> AttackProfile:
        if power["full_name"] == target:
            raise ValueError("E37: no foe damage")
        return classify_attack(power, **kwargs)

    monkeypatch.setattr(chain_report, "classify_attack", classify)
    setup = _setup()
    reasons = dict(setup.skipped)
    assert "E37" in reasons[target]
    assert target not in [a.full_name for a in setup.context.attacks]


def test_skipped_powers_are_reported_as_p_chain_002_with_a_fix() -> None:
    """Each skipped power gets an info diagnostic naming it, its reason and a fix."""
    setup = replace(_setup(), skipped=(("A.B.Gone", "no canonical file (E35)"), ("A.B.Odd", "not damaging (E37)")))
    found = [d for d in run_chain(setup, [Stance.MELEE])[0].diagnostics if "A.B." in d.message]
    assert [d.rule_id for d in found] == ["P-CHAIN-002", "P-CHAIN-002"]
    assert {d.severity for d in found} == {"info"}
    assert "A.B.Gone" in found[0].message
    assert "no canonical file (E35)" in found[0].message
    assert all(d.fix for d in found)


MOONBEAM = "Epic.Stalker_Soul_Mastery.Moonbeam"


def _without_moonbeam(setup: ChainSetup) -> ChainSetup:
    """The fixture with its one ranged attack removed, leaving the ranged stance nothing to cast."""
    attacks = tuple(a for a in setup.context.attacks if a.full_name != MOONBEAM)
    return replace(setup, context=replace(setup.context, attacks=attacks))


def test_fixture_classifies_its_redirecting_snipe_and_skips_nothing() -> None:
    """Moonbeam has no effects of its own; its default redirect makes it a ranged chain attack."""
    setup = _setup()
    assert setup.skipped == ()
    (moonbeam,) = [a for a in setup.context.attacks if a.full_name == MOONBEAM]
    assert (moonbeam.profile.role, moonbeam.profile.delivery) == (Role.CHAIN, Delivery.RANGED)


def _ref(name: str, damage: float = 0.0) -> PowerDamageRef:
    return PowerDamageRef(name, damage, 1.0, 1.0, 1.0, 0.0, "Click", True, 0.0, 0.0, 0.0, 0.0)


def _write_power(root: Path, name: str, effects: list[dict[str, Any]]) -> None:
    path = root.joinpath(*[s.lower() for s in name.split(".")]).with_suffix(".json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"full_name": name, "effects": effects}), encoding="utf-8")


def test_candidates_keep_entity_creators_and_skip_missing_virtual_powers(tmp_path: Path) -> None:
    """Zero-signal powers count only when their file exists and creates an entity (child effects included)."""
    entity = [{"templates": [], "child_effects": [{"templates": [{"attribs": ["Create_Entity"]}]}]}]
    plain = [{"templates": [{"attribs": ["Defense"]}]}]
    _write_power(tmp_path, "A.B.Summon", entity)
    _write_power(tmp_path, "A.B.Plain", plain)
    _write_power(tmp_path, "A.B.Hit", plain)
    refs = {n: _ref(n, 5.0 if n == "A.B.Hit" else 0.0) for n in ("A.B.Summon", "A.B.Plain", "A.B.Hit", "A.B.Virtual")}
    found, skipped = chain_report._candidates(refs, tmp_path)
    assert sorted(found) == ["A.B.Hit", "A.B.Summon"]
    # The virtual power has no file: recorded, with the reason. A power that exists but does nothing here is not.
    assert [name for name, _ in skipped] == ["A.B.Virtual"]
    assert "E35" in skipped[0][1]


def test_candidates_do_not_record_inherent_or_incarnate_skips(tmp_path: Path) -> None:
    """Virtual inherents (set bonuses, accolades) are expected to lack a file and are not reported."""
    refs = {n: _ref(n) for n in ("Inherent.Inherent.Special_Set_Bonuses", "Incarnate.Lore.X")}
    assert chain_report._candidates(refs, tmp_path) == ({}, ())


def test_toggle_damage_buff_needs_no_canonical_file(tmp_path: Path) -> None:
    """A toggle damage buff (Tactical Training: Assault) is already in Mids' totals: never a click-buff candidate.

    Its canonical file may not exist at all (colon names), so it must not be required.
    """
    toggle_buff = replace(
        _ref("A.B.Assault"), power_type="Toggle", self_damage_buff=0.1, self_damage_buff_duration=0.75
    )
    click_buff = replace(_ref("A.B.Aim"), self_damage_buff=0.625, self_damage_buff_duration=10.0)
    _write_power(tmp_path, "A.B.Aim", [{"templates": [{"attribs": ["Smashing_Dmg"]}]}])
    found, skipped = chain_report._candidates({"A.B.Assault": toggle_buff, "A.B.Aim": click_buff}, tmp_path)
    assert sorted(found) == ["A.B.Aim"]
    # The toggle has no file here, so it is reported rather than silently dropped.
    assert [name for name, _ in skipped] == ["A.B.Assault"]


def test_incarnate_powers_are_never_candidates(tmp_path: Path) -> None:
    """Incarnate summons (Lore pets) and damage (Judgement) are out of scope (#41), even with a file."""
    _write_power(tmp_path, "Incarnate.Lore.Knives", [{"templates": [{"attribs": ["Create_Entity"]}]}])
    _write_power(tmp_path, "Incarnate.Judgement.Ion", [{"templates": [{"attribs": ["Energy_Dmg"]}]}])
    refs = {"Incarnate.Lore.Knives": _ref("Incarnate.Lore.Knives"), "Incarnate.Judgement.Ion": _ref("x", 300.0)}
    assert chain_report._candidates(refs, tmp_path) == ({}, ())


def test_candidates_propagate_non_e35_errors(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    """Only a missing file is tolerated for a zero-signal power."""

    def boom(root: Path, names: list[str]) -> dict[str, Any]:
        if names:
            raise ValueError("E99: boom")
        return {}

    monkeypatch.setattr(chain_report, "load_canonical_powers", boom)
    with pytest.raises(ValueError, match="E99"):
        chain_report._candidates({"A.B.C": _ref("A.B.C")}, tmp_path)


@pytest.mark.parametrize(
    "items", [["a"], ["a=x"], ["=1"], ["a=1", "b"], ["a=nan"], ["a=inf"], ["a=-inf"], ["a=-Infinity"]]
)
def test_bad_assumptions_are_e67(items: list[str]) -> None:
    """Missing ``=``, a non-numeric or non-finite value, or an empty key."""
    with pytest.raises(ValueError, match="E67"):
        parse_assumptions(items)


def test_e67_hint_names_a_real_assumption_key() -> None:
    """The example in the message is a key the engine reads."""
    with pytest.raises(ValueError, match=r"--assume Class_Brute\.fury_level=50"):
        parse_assumptions(["oops"])


def test_assumptions_parse() -> None:
    """KEY=VALUE pairs become floats."""
    assert parse_assumptions(["a.b=0.5", " c = 2"]) == {"a.b": 0.5, "c": 2.0}


@pytest.mark.parametrize("text", ["many", "spawn", "spawn:0", "spawn:x", "spawn:-1", "spawn:²", "spawn:٣"])
def test_bad_targets_are_e68(text: str) -> None:
    """Anything but ``single`` or ``spawn:N`` with N >= 1."""
    with pytest.raises(ValueError, match="E68"):
        parse_target(text)


def test_targets_parse() -> None:
    """``single`` and ``spawn:N``."""
    assert parse_target("single").targets == 1
    assert parse_target("spawn:8").targets == 8


def _drained(setup: ChainSetup) -> ChainSetup:
    """The same build with no recovery and a toggle drain that empties the pool in 2 s, under any 4 cycles."""
    budget = EnduranceBudget(max_end=100.0, recovery_per_sec=0.0, toggle_drain_per_sec=50.0)
    return replace(setup, context=replace(setup.context, budget=budget))


def test_p_end_002_warns_when_nothing_is_affordable() -> None:
    """No chain lasts its minimum cycles -> warning stating the cycle floor and the actual empty time."""
    results = run_chain(_drained(_setup()), [Stance.MELEE])
    (warning,) = [d for d in results[0].diagnostics if d.rule_id == "P-END-002"]
    assert warning.severity == "warning"
    assert str(warning.expected).startswith(">= 4 cycles (")
    assert warning.actual.endswith(" s")


def _single_attack_setup(max_end: float) -> ChainSetup:
    """One 10 s-cycle attack costing 0.5 end/s, no recovery or toggles: empties in exactly max_end / 0.5 s."""
    profile = AttackProfile("Bolt", Delivery.RANGED, Shape.SINGLE, Role.CHAIN, 80.0, 0.0, 0.0, 0)
    bolt = ChainAttack("Bolt", 100.0, 1.0, f32(10.0 - arcana_cast_time(1.0)), 5.0, profile)
    setup = _setup()
    budget = EnduranceBudget(max_end=max_end, recovery_per_sec=0.0, toggle_drain_per_sec=0.0)
    return replace(setup, context=replace(setup.context, attacks=(bolt,), budget=budget))


def test_chain_affordable_exactly_at_the_minimum_cycles() -> None:
    """time_to_empty == min_cycles x the period (4 x 10 s): affordable, labelled so, no P-END-002."""
    result = run_chain(_single_attack_setup(20.0), [Stance.MELEE])[0]
    assert result.chain is not None
    assert result.chain.cycle_time == 10.0  # exact in f32, so the boundary is exact
    assert result.chain.time_to_empty == 40.0
    assert "P-END-002" not in _rules([result])
    (chain,) = [s for s in result.report.sources if s.source == "chain"]
    assert "affordable: empties in" in chain.label
    assert "not affordable" not in chain.label


def test_chain_just_below_the_minimum_cycles_is_not_affordable() -> None:
    """39 s against a 40 s floor: P-END-002 fires and the label says not affordable."""
    result = run_chain(_single_attack_setup(19.5), [Stance.MELEE])[0]
    assert result.chain is not None
    assert result.chain.time_to_empty == 39.0
    assert "P-END-002" in _rules([result])
    (chain,) = [s for s in result.report.sources if s.source == "chain"]
    assert "not affordable" in chain.label


def test_p_end_002_is_silent_when_sustainable() -> None:
    """A sustainable chain raises no endurance warning."""
    assert "P-END-002" not in _rules(run_chain(_setup(), ALL_STANCES))


def test_p_end_002_is_silent_when_empty_time_clears_the_minimum(monkeypatch: pytest.MonkeyPatch) -> None:
    """Not sustainable, but lasting at least the minimum cycles (500 s against 4 cycles of 1 s)."""
    setup = _setup()
    option = ChainOption(
        rotation=("x",),
        stance=Stance.MELEE,
        cast_sum=1.0,
        switches=0,
        move_time=0.0,
        cycle_time=1.0,
        gap=0.0,
        gap_free=True,
        damage_per_cycle=1.0,
        dps=1.0,
        end_per_sec=1.0,
        net_end_per_sec=-1.0,
        time_to_empty=500.0,
        sustainable=False,
    )
    monkeypatch.setattr(chain_report, "chosen_chain", lambda ctx: option)
    assert "P-END-002" not in _rules(run_chain(setup, [Stance.MELEE]))


def test_p_chain_001_reads_the_stance_defense_and_goes_quiet_at_the_soft_cap() -> None:
    """Melee uses def_[10], ranged def_[11]; at 45% or more nothing is reported."""
    setup = _setup()
    low = run_chain(setup, ALL_STANCES)
    texts = {r.stance: [d.message for d in r.diagnostics if d.rule_id == "P-CHAIN-001"] for r in low}
    assert "melee defense is 14.5%" in texts[Stance.MELEE][0]
    assert "ranged defense is 35.0%" in texts[Stance.RANGED][0]
    assert "melee defense is 14.5%" in texts[Stance.HYBRID][0]
    capped = replace(setup.capped, def_=[0.5] * len(setup.capped.def_))
    assert "P-CHAIN-001" not in _rules(run_chain(replace(setup, capped=capped), ALL_STANCES))


def test_json_view_is_serialisable_and_complete() -> None:
    """JSON carries rotation, layers, modifiers, endurance, health and diagnostics; inf becomes null."""
    setup = _setup()
    data = [stance_to_json(setup, r) for r in run_chain(setup, ALL_STANCES)]
    text = json.dumps(data, allow_nan=False)
    melee, ranged = json.loads(text)[:2]
    assert melee["rotation"]["attacks"] == [
        "Scrapper_Melee.Dark_Melee.Shadow_Maul",
        "Scrapper_Melee.Dark_Melee.Siphon_Life",
        "Scrapper_Melee.Dark_Melee.Smite",
        "Inherent.Inherent.Brawl",
    ]
    assert melee["dps"]["total"] > 0.0
    chain_items = melee["dps"]["detail"]["chain"]
    assert [i["name"] for i in chain_items] == melee["rotation"]["attacks"]
    assert sum(i["dps"] for i in chain_items) == pytest.approx(melee["dps"]["unscaled"]["chain"], rel=1e-4)
    assert "#31" in melee["dps"]["labels"]["chain"]
    # Drains, but lasts the default 4 cycles, so it is affordable.
    assert melee["endurance"]["time_to_empty_s"] >= 4 * melee["rotation"]["cycle_time_s"]
    assert ranged["rotation"]["attacks"] == [MOONBEAM]
    assert melee["health"]["max_hp"] > 0.0
    assert melee["modifiers"][0]["factor"] > 1.0
    assert melee["diagnostics"][0]["rule_id"].startswith("P-")
    melee_only = _without_moonbeam(setup)
    (bare,) = [stance_to_json(melee_only, r) for r in run_chain(melee_only, [Stance.RANGED])]
    assert bare["endurance"]["time_to_empty_s"] is None  # toggles alone: inf -> null
    assert bare["rotation"] is None


def test_text_view_for_sustainable_and_draining_builds() -> None:
    """Text names the rotation, layers, modifiers, endurance, health and diagnostics."""
    setup = _setup()
    melee, ranged, _ = run_chain(setup, ALL_STANCES)
    text = stance_to_text(setup, melee)
    for needle in (
        "== melee stance ==",
        "rotation: Scrapper_Melee.Dark_Melee.Shadow_Maul > Scrapper_Melee.Dark_Melee.Siphon_Life > ",
        "cycle ",
        "total:",
        "x1.100",
        "not sustainable",
    ):
        assert needle in text
    assert "health: max" in text
    assert "P-CHAIN-001" in text
    assert "    Scrapper_Melee.Dark_Melee.Smite: " in text  # per-item detail under its layer
    assert f"rotation: {MOONBEAM}" in stance_to_text(setup, ranged)
    melee_only = _without_moonbeam(setup)
    ranged_text = stance_to_text(melee_only, run_chain(melee_only, [Stance.RANGED])[0])
    assert "rotation: none" in ranged_text
    assert "time to empty sustainable" in ranged_text

    drained = _drained(setup)
    result = run_chain(drained, [Stance.MELEE])[0]
    drained_text = stance_to_text(drained, result)
    assert "not sustainable" in drained_text
    assert "P-END-002" in drained_text
    assert "time to empty sustainable" not in drained_text


def test_modifier_without_assumption_text_is_labelled(monkeypatch: pytest.MonkeyPatch) -> None:
    """A modifier with an empty assumption prints a placeholder; no modifiers prints ``none``."""
    setup = _setup()
    result = run_chain(setup, [Stance.MELEE])[0]
    blank = replace(
        result, report=replace(result.report, modifiers=(replace(result.report.modifiers[0], assumption=""),))
    )
    assert "no assumption" in stance_to_text(setup, blank)
    empty = replace(result, report=replace(result.report, modifiers=()))
    assert "modifiers:\n  none" in stance_to_text(setup, empty)


def _two_form_setup() -> ChainSetup:
    """The fixture build plus a Nova-locked copy of its first attack, so the build has two forms."""
    setup = _setup()
    base = setup.context.attacks[0]
    nova = replace(base, full_name="Nova.Bolt", profile=replace(base.profile, modes_required=("Nova",)))
    return replace(setup, context=replace(setup.context, attacks=(*setup.context.attacks, nova)))


def test_classification_receives_a_canonical_resolver(monkeypatch: pytest.MonkeyPatch) -> None:
    """Every power is classified with ``resolve=`` so Execute_Power and Create_Entity links are followed."""
    seen: list[Any] = []

    def classify(power: dict[str, Any], **kwargs: Any) -> AttackProfile:
        seen.append(kwargs.get("resolve"))
        return classify_attack(power, **kwargs)

    monkeypatch.setattr(chain_report, "classify_attack", classify)
    _setup()
    assert seen
    assert all(callable(resolve) for resolve in seen)


def test_a_build_without_forms_has_only_the_base_form() -> None:
    """No attack names a mode: one block per stance, form empty, header unchanged."""
    setup = _setup()
    assert chain_report.available_forms(setup.context) == ("",)
    results = run_chain(setup, ALL_STANCES)
    assert [r.form for r in results] == ["", "", ""]
    assert stance_to_json(setup, results[0])["form"] == ""
    assert stance_to_text(setup, results[0]).startswith("== melee stance ==\n")


def test_each_stance_runs_once_per_form() -> None:
    """Stances run as before, each repeated for the base form and then every mode, in sorted order."""
    setup = _two_form_setup()
    assert chain_report.available_forms(setup.context) == ("", "Nova")
    results = run_chain(setup, [Stance.MELEE, Stance.RANGED])
    assert [(r.stance, r.form) for r in results] == [
        (Stance.MELEE, ""),
        (Stance.MELEE, "Nova"),
        (Stance.RANGED, ""),
        (Stance.RANGED, "Nova"),
    ]


def test_form_blocks_are_labelled_in_text_and_json() -> None:
    """A form block reads ``== melee stance, Nova form ==`` and carries a ``form`` key."""
    setup = _two_form_setup()
    base, nova = run_chain(setup, [Stance.MELEE])
    assert stance_to_text(setup, base).startswith("== melee stance ==\n")
    assert stance_to_text(setup, nova).startswith("== melee stance, Nova form ==\n")
    assert stance_to_json(setup, nova)["form"] == "Nova"


def test_forms_can_be_restricted_to_one() -> None:
    """``forms=`` runs only the named forms; the base form is the empty string."""
    setup = _two_form_setup()
    assert [r.form for r in run_chain(setup, [Stance.MELEE], forms=["Nova"])] == ["Nova"]
    assert [r.form for r in run_chain(setup, [Stance.MELEE], forms=[""])] == [""]


def test_unknown_form_is_e65_naming_the_available_forms() -> None:
    """An unknown form lists what the build offers."""
    with pytest.raises(ValueError, match=r"E65.*'Dwarf'.*base.*Nova"):
        run_chain(_two_form_setup(), [Stance.MELEE], forms=["Dwarf"])


def test_base_form_has_no_rotation_when_every_attack_is_form_locked() -> None:
    """Only form-locked attacks: the base block prints ``rotation: none`` and the form block has a chain."""
    setup = _setup()
    locked = tuple(replace(a, profile=replace(a.profile, modes_required=("Nova",))) for a in setup.context.attacks)
    setup = replace(setup, context=replace(setup.context, attacks=locked))
    base, nova = run_chain(setup, [Stance.MELEE])
    assert "rotation: none" in stance_to_text(setup, base)
    assert nova.chain is not None
