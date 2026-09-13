"""Forum-section discovery and the threaded, shared-pacer crawl."""

import json
import threading
import time
from pathlib import Path

import pytest

from scripts.forum_scrape.scrape import (
    BASE,
    DEFAULT_FORUM_EXCLUDES,
    ForumResult,
    crawl_forum,
    crawl_forums,
    discover_forums,
    main,
)
from scripts.forum_scrape.tests.conftest import TOPIC

BOARD = """
<li class="cForumRow"><a href="https://forums.homecomingservers.com/forum/19-mastermind/">Mastermind</a>
    <a href="https://forums.homecomingservers.com/forum/19-mastermind.xml/">RSS</a>
    <a href="https://forums.homecomingservers.com/forum/19-mastermind/page/2/">2</a></li>
<li class="cForumRow"><a href='https://forums.homecomingservers.com/forum/15-scrapper/'>Scrapper</a></li>
<li class="cForumRow"><a href="https://forums.homecomingservers.com/forum/11-announcements/">Announcements</a></li>
<li class="cForumRow"><a href="https://forums.homecomingservers.com/forum/154-patch-notes/">Patch Notes</a></li>
<li class="cForumRow"><a href="https://forums.homecomingservers.com/forum/19-mastermind/">Mastermind again</a></li>
"""


def test_discover_forums_takes_sections_not_feeds_or_page_variants() -> None:
    assert discover_forums(BOARD) == [
        f"{BASE}/forum/19-mastermind/",
        f"{BASE}/forum/15-scrapper/",
    ]


def test_discover_forums_drops_the_staff_only_sections_by_default() -> None:
    """Guests cannot read posting permissions, so the administrative slugs are named."""
    everything = discover_forums(BOARD, exclude=())
    assert f"{BASE}/forum/11-announcements/" in everything
    assert f"{BASE}/forum/154-patch-notes/" in everything
    assert all(not any(slug in url for slug in DEFAULT_FORUM_EXCLUDES) for url in discover_forums(BOARD))


def test_discover_forums_takes_extra_excludes() -> None:
    assert discover_forums(BOARD, exclude=[*DEFAULT_FORUM_EXCLUDES, "scrapper"]) == [f"{BASE}/forum/19-mastermind/"]


class StubFetcher:
    """A Fetcher-shaped stub that serves canned pages and records concurrency."""

    def __init__(self, pages: dict[str, str], latency: float = 0.0) -> None:
        self.pages = pages
        self.latency = latency
        self.lock = threading.Lock()
        self.live = 0
        self.peak = 0
        self.requested: list[str] = []

    def get(self, url: str) -> str | None:
        with self.lock:
            self.requested.append(url)
            self.live += 1
            self.peak = max(self.peak, self.live)
        time.sleep(self.latency)
        with self.lock:
            self.live -= 1
        return self.pages.get(url)

    def fetch(self, url: str) -> tuple[bytes, str] | None:
        return None


FIXTURE = Path(__file__).parent / "fixtures" / "topic_sample.html"


def test_crawl_forum_reports_an_empty_section_as_skipped(tmp_path: Path) -> None:
    fetcher = StubFetcher({f"{BASE}/forum/154-patch-notes/": "<html>no topics</html>"})
    result = crawl_forum(fetcher, f"{BASE}/forum/154-patch-notes/", 1, tmp_path, False)  # type: ignore[arg-type]
    assert result == ForumResult(f"{BASE}/forum/154-patch-notes/", 0, 0, 0, "no topics visible")


def test_crawl_forum_harvests_the_topics_it_lists(tmp_path: Path) -> None:
    page = FIXTURE.read_text(encoding="utf-8")
    forum = f"{BASE}/forum/19-mastermind/"
    fetcher = StubFetcher({forum: listing_row(TOPIC), TOPIC: page})

    result = crawl_forum(fetcher, forum, 1, tmp_path, False)  # type: ignore[arg-type]
    assert (result.topics, result.found, result.written) == (1, 4, 4)
    assert (tmp_path / "index.jsonl").exists()


def listing_row(topic: str, last_activity: str = "2026-01-01T00:00:00Z") -> str:
    """One topic row as Invision renders it: the link plus started/last-posted times."""
    return (
        "<li class='ipsDataItem ipsDataItem_responsivePhoto'>"
        f"<a href='{topic}'>t</a>"
        "<time datetime='2019-01-01T00:00:00Z'>started</time>"
        f"<time datetime='{last_activity}'>last reply</time></li>"
    )


def paginated(topic: str, base: str, pages: int, last_activity: str = "2026-01-01T00:00:00Z") -> str:
    """A listing page carrying Invision's real pagination element and one topic row."""
    links = "".join(f'<li><a href="{base}page/{n}/">{n}</a></li>' for n in range(2, pages + 1))
    return (
        f"{listing_row(topic, last_activity)}"
        f"<ul class='ipsPagination' data-pages='{pages}' data-ipsPagination-perPage='25'>{links}</ul>"
    )


def test_crawl_forum_survives_an_unreachable_listing_page(tmp_path: Path) -> None:
    forum = f"{BASE}/forum/19-mastermind/"
    listing = paginated(TOPIC, forum, 2)
    fetcher = StubFetcher({forum: listing, TOPIC: FIXTURE.read_text(encoding="utf-8")})
    # The listing advertises a page 2 the stub does not serve, so get() returns None.
    result = crawl_forum(fetcher, forum, 2, tmp_path, False)  # type: ignore[arg-type]
    assert result.topics == 1


def test_crawl_forum_reports_an_unreachable_section(tmp_path: Path) -> None:
    """A 403 section (guests cannot read it) is named as such, not as empty."""
    forum = f"{BASE}/forum/92-torchbearer-player-led-event-calendar/"
    result = crawl_forum(StubFetcher({}), forum, 1, tmp_path, False)  # type: ignore[arg-type]
    assert result == ForumResult(forum, 0, 0, 0, "listing unreachable")


