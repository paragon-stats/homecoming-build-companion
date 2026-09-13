"""Fetch-layer and CLI tests — the network is faked, nothing here leaves the machine.

:class:`Fetcher` is the only part that touches the site, so its contract is pinned here:
it obeys robots.txt, it waits between requests, and a failed fetch degrades to ``None``
instead of aborting a crawl.
"""

import base64
import json
from pathlib import Path

import brotli
import pytest

from scripts.forum_scrape import scrape
from scripts.forum_scrape.scrape import (
    Fetcher,
    check_block,
    harvest_topic,
    main,
    page_title,
    strip_html,
    write_artifacts,
)
from scripts.forum_scrape.tests.conftest import ATTACHMENT, FIXTURE, TOPIC
from scripts.forum_scrape.tests.test_forums import listing_row


def test_fetcher_honours_robots_and_returns_text(fake_net) -> None:
    routes, calls = fake_net
    routes[TOPIC] = b"<html>hello</html>"
    fetcher = Fetcher("test-agent", delay=0.0)

    assert fetcher.get(TOPIC) == "<html>hello</html>"
    assert fetcher.get("https://forums.homecomingservers.com/search/?q=x") is None
    assert calls == ["https://forums.homecomingservers.com/robots.txt", TOPIC]


def test_fetcher_gunzips_and_waits_between_requests(fake_net, monkeypatch: pytest.MonkeyPatch) -> None:
    routes, _ = fake_net
    routes[TOPIC + "#gzip"] = b"compressed body"
    slept: list[float] = []
    monkeypatch.setattr(scrape.time, "sleep", slept.append)
    fetcher = Fetcher("test-agent", delay=5.0)

    assert fetcher.get(TOPIC + "#gzip") == "compressed body"
    assert slept and slept[-1] > 0  # the delay was actually observed


def test_fetcher_reports_and_survives_a_failed_fetch(fake_net, capsys: pytest.CaptureFixture[str]) -> None:
    fetcher = Fetcher("test-agent", delay=0.0)
    assert fetcher.get("https://forums.homecomingservers.com/topic/1-gone/") is None
    assert "E01: fetch failed" in capsys.readouterr().err


def test_harvest_topic_tolerates_missing_pages(fake_net) -> None:
    routes, _ = fake_net
    routes[TOPIC] = FIXTURE.read_bytes()  # links a page 2 that the fake net does not serve
    fetcher = Fetcher("test-agent", delay=0.0)

    assert len(harvest_topic(fetcher, TOPIC)) == 4
    assert harvest_topic(fetcher, "https://forums.homecomingservers.com/topic/2-missing/") == []


def test_harvest_topic_reads_every_page_of_a_thread(fake_net) -> None:
    routes, _ = fake_net
    routes[TOPIC] = FIXTURE.read_bytes()
    routes[TOPIC + "page/2/"] = FIXTURE.read_bytes()
    fetcher = Fetcher("test-agent", delay=0.0)

    assert len(harvest_topic(fetcher, TOPIC)) == 8


def test_attached_build_file_is_downloaded_and_written(fake_net, tmp_path: Path) -> None:
    routes, _ = fake_net
    routes[TOPIC] = FIXTURE.read_bytes()
    routes[ATTACHMENT] = (
        b'{"BuiltWith": {"App": "Mids Reborn"}, "Level": "49", "Class": "Class_Mastermind", "PowerEntries": []}'
    )
    fetcher = Fetcher("test-agent", delay=0.0)

    artifacts = harvest_topic(fetcher, TOPIC)
    attached = [a for a in artifacts if a.kind == "attachment"]
    assert len(attached) == 1
    assert (attached[0].chunk_format, attached[0].decoded_ok, attached[0].suffix) == ("mbd", True, ".mbd")

    write_artifacts(artifacts, tmp_path, force=False)
    saved = tmp_path / "99999-a-bots-build-thread" / "111111-greenbone.mbd"
    assert json.loads(saved.read_text(encoding="utf-8"))["Class"] == "Class_Mastermind"
    meta = json.loads((saved.with_suffix(".mbd.json")).read_text(encoding="utf-8"))
    assert meta["kind"] == "attachment" and meta["author"] == "Greenbone"


