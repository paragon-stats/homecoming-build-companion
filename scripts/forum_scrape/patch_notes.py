"""Read Homecoming's patch notes, from three sources, into dated change records.

Each entry becomes one JSON file under ``data/patches/`` so a captured guide's claims can
be dated against the changes that followed them. The sources, by the span they cover:

- ``release-notes``: Invision's records application at ``/patch-notes/``, browsed as
    Issue > Page categories, one entry per update with a publish date. Issue 27 page 6
    (April 2023) onward. Live patch notes are not forum threads.
- ``forum-beta``: the beta and staging patch-note sections of the forum, 2019 to 2023. Each
    release candidate re-posts a page's full notes, so one series keeps only its last
    thread across both sections, and a run of numbered candidates without an Issue/Page
    label keeps only its last; other interim patches are kept one per thread. Threads
    dated inside the live application's coverage are skipped: the live entry wins.
- ``paragonwiki``: the Paragon Wiki Archive's dated ``Patch_Notes/YYYY-MM-DD`` pages,
    2004 to 2012.

Usage::

    python -m scripts.forum_scrape.patch_notes --out data/patches
    python -m scripts.forum_scrape.patch_notes --source paragonwiki --out data/patches

Re-runs fetch only entries whose URL is not yet in the output index.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from dataclasses import asdict, dataclass, field
from pathlib import Path

from scripts.forum_scrape.guides import powersets_from_canonical
from scripts.forum_scrape.scrape import (
    BASE,
    DEFAULT_UA,
    Fetcher,
    iter_posts,
    page_title,
    strip_html,
    walk_listing,
)

SOURCES = ("release-notes", "forum-beta", "paragonwiki")
CATEGORIES_URL = f"{BASE}/patch-notes/?show=categories"
BETA_SECTIONS = (f"{BASE}/forum/73-open-beta-patch-notes/", f"{BASE}/forum/100-staging-patch-notes/")
WIKI_BASE = "https://archive.paragonwiki.com"
WIKI_INDEX_URL = f"{WIKI_BASE}/wiki/Patch_Notes"

_PAGE_CATEGORY = re.compile(rf"""['\"]({re.escape(BASE)}/patch-notes/issue-\d+/page-\d+/)['\"]""")
_ENTRY = re.compile(rf"""['\"]({re.escape(BASE)}/patch-notes/(issue-\d+)/(page-\d+)/([a-z0-9-]+)-r(\d+)/)['\"]""")
_PUBLISHED = re.compile(r"""<time\s[^>]*datetime=['\"]([^'\"]+)['\"]""", re.I)
_BODY = re.compile(
    r"""<section\s[^>]*class=['\"][^'\"]*ipsType_richText[^'\"]*['\"][^>]*>(.*?)</section>""", re.I | re.S
)
# A thread slug names its series: "...-issue-27-second-chances-page-3-release-candidate-1".
_SERIES = re.compile(r"issue-(\d+).*?page-(\d+)")
_CANDIDATE = re.compile(r"release-candidate-(\d+)$")
_WIKI_PAGE = re.compile(r"""href=['\"](/wiki/Patch_Notes/(\d{4}-\d{2}-\d{2})[^'\"#?]*)['\"]""")
_WIKI_BODY = re.compile(r"""class=['\"][^'\"]*mw-parser-output[^'\"]*['\"][^>]*>(.*)""", re.S)
_WIKI_TOKEN = re.compile(r"<h([1-6])\b[^>]*>(.*?)</h\1>|<li\b[^>]*>", re.I | re.S)
_WIKI_HEADLINE = re.compile(r"""<span\s[^>]*class=['\"]mw-headline['\"][^>]*>(.*?)</span>""", re.I | re.S)
_WIKI_ISSUE = re.compile(r"issue\s*(\d+)", re.I)
_WIKI_FOOTER = "printfooter"

