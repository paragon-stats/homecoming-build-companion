"""Extraction tests for the forum scraper — pure functions, no network.

The fixture is a trimmed real topic page: post 1 hides a genuine ``|MxDz;…|`` block
(captured live, so it really inflates) plus a forum export inside a spoiler; post 2
carries a ``|MBD;…|`` block and a cohplanner link.
"""

import json
import re
from pathlib import Path
from typing import cast

import pytest

from scripts.forum_scrape.scrape import (
    Fetcher,
    check_block,
    find_forum_export,
    find_planner_links,
    find_share_blocks,
    iter_posts,
    last_page_number,
    page_title,
    slugify,
    strip_html,
    write_artifacts,
)
from scripts.forum_scrape.scrape import (
    harvest_topic as _harvest_topic,  # imported to keep the module's public surface exercised
)

FIXTURE = Path(__file__).parent / "fixtures" / "topic_sample.html"
TOPIC_URL = "https://forums.homecomingservers.com/topic/99999-a-bots-build-thread/"


@pytest.fixture
def page() -> str:
    return FIXTURE.read_text(encoding="utf-8")


@pytest.fixture
def posts(page: str):
    return list(iter_posts(page))


def test_iter_posts_finds_every_article_with_attribution(posts) -> None:
    assert [p.comment_id for p in posts] == ["111111", "222222"]
    assert [p.author for p in posts] == ["Greenbone", "Tidge"]
    assert posts[0].author_profile.endswith("/profile/6090-greenbone/")
    assert posts[0].posted_at == "2026-09-01T23:28:24Z"


def test_spoiler_contents_are_not_truncated(posts) -> None:
    """The nested spoiler div must not end the post body early."""
    assert "MxDz" in posts[0].text
    assert "Let me know what you think." in posts[0].text


def test_share_block_extracted_and_really_decodes(posts) -> None:
    blocks = find_share_blocks(posts[0].text)
    assert len(blocks) == 1
    assert blocks[0].startswith("|MxDz;1549;720;1440;HEX;|")
    assert "----" not in blocks[0]  # the dashed banner is not payload
    chunk, ok, error, decoded = check_block(blocks[0])
    assert (chunk, ok, error) == ("mxd", True, "")
    assert decoded is None  # an .mxd inflates to a binary buffer, not a file Mids opens


def test_mbd_block_decodes_to_loadable_build_json(posts) -> None:
    blocks = find_share_blocks(posts[1].text)
    assert len(blocks) == 1
    chunk, ok, error, decoded = check_block(blocks[0])
    assert (chunk, ok, error) == ("mbd", True, "")
    assert decoded is not None
    build = json.loads(decoded.decode("utf-8"))
    assert build["Class"] == "Class_Mastermind"
    assert build["PowerEntries"][0]["PowerName"] == "Mastermind_Summon.Robotics.Battle_Drones"


def test_planner_link_rebuilds_a_share_block(posts) -> None:
    blocks = find_planner_links(posts[1].html)
    assert len(blocks) == 1
    header, *payload = blocks[0].split("\n")
    assert re.fullmatch(r"[|]MxDz;1549;720;\d+;HEX;[|]", header)
    assert "".join(line.strip("|") for line in payload) in posts[1].html
    assert check_block(blocks[0])[0] == "mxd"


def test_forum_export_stops_at_the_share_block_that_follows_it() -> None:
    """Without the cut, the export swallows the block header and misclassifies as mbd."""
    from scripts.forum_scrape.scrape import classify

    text = "\n".join([*(f"Level {n}: Power {n}" for n in range(1, 8)), "|MBD;1;2;3;BASE64;|", "|QUJD|"])
    export = find_forum_export(text)
    assert export.endswith("Level 7: Power 7")
    assert classify(export) is None


