"""The .mbd -> engine DTO bridge, and the hard-limit verdicts it enables."""

import json
from pathlib import Path

import pytest

from coh_engine.buildfile.mbd import parse_mbd
from coh_engine.levels import load_level_schedule
from scripts.forum_scrape.validate import (
    PowerCatalog,
    build_dtos,
    load_enhancement_ids,
    load_power_catalog,
    load_server_limits,
    parse_requires,
    validate_many,
    validate_mbd,
)

# The engine's tracked Mids reference dump, so these tests run anywhere, not only on a
# machine that has generated tools/mids-dump/out.
DUMP = Path("src/coh_engine/tests/fixtures/mids")
CANONICAL = Path("data/canonical")
TIDGE = Path(__file__).parent / "fixtures" / "tidge_bots_traps.mbd"


@pytest.fixture(scope="module")
def schedule():
    return load_level_schedule(DUMP / "levels.json")


@pytest.fixture(scope="module")
def catalog() -> PowerCatalog:
    return load_power_catalog(CANONICAL)


# --- requires parsing --------------------------------------------------------------


def test_parse_requires_builds_or_of_ands() -> None:
    assert parse_requires("Pool.Fighting.Boxing || Pool.Fighting.Kick") == (
        ("Pool.Fighting.Boxing",),
        ("Pool.Fighting.Kick",),
    )
    assert parse_requires("Pool.Fighting.Boxing && Pool.Fighting.Kick") == (
        ("Pool.Fighting.Boxing", "Pool.Fighting.Kick"),
    )


def test_parse_requires_drops_expressions_it_cannot_evaluate() -> None:
    """A prerequisite the engine cannot check must vanish, not become an empty group.

    An empty AND-group reads as satisfied, which would pass a build that actually fails
    the requirement — worse than not checking it at all.
    """
    assert parse_requires("ownPowerNum?(Epic) > 0") == ()
    assert parse_requires("($archetype == @Class_Mastermind) && (Owned?(Beta))") == ()
    # A mixed clause keeps only the checkable half of the OR.
    assert parse_requires("ownPowerNum?(Epic) > 0 || Pool.Fighting.Kick") == (("Pool.Fighting.Kick",),)
    assert parse_requires("") == ()


# --- the catalog -------------------------------------------------------------------


def test_catalog_reads_real_pick_levels(catalog: PowerCatalog) -> None:
    # 0-based, matching the build's own levels: Assault Bot is available at level 22.
    assert catalog.levels["Mastermind_Summon.Robotics.Assault_Bot"] == 21
    assert catalog.levels["Mastermind_Summon.Robotics.Battle_Drones"] == 0
    assert catalog.levels["Mastermind_Buff.Traps.Force_Field_Generator"] == 15


def test_catalog_reads_real_tier_prerequisites(catalog: PowerCatalog) -> None:
    groups = catalog.requires["Pool.Fighting.Tough"]
    assert ("Pool.Fighting.Boxing",) in groups
    assert ("Pool.Fighting.Kick",) in groups


def test_catalog_survives_an_empty_tree(tmp_path: Path) -> None:
    empty = load_power_catalog(tmp_path)
    assert empty.levels == {} and empty.requires == {}


def test_catalog_skips_a_non_numeric_level(tmp_path: Path) -> None:
    """A malformed level must be dropped, not coerced — a wrong level fails real builds."""
    folder = tmp_path / "powers" / "someset"
    folder.mkdir(parents=True)
    (folder / "index.json").write_text(
        json.dumps({"power_names": ["A.B.Good", "A.B.Bad"], "available_level": [4, None]}),
        encoding="utf-8",
    )
    assert load_power_catalog(tmp_path).levels == {"A.B.Good": 4}


