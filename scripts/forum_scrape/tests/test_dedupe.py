"""Build identity: the same build must collapse however it was saved or posted."""

import json
from pathlib import Path

import pytest

from scripts.forum_scrape.dedupe import (
    artifact_identity,
    dedupe,
    identity_from_mbd,
    load_index_maps,
)

# The engine's tracked Mids reference dump, so these tests run without a local tools/mids-dump/out.
DUMP = Path("src/coh_engine/tests/fixtures/mids")

BUILD = {
    "BuiltWith": {"App": "Mids Reborn", "Version": "3.8.6.0"},
    "Level": "49",
    "Name": "Bots-Traps",
    "Comment": "first draft",
    "Class": "Class_Mastermind",
    "LastPower": 25,
    "PowerSets": ["Mastermind_Summon.Robotics", "Mastermind_Buff.Traps", ""],
    "PowerEntries": [
        {
            "PowerName": "Mastermind_Summon.Robotics.Battle_Drones",
            "Level": 1,
            "StatInclude": True,
            "SlotEntries": [
                {"Level": 1, "Enhancement": {"Uid": "Crafted_X", "IoLevel": 49, "RelativeLevel": "Even"}},
                {"Level": 3, "Enhancement": None},
            ],
        }
    ],
}


def write(tmp_path: Path, name: str, build: dict) -> Path:
    path = tmp_path / name
    path.write_text(json.dumps(build, indent=2), encoding="utf-8")
    return path


def test_identity_ignores_name_comment_and_version(tmp_path: Path) -> None:
    """Re-saving a build under a new character name is the same build."""
    original = identity_from_mbd(json.dumps(BUILD).encode("utf-8"))
    renamed = {**BUILD, "Name": "Something Else", "Comment": "v2", "LastPower": 24}
    renamed["BuiltWith"] = {"App": "Mids Reborn", "Version": "3.9.0.0"}
    assert identity_from_mbd(json.dumps(renamed).encode("utf-8")) == original


def test_identity_ignores_the_totals_pane_toggle(tmp_path: Path) -> None:
    toggled = json.loads(json.dumps(BUILD))
    toggled["PowerEntries"][0]["StatInclude"] = False
    assert identity_from_mbd(json.dumps(toggled).encode("utf-8")) == identity_from_mbd(
        json.dumps(BUILD).encode("utf-8")
    )


def test_identity_changes_when_an_enhancement_changes() -> None:
    swapped = json.loads(json.dumps(BUILD))
    swapped["PowerEntries"][0]["SlotEntries"][0]["Enhancement"]["Uid"] = "Crafted_Y"
    assert identity_from_mbd(json.dumps(swapped).encode("utf-8")) != identity_from_mbd(
        json.dumps(BUILD).encode("utf-8")
    )


def test_identity_changes_when_a_power_changes() -> None:
    swapped = json.loads(json.dumps(BUILD))
    swapped["PowerEntries"][0]["PowerName"] = "Mastermind_Buff.Traps.Caltrops"
    assert identity_from_mbd(json.dumps(swapped).encode("utf-8")) != identity_from_mbd(
        json.dumps(BUILD).encode("utf-8")
    )


def test_identity_rejects_what_is_not_a_build() -> None:
    with pytest.raises(ValueError, match=r"not .mbd JSON"):
        identity_from_mbd(b"<html>nope</html>")
    with pytest.raises(ValueError, match="no PowerEntries"):
        identity_from_mbd(b'{"Class": "Class_Mastermind"}')


def test_artifact_identity_prefers_the_decoded_build(tmp_path: Path) -> None:
    """A share block identifies from its decoded .mbd, not the envelope text."""
    block = tmp_path / "1-a.txt"
    block.write_text("|MBD;1;2;3;BASE64;|\n|QUJD|\n", encoding="utf-8")
    write(tmp_path, "1-a.mbd", BUILD)
    row = {"path": str(block), "chunk_format": "mbd", "decoded_file": "1-a.mbd"}
    assert artifact_identity(row) == identity_from_mbd(json.dumps(BUILD).encode("utf-8"))


def test_artifact_identity_reads_an_mbd_attachment(tmp_path: Path) -> None:
    path = write(tmp_path, "1-a.mbd", BUILD)
    row = {"path": str(path), "chunk_format": "mbd"}
    assert artifact_identity(row) == identity_from_mbd(json.dumps(BUILD).encode("utf-8"))


def test_artifact_identity_falls_back_to_bytes(tmp_path: Path) -> None:
    """Readable exports have no parse, so verbatim reposts still collapse on content."""
    path = tmp_path / "1-a.export.txt"
    path.write_text("Level 1: Battle Drones\n", encoding="utf-8")
    row = {"path": str(path), "chunk_format": "text"}
    identity = artifact_identity(row)
    assert identity.startswith("bytes:")

    same = tmp_path / "2-b.export.txt"
    same.write_text("Level 1: Battle Drones\n", encoding="utf-8")
    assert artifact_identity({"path": str(same), "chunk_format": "text"}) == identity