# The forum-side body is paragraphs (headings, when they hold only a bold label) and list
# items (the changes). Heading level is the label's font size: 18px and up is a category, 16px
# a subsection, or inside a Powers category a powerset when it names a canonical set (update
# banners such as "Travel Power Stacking" sit at the same size); an unsized bold label under a
# powerset names the power. A list item's text runs to the next item or the end of its list,
# so nested items become their own lines.
_TOKEN = re.compile(r"<p\b[^>]*>(.*?)</p>|<li\b[^>]*>", re.I | re.S)
_ITEM_END = re.compile(r"<li\b|</[uo]l>", re.I)
_FONT_SIZE = re.compile(r"font-size:\s*(\d+)px", re.I)
_BOLD = re.compile(r"<(?:strong|b)\b[^>]*>(.*?)</(?:strong|b)>", re.I | re.S)
# "All > Sonic Resonance > Clarity - text", "Arsenal Control > Auto Turret: text",
# "Epic > Arsenal Mastery > LRM: text": one to three ">" separators, then "-" or ":".
_POWER_PATH = re.compile(r"^([^:>\n]{1,80}(?:\s*>\s*[^:>\n]{1,80}){1,3}?)\s*[-:]\s+(.+)$", re.S)
_CATEGORY_FONT_PX = 18
# Zero-width marks pasted into forum posts; a JSON linter rejects them and they carry nothing.
_ZERO_WIDTH = re.compile("[\u200b\u200c\u200d\u2060\ufeff]")
# MediaWiki's table of contents is a heading named Contents over a list of section numbers.
_WIKI_TOC = "Contents"


@dataclass(frozen=True, slots=True)
class Change:
    """One change line, with the headings it sat under and the power path if it named one."""

    category: str
    subsection: str
    archetype: str
    powerset: str
    power: str
    text: str


@dataclass(slots=True)
class PatchEntry:
    """One patch-notes entry: where it sits in the Issue > Page tree, when, from which source, and what changed."""

    title: str
    url: str
    published: str
    issue: str
    page: str
    revision: int
    source: str = "release-notes"
    changes: list[Change] = field(default_factory=list)


def list_categories(categories_html: str) -> list[str]:
    """Every Issue > Page category URL linked from the category browser, once each, in page order."""
    return list(dict.fromkeys(_PAGE_CATEGORY.findall(categories_html)))


def list_entries(listing_html: str) -> list[str]:
    """Every entry URL on a page-category listing, once each, in listing order."""
    return list(dict.fromkeys(match.group(1) for match in _ENTRY.finditer(listing_html)))


def _clean(fragment: str) -> str:
    """Plain text with zero-width marks dropped and every run of Unicode whitespace folded to one space."""
    return " ".join(_ZERO_WIDTH.sub("", strip_html(fragment)).split())


def _heading(inner: str) -> str:
    """The bold label a paragraph holds when it holds nothing else, or "" for prose."""
    text = _clean(inner)
    bold = _BOLD.search(inner)
    if not text or bold is None or _clean(bold.group(1)) != text:
        return ""
    return text


def _change(text: str, category: str, subsection: str, powerset: str = "", power: str = "") -> Change:
    path = _POWER_PATH.match(text)
    if not path:
        return Change(category, subsection, "", powerset, power, text)
    parts = [" ".join(p.split()) for p in path.group(1).split(">")]
    return Change(category, subsection, " > ".join(parts[:-2]), parts[-2], parts[-1], " ".join(path.group(2).split()))


def parse_changes(html: str, powersets: frozenset[str] = frozenset()) -> list[Change]:
    """The change lines of a forum-side body (an application entry or a beta thread's opener).

    ``powersets`` are the canonical set names in lower case; a heading is read as a powerset
    only when it is one of them, so with none every heading is a category or subsection.
    """
    changes: list[Change] = []
    category = subsection = powerset = power = ""
    for token in _TOKEN.finditer(html):
        if token.group(1) is not None:
            label = _heading(token.group(1))
            if not label:
                continue
            size = _FONT_SIZE.search(token.group(1))
            px = int(size.group(1)) if size else 0
            if px >= _CATEGORY_FONT_PX or not category:
                category, subsection, powerset, power = label, "", "", ""
            elif px == 0 and powerset:
                power = label.rstrip(",:")
            elif "power" in category.lower() and label.rstrip(",:").lower() in powersets:
                powerset, power = label.rstrip(",:"), ""
            else:
                subsection, powerset, power = label, "", ""
            continue
        end = _ITEM_END.search(html, token.end())
        text = _clean(html[token.end() : end.start() if end else len(html)])
        if text:
            changes.append(_change(text, category or "General", subsection, powerset, power))
    return changes