def test_catalog_skips_unreadable_files(tmp_path: Path) -> None:
    (tmp_path / "powers" / "pool" / "broken").mkdir(parents=True)
    (tmp_path / "powers" / "pool" / "broken" / "index.json").write_text("{not json", encoding="utf-8")
    (tmp_path / "powers" / "pool" / "broken" / "a.json").write_text("{not json", encoding="utf-8")
    assert load_power_catalog(tmp_path).levels == {}


# --- the bridge --------------------------------------------------------------------


def test_build_dtos_carries_levels_slots_and_pick_status(catalog: PowerCatalog) -> None:
    build = parse_mbd(json.loads(TIDGE.read_text(encoding="utf-8")))
    powers, slots = build_dtos(build, catalog, load_enhancement_ids(DUMP))

    by_name = {p.full_name: p for p in powers}
    drones = by_name["Mastermind_Summon.Robotics.Battle_Drones"]
    assert drones.pick_level == 0  # picked at level 1
    assert drones.level == 0  # and available from level 1
    assert drones.chosen

    tough = by_name["Pool.Fighting.Tough"]
    assert tough.pick_level == 43  # picked at level 44
    assert ("Pool.Fighting.Kick",) in tough.requires_powers

    # Slot levels survive as 0-based, and the enhancement resolves to a real nID.
    drone_slots = slots[drones.build_index]
    assert [s.level for s in drone_slots] == [0, 2, 4, 8, 26, 26]
    assert drone_slots[0].enh > 0
    assert drone_slots[0].relative_level == 4  # "Even"


def test_auto_granted_groups_are_not_counted_as_picks(catalog: PowerCatalog) -> None:
    """Accolades and inherents are granted, not picked; counting them breaks the 24 cap."""
    document = json.loads(TIDGE.read_text(encoding="utf-8"))
    document["PowerEntries"].append(
        {"PowerName": "Temporary_Powers.Accolades.Portal_Jockey", "Level": 50, "SlotEntries": []}
    )
    powers, _ = build_dtos(parse_mbd(document), catalog)
    accolade = next(p for p in powers if p.full_name.startswith("Temporary_Powers."))
    assert not accolade.chosen
    inherent = next(p for p in powers if p.full_name.startswith("Inherent."))
    assert not inherent.chosen


def test_an_unnamed_grid_slot_is_not_a_pick(catalog: PowerCatalog) -> None:
    document = json.loads(TIDGE.read_text(encoding="utf-8"))
    document["PowerEntries"].append({"PowerName": "", "Level": 1, "SlotEntries": []})
    powers, slots = build_dtos(parse_mbd(document), catalog)
    assert not powers[-1].chosen
    assert powers[-1].build_index in slots  # index preserved so slot mapping stays aligned


def test_a_slot_with_no_enhancement_resolves_to_empty(catalog: PowerCatalog) -> None:
    document = json.loads(TIDGE.read_text(encoding="utf-8"))
    document["PowerEntries"][0]["SlotEntries"][0]["Enhancement"] = None
    _, slots = build_dtos(parse_mbd(document), catalog)
    assert slots[0][0].enh == -1
    assert slots[0][0].grade == 0 and slots[0][0].relative_level == 0


# --- verdicts ----------------------------------------------------------------------


def test_a_known_good_build_is_legal(catalog: PowerCatalog, schedule) -> None:
    """The build validated by hand earlier: 67/67 slots, 24 picks, 4 pools."""
    assert validate_mbd(TIDGE.read_bytes(), catalog, schedule, 67, load_enhancement_ids(DUMP)) == []


def test_an_extra_slot_trips_the_budget_rules(catalog: PowerCatalog, schedule) -> None:
    document = json.loads(TIDGE.read_text(encoding="utf-8"))
    document["PowerEntries"][0]["SlotEntries"].append(
        {"Level": 51, "IsInherent": False, "Enhancement": None, "FlippedEnhancement": None}
    )
    diagnostics = validate_mbd(json.dumps(document).encode(), catalog, schedule, 67)
    fired = {d.rule_id for d in diagnostics}
    assert "H-SLOT-001" in fired  # over the 67-slot cap
    assert "H-SLOT-002" in fired  # and over 5 added slots on that power
    assert "H-SLOT-004" in fired  # at a level granting no slot
    assert all(d.severity == "error" for d in diagnostics)