def test_artifact_identity_handles_a_missing_file(tmp_path: Path) -> None:
    row = {"path": str(tmp_path / "gone.mbd"), "chunk_format": "mbd"}
    assert artifact_identity(row).startswith("missing:")


def test_artifact_identity_falls_back_when_the_decoded_file_is_junk(tmp_path: Path) -> None:
    block = tmp_path / "1-a.txt"
    block.write_text("|MBD;1;2;3;BASE64;|\n", encoding="utf-8")
    (tmp_path / "1-a.mbd").write_text("not json", encoding="utf-8")
    row = {"path": str(block), "chunk_format": "mbd", "decoded_file": "1-a.mbd"}
    assert artifact_identity(row).startswith("bytes:")


def test_mxd_path_is_skipped_without_the_index_maps(tmp_path: Path) -> None:
    path = tmp_path / "1-a.txt"
    path.write_text("|MxDz;1;2;3;HEX;|\n|ABCD|\n", encoding="utf-8")
    assert artifact_identity({"path": str(path), "chunk_format": "mxd"}).startswith("bytes:")


def test_mxd_path_degrades_when_the_block_will_not_parse(tmp_path: Path) -> None:
    path = tmp_path / "1-a.txt"
    path.write_text("|MxDz;1;2;3;HEX;|\n|ZZZZ|\n", encoding="utf-8")
    row = {"path": str(path), "chunk_format": "mxd"}
    assert artifact_identity(row, {1: "A"}, {}).startswith("bytes:")


def test_load_index_maps_reports_an_absent_dump(tmp_path: Path) -> None:
    assert load_index_maps(tmp_path) == (None, None)


def test_load_index_maps_reads_a_real_dump() -> None:
    powers, enhancements = load_index_maps(DUMP)
    assert powers and enhancements


def test_dedupe_credits_the_earliest_poster_and_lists_reposters(tmp_path: Path) -> None:
    original = write(tmp_path, "100-alice.mbd", BUILD)
    # Bob reposts Alice's build verbatim; Carol re-saves it under another name.
    copy = write(tmp_path, "200-bob.mbd", BUILD)
    renamed = write(tmp_path, "300-carol.mbd", {**BUILD, "Name": "Carol's Bots"})
    rows = [
        {"path": str(copy), "chunk_format": "mbd", "author": "Bob", "posted_at": "2026-02-01", "post_url": "u#2"},
        {"path": str(original), "chunk_format": "mbd", "author": "Alice", "posted_at": "2026-01-01", "post_url": "u#1"},
        {"path": str(renamed), "chunk_format": "mbd", "author": "Carol", "posted_at": "2026-03-01", "post_url": "u#3"},
    ]
    records = dedupe(rows)
    assert len(records) == 1
    record = records[0]
    assert record.first_author == "Alice"  # earliest, despite Bob's row coming first
    assert record.repost_count == 2
    assert sorted(record.reposts) == [("Bob", "u#2"), ("Carol", "u#3")]


def test_dedupe_keeps_genuinely_different_builds_apart(tmp_path: Path) -> None:
    other = json.loads(json.dumps(BUILD))
    other["PowerEntries"][0]["SlotEntries"][0]["Enhancement"]["Uid"] = "Crafted_Z"
    rows = [
        {
            "path": str(write(tmp_path, "1-a.mbd", BUILD)),
            "chunk_format": "mbd",
            "author": "Alice",
            "posted_at": "2026-01-01",
            "post_url": "u#1",
        },
        {
            "path": str(write(tmp_path, "2-a.mbd", other)),
            "chunk_format": "mbd",
            "author": "Alice",
            "posted_at": "2026-01-02",
            "post_url": "u#2",
        },
    ]
    assert len(dedupe(rows)) == 2


def test_dedupe_does_not_count_the_same_post_twice(tmp_path: Path) -> None:
    """The phantom-page bug harvested one post repeatedly; that is not a repost."""
    path = write(tmp_path, "1-a.mbd", BUILD)
    row = {"path": str(path), "chunk_format": "mbd", "author": "Alice", "posted_at": "2026-01-01", "post_url": "u#1"}
    records = dedupe([row, dict(row), dict(row)])
    assert len(records) == 1
    assert records[0].repost_count == 0


def test_dedupe_sorts_rows_without_a_timestamp_last(tmp_path: Path) -> None:
    undated = {
        "path": str(write(tmp_path, "1-a.mbd", BUILD)),
        "chunk_format": "mbd",
        "author": "Nobody",
        "post_url": "u#9",
    }
    dated = {
        "path": str(write(tmp_path, "2-a.mbd", BUILD)),
        "chunk_format": "mbd",
        "author": "Alice",
        "posted_at": "2026-01-01",
        "post_url": "u#1",
    }
    assert dedupe([undated, dated])[0].first_author == "Alice"