def parse_entry(page: str, url: str, powersets: frozenset[str] = frozenset()) -> PatchEntry:
    """Parse one Release Notes application entry page into its dated change records."""
    match = _ENTRY.match(f'"{url}"')
    issue, page_name, revision = (match.group(2), match.group(3), int(match.group(5))) if match else ("", "", 0)
    published = _PUBLISHED.search(page)
    entry = PatchEntry(page_title(page), url, published.group(1) if published else "", issue, page_name, revision)
    body = _BODY.search(page)
    if body is not None:
        entry.changes = parse_changes(body.group(1), powersets)
    return entry


def series_of(topic_url: str) -> tuple[str, str]:
    """``("issue-27", "page-3")`` from a beta thread's slug, or ``("", "")`` for an interim patch."""
    match = _SERIES.search(topic_url.rstrip("/").rsplit("/", 1)[-1])
    return (f"issue-{match.group(1)}", f"page-{match.group(2)}") if match else ("", "")


def beta_candidates(rows: dict[str, str]) -> list[str]:
    """The threads worth reading from a beta listing.

    The last-posted thread of each labelled series, the last of each run of unlabelled numbered
    release candidates (a number that does not rise on the next candidate starts a new run),
    and every other interim patch. Order is the topic id, which rises as threads are posted;
    the listing's last-activity time moves with every reply, so a late reply on a draft would
    otherwise outrank the final.
    """
    latest: dict[tuple[str, str], tuple[int, str]] = {}
    numbered: list[tuple[int, int, str]] = []  # (topic id, candidate number, url)
    interim: list[str] = []
    for topic_url in rows:
        slug = topic_url.rstrip("/").rsplit("/", 1)[-1]
        head = slug.split("-", 1)[0]
        topic_id = int(head) if head.isdigit() else 0
        series = series_of(topic_url)
        if series != ("", ""):
            if series not in latest or topic_id > latest[series][0]:
                latest[series] = (topic_id, topic_url)
        elif candidate := _CANDIDATE.search(slug):
            numbered.append((topic_id, int(candidate.group(1)), topic_url))
        else:
            interim.append(topic_url)
    finals: list[str] = []
    last: tuple[int, str] | None = None
    for _, number, topic_url in sorted(numbered):
        if last is not None and number <= last[0]:
            finals.append(last[1])
        last = (number, topic_url)
    if last is not None:
        finals.append(last[1])
    return [url for _, url in latest.values()] + finals + interim


def parse_beta_thread(page: str, url: str, powersets: frozenset[str] = frozenset()) -> PatchEntry:
    """Parse a beta patch-notes thread's opening post into its dated change records."""
    issue, page_name = series_of(url)
    opener = next(iter(iter_posts(page)), None)
    entry = PatchEntry(page_title(page), url, opener.posted_at if opener else "", issue, page_name, 0, "forum-beta")
    if opener is not None:
        entry.changes = parse_changes(opener.html, powersets)
    return entry


def wiki_pages(index_html: str) -> list[str]:
    """Every dated ``Patch_Notes/YYYY-MM-DD`` page the archive's index links, once each, oldest first."""
    found = {match.group(1): match.group(2) for match in _WIKI_PAGE.finditer(index_html)}
    return [f"{WIKI_BASE}{path}" for path, _ in sorted(found.items(), key=lambda item: (item[1], item[0]))]


