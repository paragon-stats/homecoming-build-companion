"""Patch notes from three sources: the Release Notes application, the beta forum threads, the wiki archive."""

import json
import urllib.error
from pathlib import Path

from scripts.forum_scrape.patch_notes import (
    WIKI_BASE,
    Change,
    PatchEntry,
    _write_dated,
    beta_candidates,
    dedupe_lines,
    list_categories,
    list_entries,
    main,
    parse_beta_thread,
    parse_changes,
    parse_entry,
    parse_wiki_page,
    series_of,
    wiki_pages,
    write_entry,
)

FIXTURE = Path(__file__).parent / "fixtures" / "patch_entry_sample.html"
BASE = "https://forums.homecomingservers.com"
ENTRY_URL = f"{BASE}/patch-notes/issue-28/page-4/october-20-update-r48/"
ROBOTS_OK = b"User-agent: *\nAllow: /\n"

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


def _h(label: str, px: int = 0) -> str:
    size = f' style="font-size:{px}px;"' if px else ""
    return f"<p><span{size}><strong>{label}</strong></span></p>"


# --- the Release Notes application ------------------------------------------------------


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
    assert (entry.issue, entry.page, entry.revision, entry.source) == ("issue-28", "page-4", 48, "release-notes")
    clarity = next(c for c in entry.changes if c.power == "Clarity")
    assert (clarity.category, clarity.subsection) == ("Powers", "Adjustments")
    assert (clarity.archetype, clarity.powerset) == ("All", "Sonic Resonance")
    assert clarity.text.startswith("Increased splash mez protection from scale 5 to 15")
    avoid = next(c for c in entry.changes if c.text.startswith("Removed Avoid from"))
    assert (avoid.category, avoid.power) == ("Powers", "")
    assert all(c.category for c in entry.changes)
    assert len(entry.changes) >= 40


def test_parse_changes_drops_zero_width_marks_and_folds_unicode_spaces() -> None:
    marks = f"This{chr(0xFEFF)} patch,{chr(0xA0)}fingers{chr(0x2009)}crossed, ships."  # BOM, NBSP, thin space
    changes = parse_changes(_h("Powers", 18) + f"<ul><li>{marks}</li></ul>")
    assert changes[0].text == "This patch, fingers crossed, ships."


def test_parse_changes_files_items_before_any_heading_under_general() -> None:
    changes = parse_changes(
        "<ul><li>orphan</li></ul><p>prose, not a heading</p>" + _h("System", 18) + "<ul><li>Fixed a thing.</li></ul>"
    )
    assert [(c.category, c.text) for c in changes] == [("General", "orphan"), ("System", "Fixed a thing.")]


def test_parse_changes_reads_the_older_layouts() -> None:
    """16px-only headings, items wrapped in <p> with nested lists, and plain-text colon paths."""
    changes = parse_changes(
        _h("Winter Event", 16) + "<ul><li><p>Ambushes spawn at player level.</p>"
        "<ul><li><p><em><strong>Elemental Guardian:</strong> 20% defense to Fire.</em></p></li></ul></li></ul>"
        + _h("Powers", 18)
        + "<ul><li>Arsenal Control &gt; Auto Turret: No longer has teleport protection</li>"
        "<li>Epic &gt; Arsenal Mastery &gt; LRM: Range reduced from 150ft to 80ft</li>"
        "<li><strong>Energy Mastery &gt; Energy Torrent: </strong>Fixed hit FX.</li><li></li></ul>"
    )
    assert len(changes) == 5
    assert [(c.category, c.subsection) for c in changes[:2]] == [("Winter Event", ""), ("Winter Event", "")]
    assert changes[1].text == "Elemental Guardian: 20% defense to Fire."
    assert (changes[2].archetype, changes[2].powerset, changes[2].power) == ("", "Arsenal Control", "Auto Turret")
    assert (changes[3].archetype, changes[3].powerset, changes[3].power) == ("Epic", "Arsenal Mastery", "LRM")
    assert (changes[4].powerset, changes[4].power, changes[4].text) == (
        "Energy Mastery",
        "Energy Torrent",
        "Fixed hit FX.",
    )


