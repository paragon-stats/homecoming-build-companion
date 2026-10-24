"""Chain report: context building, the chain-rule registry, and the text and JSON views."""

import json
import shutil
from dataclasses import replace
from pathlib import Path
from typing import Any

import pytest

from coh_engine import chain_report
from coh_engine.attack_chain import ChainOption
from coh_engine.attack_profile import classify_attack
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
    DamageContext,
    EnduranceBudget,
    Role,
    Stance,
    registered_damage_modifiers,
    registered_damage_sources,
)
from coh_engine.damage_reference import PowerDamageRef

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"
BUILD = MIDS / "builds" / "shield_scrapper_set_bonuses"
ALL_STANCES = list(Stance)


def _setup(build: Path = BUILD, **kwargs: Any) -> ChainSetup:
    return build_chain_setup(build, mids_dir=MIDS, **kwargs)


def _rules(results: list[chain_report.StanceReport]) -> list[str]:
    return [d.rule_id for r in results for d in r.diagnostics]


def test_plugins_are_registered_by_importing_chain_report() -> None:
    """The manifest import populates every default registry."""
    assert "chain_source" in registered_damage_sources()
    assert "pet_source" in registered_damage_sources()
    assert "click_buff_modifier" in registered_damage_modifiers()
    assert "scrapper_critical_hit" in registered_damage_modifiers()


def test_registered_chain_rules() -> None:
    """The three diagnostics are registered rules."""
    assert registered_chain_rules() == ["_rule_end_002", "_rule_chain_001", "_rule_chain_002"]


def test_fixture_reports_every_stance_with_positive_dps() -> None:
    """A real dump yields a report per stance; melee and hybrid have a chain with positive DPS."""
    setup = _setup()
    results = run_chain(setup, ALL_STANCES)
    assert [r.stance for r in results] == ALL_STANCES
    melee, ranged, hybrid = results
    assert melee.report.scaled_dps["chain"] > 0.0
    assert hybrid.report.scaled_dps["chain"] > 0.0
    assert melee.chain is not None
    assert ranged.chain is None
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
    setup = _setup(build)
    assert not setup.clicks_always_on
    assert setup.source.endswith("b_clicksoff")
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
    assert sorted(chain_report._candidates(refs, tmp_path)) == ["A.B.Hit", "A.B.Summon"]


def test_toggle_damage_buff_needs_no_canonical_file(tmp_path: Path) -> None:
    """A toggle damage buff (Tactical Training: Assault) is already in Mids' totals: never a click-buff candidate.

    Its canonical file may not exist at all (colon names), so it must not be required.
    """
    toggle_buff = replace(
        _ref("A.B.Assault"), power_type="Toggle", self_damage_buff=0.1, self_damage_buff_duration=0.75
    )
    click_buff = replace(_ref("A.B.Aim"), self_damage_buff=0.625, self_damage_buff_duration=10.0)
    _write_power(tmp_path, "A.B.Aim", [{"templates": [{"attribs": ["Smashing_Dmg"]}]}])
    assert sorted(chain_report._candidates({"A.B.Assault": toggle_buff, "A.B.Aim": click_buff}, tmp_path)) == [
        "A.B.Aim"
    ]


def test_candidates_propagate_non_e35_errors(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    """Only a missing file is tolerated for a zero-signal power."""

    def boom(root: Path, names: list[str]) -> dict[str, Any]:
        if names:
            raise ValueError("E99: boom")
        return {}

    monkeypatch.setattr(chain_report, "load_canonical_powers", boom)
    with pytest.raises(ValueError, match="E99"):
        chain_report._candidates({"A.B.C": _ref("A.B.C")}, tmp_path)


@pytest.mark.parametrize("items", [["a"], ["a=x"], ["=1"], ["a=1", "b"]])
def test_bad_assumptions_are_e67(items: list[str]) -> None:
    """Missing ``=``, a non-numeric value or an empty key."""
    with pytest.raises(ValueError, match="E67"):
        parse_assumptions(items)


def test_assumptions_parse() -> None:
    """KEY=VALUE pairs become floats."""
    assert parse_assumptions(["a.b=0.5", " c = 2"]) == {"a.b": 0.5, "c": 2.0}


@pytest.mark.parametrize("text", ["many", "spawn", "spawn:0", "spawn:x", "spawn:-1"])
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


def test_p_end_002_is_silent_when_sustainable() -> None:
    """A sustainable chain raises no endurance warning."""
    assert "P-END-002" not in _rules(run_chain(_setup(), ALL_STANCES))


def test_p_end_002_is_silent_when_empty_time_clears_the_minimum(monkeypatch: pytest.MonkeyPatch) -> None:
    """Not sustainable, but lasting at least the minimum cycles (500 s against 4 cycles of 1 s)."""
    setup = _setup()
    option = ChainOption(("x",), Stance.MELEE, 1.0, 0, 0.0, 1.0, 0.0, True, 1.0, 1.0, 1.0, -1.0, 500.0, False)
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
    # Drains, but lasts the default 4 cycles, so it is affordable.
    assert melee["endurance"]["time_to_empty_s"] >= 4 * melee["rotation"]["cycle_time_s"]
    assert ranged["endurance"]["time_to_empty_s"] is None  # toggles alone: inf -> null
    assert melee["health"]["max_hp"] > 0.0
    assert melee["modifiers"][0]["factor"] > 1.0
    assert melee["diagnostics"][0]["rule_id"].startswith("P-")
    assert ranged["rotation"] is None


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
    ranged_text = stance_to_text(setup, ranged)
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