def test_identity_from_a_real_mxd_block(tmp_path: Path) -> None:
    """The legacy format identifies through the same shape as .mbd, via the real reader.

    The fixture's block was captured live, so this exercises decode + StaticIndex
    resolution rather than a hand-made stand-in.
    """
    from scripts.forum_scrape.scrape import find_share_blocks, iter_posts

    fixture = Path(__file__).parent / "fixtures" / "topic_sample.html"
    post = next(iter(iter_posts(fixture.read_text(encoding="utf-8"))))
    block = find_share_blocks(post.text)[0]
    assert block.startswith("|MxDz;")

    path = tmp_path / "1-a.txt"
    path.write_text(block, encoding="utf-8")
    powers, enhancements = load_index_maps(DUMP)
    identity = artifact_identity({"path": str(path), "chunk_format": "mxd"}, powers, enhancements)
    assert not identity.startswith(("bytes:", "missing:"))  # a real parse, not the fallback

    # The same block posted again is the same build.
    twin = tmp_path / "2-b.txt"
    twin.write_text(block, encoding="utf-8")
    assert artifact_identity({"path": str(twin), "chunk_format": "mxd"}, powers, enhancements) == identity


def test_cli_dedupe_writes_the_report_and_the_ledger(tmp_path: Path, capsys) -> None:
    """`--dedupe` is a local pass over an existing --out: no crawling, no requests."""
    from scripts.forum_scrape.scrape import main

    folder = tmp_path / "1-thread"
    folder.mkdir()
    rows = []
    for name, author, when in (("a", "Alice", "2026-01-01"), ("b", "Bob", "2026-02-01")):
        path = folder / f"{name}.mbd"
        path.write_text(json.dumps(BUILD), encoding="utf-8")  # Bob reposts Alice's build
        rows.append(
            {
                "kind": "attachment",
                "chunk_format": "mbd",
                "author": author,
                "posted_at": when,
                "post_url": f"https://f/topic/1-t/#comment-{name}",
                "topic_url": "https://f/topic/1-t/",
                "path": str(path),
            }
        )
    # A readable export is excluded from the build count. It needs its own path —
    # read_index keys rows by path, so reusing one would overwrite that artifact.
    export = folder / "a.export.txt"
    export.write_text("Level 1: Battle Drones\n", encoding="utf-8")
    rows.append({**rows[0], "kind": "forum-export", "author": "Ignored", "path": str(export)})
    (tmp_path / "index.jsonl").write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")

    assert main(["--out", str(tmp_path), "--dedupe", "--dump-dir", str(tmp_path / "absent")]) == 0
    out = capsys.readouterr().out
    assert "1 distinct build(s), 1 duplicate posting(s) collapsed" in out

    ledger = [json.loads(line) for line in (tmp_path / "builds.jsonl").read_text(encoding="utf-8").splitlines()]
    assert len(ledger) == 1
    assert ledger[0]["first_author"] == "Alice"
    assert ledger[0]["reposts"] == [{"author": "Bob", "post_url": "https://f/topic/1-t/#comment-b"}]

    report = (tmp_path / "builds.md").read_text(encoding="utf-8")
    assert "| Alice | 1 |" in report
    assert "Most reposted builds" in report


def test_cli_dedupe_can_also_group_by_author(tmp_path: Path, capsys) -> None:
    folder = tmp_path / "1-thread"
    folder.mkdir()
    path = folder / "a.mbd"
    path.write_text(json.dumps(BUILD), encoding="utf-8")
    (tmp_path / "index.jsonl").write_text(
        json.dumps(
            {
                "kind": "attachment",
                "chunk_format": "mbd",
                "author": "Alice",
                "posted_at": "2026-01-01",
                "post_url": "https://f/topic/1-t/#comment-a",
                "topic_url": "https://f/topic/1-t/",
                "path": str(path),
            }
        )
        + "\n",
        encoding="utf-8",
    )
    from scripts.forum_scrape.scrape import main

    assert main(["--out", str(tmp_path), "--dedupe", "--group-by-author", "1"]) == 0
    out = capsys.readouterr().out
    assert "distinct build(s)" in out and "author folder(s)" in out


def test_dedupe_uses_the_index_maps_when_a_dump_is_given(tmp_path: Path) -> None:
    row = {
        "path": str(write(tmp_path, "1-a.mbd", BUILD)),
        "chunk_format": "mbd",
        "author": "Alice",
        "posted_at": "2026-01-01",
        "post_url": "u#1",
    }
    assert len(dedupe([row], DUMP)) == 1