def test_parse_changes_reads_beta_thread_headings() -> None:
    """A beta opener: a 20px banner, 18px categories, 16px subsections or powersets, unsized power names."""
    changes = parse_changes(
        _h("RELEASE CANDIDATE 3 CHANGES:", 20)
        + _h("Powers", 18)
        + _h("Adjustments", 16)
        + _h("Light Affinity,", 16)
        + _h("Radiance")
        + "<ul><li>Recharge reduced to 8s.</li></ul>"
        + _h("Blinding Beam")
        + "<ul><li>Now a cone.</li></ul>"
        + _h("Fixes", 16)
        + "<ul><li>Fixed a typo.</li></ul>"
        + _h("Costumes", 18)
        + _h("New Costume Parts (62)", 16)
        + "<ul><li>Male &gt; Head &gt; Detail 1 &gt; Blackwing</li></ul>"
    )
    rows = [(c.category, c.subsection, c.powerset, c.power, c.text) for c in changes]
    assert rows[0] == ("Powers", "Adjustments", "Light Affinity", "Radiance", "Recharge reduced to 8s.")
    assert rows[1] == ("Powers", "Adjustments", "Light Affinity", "Blinding Beam", "Now a cone.")
    assert rows[2] == ("Powers", "Fixes", "", "", "Fixed a typo.")
    # Outside a Powers category a 16px label is a subsection, and a ">" path that is not a power stays text.
    assert rows[3][:2] == ("Costumes", "New Costume Parts (62)")


def test_parse_entry_without_a_body_yields_no_changes() -> None:
    entry = parse_entry("<html><head><title>x - Page 4 - Homecoming</title></head><body></body></html>", ENTRY_URL)
    assert entry.changes == [] and entry.published == "" and entry.title == "x"


def test_write_entry_names_the_file_by_date_and_slug(tmp_path: Path) -> None:
    entry = parse_entry(FIXTURE.read_text(encoding="utf-8"), ENTRY_URL)
    path = write_entry(entry, tmp_path)
    assert path.name == "2026-10-20-october-20-update-r48.json"
    document = json.loads(path.read_text(encoding="utf-8"))
    assert document["url"] == ENTRY_URL and document["source"] == "release-notes"
    rows = [json.loads(line) for line in (tmp_path / "index.jsonl").read_text(encoding="utf-8").splitlines()]
    assert rows == [
        {
            "url": ENTRY_URL,
            "published": "2026-10-20T14:05:01Z",
            "revision": 48,
            "source": "release-notes",
            "issue": "issue-28",
            "page": "page-4",
            "file": path.name,
        }
    ]


def test_write_entry_without_a_date_falls_back_to_the_revision(tmp_path: Path) -> None:
    entry = PatchEntry("t", ENTRY_URL, "", "issue-28", "page-4", 48)
    assert write_entry(entry, tmp_path).name == "undated-october-20-update-r48.json"


def test_main_release_notes_walks_categories_and_skips_written_entries(tmp_path: Path, fake_net, capsys) -> None:
    routes, calls = fake_net
    routes[f"{BASE}/patch-notes/?show=categories"] = CATEGORIES.encode()
    routes[f"{BASE}/patch-notes/issue-28/page-4/"] = LISTING.encode()
    routes[f"{BASE}/patch-notes/issue-28/page-3/"] = (
        f"<a href='{BASE}/patch-notes/issue-28/page-3/gone-r1/'>x</a>".encode()
    )
    routes[f"{BASE}/patch-notes/issue-27/page-7/"] = b"<html></html>"
    routes[ENTRY_URL] = FIXTURE.read_bytes()
    routes[f"{BASE}/patch-notes/issue-28/page-4/issue-28-page-4-r47/"] = FIXTURE.read_bytes()

    args = ["--out", str(tmp_path), "--delay", "0", "--source", "release-notes"]
    assert main(args) == 0
    assert "2 entries written" in capsys.readouterr().out
    assert sorted(p.name for p in tmp_path.glob("*.json")) == [
        "2026-10-20-issue-28-page-4-r47.json",
        "2026-10-20-october-20-update-r48.json",
    ]
    fetched = calls.count(ENTRY_URL)
    assert main(args) == 0
    assert "0 entries written" in capsys.readouterr().out
    assert calls.count(ENTRY_URL) == fetched