def parse_wiki_page(page: str, url: str) -> PatchEntry:
    """Parse one archived wiki patch-notes page: h2 headings are categories, deeper ones subsections."""
    date = url.rstrip("/").rsplit("/", 1)[-1][:10]
    entry = PatchEntry(page_title_wiki(page), url, date, "", "", 0, "paragonwiki")
    body = _WIKI_BODY.search(page)
    if body is None:
        return entry
    html = body.group(1)
    footer = html.find(_WIKI_FOOTER)
    html = html[:footer] if footer >= 0 else html
    category = subsection = ""
    title_issue = _WIKI_ISSUE.search(entry.title)
    entry.issue = f"issue-{title_issue.group(1)}" if title_issue else ""
    for token in _WIKI_TOKEN.finditer(html):
        if token.group(1) is not None:
            headline = _WIKI_HEADLINE.search(token.group(2))
            label = _clean(headline.group(1) if headline else token.group(2))
            level = int(token.group(1))
            # The page names its Issue in its title or in whichever heading opens the notes.
            heading_issue = _WIKI_ISSUE.search(label)
            if heading_issue and not entry.issue:
                entry.issue = f"issue-{heading_issue.group(1)}"
            if level == 1:
                # The page heading follows the contents list; some pages then use h3 sections
                # with no h2, so the next heading of any level opens the first category.
                category = subsection = ""
                continue
            if level == 2 or not category:
                category, subsection = label, ""
            else:
                subsection = label
            continue
        if category == _WIKI_TOC:
            continue
        end = _ITEM_END.search(html, token.end())
        text = _clean(html[token.end() : end.start() if end else len(html)])
        if text:
            entry.changes.append(_change(text, category or "General", subsection))
    return entry


def page_title_wiki(page: str) -> str:
    """The wiki page's own title, without the site suffix."""
    match = re.search(r"<title>(.*?)</title>", page, re.I | re.S)
    return " ".join(match.group(1).split()).split(" - ")[0] if match else ""


def _slug(entry: PatchEntry) -> str:
    tail = entry.url.rstrip("/").rsplit("/", 1)[-1]
    if entry.source == "paragonwiki":
        return "paragonwiki-" + tail.lower().replace("_", "-")
    if entry.source == "forum-beta":
        return "beta-" + tail.split("-", 1)[-1]
    return tail


