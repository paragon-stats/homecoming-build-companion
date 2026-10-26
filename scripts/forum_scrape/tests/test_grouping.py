"""Author grouping: the build-masters get folders, the one-off posters get pooled."""

import json
from pathlib import Path

import pytest

from scripts.forum_scrape.scrape import author_counts, group_by_author, main, read_index

TOPIC = "https://forums.homecomingservers.com/topic/99999-a-bots-build-thread/"


def _row(out_dir: Path, author: str, name: str, kind: str = "share-block") -> dict[str, object]:
    """Write a stand-in artifact file and return the index row that points at it."""
    path = out_dir / "99999-a-bots-build-thread" / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(f"build by {author}\n", encoding="utf-8")
    return {"author": author, "kind": kind, "topic_url": TOPIC, "path": str(path)}


@pytest.fixture
def indexed(tmp_path: Path) -> Path:
    rows = [
        *(_row(tmp_path, "Buildmaster", f"{i}-buildmaster.txt") for i in range(3)),
        *(_row(tmp_path, "Casual", f"{i}-casual.txt") for i in range(10, 11)),
        _row(tmp_path, "Buildmaster", "20-buildmaster.export.txt", kind="forum-export"),
    ]
    (tmp_path / "index.jsonl").write_text(
        "".join(json.dumps(row) + "\n" for row in rows),
        encoding="utf-8",
    )
    return tmp_path


def test_read_index_keeps_one_row_per_path(tmp_path: Path) -> None:
    assert read_index(tmp_path) == []  # no index yet
    row = _row(tmp_path, "A", "1-a.txt")
    (tmp_path / "index.jsonl").write_text(
        json.dumps({**row, "posted_at": "old"}) + "\n\n" + json.dumps({**row, "posted_at": "new"}) + "\n",
        encoding="utf-8",
    )
    rows = read_index(tmp_path)
    assert len(rows) == 1 and rows[0]["posted_at"] == "new"


def test_author_counts_ignore_the_readable_export(indexed: Path) -> None:
    # Buildmaster has 4 rows but only 3 builds — the export is a second view of one.
    assert author_counts(read_index(indexed)) == {"Buildmaster": 3, "Casual": 1}


def test_grouping_splits_on_the_threshold(indexed: Path) -> None:
    named, pooled = group_by_author(indexed, threshold=3)
    assert (named, pooled) == (1, 1)

    root = indexed / "by-author"
    assert sorted(p.name for p in root.iterdir() if p.is_dir()) == ["_pool", "buildmaster"]
    # The export follows its author into their folder even though it did not count.
    assert len(list((root / "buildmaster").iterdir())) == 4
    assert len(list((root / "_pool").iterdir())) == 1
    # Copies, not moves: the by-topic tree is still intact.
    assert (indexed / "99999-a-bots-build-thread" / "0-buildmaster.txt").exists()


def test_regrouping_at_a_new_threshold_replaces_the_old_split(indexed: Path) -> None:
    group_by_author(indexed, threshold=3)
    named, pooled = group_by_author(indexed, threshold=10)
    assert (named, pooled) == (0, 2)
    assert not (indexed / "by-author" / "buildmaster").exists()
    assert len(list((indexed / "by-author" / "_pool").iterdir())) == 5


def test_report_lists_every_author_with_their_count(indexed: Path) -> None:
    group_by_author(indexed, threshold=3)
    report = (indexed / "by-author" / "authors.md").read_text(encoding="utf-8")
    assert "| Buildmaster | 3 | buildmaster |" in report
    assert "| Casual | 1 | _pool |" in report
    assert json.loads((indexed / "by-author" / "authors.json").read_text(encoding="utf-8")) == {
        "Buildmaster": 3,
        "Casual": 1,
    }


def test_missing_files_are_skipped_not_fatal(indexed: Path) -> None:
    (indexed / "99999-a-bots-build-thread" / "0-buildmaster.txt").unlink()
    group_by_author(indexed, threshold=3)
    assert len(list((indexed / "by-author" / "buildmaster").iterdir())) == 3


def test_regroup_only_needs_a_threshold(indexed: Path, capsys: pytest.CaptureFixture[str]) -> None:
    with pytest.raises(SystemExit):
        main(["--out", str(indexed), "--regroup-only"])
    assert main(["--out", str(indexed), "--regroup-only", "--group-by-author", "3"]) == 0
    assert "1 author folder(s), 1 author(s) pooled" in capsys.readouterr().out