def test_main_reports_an_unreachable_browser(tmp_path: Path, fake_net, capsys) -> None:
    routes, _ = fake_net
    routes[f"{BASE}/patch-notes/?show=categories"] = urllib.error.URLError("boom")
    assert main(["--out", str(tmp_path), "--delay", "0", "--source", "release-notes"]) == 1
    assert "E60" in capsys.readouterr().err


# --- the beta forum threads -------------------------------------------------------------

BETA = f"{BASE}/forum/73-open-beta-patch-notes/"
STAGING = f"{BASE}/forum/100-staging-patch-notes/"
RC1 = f"{BASE}/topic/1-beta-patch-notes-for-november-5th-2021-issue-27-second-chances-page-3-build-1/"
RC2 = f"{BASE}/topic/2-beta-patch-notes-for-november-24th-2021-issue-27-second-chances-page-3-release-candidate-2/"
INTERIM_OLD = f"{BASE}/topic/3-patch-notes-for-august-11th-2019/"
INTERIM_NEW = f"{BASE}/topic/4-open-beta-patch-notes-for-march-3rd-2026/"
LIVE_SERIES = f"{BASE}/topic/5-open-beta-patch-notes-for-july-22nd-2024-issue-28-legacy-page-1-release-candidate-2/"


def _row(topic: str, when: str) -> str:
    return f"<li class='ipsDataItem'><a href='{topic}'>t</a><time datetime='{when}'></time><time datetime='{when}'></time></li>"


def _thread(title: str, when: str, body: str) -> bytes:
    return (
        f"<html><head><title>{title} - Open Beta Patch Notes - Homecoming</title></head><body>"
        f"<article id='elComment_1'><a href='{BASE}/profile/1-curator/' title=\"Go to The Curator's profile\"></a>"
        f"<time datetime='{when}'></time><div data-role='commentContent'>{body}</div></article></body></html>"
    ).encode()


def test_series_of_and_beta_candidates_keep_the_latest_of_a_series_and_every_interim_patch() -> None:
    assert series_of(RC2) == ("issue-27", "page-3") and series_of(INTERIM_OLD) == ("", "")
    rc0 = f"{BASE}/topic/0-staging-patch-notes-issue-27-page-3-build-0/"
    # Unlabelled numbered candidates: 1, 2, 3 are one run; the next 1, 2 are another; a lone 2 its own.
    run_a = [f"{BASE}/topic/{10 + n}-patch-notes-for-august-2{n}th-2019-release-candidate-{n}/" for n in (1, 2, 3)]
    run_b = [f"{BASE}/topic/{20 + n}-patch-notes-for-february-1{n}th-2020-release-candidate-{n}/" for n in (1, 2)]
    lone = f"{BASE}/topic/odd-patch-notes-release-candidate-2/"  # no topic id: sorts first
    rows = {
        RC1: "2021-11-05T00:00:00Z",
        RC2: "2021-11-24T00:00:00Z",
        INTERIM_OLD: "2019-08-11T00:00:00Z",
        rc0: "2021-11-01T00:00:00Z",
        **dict.fromkeys(run_a + run_b + [lone], "2020-01-01T00:00:00Z"),
    }
    assert beta_candidates(rows) == [RC2, lone, run_a[2], run_b[1], INTERIM_OLD]


def test_parse_beta_thread_reads_the_opener() -> None:
    page = _thread(
        "Beta notes", "2021-11-24T10:00:00Z", _h("Powers", 18) + "<ul><li>Dark Melee &gt; Smite: Buffed.</li></ul>"
    )
    entry = parse_beta_thread(page.decode(), RC2)
    assert (entry.source, entry.issue, entry.page, entry.published) == (
        "forum-beta",
        "issue-27",
        "page-3",
        "2021-11-24T10:00:00Z",
    )
    assert entry.changes[0].power == "Smite"
    assert parse_beta_thread("<html><head><title>e - s - Homecoming</title></head></html>", RC2).changes == []