def test_crawl_forum_stops_at_the_real_page_count(tmp_path: Path) -> None:
    """Out-of-range pages re-serve page 1, so asking for more must not fetch them."""
    forum = f"{BASE}/forum/19-mastermind/"
    listing = paginated(TOPIC, forum, 2)
    pages = {forum: listing, f"{forum}page/2/": listing, TOPIC: FIXTURE.read_text(encoding="utf-8")}
    fetcher = StubFetcher(pages)

    crawl_forum(fetcher, forum, 50, tmp_path, False)  # type: ignore[arg-type]
    listing_fetches = [u for u in fetcher.requested if "/forum/" in u]
    assert listing_fetches == [forum, f"{forum}page/2/"]  # not 50 of them


def test_crawl_forum_follows_an_alias_sections_canonical_pagination(tmp_path: Path) -> None:
    """An alias section paginates under its real id; appending to the alias re-serves page 1.

    This is the /forum/76-mids-reborn-hero-designer/ case, which paginates as
    /forum/74-forum/page/N/ — the bug that silently capped that section at 25 topics.
    """
    alias = f"{BASE}/forum/76-mids-reborn-hero-designer/"
    canonical = f"{BASE}/forum/74-forum/"
    other = "https://forums.homecomingservers.com/topic/88888-second-thread/"
    fetcher = StubFetcher(
        {
            alias: paginated(TOPIC, canonical, 3),
            f"{canonical}page/2/": paginated(other, canonical, 3),
            f"{canonical}page/3/": paginated(other, canonical, 3),
            TOPIC: FIXTURE.read_text(encoding="utf-8"),
            other: FIXTURE.read_text(encoding="utf-8"),
        }
    )

    result = crawl_forum(fetcher, alias, 10, tmp_path, False)  # type: ignore[arg-type]
    assert [u for u in fetcher.requested if "/forum/" in u] == [
        alias,
        f"{canonical}page/2/",
        f"{canonical}page/3/",
    ]
    assert result.topics == 2  # both threads, not just the one on the alias's page 1


def test_listing_base_falls_back_when_there_is_no_pagination(tmp_path: Path) -> None:
    forum = f"{BASE}/forum/155-fort-trident/"
    fetcher = StubFetcher({forum: listing_row(TOPIC), TOPIC: FIXTURE.read_text(encoding="utf-8")})
    result = crawl_forum(fetcher, forum, 5, tmp_path, False)  # type: ignore[arg-type]
    # No pagination element: one page, and no phantom page fetches.
    assert [u for u in fetcher.requested if "/forum/" in u] == [forum]
    assert result.topics == 1


def test_crawl_forums_runs_sections_concurrently(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    forums = [f"{BASE}/forum/{n}-section/" for n in range(6)]
    fetcher = StubFetcher(dict.fromkeys(forums, "<html>empty</html>"), latency=0.05)

    results = crawl_forums(fetcher, forums, 1, tmp_path, False, workers=4)  # type: ignore[arg-type]
    assert len(results) == 6
    assert all(r.skipped_reason for r in results)
    assert fetcher.peak > 1  # they really did overlap
    assert capsys.readouterr().out.count("skip") == 6


def test_crawl_forums_serialises_when_asked_for_one_worker(tmp_path: Path) -> None:
    forums = [f"{BASE}/forum/{n}-section/" for n in range(3)]
    fetcher = StubFetcher(dict.fromkeys(forums, "<html>empty</html>"), latency=0.02)

    crawl_forums(fetcher, forums, 1, tmp_path, False, workers=0)  # type: ignore[arg-type]
    assert fetcher.peak == 1  # workers is floored at 1, never zero


def test_main_queues_every_section_off_the_board_index(
    fake_net, topic_page: str, tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    routes, _ = fake_net
    routes[f"{BASE}/"] = BOARD.encode()
    routes[f"{BASE}/forum/19-mastermind/"] = listing_row(TOPIC).encode()
    routes[f"{BASE}/forum/15-scrapper/"] = b"<html>empty</html>"
    routes[TOPIC] = topic_page.encode()

    assert main(["--all-forums", "--out", str(tmp_path), "--delay", "0", "--workers", "2"]) == 0
    out = capsys.readouterr().out
    assert "queued 2 forum section(s) across 2 worker(s)" in out  # announcements/patch-notes dropped
    assert "4 artifact(s) written" in out
    # The barren section is named, with the flag that skips it next time.
    assert "1 section(s) yielded no builds" in out
    assert "--exclude-forum 15-scrapper" in out

    report = (tmp_path / "sections.md").read_text(encoding="utf-8")
    assert "| 19-mastermind | 1 | 4 |" in report
    assert "| 15-scrapper | 0 | 0 | no topics visible |" in report
    assert json.loads((tmp_path / "sections.json").read_text(encoding="utf-8"))[0]["found"] == 4


def test_main_can_exclude_extra_sections(fake_net, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    routes, _ = fake_net
    routes[f"{BASE}/"] = BOARD.encode()
    routes[f"{BASE}/forum/15-scrapper/"] = b"<html>empty</html>"

    argv = ["--all-forums", "--exclude-forum", "mastermind", "--out", str(tmp_path), "--delay", "0"]
    assert main(argv) == 0
    assert "queued 1 forum section(s)" in capsys.readouterr().out


def test_main_reports_an_unreadable_board_index(fake_net, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    assert main(["--all-forums", "--out", str(tmp_path), "--delay", "0"]) == 1
    assert "E02: could not read the board index" in capsys.readouterr().err