def test_main_crawls_a_forum_listing_and_writes(fake_net, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    routes, _ = fake_net
    forum = "https://forums.homecomingservers.com/forum/19-mastermind/"
    routes[forum] = listing_row(TOPIC).encode()
    routes[TOPIC] = FIXTURE.read_bytes()
    # Listing page 2 is deliberately unrouted: a 404 mid-crawl must skip, not abort.

    assert main(["--forum", forum, "--pages", "2", "--out", str(tmp_path), "--delay", "0"]) == 0
    out = capsys.readouterr().out
    assert "4 artifact(s) written" in out
    assert "by-author" not in out  # grouping is opt-in
    assert (tmp_path / "index.jsonl").exists()


def test_main_can_group_by_author_in_the_same_run(fake_net, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    routes, _ = fake_net
    routes[TOPIC] = FIXTURE.read_bytes()

    argv = ["--topic", TOPIC, "--out", str(tmp_path), "--delay", "0", "--group-by-author", "2"]
    assert main(argv) == 0
    assert "by-author/: 1 author folder(s), 1 author(s) pooled" in capsys.readouterr().out
    # Tidge's post carries a share block and a planner link — two builds, so a folder.
    assert (tmp_path / "by-author" / "tidge").is_dir()
    # Greenbone's single block (the attachment is unrouted here) lands in the pool.
    assert (tmp_path / "by-author" / "_pool").is_dir()


def test_main_requires_a_target(tmp_path: Path) -> None:
    with pytest.raises(SystemExit):
        main(["--out", str(tmp_path)])


def test_check_block_rejects_what_is_not_a_share_block() -> None:
    assert check_block("just some prose") == ("unknown", False, "not a recognised share block", None)
    bad_mxd = "|MxDz;10;4;6;HEX;|\n|ZZZZZZ|"
    kind, ok, error, decoded = check_block(bad_mxd)
    assert (kind, ok, decoded) == ("mxd", False, None) and error
    header_only = "|MBD;10;4;6;BASE64;|"
    kind, ok, error, _ = check_block(header_only)
    assert (kind, ok) == ("mbd", False) and "payload" in error


def test_mbd_block_rejects_a_corrupt_payload() -> None:
    """Every failure mode of the base64 -> Brotli -> JSON chain is reported, not raised."""
    # Five base64-alphabet characters: passes the payload-line shape, fails to decode.
    not_base64 = "|MBD;10;4;5;BASE64;|\n|QUJDR|"
    assert check_block(not_base64)[:2] == ("mbd", False)
    assert "base64" in check_block(not_base64)[2]

    wrong_size = "|MBD;10;999;4;BASE64;|\n|QUJD|"
    assert check_block(wrong_size)[2] == "mbd compressed size mismatch: header 999, actual 3"

    not_brotli = "|MBD;10;3;4;BASE64;|\n|QUJD|"  # "ABC" is valid base64, not valid Brotli
    assert "brotli" in check_block(not_brotli)[2]

    valid_brotli_not_a_build = _mbd_block(b'{"nope": 1}')
    kind, ok, error, decoded = check_block(valid_brotli_not_a_build)
    assert (kind, ok, decoded) == ("mbd", False, None)
    assert error == "attachment JSON has no PowerEntries"


def _mbd_block(payload: bytes) -> str:
    """Wrap arbitrary bytes the way Mids wraps a build: Brotli, base64, 67-char lines."""
    compressed = brotli.compress(payload)
    encoded = base64.b64encode(compressed).decode("ascii")
    lines = [f"|{encoded[i : i + 67]}|" for i in range(0, len(encoded), 67)]
    return "\n".join([f"|MBD;{len(payload)};{len(compressed)};{len(encoded)};BASE64;|", *lines])


def test_header_without_payload_is_not_a_block() -> None:
    assert scrape.find_share_blocks("|MBD;1;2;3;BASE64;|\nback to prose") == []


def test_author_falls_back_to_the_profile_slug() -> None:
    article = (
        "<article id='elComment_7'>"
        "<a href='https://forums.homecomingservers.com/profile/42-some-poster/'>x</a>"
        "<div data-role='commentContent'>body</div></article>"
    )
    assert next(iter(scrape.iter_posts(article))).author == "poster"


def test_unclosed_content_div_returns_the_rest_of_the_post() -> None:
    article = "<article id='elComment_8'><div data-role='commentContent'>tail without a close"
    assert next(iter(scrape.iter_posts(article))).text == "tail without a close"


def test_page_title_absent() -> None:
    assert page_title("<html>no title element</html>") == ""


def test_strip_html_unescapes_entities() -> None:
    assert strip_html("<p>Copy &amp; Paste</p>") == "Copy & Paste\n"
