"""The manifest lists every capture, without post text, and is byte-identical on re-run."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from scripts.forum_scrape.guides import content_hash
from scripts.forum_scrape.manifest import build_manifest, main

SOURCE = Path(__file__).parent / "fixtures" / "source"
BIO_BODY = "# Bio Armor notes\n\nNo stored hash and no URL here; both come from the body and the ledger.\n"


def test_rows_cover_every_capture_sorted_by_path() -> None:
    rows = build_manifest(SOURCE / "community")
    assert [r["path"] for r in rows] == [
        "community/archetypes/scrapper/bio.md",
        "community/mechanics/claws.md",
        "community/pre-sunset/mechanics/old.md",
    ]
    bio, claws, old = rows
    assert claws == {
        "url": "https://forums.example/topic/1-claws/",
        "title": "Claws guide",
        "date_posted": "2011-06-01",
        "archetype": "",
        "content_hash": "aaaa1111aaaa1111",
        "path": "community/mechanics/claws.md",
        "era": "live",
    }
    assert bio["url"] == "https://forums.example/topic/2-bio/" and bio["archetype"] == "scrapper"
    assert bio["content_hash"] == content_hash(BIO_BODY)
    assert old["era"] == "pre-sunset" and old["url"] == "https://archive.example/topic/3-old/"


def test_main_writes_identical_bytes_and_no_post_text(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    out = tmp_path / "nested" / "manifest.json"
    argv = ["--source-root", str(SOURCE), "--out", str(out)]
    assert main(argv) == 0
    first = out.read_bytes()
    assert main(argv) == 0
    assert out.read_bytes() == first
    assert b"\r" not in first and b"never reach" not in first
    assert len(json.loads(first)) == 3
    assert "3 capture(s) listed" in capsys.readouterr().out


def test_main_reports_an_empty_root(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    assert main(["--source-root", str(tmp_path), "--out", str(tmp_path / "m.json")]) == 1
    err = capsys.readouterr().err
    assert f"E05: no captures under {tmp_path / 'community'}" in err
    assert "pass --source-root" in err
    assert not (tmp_path / "m.json").exists()