def test_dedupe_lines_keeps_first_appearance_and_names_the_origin() -> None:
    first = Change("Powers", "", "", "Dark Melee", "Smite", "Buffed.")
    again = Change("Powers", "", "", "Dark Melee", "Smite", "Buffed.")
    new = Change("Powers", "", "", "Dark Melee", "Siphon Life", "Heals more.")
    entry = PatchEntry("t", "u", "2021-01-01", "", "", 0, "forum-beta", [first, again, new])
    assert dedupe_lines(entry, {}) == "" and entry.changes == [first, new]  # the in-entry repeat is gone
    later = PatchEntry("t", "u2", "2021-02-01", "", "", 0, "forum-beta", [again, new])
    assert dedupe_lines(later, {("Powers", "", "", "Dark Melee", "Smite", "Buffed."): "a.json"}) == "a.json"
    assert later.changes == [new]


def test_write_dated_records_an_entry_that_parsed_to_nothing(tmp_path: Path) -> None:
    empty = PatchEntry("t", "u", "2021-01-01", "", "", 0, "forum-beta", [])
    assert _write_dated([empty], tmp_path, [], set()) == 1
    (written,) = tmp_path.glob("2021-01-01-*.json")
    assert json.loads(written.read_text(encoding="utf-8"))["changes"] == []


REPOST = f"{BASE}/topic/8-staging-patch-notes-for-november-25th-2021/"