def test_attachments_keep_build_files_and_drop_images(posts) -> None:
    from scripts.forum_scrape.scrape import check_attachment, find_attachments

    found = find_attachments(posts[0].html)
    assert len(found) == 1  # the .png attachment in the same post is not a build
    url, extension = found[0]
    assert extension == "mbd"
    assert "attachment.php?id=119180&key=abc123" in url  # the &amp; is unescaped

    valid = json.dumps(
        {"BuiltWith": {"App": "Mids Reborn"}, "Level": "49", "Class": "Class_Scrapper", "PowerEntries": []}
    ).encode()
    assert check_attachment(valid, "mbd") == (True, "")
    assert check_attachment(b"{}", "mbd") == (False, "attachment JSON has no PowerEntries")
    assert check_attachment(b"<html>error</html>", "mbd")[0] is False
    assert check_attachment(b"binary", "mxd") == (None, "legacy .mxd attachment stored as-is; not parsed")
    # The engine's reader is the authority on "is this a build", so a document it rejects
    # is reported as such rather than being waved through by a looser local check.
    incomplete = json.dumps({"PowerEntries": []}).encode()
    ok, error = check_attachment(incomplete, "mbd")
    assert ok is False and "does not parse as a build" in error


def test_forum_export_found_only_where_it_exists(posts) -> None:
    assert find_forum_export(posts[0].text).startswith("Level 1: Battle Drones")
    assert find_forum_export(posts[1].text) == ""


def test_forum_export_ignores_incidental_level_mentions() -> None:
    assert find_forum_export("Level 1: hi\nLevel 2: there") == ""


def test_forum_export_keeps_its_tail_when_nothing_follows() -> None:
    text = "\n".join([*(f"Level {n}: Power {n}" for n in range(1, 8)), "(A) Kismet: Accuracy +6%"])
    assert find_forum_export(text).endswith("(A) Kismet: Accuracy +6%")


def test_decode_mbd_rejects_an_mxd_block(posts) -> None:
    from scripts.forum_scrape.scrape import decode_mbd

    mxd_block = find_share_blocks(posts[0].text)[0]
    with pytest.raises(ValueError, match="not an mbd share block"):
        decode_mbd(mxd_block)


def test_strip_html_keeps_line_structure() -> None:
    assert strip_html("<p>a<br />b</p><li>c</li>") == "a\nb\nc\n"


def test_page_metadata(page: str) -> None:
    assert page_title(page) == "A bots build thread"
    assert last_page_number(page) == 2


def test_slugify() -> None:
    assert slugify("Tidge's Bots/Traps!") == "tidge-s-bots-traps"
    assert slugify("") == "unknown"


def test_write_artifacts_pairs_each_build_with_a_sidecar(tmp_path: Path, page: str) -> None:
    class FakeFetcher:
        """Stands in for Fetcher so the harvest path runs without touching the network."""

        def get(self, url: str) -> str | None:
            return page if url == TOPIC_URL else None

        def fetch(self, url: str) -> tuple[bytes, str] | None:
            return None  # the attachment download fails; the rest of the post still lands

    artifacts = _harvest_topic(cast(Fetcher, FakeFetcher()), TOPIC_URL)
    kinds = sorted({(a.kind, a.chunk_format) for a in artifacts})
    assert kinds == [
        ("forum-export", "text"),
        ("planner-link", "mxd"),
        ("share-block", "mbd"),
        ("share-block", "mxd"),
    ]

    assert write_artifacts(artifacts, tmp_path, force=False) == len(artifacts)
    assert write_artifacts(artifacts, tmp_path, force=False) == 0  # idempotent re-run

    folder = tmp_path / "99999-a-bots-build-thread"
    body = folder / "111111-greenbone.txt"
    meta = json.loads((folder / "111111-greenbone.json").read_text(encoding="utf-8"))
    assert body.read_text(encoding="utf-8").startswith("|MxDz;")
    assert meta["author"] == "Greenbone"
    assert meta["post_url"] == f"{TOPIC_URL}#comment-111111"
    assert meta["topic_title"] == "A bots build thread"
    assert "ai-train=no" in meta["license_note"]
    assert (tmp_path / "index.jsonl").read_text(encoding="utf-8").count("\n") == len(artifacts)
