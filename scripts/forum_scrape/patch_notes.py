"""Read the Homecoming Release Notes application into dated change records.

Live patch notes are not forum threads. They live in Invision's records application at
``/patch-notes/``, browsed as Issue > Page categories, one entry per update with a publish
date and change lines grouped under headings such as ``Powers > Adjustments``. Each entry
becomes one JSON file under ``data/patches/`` so a captured guide's claims can be dated
against the changes that followed them.

Usage::

    python -m scripts.forum_scrape.patch_notes --out data/patches

Re-runs fetch only entries whose URL (which carries the revision number) is not yet in the
output index.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import asdict, dataclass, field
from pathlib import Path

from scripts.forum_scrape.scrape import BASE, DEFAULT_UA, Fetcher, page_title, strip_html

CATEGORIES_URL = f"{BASE}/patch-notes/?show=categories"
_PAGE_CATEGORY = re.compile(rf"""['\"]({re.escape(BASE)}/patch-notes/issue-\d+/page-\d+/)['\"]""")
_ENTRY = re.compile(rf"""['\"]({re.escape(BASE)}/patch-notes/(issue-\d+)/(page-\d+)/([a-z0-9-]+)-r(\d+)/)['\"]""")
_PUBLISHED = re.compile(r"""<time\s[^>]*datetime=['\"]([^'\"]+)['\"]""", re.I)
_BODY = re.compile(
    r"""<section\s[^>]*class=['\"][^'\"]*ipsType_richText[^'\"]*['\"][^>]*>(.*?)</section>""", re.I | re.S
)
# The body is paragraphs (headings, when they hold only a bold label) and list items (the
# changes). Heading level is the label's font size: 18px and up is a category, smaller a
# subsection under it; an entry that uses one size throughout gets that as its category.
# A list item's text runs to the next item or the end of its list, so nested items become
# their own change lines under the same headings.
_TOKEN = re.compile(r"<p\b[^>]*>(.*?)</p>|<li\b[^>]*>", re.I | re.S)
_ITEM_END = re.compile(r"<li\b|</[uo]l>", re.I)
_FONT_SIZE = re.compile(r"font-size:\s*(\d+)px", re.I)
_BOLD = re.compile(r"<(?:strong|b)\b[^>]*>(.*?)</(?:strong|b)>", re.I | re.S)
# "All > Sonic Resonance > Clarity - text", "Arsenal Control > Auto Turret: text",
# "Epic > Arsenal Mastery > LRM: text": one to three ">" separators, then "-" or ":".
_POWER_PATH = re.compile(r"^([^:>\n]{1,80}(?:\s*>\s*[^:>\n]{1,80}){1,3}?)\s*[-:]\s+(.+)$", re.S)
_CATEGORY_FONT_PX = 18


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
    """One Release Notes entry: where it sits in the Issue > Page tree, when, and what changed."""

    title: str
    url: str
    published: str
    issue: str
    page: str
    revision: int
    changes: list[Change] = field(default_factory=list)


def list_categories(categories_html: str) -> list[str]:
    """Every Issue > Page category URL linked from the category browser, once each, in page order."""
    return list(dict.fromkeys(_PAGE_CATEGORY.findall(categories_html)))


def list_entries(listing_html: str) -> list[str]:
    """Every entry URL on a page-category listing, once each, in listing order."""
    return list(dict.fromkeys(match.group(1) for match in _ENTRY.finditer(listing_html)))


def _heading(inner: str) -> str:
    """The bold label a paragraph holds when it holds nothing else, or "" for prose."""
    text = " ".join(strip_html(inner).split())
    bold = _BOLD.search(inner)
    if not text or bold is None or " ".join(strip_html(bold.group(1)).split()) != text:
        return ""
    return text


def _change(text: str, category: str, subsection: str) -> Change:
    path = _POWER_PATH.match(text)
    if not path:
        return Change(category, subsection, "", "", "", text)
    parts = [" ".join(p.split()) for p in path.group(1).split(">")]
    power, powerset = parts[-1], parts[-2]
    return Change(category, subsection, " > ".join(parts[:-2]), powerset, power, " ".join(path.group(2).split()))


def parse_entry(page: str, url: str) -> PatchEntry:
    """Parse one entry page into its dated change records."""
    match = _ENTRY.match(f'"{url}"')
    issue, page_name, revision = (match.group(2), match.group(3), int(match.group(5))) if match else ("", "", 0)
    published = _PUBLISHED.search(page)
    entry = PatchEntry(page_title(page), url, published.group(1) if published else "", issue, page_name, revision)
    body = _BODY.search(page)
    if body is None:
        return entry
    html = body.group(1)
    category = subsection = ""
    for token in _TOKEN.finditer(html):
        if token.group(1) is not None:
            label = _heading(token.group(1))
            if not label:
                continue
            size = _FONT_SIZE.search(token.group(1))
            if (size and int(size.group(1)) >= _CATEGORY_FONT_PX) or not category:
                category, subsection = label, ""
            else:
                subsection = label
            continue
        end = _ITEM_END.search(html, token.end())
        text = " ".join(strip_html(html[token.end() : end.start() if end else len(html)]).split())
        if text:
            entry.changes.append(_change(text, category or "General", subsection))
    return entry


def write_entry(entry: PatchEntry, out_dir: Path) -> Path:
    """Write ``<date>-<slug>-r<rev>.json`` and append the entry to ``index.jsonl``; return the file."""
    out_dir.mkdir(parents=True, exist_ok=True)
    slug = entry.url.rstrip("/").rsplit("/", 1)[-1]
    path = out_dir / f"{entry.published[:10] or 'undated'}-{slug}.json"
    path.write_text(json.dumps(asdict(entry), indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    row = {"url": entry.url, "published": entry.published, "revision": entry.revision, "file": path.name}
    with (out_dir / "index.jsonl").open("a", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(row, ensure_ascii=False) + "\n")
    return path


def _written(out_dir: Path) -> set[str]:
    index = out_dir / "index.jsonl"
    if not index.exists():
        return set()
    return {json.loads(line)["url"] for line in index.read_text(encoding="utf-8").splitlines() if line.strip()}


def main(argv: list[str] | None = None) -> int:
    """CLI entry point: walk every category and write the entries not yet on disk."""
    parser = argparse.ArgumentParser(
        prog="python -m scripts.forum_scrape.patch_notes",
        description="Collect Homecoming's live patch notes as dated change records.",
    )
    parser.add_argument(
        "--out", type=Path, default=Path("data/patches"), help="output directory (default data/patches)"
    )
    parser.add_argument("--delay", type=float, default=2.0, help="minimum seconds between requests (default 2)")
    parser.add_argument("--user-agent", default=DEFAULT_UA)
    args = parser.parse_args(argv)

    fetcher = Fetcher(args.user_agent, args.delay)
    categories = fetcher.get(CATEGORIES_URL)
    if categories is None:
        print(f"E60: could not read the patch-notes category browser at {CATEGORIES_URL}", file=sys.stderr)
        return 1
    written = _written(args.out)
    count = 0
    for category_url in list_categories(categories):
        listing = fetcher.get(category_url) or ""
        for entry_url in list_entries(listing):
            if entry_url in written:
                continue
            page = fetcher.get(entry_url)
            if page is None:
                continue
            path = write_entry(parse_entry(page, entry_url), args.out)
            written.add(entry_url)
            count += 1
            print(f"wrote {path.name}")
    print(f"{count} entries written to {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
