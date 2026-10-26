"""The Release Notes application reader: categories, listings, entries, and the dated records it writes."""

import json
import urllib.error
from pathlib import Path

from scripts.forum_scrape.patch_notes import (
    PatchEntry,
    list_categories,
    list_entries,
    main,
    parse_entry,
    write_entry,
)

FIXTURE = Path(__file__).parent / "fixtures" / "patch_entry_sample.html"
BASE = "https://forums.homecomingservers.com"
ENTRY_URL = f"{BASE}/patch-notes/issue-28/page-4/october-20-update-r48/"

CATEGORIES = f"""
<a href="{BASE}/patch-notes/">Patch Notes</a>
<a href="{BASE}/patch-notes/issue-28/">Issue 28</a>
<a href="{BASE}/patch-notes/issue-28/page-4/">Page 4</a>
<a href="{BASE}/patch-notes/issue-28/page-3/">Page 3</a>
<a href="{BASE}/patch-notes/issue-27/">Issue 27</a>
<a href="{BASE}/patch-notes/issue-27/page-7/">Page 7</a>
<a href="{BASE}/patch-notes/issue-28/page-4/">Page 4 again</a>
"""
LISTING = f"""
<a href='{ENTRY_URL}'>October 20 Update</a>
<time datetime='2026-10-20T14:05:01Z'>Tuesday</time>
<a href='{BASE}/patch-notes/issue-28/page-4/issue-28-page-4-r47/'>Issue 28, Page 4</a>
<a href='{ENTRY_URL}'>Read more</a>
"""


def test_list_categories_returns_page_categories_once_in_order() -> None:
    assert list_categories(CATEGORIES) == [
        f"{BASE}/patch-notes/issue-28/page-4/",
        f"{BASE}/patch-notes/issue-28/page-3/",
        f"{BASE}/patch-notes/issue-27/page-7/",
    ]


def test_list_entries_returns_each_entry_once() -> None:
    assert list_entries(LISTING) == [ENTRY_URL, f"{BASE}/patch-notes/issue-28/page-4/issue-28-page-4-r47/"]


def test_parse_entry_reads_date_hierarchy_and_change_lines() -> None:
    entry = parse_entry(FIXTURE.read_text(encoding="utf-8"), ENTRY_URL)
    assert entry.title == "October 20 Update"
    assert entry.published == "2026-10-20T14:05:01Z"
    assert (entry.issue, entry.page, entry.revision) == ("issue-28", "page-4", 48)
    clarity = next(c for c in entry.changes if c.power == "Clarity")
    assert (clarity.category, clarity.subsection) == ("Powers", "Adjustments")
    assert (clarity.archetype, clarity.powerset) == ("All", "Sonic Resonance")
    assert clarity.text.startswith("Increased splash mez protection from scale 5 to 15")
    # A line with no "AT > Set > Power -" prefix is still a change, under its headings.
    avoid = next(c for c in entry.changes if c.text.startswith("Removed Avoid from"))
    assert (avoid.category, avoid.power) == ("Powers", "")
    # Headings never become changes; every change carries a category.
    assert all(c.category for c in entry.changes)
    assert len(entry.changes) >= 40


def _entry(body: str) -> str:
    return f'<html><head><title>x - Page 4 - Homecoming</title></head><body><section class="ipsType_richText">{body}</section></body></html>'


def test_parse_entry_files_items_before_any_heading_under_general() -> None:
    page = _entry(
        "<ul><li>orphan</li></ul><p>prose, not a heading</p>"
        '<p><span style="font-size:18px;"><strong>System</strong></span></p><ul><li>Fixed a thing.</li></ul>'
    )
    entry = parse_entry(page, ENTRY_URL)
    assert [(c.category, c.text) for c in entry.changes] == [("General", "orphan"), ("System", "Fixed a thing.")]