def test_main_forum_beta_writes_final_candidates_outside_live_coverage(tmp_path: Path, fake_net, capsys) -> None:
    routes, calls = fake_net
    body = _h("Powers", 18) + "<ul><li>Dark Melee &gt; Smite: Buffed.</li></ul>"
    body_rc2 = (
        _h("Powers", 18)
        + "<ul><li>Dark Melee &gt; Smite: Buffed.</li><li>Dark Melee &gt; Siphon Life: Heals more.</li>"
        "<li>Dark Melee &gt; Touch of Fear: Wider cone.</li></ul>"
    )
    gone = f"{BASE}/topic/6-patch-notes-for-june-8th-2019/"  # no route: the fetch fails
    # Three listing pages declared; page 3 has no route, so a failed listing page is skipped, not fatal.
    pagination = f"<ul class='ipsPagination' data-pages='3'><li><a href='{BETA}page/2/'>2</a></li></ul>"
    routes[BETA] = (
        pagination
        + _row(RC1, "2021-11-05T00:00:00Z")
        + _row(RC2, "2021-11-24T00:00:00Z")
        + _row(gone, "2019-06-08T00:00:00Z")
    ).encode()
    routes[f"{BETA}page/2/"] = (
        _row(INTERIM_OLD, "2019-08-11T00:00:00Z")
        + _row(INTERIM_NEW, "2026-03-03T00:00:00Z")
        + _row(LIVE_SERIES, "2024-07-22T00:00:00Z")
    ).encode()
    # The staging section reposts the final candidate's notes a day later, word for word.
    routes[STAGING] = _row(REPOST, "2021-11-25T00:00:00Z").encode()
    routes[REPOST] = _thread("Staging repost", "2021-11-25T10:00:00Z", body_rc2)
    routes[RC2] = _thread("RC2", "2021-11-24T10:00:00Z", body_rc2)
    routes[INTERIM_OLD] = _thread("Aug 2019", "2019-08-11T10:00:00Z", body)
    routes[INTERIM_NEW] = _thread("Mar 2026", "2026-03-03T10:00:00Z", body)
    routes[LIVE_SERIES] = _thread("i28p1", "2024-07-22T10:00:00Z", body)
    # The live index already covers issue-28 page-1 and starts in April 2023; its row predates the
    # source and series columns, as the first release-notes run wrote them.
    tmp_path.mkdir(exist_ok=True)
    old_row = {
        "url": f"{BASE}/patch-notes/issue-28/page-1/x-r22/",
        "published": "2023-04-25T00:00:00Z",
        "revision": 22,
        "file": "x",
    }
    (tmp_path / "index.jsonl").write_text(json.dumps(old_row) + "\n", encoding="utf-8")
    # A row in the current shape names its series directly.
    write_entry(
        PatchEntry(
            "live2", f"{BASE}/patch-notes/issue-27/page-7/y-r11/", "2024-02-20T00:00:00Z", "issue-27", "page-7", 11
        ),
        tmp_path,
    )

    assert main(["--out", str(tmp_path), "--delay", "0", "--source", "forum-beta"]) == 0
    assert "2 entries written" in capsys.readouterr().out
    names = sorted(p.name for p in tmp_path.glob("*.json"))
    rc2_file = "2021-11-24-beta-beta-patch-notes-for-november-24th-2021-issue-27-second-chances-page-3-release-candidate-2.json"
    assert rc2_file in names and "2019-08-11-beta-patch-notes-for-august-11th-2019.json" in names
    assert calls.count(RC1) == 0  # the earlier candidate of the series is never fetched
    # The 2019 patch recorded Smite first; the 2021 candidate keeps only its two new lines.
    rc2 = json.loads((tmp_path / rc2_file).read_text(encoding="utf-8"))
    assert [c["power"] for c in rc2["changes"]] == ["Siphon Life", "Touch of Fear"]
    rows = [json.loads(line) for line in (tmp_path / "index.jsonl").read_text(encoding="utf-8").splitlines()]
    assert {row.get("source", "release-notes") for row in rows} == {"release-notes", "forum-beta"}
    repost = next(row for row in rows if row["url"] == REPOST)
    assert repost["file"] == "" and repost["repeat_of"] == rc2_file  # the word-for-word repost has no file

    # A later run: the staging listing is unreachable, and one new interim thread repeats a recorded line.
    late = f"{BASE}/topic/9-patch-notes-for-january-4th-2020/"
    del routes[STAGING]
    routes[BETA] = (pagination + _row(RC2, "2021-11-24T00:00:00Z") + _row(late, "2020-01-04T00:00:00Z")).encode()
    routes[late] = _thread("Jan 2020", "2020-01-04T10:00:00Z", body)
    assert main(["--out", str(tmp_path), "--delay", "0", "--source", "forum-beta"]) == 0
    assert "0 entries written" in capsys.readouterr().out
    rows = [json.loads(line) for line in (tmp_path / "index.jsonl").read_text(encoding="utf-8").splitlines()]
    late_row = next(row for row in rows if row["url"] == late)
    assert late_row["file"] == "" and late_row["repeat_of"] == "2019-08-11-beta-patch-notes-for-august-11th-2019.json"

    fetched = calls.count(RC2)
    assert main(["--out", str(tmp_path), "--delay", "0", "--source", "forum-beta"]) == 0
    assert "0 entries written" in capsys.readouterr().out
    assert calls.count(RC2) == fetched  # written entries are not refetched


# --- the wiki archive --------------------------------------------------------------------

WIKI_INDEX = """
<a href="/wiki/Patch_Notes/2005-10-27">Oct 27 2005</a>
<a href="/wiki/Patch_Notes/2004-05-11">May 11 2004</a>
<a href="/wiki/Patch_Notes/2012-08-23_Beta">Beta</a>
<a href="/wiki/Patch_Notes/2005-10-27#Powers">anchor</a>
<a href="/wiki/Patch_Notes">index</a>
"""
WIKI_PAGE = """<html><head><title>Patch Notes/2005-10-27 - Paragon Wiki Archive</title></head><body>
<div class="mw-content-ltr mw-parser-output" lang="en">
<h1><span class="mw-headline" id="x">10/27/05 Patch Notes - Issue 6: Along Came a Spider</span></h1>
<ul><li>Early note before any section.</li></ul>
<div id="toc"><h2 id="mw-toc-heading">Contents</h2><ul><li>1.1 PvP Zones:</li><li>1.2 Powers:</li></ul></div>
<h2><span class="mw-headline" id="PvP_Zones">PvP Zones</span></h2>
<ul><li>Recluse's Victory opens.</li></ul>
<h2><span class="mw-headline" id="Powers">Powers</span></h2>
<h3><span class="mw-headline" id="Tankers">Tankers</span></h3>
<ul><li>Improved Damage DeBuff of Tanker/Ice Armor/Chilling Embrace.</li>
<li>Sonic Repulsion and Sonic Dispersion:<ul><li>Switched in pick order.</li></ul></li><li></li></ul>
<div class="printfooter">Retrieved from</div><ul><li>ignored footer item</li></ul></div></body></html>"""