def write_entry(entry: PatchEntry, out_dir: Path) -> Path:
    """Write ``<date>-<slug>.json`` and append the entry to ``index.jsonl``; return the file."""
    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / f"{entry.published[:10] or 'undated'}-{_slug(entry)}.json"
    path.write_text(json.dumps(asdict(entry), indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    row = {
        "url": entry.url,
        "published": entry.published,
        "revision": entry.revision,
        "source": entry.source,
        "issue": entry.issue,
        "page": entry.page,
        "file": path.name,
    }
    with (out_dir / "index.jsonl").open("a", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(row, ensure_ascii=False) + "\n")
    return path


def write_repeat(entry: PatchEntry, out_dir: Path, repeat_of: str) -> None:
    """Index an entry whose every line an earlier entry already recorded; no file is written."""
    row = {
        "url": entry.url,
        "published": entry.published,
        "revision": entry.revision,
        "source": entry.source,
        "issue": entry.issue,
        "page": entry.page,
        "file": "",
        "repeat_of": repeat_of,
    }
    with (out_dir / "index.jsonl").open("a", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(row, ensure_ascii=False) + "\n")


def _index(out_dir: Path) -> list[dict[str, object]]:
    index = out_dir / "index.jsonl"
    if not index.exists():
        return []
    return [json.loads(line) for line in index.read_text(encoding="utf-8").splitlines() if line.strip()]


LineKey = tuple[str, str, str, str, str, str]


def _line_key(change: Change) -> LineKey:
    return (change.category, change.subsection, change.archetype, change.powerset, change.power, change.text)


def dedupe_lines(entry: PatchEntry, seen: dict[LineKey, tuple[str, str]]) -> str:
    """Keep only the lines no earlier entry recorded: a change is dated by its first appearance.

    Staging reposts the beta notes days later, each beta thread restates the ones before it,
    and the wiki republished pages with corrections; recorded whole, every such line would
    carry two dates. ``seen`` maps each recorded line to ``(file, published)``. A line held
    only by a later-dated file stays: a thread found on a later run is older than what is
    on disk, and dropping its line would date the change by the repeat. Repeats inside one
    entry go too. Returns the file most dropped lines first appeared in, or "" for none.
    """
    kept: list[Change] = []
    origins: Counter[str] = Counter()
    local: set[LineKey] = set()
    for change in entry.changes:
        key = _line_key(change)
        if key in local:
            continue
        local.add(key)
        if key in seen and seen[key][1] <= entry.published:
            origins[seen[key][0]] += 1
            continue
        kept.append(change)
    entry.changes = kept
    return origins.most_common(1)[0][0] if origins else ""


def _seen_lines(out_dir: Path, rows: list[dict[str, object]], source: str) -> dict[LineKey, tuple[str, str]]:
    seen: dict[LineKey, tuple[str, str]] = {}
    for row in sorted(rows, key=lambda r: str(r.get("published", ""))):
        if row.get("source") != source or not row.get("file"):
            continue
        document = json.loads((out_dir / str(row["file"])).read_text(encoding="utf-8"))
        for change in document["changes"]:
            seen.setdefault(
                (*(change[k] for k in ("category", "subsection", "archetype", "powerset", "power", "text")),),
                (str(row["file"]), str(row.get("published", ""))),
            )
    return seen


def _write_dated(entries: list[PatchEntry], out_dir: Path, rows: list[dict[str, object]], written: set[str]) -> int:
    """Write entries oldest first, each stripped of lines an earlier one of its source already holds."""
    if not entries:
        return 0
    seen = _seen_lines(out_dir, rows, entries[0].source)
    count = 0
    for entry in sorted(entries, key=lambda e: (e.published, e.url)):
        if not entry.changes:
            print(f"E67: no change lines parsed from {entry.url}; check the post's layout", file=sys.stderr)
        origin = dedupe_lines(entry, seen)
        if entry.changes or not origin:  # a thread that parsed to nothing is recorded, not a repeat
            path = write_entry(entry, out_dir)
            for change in entry.changes:
                seen.setdefault(_line_key(change), (path.name, entry.published))
            print(f"wrote {path.name}")
            count += 1
        else:
            write_repeat(entry, out_dir, origin)
            print(f"repeat of {origin}: {entry.url}")
        written.add(entry.url)
    return count


def _collect_release_notes(
    fetcher: Fetcher, written: set[str], out: Path, powersets: frozenset[str] = frozenset()
) -> int:
    categories = fetcher.get(CATEGORIES_URL)
    if categories is None:
        print(f"E60: could not read the patch-notes category browser at {CATEGORIES_URL}", file=sys.stderr)
        return -1
    count = 0
    for category_url in list_categories(categories):
        listing = fetcher.get(category_url) or ""
        for entry_url in list_entries(listing):
            if entry_url in written:
                continue
            page = fetcher.get(entry_url)
            if page is None:
                continue
            entry = parse_entry(page, entry_url, powersets)
            if not entry.changes:
                print(f"E67: no change lines parsed from {entry_url}; check the post's layout", file=sys.stderr)
            print(f"wrote {write_entry(entry, out).name}")
            written.add(entry_url)
            count += 1
    return count


def _live_series(row: dict[str, object]) -> tuple[str, str]:
    if row.get("issue") or row.get("page"):
        return str(row.get("issue", "")), str(row.get("page", ""))
    match = _ENTRY.match(f'"{row["url"]}"')
    return (match.group(2), match.group(3)) if match else ("", "")


def _collect_forum_beta(
    fetcher: Fetcher,
    written: set[str],
    out: Path,
    rows: list[dict[str, object]],
    powersets: frozenset[str] = frozenset(),
) -> tuple[int, bool]:
    """Beta threads not covered by the live application: the live entry wins on the same series or date.

    Returns the entries written and whether every listing page was read: a missing page hides
    its threads, and a series final may have been chosen without its later candidates.
    """
    live = [row for row in rows if row.get("source", "release-notes") == "release-notes"]
    # Index rows written before the source and series columns existed carry the series in the URL.
    live_series = {_live_series(row) for row in live}
    live_from = min((str(row["published"]) for row in live), default="9999")
    # A series runs across both sections, so its last thread is chosen over their union.
    listing: dict[str, str] = {}
    gaps: list[str] = []
    for section in BETA_SECTIONS:
        topics, failed = walk_listing(fetcher, section)
        gaps += failed
        for topic_url, last_activity in topics.items():
            listing.setdefault(topic_url, last_activity)
    for url in gaps:
        print(f"E66: beta listing page unavailable: {url}; re-run to pick up its threads", file=sys.stderr)
    entries = []
    for topic_url in beta_candidates(listing):
        if topic_url in written:
            continue
        page = fetcher.get(topic_url)
        if page is None:
            continue
        entry = parse_beta_thread(page, topic_url, powersets)
        labelled = entry.issue != ""
        if (labelled and (entry.issue, entry.page) in live_series) or (not labelled and entry.published >= live_from):
            continue
        entries.append(entry)
    return _write_dated(entries, out, rows, written), not gaps


def _collect_wiki(fetcher: Fetcher, written: set[str], out: Path, rows: list[dict[str, object]]) -> int:
    index = fetcher.get(WIKI_INDEX_URL)
    if index is None:
        print(f"E64: could not read the wiki patch-notes index at {WIKI_INDEX_URL}", file=sys.stderr)
        return -1
    entries = []
    for page_url in wiki_pages(index):
        if page_url in written:
            continue
        page = fetcher.get(page_url)
        if page is not None:
            entries.append(parse_wiki_page(page, page_url))
    return _write_dated(entries, out, rows, written)


def main(argv: list[str] | None = None) -> int:
    """CLI entry point: collect every requested source and write the entries not yet on disk."""
    parser = argparse.ArgumentParser(
        prog="python -m scripts.forum_scrape.patch_notes",
        description="Collect Homecoming's patch notes as dated change records.",
    )
    parser.add_argument(
        "--out", type=Path, default=Path("data/patches"), help="output directory (default data/patches)"
    )
    parser.add_argument(
        "--source", action="append", choices=SOURCES, default=[], help="a source to collect (repeatable; default all)"
    )
    parser.add_argument("--delay", type=float, default=2.0, help="minimum seconds between requests (default 2)")
    parser.add_argument("--user-agent", default=DEFAULT_UA)
    parser.add_argument(
        "--canonical", type=Path, default=Path("data/canonical"), help="canonical data, for powerset names"
    )
    args = parser.parse_args(argv)
    sources = args.source or list(SOURCES)
    powersets = powersets_from_canonical(args.canonical)
    if not powersets:
        print(f"E68: no powerset names under {args.canonical}; headings will not fill powerset", file=sys.stderr)

    rows = _index(args.out)
    written = {str(row["url"]) for row in rows}
    total = 0
    failed = False
    forum = Fetcher(args.user_agent, args.delay)
    if "release-notes" in sources:
        count = _collect_release_notes(forum, written, args.out, powersets)
        failed |= count < 0
        total += max(count, 0)
        rows = _index(args.out)
    if "forum-beta" in sources:
        count, complete = _collect_forum_beta(forum, written, args.out, rows, powersets)
        failed |= not complete
        total += count
    if "paragonwiki" in sources:
        count = _collect_wiki(Fetcher(args.user_agent, args.delay, base=WIKI_BASE), written, args.out, rows)
        failed |= count < 0
        total += max(count, 0)
    print(f"{total} entries written to {args.out}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
