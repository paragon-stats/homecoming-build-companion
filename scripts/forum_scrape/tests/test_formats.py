"""Format selection and the topic ledger that makes a second pass cheap."""

import json
from pathlib import Path

import pytest

from scripts.forum_scrape.scrape import (
    ALL_FORMATS,
    BASE,
    Artifact,
    crawl_forum,
    harvest_topic,
    load_topic_ledger,
    main,
    record_section,
    record_topic,
)
from scripts.forum_scrape.tests.conftest import TOPIC
from scripts.forum_scrape.tests.test_forums import FIXTURE, StubFetcher, paginated

FORUM = f"{BASE}/forum/19-mastermind/"
OTHER = "https://forums.homecomingservers.com/topic/88888-another-thread/"


def fetcher_for(page: str | None = None) -> StubFetcher:
    body = page if page is not None else FIXTURE.read_text(encoding="utf-8")
    return StubFetcher({FORUM: paginated(TOPIC, FORUM, 1), TOPIC: body})


# --- format filtering --------------------------------------------------------------


def test_default_collects_every_carrier() -> None:
    found = harvest_topic(fetcher_for(), TOPIC)  # type: ignore[arg-type]
    assert {a.chunk_format for a in found} == {"mbd", "mxd", "text"}


def test_only_legacy_keeps_mxd_and_drops_the_rest() -> None:
    found = harvest_topic(fetcher_for(), TOPIC, ["mxd"])  # type: ignore[arg-type]
    assert found
    assert {a.chunk_format for a in found} == {"mxd"}


def test_only_current_keeps_mbd() -> None:
    found = harvest_topic(fetcher_for(), TOPIC, ["mbd"])  # type: ignore[arg-type]
    assert found
    assert {a.chunk_format for a in found} == {"mbd"}


def test_text_selects_the_readable_export_alone() -> None:
    found = harvest_topic(fetcher_for(), TOPIC, ["text"])  # type: ignore[arg-type]
    assert [a.kind for a in found] == ["forum-export"]


def test_both_build_formats_excludes_the_readable_export() -> None:
    found = harvest_topic(fetcher_for(), TOPIC, ["mbd", "mxd"])  # type: ignore[arg-type]
    assert found and all(a.kind != "forum-export" for a in found)


def test_an_attachment_is_filtered_before_it_is_downloaded() -> None:
    """Narrowing the formats must save requests, not just disk."""

    class CountingFetcher(StubFetcher):
        def __init__(self, pages):
            super().__init__(pages)
            self.downloads: list[str] = []

        def fetch(self, url: str):
            self.downloads.append(url)
            return (b'{"BuiltWith": {}, "Level": "49", "Class": "X", "PowerEntries": []}', "x.mbd")

    page = FIXTURE.read_text(encoding="utf-8")  # its only attachment is a .mbd
    counting = CountingFetcher({FORUM: paginated(TOPIC, FORUM, 1), TOPIC: page})
    harvest_topic(counting, TOPIC, ["mxd"])  # type: ignore[arg-type]
    assert counting.downloads == []  # never asked for the .mbd attachment

    counting = CountingFetcher({FORUM: paginated(TOPIC, FORUM, 1), TOPIC: page})
    harvest_topic(counting, TOPIC, ["mbd"])  # type: ignore[arg-type]
    assert len(counting.downloads) == 1


# --- the ledger --------------------------------------------------------------------


def test_ledger_is_empty_before_any_run(tmp_path: Path) -> None:
    assert load_topic_ledger(tmp_path) == {}


def test_ledger_records_what_was_asked_for_and_what_turned_up(tmp_path: Path) -> None:
    artifacts = [
        Artifact(
            kind="share-block",
            chunk_format="mxd",
            author="A",
            author_profile="",
            post_url="",
            posted_at="",
            topic_title="",
            topic_url=TOPIC,
            fetched_at="",
        )
    ]
    record_topic(tmp_path, TOPIC, ["mbd", "mxd"], artifacts)
    row = json.loads((tmp_path / "topics.jsonl").read_text(encoding="utf-8").strip())
    assert row["formats_requested"] == ["mbd", "mxd"]
    assert row["formats_found"] == ["mxd"]  # asked for both, only the legacy one was there
    assert row["artifacts"] == 1
    assert load_topic_ledger(tmp_path)[TOPIC].formats == {"mbd", "mxd"}


def test_ledger_unions_across_runs(tmp_path: Path) -> None:
    record_topic(tmp_path, TOPIC, ["mbd"], [])
    record_topic(tmp_path, TOPIC, ["mxd"], [])
    assert load_topic_ledger(tmp_path)[TOPIC].formats == {"mbd", "mxd"}


def test_ledger_tolerates_blank_lines(tmp_path: Path) -> None:
    """An interrupted append can leave a stray newline; that is not a reason to fail."""
    record_topic(tmp_path, TOPIC, ["mbd"], [])
    path = tmp_path / "topics.jsonl"
    path.write_text(path.read_text(encoding="utf-8") + "\n\n", encoding="utf-8")
    assert load_topic_ledger(tmp_path)[TOPIC].formats == {"mbd"}


