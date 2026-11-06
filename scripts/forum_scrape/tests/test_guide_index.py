"""Guide subject index: capture snapshot, document type, subjects, issue, and the coverage report."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest

from coh_engine.power_names import PowerName, PowerNameResolver, PowersetName
from scripts.forum_scrape import guide_index
from scripts.forum_scrape.guide_index import (
    build_coverage,
    build_index,
    document_type,
    issue_on,
    issue_timeline,
    main,
    tags_from,
    write_outputs,
)


def _set(internal: str, display: str, archetypes: tuple[str, ...], *powers: str) -> PowersetName:
    category = internal.split(".")[0]
    return PowersetName(
        internal,
        display,
        category,
        archetypes,
        tuple(PowerName(f"{internal}.{p.replace(' ', '_')}", p, internal) for p in powers),
    )


def small_resolver() -> PowerNameResolver:
    """A resolver over a handful of sets, enough for every subject rule."""
    return PowerNameResolver(
        (
            _set("Scrapper_Melee.Claws", "Claws", ("scrapper", "stalker"), "Eviscerate", "Spin"),
            _set("Scrapper_Defense.Super_Reflexes", "Super Reflexes", ("scrapper", "stalker")),
            _set("Brute_Defense.Bio_Organic_Armor", "Bio Armor", ("brute", "scrapper"), "Genetic Contamination"),
            _set("Brute_Defense.Regeneration", "Regeneration", ("brute", "scrapper"), "Reconstruction"),
            _set("Corruptor_Ranged.Fire_Blast", "Fire Blast", ("corruptor",), "Flares"),
            _set("Defender_Ranged.Fire_Blast", "Fire Blast", ("defender",), "Flares"),
            _set("Corruptor_Buff.Kinetics", "Kinetics", ("corruptor", "defender")),
            _set("Dominator_Assault.Savage_Assault", "Savage Assault", ("dominator",)),
            _set("Arachnos_Widow.Widow_Training", "Widow Training", ("arachnos_widow",)),
            _set("Pool.Speed", "Speed", ()),
        )
    )


def capture(
    root: Path,
    relative: str,
    title: str,
    body: str = "",
    *,
    url: str | None = None,
    digest: str | None = None,
    posted: str = "2011-06-01",
    tags: str = "[]",
) -> None:
    front = ["---", f"title: {json.dumps(title)}"]
    if url is not None:
        front.append(f"url: {json.dumps(url)}")
    front.append(f"date_posted: {json.dumps(posted)}")
    front.append(f"topic_tags: {tags}")
    if digest is not None:
        front.append(f"content_hash: {json.dumps(digest)}")
    front.append("---")
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(front) + f"\n\n# {title}\n\n{body}\n", encoding="utf-8", newline="\n")


EXPORT = "\n".join(f"Level {n}: Power {n}" for n in range(1, 8))
TIMELINE = [("2008-01-01", "issue-12"), ("2010-01-01", "issue-18")]


def test_issue_on_maps_the_date_to_the_live_issue_and_a_title_marker_overrides_it() -> None:
    assert issue_on(TIMELINE, "2009-05-05", "A guide") == "issue-12"
    assert issue_on(TIMELINE, "2010-01-01", "A guide") == "issue-18"
    assert issue_on(TIMELINE, "2005-05-05", "A guide") == ""
    assert issue_on(TIMELINE, "2011-01-01", "Claws for I13") == "issue-13"
    assert issue_on(TIMELINE, "2011-01-01", "Claws, Issue 7 edition") == "issue-7"


def test_issue_timeline_reads_dated_records_with_an_issue(tmp_path: Path) -> None:
    assert issue_timeline(tmp_path) == []
    rows = [
        {"published": "2024-01-19T14:13:42Z", "issue": "issue-27"},
        {"published": "2004-05-11", "issue": ""},
        {"published": "2010-02-02", "issue": "issue-17"},
    ]
    (tmp_path / "index.jsonl").write_text("\n".join(json.dumps(r) for r in rows) + "\n\n", encoding="utf-8")
    assert issue_timeline(tmp_path) == [("2010-02-02", "issue-17"), ("2024-01-19", "issue-27")]


@pytest.mark.parametrize(
    ("title", "tags", "body", "expected"),
    [
        ("A concept for a fire tanker", [], "", "theme"),
        ("Fiery Brute", ["theme-rp"], "", "theme"),
        ("Please critique my Claws build", [], EXPORT, "build post"),
        ("Please critique my Claws build", [], "no export here", "discussion"),
        ("Claws notes", ["discussion"], "", "discussion"),
        ("Claws build", [], EXPORT, "guide"),
        ("The Claws guide", [], "", "guide"),
    ],
)
def test_document_type(title: str, tags: list[str], body: str, expected: str) -> None:
    assert document_type(title, tags, body) == expected


@pytest.mark.parametrize(
    ("value", "expected"),
    [
        ('["brute", "discussion"]', ["brute", "discussion"]),
        ("[farming, builds, ae]", ["farming", "builds", "ae"]),
        ("[]", []),
        ("", []),
    ],
)
def test_tags_reads_the_scraper_and_the_hand_written_forms(value: str, expected: list[str]) -> None:
    assert tags_from(value) == expected


def _index(tmp_path: Path):
    return build_index(tmp_path, small_resolver(), TIMELINE)


def test_index_rows_carry_type_archetype_subjects_powers_and_issue(tmp_path: Path, capsys) -> None:
    capture(tmp_path, "mechanics/fire-kin.md", "Fire/Kin Corruptor guide", "Flares everywhere.", digest="h1", url="u1")
    capture(
        tmp_path,
        "pre-sunset/archetypes/scrapper/claws-sr.md",
        "Claws/SR help please",
        f"Eviscerate and Spin.\n{EXPORT}",
        digest="h2",
        url="u2",
        posted="2009-03-03",
    )
    capture(
        tmp_path,
        "pre-sunset/powersets/notes.md",
        "General notes for I13",
        "Bio Armor is fun. bio organic armor works. Genetic Contamination and bio armor again. Speed Speed Speed Speed.",
        digest="h3",
        url="u3",
    )
    capture(tmp_path, "pre-sunset/powersets/two.md", "Two mentions", "Bio Armor and Bio Armor.", digest="h4", url="u4")
    capture(tmp_path, "mechanics/regen.md", "Regeneration and Fire/Kin Scrapper", "x", digest="h5", url="u5")
    capture(tmp_path, "mechanics/odd.md", "Claws/DM, Fire/Nope, SS vs WP", "x", digest="h6", url="u6")
    capture(tmp_path, "archetypes/arachnos/widow.md", "Widow Training for the sentinel", "x", digest="h7", url="u7")
    (tmp_path / "INDEX.md").write_text("no front matter\n", encoding="utf-8")
    (tmp_path / "notitle.md").write_text("---\nurl: x\n---\n\nbody\n", encoding="utf-8")

    rows, folded = _index(tmp_path)

    assert folded == []
    assert "E24: notitle.md: front matter has no title; capture skipped" in capsys.readouterr().err
    by_file = {r["file"]: r for r in rows}
    assert [r["file"] for r in rows] == sorted(by_file)

    fire = by_file["mechanics/fire-kin.md"]
    assert fire["powerset_names"] == ["Fire Blast", "Kinetics"]
    assert fire["archetype"] == "corruptor"
    # Fire Blast exists on two archetypes; the capture's archetype scopes the internal names.
    assert fire["powersets"] == ["Corruptor_Buff.Kinetics", "Corruptor_Ranged.Fire_Blast"]
    assert fire["powers"] == ["Corruptor_Ranged.Fire_Blast.Flares"]
    assert (fire["document_type"], fire["era"], fire["issue"], fire["topic_url"]) == ("guide", "live", "issue-18", "u1")

    claws = by_file["pre-sunset/archetypes/scrapper/claws-sr.md"]
    assert claws["powerset_names"] == ["Claws", "Super Reflexes"]
    assert (claws["archetype"], claws["era"], claws["document_type"]) == ("scrapper", "pre-sunset", "build post")
    assert claws["powers"] == ["Scrapper_Melee.Claws.Eviscerate", "Scrapper_Melee.Claws.Spin"]
    assert claws["issue"] == "issue-12" and claws["date_posted"] == "2009-03-03"

    notes = by_file["pre-sunset/powersets/notes.md"]
    # Display name, internal name and a second display mention reach three; "Speed" is a pool and never matches.
    assert notes["powerset_names"] == ["Bio Armor"]
    assert notes["powers"] == ["Brute_Defense.Bio_Organic_Armor.Genetic_Contamination"]
    assert notes["issue"] == "issue-13"
    assert by_file["pre-sunset/powersets/two.md"]["powerset_names"] == []

    # A combo needs a shared archetype: Fire/Kin on a scrapper names no Fire set, and Kinetics is no Scrapper set.
    assert by_file["mechanics/regen.md"]["powerset_names"] == ["Regeneration"]
    # An alias whose set is absent, an unknown half, and aliases in running text.
    assert by_file["mechanics/odd.md"]["powerset_names"] == ["Claws"]

    widow = by_file["archetypes/arachnos/widow.md"]
    assert widow["powerset_names"] == ["Widow Training"] and widow["powersets"] == ["Arachnos_Widow.Widow_Training"]


def test_a_set_the_capture_archetype_cannot_take_is_not_a_subject(tmp_path: Path) -> None:
    capture(tmp_path, "archetypes/blaster/fire.md", "Fire Blast and Widow Training", "x", digest="h1", url="u1")
    capture(tmp_path, "mechanics/fire.md", "Fire Blast notes", "x", digest="h2", url="u2")
    rows, _ = _index(tmp_path)
    by_file = {r["file"]: r for r in rows}
    assert by_file["archetypes/blaster/fire.md"]["powerset_names"] == []
    # With no archetype, every version of the set is the subject.
    assert by_file["mechanics/fire.md"]["powersets"] == ["Corruptor_Ranged.Fire_Blast", "Defender_Ranged.Fire_Blast"]


def test_index_folds_duplicate_hashes_and_urls_keeping_the_live_capture(tmp_path: Path) -> None:
    capture(tmp_path, "pre-sunset/mechanics/a.md", "Claws", "x", digest="same", url="u-a")
    capture(tmp_path, "mechanics/b.md", "Claws", "x", digest="same", url="u-b")
    capture(tmp_path, "mechanics/c.md", "Claws again", "y", digest="other", url="u-b")
    capture(tmp_path, "mechanics/d.md", "Claws newer", "z", digest="third", url="u-d")

    rows, folded = _index(tmp_path)

    assert [r["file"] for r in rows] == ["mechanics/b.md", "mechanics/d.md"]
    assert folded == [
        {"file": "mechanics/c.md", "kept": "mechanics/b.md", "reason": "topic_url"},
        {"file": "pre-sunset/mechanics/a.md", "kept": "mechanics/b.md", "reason": "content_hash"},
    ]


def test_index_hashes_a_hand_capture_and_takes_its_url_from_the_ledger(tmp_path: Path) -> None:
    capture(tmp_path, "mechanics/hand.md", "Hand made", "body", digest=None, url=None)
    capture(tmp_path, "pre-sunset/mechanics/old.md", "Old hand made", "body two", digest=None, url=None)
    capture(tmp_path, "mechanics/loose.md", "Loose", "body three", digest=None, url=None)
    loose = tmp_path / "mechanics" / "loose.md"
    loose.write_text(loose.read_text(encoding="utf-8").replace('"Loose"', "Loose"), encoding="utf-8")
    (tmp_path / "guides.jsonl").write_text(
        json.dumps({"topic_url": "https://x/live", "file": "mechanics/hand.md"})
        + "\n"
        + json.dumps({"topic_url": "n/a"})
        + "\n",
        encoding="utf-8",
    )
    (tmp_path / "pre-sunset" / "guides.jsonl").write_text(
        json.dumps({"topic_url": "https://x/old", "file": "mechanics/old.md"}) + "\n", encoding="utf-8"
    )

    rows, _ = _index(tmp_path)

    assert [(r["file"], r["topic_url"]) for r in rows] == [
        ("mechanics/hand.md", "https://x/live"),
        ("mechanics/loose.md", ""),
        ("pre-sunset/mechanics/old.md", "https://x/old"),
    ]
    assert all(len(str(r["content_hash"])) == 16 for r in rows)


def _row(file: str, archetype: str, *names: str) -> dict[str, Any]:
    return {"file": file, "archetype": archetype, "powerset_names": list(names)}


def test_coverage_groups_pairs_and_names_unguided_and_transfer_sets() -> None:
    rows = [
        _row("g1", "scrapper", "Claws"),
        _row("g2", "scrapper", "Claws"),
        _row("g3", "scrapper", "Claws"),
        _row("g4", "stalker", "Claws"),
        _row("g5", "stalker", "Claws"),
        _row("g6", "stalker", "Claws"),
        _row("g7", "brute", "Bio Armor"),
        _row("g8", "", "Kinetics"),
        _row("g9", "corruptor", "Regeneration"),
    ]

    report = build_coverage(rows, small_resolver())

    pairs = {(p["powerset"], p["archetype"]): p for p in report["pairs"]}
    assert pairs[("Claws", "scrapper")]["group"] == "own"
    assert pairs[("Claws", "scrapper")]["own_guides"] == ["g1", "g2", "g3"]
    assert pairs[("Bio Armor", "brute")]["group"] == "own"
    assert pairs[("Bio Armor", "scrapper")]["group"] == "transfer"
    assert pairs[("Kinetics", "corruptor")]["group"] == "unknown_source"
    assert pairs[("Fire Blast", "defender")]["group"] == "none"
    assert pairs[("Regeneration", "brute")]["group"] == "transfer"
    assert report["pair_group_counts"] == {"own": 3, "transfer": 3, "unknown_source": 2, "none": 6}
    assert report["unguided"] == ["Fire Blast", "Savage Assault", "Super Reflexes", "Widow Training"]
    assert report["transfer_test_set"] == ["Claws"]
    assert report["powersets"]["Fire Blast"] == {
        "archetypes": ["corruptor", "defender"],
        "guides": [],
        "internal": ["Corruptor_Ranged.Fire_Blast", "Defender_Ranged.Fire_Blast"],
    }
    assert report["powersets"]["Claws"]["guides"] == ["g1", "g2", "g3", "g4", "g5", "g6"]
    assert "Speed" not in report["powersets"]


def _install_fake_resolver(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(
        guide_index.PowerNameResolver, "from_canonical", classmethod(lambda _cls, _path: small_resolver())
    )


def test_write_outputs_and_main_are_byte_identical_across_runs(tmp_path: Path, monkeypatch, capsys) -> None:
    _install_fake_resolver(monkeypatch)
    community, patches, out = tmp_path / "community", tmp_path / "patches", tmp_path / "out"
    capture(community, "mechanics/b.md", "Claws guide", "x", digest="h2", url="u2")
    capture(community, "mechanics/a.md", "Bio Armor guide", "x", digest="h1", url="u1")
    capture(community, "mechanics/dup.md", "Bio Armor guide", "x", digest="h1", url="u1")
    patches.mkdir()
    (patches / "index.jsonl").write_text(
        json.dumps({"published": "2010-01-01", "issue": "issue-18"}) + "\n", encoding="utf-8"
    )
    argv = ["--community", str(community), "--out", str(out), "--canonical", str(tmp_path), "--patches", str(patches)]

    assert main(argv) == 0
    first = {p.name: p.read_bytes() for p in out.iterdir()}
    assert main(argv) == 0
    assert {p.name: p.read_bytes() for p in out.iterdir()} == first
    assert sorted(first) == ["coverage.json", "folded.jsonl", "guides.jsonl"]
    assert b"\r" not in b"".join(first.values())
    assert "2 capture(s) indexed, 1 folded" in capsys.readouterr().out
    rows = [json.loads(line) for line in first["guides.jsonl"].decode().splitlines()]
    assert [r["file"] for r in rows] == ["mechanics/a.md", "mechanics/b.md"]


def test_main_reports_empty_roots_and_a_missing_patch_index(tmp_path: Path, monkeypatch, capsys) -> None:
    _install_fake_resolver(monkeypatch)
    argv = [
        "--community",
        str(tmp_path),
        "--out",
        str(tmp_path / "out"),
        "--canonical",
        str(tmp_path),
        "--patches",
        str(tmp_path / "none"),
    ]
    assert main(argv) == 1
    err = capsys.readouterr().err
    assert "E25:" in err and f"E23: no captures under {tmp_path}" in err
    assert not (tmp_path / "out").exists()


def test_write_outputs_creates_the_folder(tmp_path: Path) -> None:
    write_outputs(tmp_path / "a" / "b", [], [], {})
    assert (tmp_path / "a" / "b" / "coverage.json").read_text(encoding="utf-8") == "{}\n"


BOLD_EXPORT = "\n".join(f"**Level {n}: Power {n}** -- Mako-Acc/Dmg:50(A), Mako-Dmg/EndRdx:50(3)" for n in range(1, 8))


def test_a_rendered_bold_export_still_makes_a_build_post() -> None:
    assert document_type("Fire/Stone build feedback", [], BOLD_EXPORT) == "build post"
    assert document_type("Fire/Stone build", [], BOLD_EXPORT) == "guide"


def _wide_resolver() -> PowerNameResolver:
    return PowerNameResolver(
        (
            _set("Corruptor_Ranged.Fire_Blast", "Fire Blast", ("corruptor",)),
            _set("Corruptor_Buff.Kinetics", "Kinetics", ("corruptor", "defender")),
            _set("Epic.Corruptor_Fire_Mastery", "Fire Mastery", ("corruptor",)),
            _set("Epic.Blaze_Mastery", "Fire Mastery", ("corruptor",)),
            _set("Warshade_Defensive.Umbral_Aura", "Umbral Aura", ("warshade",)),
            _set("Arachnos_Soldiers.Bane_Spider_Soldier", "Bane Spider Soldier", ("arachnos_soldier",)),
            _set("Widow_Training.Night_Widow_Training", "Night Widow Training", ("arachnos_widow",)),
        )
    )


def _names(tmp_path: Path, title: str, body: str = "x") -> list[str]:
    path = tmp_path / "mechanics" / "c.md"
    path.unlink(missing_ok=True)
    capture(tmp_path, "mechanics/c.md", title, body, digest="h", url="u")
    (row,), _ = build_index(tmp_path, _wide_resolver(), TIMELINE)
    return list(row["powerset_names"])


def test_an_epic_pool_is_named_only_by_its_full_display_name(tmp_path: Path) -> None:
    assert _names(tmp_path, "Fire/Kin Corruptor") == ["Fire Blast", "Kinetics"]
    assert _names(tmp_path, "Fire Mastery for corruptors") == ["Fire Mastery"]
    assert _names(tmp_path, "Notes", "Blaze Mastery " * 4) == []


def test_aliases_name_the_arachnos_branches(tmp_path: Path) -> None:
    assert _names(tmp_path, "Bane build for the VEAT") == ["Bane Spider Soldier"]
    assert _names(tmp_path, "Night Widow soloing") == ["Night Widow Training"]


def test_a_group_folder_covers_each_of_its_archetype_keys() -> None:
    rows = [
        {"file": "k", "archetype": "kheldian", "powerset_names": ["Umbral Aura"]},
        {"file": "a", "archetype": "arachnos", "powerset_names": ["Night Widow Training"]},
    ]
    report = build_coverage(rows, _wide_resolver())
    pairs = {(p["powerset"], p["archetype"]): p["group"] for p in report["pairs"]}
    assert pairs[("Umbral Aura", "warshade")] == "own"
    assert pairs[("Night Widow Training", "arachnos_widow")] == "own"
    assert report["known_over_matches"] == {"Fire Blast": 0}