def test_parse_entry_reads_the_older_layouts() -> None:
    """16px-only headings, items wrapped in <p> with nested lists, and plain-text colon paths."""
    page = _entry(
        '<p><span style="font-size:16px;"><strong>Winter Event</strong></span></p>'
        "<ul><li><p>Ambushes spawn at player level.</p>"
        "<ul><li><p><em><strong>Elemental Guardian:</strong> 20% defense to Fire.</em></p></li></ul></li></ul>"
        '<p><span style="font-size:18px;"><strong>Powers</strong></span></p>'
        "<ul><li>Arsenal Control &gt; Auto Turret: No longer has teleport protection</li>"
        "<li>Epic &gt; Arsenal Mastery &gt; LRM: Range reduced from 150ft to 80ft</li>"
        "<li><strong>Energy Mastery &gt; Energy Torrent: </strong>Fixed hit FX.</li><li></li></ul>"
    )
    changes = parse_entry(page, ENTRY_URL).changes
    assert len(changes) == 5  # the empty item is not a change
    assert [(c.category, c.subsection) for c in changes[:2]] == [("Winter Event", ""), ("Winter Event", "")]
    assert changes[1].text == "Elemental Guardian: 20% defense to Fire."
    assert (changes[2].archetype, changes[2].powerset, changes[2].power) == ("", "Arsenal Control", "Auto Turret")
    assert changes[2].text == "No longer has teleport protection"
    assert (changes[3].archetype, changes[3].powerset, changes[3].power) == ("Epic", "Arsenal Mastery", "LRM")
    assert (changes[4].powerset, changes[4].power, changes[4].text) == (
        "Energy Mastery",
        "Energy Torrent",
        "Fixed hit FX.",
    )


def test_parse_entry_without_a_body_yields_no_changes() -> None:
    entry = parse_entry("<html><head><title>x - Page 4 - Homecoming</title></head><body></body></html>", ENTRY_URL)
    assert entry.changes == [] and entry.published == "" and entry.title == "x"


def test_write_entry_names_the_file_by_date_and_slug(tmp_path: Path) -> None:
    entry = parse_entry(FIXTURE.read_text(encoding="utf-8"), ENTRY_URL)
    path = write_entry(entry, tmp_path)
    assert path.name == "2026-10-20-october-20-update-r48.json"
    document = json.loads(path.read_text(encoding="utf-8"))
    assert document["url"] == ENTRY_URL
    assert document["changes"][0]["category"]
    # The index row is appended once per written entry.
    rows = [json.loads(line) for line in (tmp_path / "index.jsonl").read_text(encoding="utf-8").splitlines()]
    assert rows == [{"url": ENTRY_URL, "published": "2026-10-20T14:05:01Z", "revision": 48, "file": path.name}]


def test_write_entry_without_a_date_falls_back_to_the_revision(tmp_path: Path) -> None:
    entry = PatchEntry(title="t", url=ENTRY_URL, published="", issue="issue-28", page="page-4", revision=48, changes=[])
    assert write_entry(entry, tmp_path).name == "undated-october-20-update-r48.json"


def test_main_walks_categories_and_skips_entries_already_written(tmp_path: Path, fake_net, capsys) -> None:
    routes, calls = fake_net
    routes[f"{BASE}/patch-notes/?show=categories"] = CATEGORIES.encode()
    routes[f"{BASE}/patch-notes/issue-28/page-4/"] = LISTING.encode()
    routes[f"{BASE}/patch-notes/issue-28/page-3/"] = b"<html></html>"
    routes[f"{BASE}/patch-notes/issue-27/page-7/"] = b"<html></html>"
    routes[ENTRY_URL] = FIXTURE.read_bytes()
    routes[f"{BASE}/patch-notes/issue-28/page-4/issue-28-page-4-r47/"] = FIXTURE.read_bytes()
    routes[f"{BASE}/patch-notes/issue-28/page-3/"] = (
        f"<a href='{BASE}/patch-notes/issue-28/page-3/gone-r1/'>x</a>".encode()
    )

    assert main(["--out", str(tmp_path), "--delay", "0"]) == 0
    assert "2 entries written" in capsys.readouterr().out
    assert sorted(p.name for p in tmp_path.glob("*.json")) == [
        "2026-10-20-issue-28-page-4-r47.json",
        "2026-10-20-october-20-update-r48.json",
    ]
    fetched = calls.count(ENTRY_URL)

    assert main(["--out", str(tmp_path), "--delay", "0"]) == 0
    assert "0 entries written" in capsys.readouterr().out
    assert calls.count(ENTRY_URL) == fetched  # the second run did not refetch a written revision


def test_main_reports_an_unreachable_browser(tmp_path: Path, fake_net, capsys) -> None:
    routes, _ = fake_net
    routes[f"{BASE}/patch-notes/?show=categories"] = urllib.error.URLError("boom")
    assert main(["--out", str(tmp_path), "--delay", "0"]) == 1
    assert "E60" in capsys.readouterr().err