def test_ledger_tracks_requests_not_finds(tmp_path: Path) -> None:
    """A thread crawled for mbd that held none must not look like one never crawled.

    Recording only what was *found* would make those two indistinguishable, and every
    repeat run would re-fetch every empty thread forever.
    """
    record_topic(tmp_path, TOPIC, ["mbd"], [])
    assert load_topic_ledger(tmp_path)[TOPIC].formats == {"mbd"}


# --- section size: how far along a run is ------------------------------------------


def test_a_section_row_records_the_size_the_listing_reported(tmp_path: Path) -> None:
    record_section(tmp_path, FORUM, 26, 640)
    row = json.loads((tmp_path / "sections.jsonl").read_text(encoding="utf-8").strip())
    assert (row["forum_url"], row["pages"], row["topics"]) == (FORUM, 26, 640)


def test_a_section_row_stays_out_of_the_thread_ledger(tmp_path: Path) -> None:
    """A per-thread file whose line count is not a thread count is a miscount waiting."""
    record_section(tmp_path, FORUM, 26, 640)
    record_topic(tmp_path, TOPIC, ["mbd"], [])
    assert len((tmp_path / "topics.jsonl").read_text(encoding="utf-8").splitlines()) == 1
    assert set(load_topic_ledger(tmp_path)) == {TOPIC}


def test_crawling_records_the_section_size_before_working_through_it(tmp_path: Path) -> None:
    """Written off the listing walk, so progress is answerable without asking the site."""
    fetcher = fetcher_for()
    fetcher.pages[FORUM] = paginated(TOPIC, FORUM, 3)
    fetcher.pages[f"{FORUM}page/2/"] = paginated(OTHER, FORUM, 3)
    fetcher.pages[f"{FORUM}page/3/"] = paginated(OTHER, FORUM, 3)
    fetcher.pages[OTHER] = FIXTURE.read_text(encoding="utf-8")

    crawl_forum(fetcher, FORUM, 10, tmp_path, False, ["mbd"])  # type: ignore[arg-type]
    row = json.loads((tmp_path / "sections.jsonl").read_text(encoding="utf-8").strip())
    assert (row["forum_url"], row["pages"], row["topics"]) == (FORUM, 3, 2)


def test_an_empty_section_records_no_size(tmp_path: Path) -> None:
    """Nothing to be a fraction of, so the row would only be noise."""
    crawl_forum(StubFetcher({FORUM: "<html>no topics</html>"}), FORUM, 1, tmp_path, False)  # type: ignore[arg-type]
    assert not (tmp_path / "sections.jsonl").exists()


# --- skipping ----------------------------------------------------------------------


def test_a_thread_already_covered_is_not_fetched_again(tmp_path: Path) -> None:
    fetcher = fetcher_for()
    first = crawl_forum(fetcher, FORUM, 1, tmp_path, False, ["mbd"])  # type: ignore[arg-type]
    assert first.found and not first.skipped_reason

    ledger = load_topic_ledger(tmp_path)
    again = crawl_forum(fetcher_for(), FORUM, 1, tmp_path, False, ["mbd"], ledger)  # type: ignore[arg-type]
    assert again.found == 0
    assert again.skipped_reason == "1 topic(s) unchanged since last crawl"


def test_a_new_format_still_gets_crawled(tmp_path: Path) -> None:
    """The point of the ledger: --formats mxd after --formats mbd still has work to do."""
    crawl_forum(fetcher_for(), FORUM, 1, tmp_path, False, ["mbd"])  # type: ignore[arg-type]
    ledger = load_topic_ledger(tmp_path)

    legacy = crawl_forum(fetcher_for(), FORUM, 1, tmp_path, False, ["mxd"], ledger)  # type: ignore[arg-type]
    assert legacy.found > 0
    assert not legacy.skipped_reason
    assert load_topic_ledger(tmp_path)[TOPIC].formats == {"mbd", "mxd"}


def test_force_overrides_the_ledger(tmp_path: Path) -> None:
    crawl_forum(fetcher_for(), FORUM, 1, tmp_path, False, ["mbd"])  # type: ignore[arg-type]
    ledger = load_topic_ledger(tmp_path)
    forced = crawl_forum(fetcher_for(), FORUM, 1, tmp_path, True, ["mbd"], ledger)  # type: ignore[arg-type]
    assert forced.found > 0


# --- necro posts: a thread is never finished ---------------------------------------


def test_topic_rows_pairs_each_thread_with_its_last_post_time() -> None:
    """The listing already carries it, so freshness costs no extra request."""
    from scripts.forum_scrape.scrape import topic_rows
    from scripts.forum_scrape.tests.test_forums import listing_row

    listing = listing_row(TOPIC, "2026-09-07T17:15:18Z") + listing_row(OTHER, "2025-04-17T12:28:42Z")
    assert topic_rows(listing) == {TOPIC: "2026-09-07T17:15:18Z", OTHER: "2025-04-17T12:28:42Z"}