def test_wiki_pages_lists_dated_pages_once_oldest_first() -> None:
    assert wiki_pages(WIKI_INDEX) == [
        f"{WIKI_BASE}/wiki/Patch_Notes/2004-05-11",
        f"{WIKI_BASE}/wiki/Patch_Notes/2005-10-27",
        f"{WIKI_BASE}/wiki/Patch_Notes/2012-08-23_Beta",
    ]


def test_parse_wiki_page_reads_headings_items_and_the_issue() -> None:
    entry = parse_wiki_page(WIKI_PAGE, f"{WIKI_BASE}/wiki/Patch_Notes/2005-10-27")
    assert (entry.source, entry.published, entry.issue, entry.title) == (
        "paragonwiki",
        "2005-10-27",
        "issue-6",
        "Patch Notes/2005-10-27",
    )
    rows = [(c.category, c.subsection, c.text) for c in entry.changes]
    assert rows[0] == ("General", "", "Early note before any section.")
    assert rows[1] == ("PvP Zones", "", "Recluse's Victory opens.")
    assert rows[2] == ("Powers", "Tankers", "Improved Damage DeBuff of Tanker/Ice Armor/Chilling Embrace.")
    assert rows[3][2] == "Sonic Repulsion and Sonic Dispersion:" and rows[4][2] == "Switched in pick order."
    assert all("footer" not in c.text for c in entry.changes)
    assert all(c.category != "Contents" for c in entry.changes)  # the wiki's table of contents is not a change
    assert (
        parse_wiki_page(
            "<html><head></head><body>no body</body></html>", f"{WIKI_BASE}/wiki/Patch_Notes/2004-05-11"
        ).changes
        == []
    )


def test_main_paragonwiki_writes_each_dated_page_and_reports_a_missing_index(tmp_path: Path, fake_net, capsys) -> None:
    routes, _ = fake_net
    routes[f"{WIKI_BASE}/robots.txt"] = ROBOTS_OK
    routes[f"{WIKI_BASE}/wiki/Patch_Notes"] = WIKI_INDEX.encode()
    routes[f"{WIKI_BASE}/wiki/Patch_Notes/2005-10-27"] = WIKI_PAGE.encode()
    # The earlier page carries the same notes but for one line, as a republished page does.
    earlier = WIKI_PAGE.replace("2005-10-27", "2004-05-11").replace("Recluse's Victory opens.", "Grandville opens.")
    routes[f"{WIKI_BASE}/wiki/Patch_Notes/2004-05-11"] = earlier.encode()
    assert main(["--out", str(tmp_path), "--delay", "0", "--source", "paragonwiki"]) == 0
    assert "2 entries written" in capsys.readouterr().out
    assert sorted(p.name for p in tmp_path.glob("*.json")) == [
        "2004-05-11-paragonwiki-2004-05-11.json",
        "2005-10-27-paragonwiki-2005-10-27.json",
    ]
    later = json.loads((tmp_path / "2005-10-27-paragonwiki-2005-10-27.json").read_text(encoding="utf-8"))
    assert [c["text"] for c in later["changes"]] == ["Recluse's Victory opens."]  # only the line the 2004 page lacked
    assert main(["--out", str(tmp_path), "--delay", "0", "--source", "paragonwiki"]) == 0
    assert (
        "0 entries written" in capsys.readouterr().out
    )  # written pages are skipped; the unrouted beta page fails quietly

    routes[f"{WIKI_BASE}/wiki/Patch_Notes"] = urllib.error.URLError("down")
    assert main(["--out", str(tmp_path), "--delay", "0", "--source", "paragonwiki"]) == 1
    assert "E64" in capsys.readouterr().err