def test_a_power_picked_too_early_is_caught(catalog: PowerCatalog, schedule) -> None:
    document = json.loads(TIDGE.read_text(encoding="utf-8"))
    for entry in document["PowerEntries"]:
        if entry["PowerName"].endswith("Assault_Bot"):
            entry["Level"] = 2  # available at 22, not 2
            entry["SlotEntries"] = [{"Level": 2, "IsInherent": False, "Enhancement": None}]
    diagnostics = validate_mbd(json.dumps(document).encode(), catalog, schedule, 67)
    assert any(d.rule_id == "H-POWER-002" for d in diagnostics)


def test_validate_rejects_what_is_not_a_build(catalog: PowerCatalog, schedule) -> None:
    with pytest.raises(ValueError, match=r"not a .mbd build"):
        validate_mbd(b'{"nope": 1}', catalog, schedule, 67)


# --- batch + reference loading -----------------------------------------------------


def test_server_limits_reads_the_real_cap() -> None:
    assert load_server_limits(DUMP) == 67


def test_server_limits_defaults_without_a_dump(tmp_path: Path) -> None:
    assert load_server_limits(tmp_path) == 67


def test_enhancement_ids_absent_dump(tmp_path: Path) -> None:
    assert load_enhancement_ids(tmp_path) == {}


def test_a_legacy_mxd_share_block_validates_through_the_same_rules(
    tmp_path: Path, catalog: PowerCatalog, schedule
) -> None:
    """The old format is still live data — MidsReborn opens and converts it.

    The fixture's block was captured from the forum, so this runs the real decode +
    StaticIndex resolution and then the same twelve rules a ``.mbd`` gets.
    """
    from coh_engine.buildfile.dbindex import load_enhancement_index, load_power_index
    from scripts.forum_scrape.scrape import find_share_blocks, iter_posts
    from scripts.forum_scrape.validate import validate_mxd

    fixture = Path(__file__).parent / "fixtures" / "topic_sample.html"
    post = next(iter(iter_posts(fixture.read_text(encoding="utf-8"))))
    block = find_share_blocks(post.text)[0]
    assert block.startswith("|MxDz;")

    diagnostics = validate_mxd(
        block,
        catalog,
        schedule,
        67,
        load_power_index(DUMP / "power_static_index.json"),
        load_enhancement_index(DUMP / "enhancements.json"),
        load_enhancement_ids(DUMP),
    )
    # A real posted build: whatever the verdict, every diagnostic is a real rule.
    assert all(d.rule_id.startswith("H-") for d in diagnostics)


def test_validate_many_picks_the_format_from_the_file(tmp_path: Path) -> None:
    """A scraped artifact is a .txt whichever format it holds, so the extension is no guide."""
    fixture = Path(__file__).parent / "fixtures" / "topic_sample.html"
    from scripts.forum_scrape.scrape import find_share_blocks, iter_posts

    post = next(iter(iter_posts(fixture.read_text(encoding="utf-8"))))
    legacy = tmp_path / "block.txt"  # .txt holding an .mxd block
    legacy.write_text(find_share_blocks(post.text)[0], encoding="utf-8")

    results = dict(validate_many([legacy], CANONICAL, DUMP))
    assert results[legacy] is not None  # it parsed, rather than falling through to None


def test_validate_many_separates_unparseable_from_illegal(tmp_path: Path) -> None:
    junk = tmp_path / "junk.mbd"
    junk.write_text("not a build", encoding="utf-8")
    results = dict(validate_many([TIDGE, junk], CANONICAL, DUMP))
    assert results[TIDGE] == []  # legal
    assert results[junk] is None  # never parsed, which is not the same as illegal