def test_topic_rows_reports_unknown_when_a_row_has_one_timestamp() -> None:
    from scripts.forum_scrape.scrape import topic_rows

    row = f"<li class='ipsDataItem'><a href='{TOPIC}'>t</a><time datetime='2019-01-01T00:00:00Z'>x</time></li>"
    assert topic_rows(row) == {TOPIC: ""}


def test_a_necroed_thread_is_crawled_again(tmp_path: Path) -> None:
    """Someone revives a 2019 build thread with a follow-up; the answer is new material.

    This is the whole reason the ledger stores freshness rather than a done flag.
    """
    old = fetcher_for()
    old.pages[FORUM] = paginated(TOPIC, FORUM, 1, last_activity="2024-01-01T00:00:00Z")
    crawl_forum(old, FORUM, 1, tmp_path, False, ["mbd"])  # type: ignore[arg-type]

    # Same thread, but someone has posted in it since.
    revived = fetcher_for()
    revived.pages[FORUM] = paginated(TOPIC, FORUM, 1, last_activity="2026-09-07T17:15:18Z")
    again = crawl_forum(revived, FORUM, 1, tmp_path, False, ["mbd"], load_topic_ledger(tmp_path))  # type: ignore[arg-type]

    assert again.found > 0
    assert not again.skipped_reason
    assert load_topic_ledger(tmp_path)[TOPIC].last_activity == "2026-09-07T17:15:18Z"


def test_an_untouched_thread_is_still_skipped(tmp_path: Path) -> None:
    stamp = "2024-01-01T00:00:00Z"
    first = fetcher_for()
    first.pages[FORUM] = paginated(TOPIC, FORUM, 1, last_activity=stamp)
    crawl_forum(first, FORUM, 1, tmp_path, False, ["mbd"])  # type: ignore[arg-type]

    second = fetcher_for()
    second.pages[FORUM] = paginated(TOPIC, FORUM, 1, last_activity=stamp)
    again = crawl_forum(second, FORUM, 1, tmp_path, False, ["mbd"], load_topic_ledger(tmp_path))  # type: ignore[arg-type]
    assert again.found == 0
    assert again.skipped_reason == "1 topic(s) unchanged since last crawl"


def test_an_unknown_timestamp_means_look_again(tmp_path: Path) -> None:
    """Ledger rows written before freshness existed must not read as 'unchanged'.

    Guessing would freeze those threads at whatever they held when first seen.
    """
    from scripts.forum_scrape.scrape import TopicCoverage, is_covered

    known = TopicCoverage(frozenset({"mbd"}), "2024-01-01T00:00:00Z")
    assert is_covered(known, ["mbd"], "2024-01-01T00:00:00Z")
    assert not is_covered(known, ["mbd"], "2026-01-01T00:00:00Z")  # newer post
    assert not is_covered(known, ["mbd", "mxd"], "2024-01-01T00:00:00Z")  # new format
    assert not is_covered(None, ["mbd"], "2024-01-01T00:00:00Z")  # never crawled

    legacy = TopicCoverage(frozenset({"mbd"}), "")  # pre-freshness ledger row
    assert not is_covered(legacy, ["mbd"], "2024-01-01T00:00:00Z")
    assert not is_covered(known, ["mbd"], "")  # listing gave no time


def test_ledger_keeps_the_newest_activity_seen(tmp_path: Path) -> None:
    record_topic(tmp_path, TOPIC, ["mbd"], [], "2024-01-01T00:00:00Z")
    record_topic(tmp_path, TOPIC, ["mbd"], [], "2026-09-07T17:15:18Z")
    assert load_topic_ledger(tmp_path)[TOPIC].last_activity == "2026-09-07T17:15:18Z"


# --- the flag ----------------------------------------------------------------------


def test_cli_rejects_an_unknown_format(tmp_path: Path) -> None:
    with pytest.raises(SystemExit):
        main(["--topic", TOPIC, "--out", str(tmp_path), "--formats", "mdb"])  # a typo for mbd


def test_cli_narrows_collection_and_records_the_request(fake_net, tmp_path: Path, topic_page: str) -> None:
    routes, _ = fake_net
    routes[TOPIC] = topic_page.encode()

    assert main(["--topic", TOPIC, "--out", str(tmp_path), "--delay", "0", "--formats", "mxd"]) == 0
    rows = [json.loads(line) for line in (tmp_path / "index.jsonl").read_text(encoding="utf-8").splitlines()]
    assert {r["chunk_format"] for r in rows} == {"mxd"}
    assert load_topic_ledger(tmp_path)[TOPIC].formats == {"mxd"}


def test_all_formats_is_the_documented_default() -> None:
    assert ALL_FORMATS == ("mbd", "mxd", "text")


def test_topic_rows_ignores_rows_that_are_not_topics() -> None:
    """A board index lists forums in the same row markup; those are not threads."""
    from scripts.forum_scrape.scrape import topic_rows
    from scripts.forum_scrape.tests.test_forums import listing_row

    forum_row = "<li class='ipsDataItem'><a href='https://forums.homecomingservers.com/forum/19-x/'>f</a></li>"
    assert topic_rows(forum_row + listing_row(TOPIC)) == {TOPIC: "2026-01-01T00:00:00Z"}
