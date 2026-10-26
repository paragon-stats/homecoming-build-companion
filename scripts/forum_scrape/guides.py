"""Capture forum guides as community reference files, in the manual capture's format.

The build scraper keeps only posts that carry a build; a guide carries prose. This reads
the opening post of each thread in the sections you name, keeps the ones shaped like a
guide (long, structured: bold labels, lists, tables), and writes each as a
``community/<folder>/<slug>.md`` capture with the same frontmatter the manual browser
workflow produces, so the two paths are indistinguishable downstream.

The thresholds come from a probe of the Guides and archetype sections: guide-shaped
openers ran a median 1,800 words with 30 bold runs and 17 list items; everything else a
median 100 words with none.

Usage::

    python -m scripts.forum_scrape.guides --forum https://forums.homecomingservers.com/forum/44-guides/ --out community

Re-runs fetch only threads with activity newer than the ledger's; ``--force`` refetches all.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter
from datetime import UTC, datetime
from pathlib import Path

from scripts.forum_scrape.scoring import REQUEST_TITLE
from scripts.forum_scrape.scrape import (
    DEFAULT_UA,
    Fetcher,
    Post,
    iter_posts,
    last_page_number,
    listing_base,
    page_title,
    slugify,
    strip_html,
    topic_rows,
)

GUIDE_MIN_WORDS = 500
GUIDE_MIN_STRUCTURE = 6
# A thread that is a conversation rather than a guide: a request (the scorer's list), a
# showcase, or roleplay. Captured for the information in it, tagged so it never poses as a guide.
DISCUSSION_TITLE = re.compile(r"\b(rp|roleplay|role-play|showcase|favou?rite|your most|post your|what are you)\b", re.I)
# Frontmatter a reviewer owns; a refresh never overwrites these.
REVIEWER_KEYS = ("trust", "contradicts_data")
# Ledger outcomes that do not settle a thread: it is looked at again next run.
UNSETTLED = frozenset({"unavailable", "no posts"})
# Consecutive unsettled threads that stop a run: the failure is the site or the markup, not the thread.
BREAKER_STREAK = 10
_VERBATIM_HEADING = "## Verbatim excerpt"
# Build vocabulary per 100 words. In the probe every bind, badge, font, install and market
# guide sat below 1.0; mechanics and archetype guides had a median of 3.3.
BUILD_VOCAB_MIN_PER_100 = 2.0
BUILD_VOCAB = re.compile(
    r"\b(procs?|ppm|set bonus(es)?|enhancements?|slots?|slotting|recharge|defen[cs]e|resist(ance)?|endurance|"
    r"recovery|incarnate|alpha|damage|dps|accuracy|to-?hit|powers?|powersets?|archetype|builds?|ios?|attacks?|"
    r"regen(eration)?|hp|hit points|mez|hold|debuff|buff)\b",
    re.I,
)
# Tag order is the order of value to build reasoning; the first match leads the list.
TOPICS: tuple[tuple[str, re.Pattern[str]], ...] = tuple(
    (name, re.compile(pattern, re.I))
    for name, pattern in (
        ("procs-ppm", r"\bproc(s|ing)?\b|\bppm\b|chance for"),
        ("set-bonuses-io", r"set bonus|\bios?\b|invention|enhancement set|purple|winter.?o|\bato\b|rule of five"),
        ("incarnate", r"incarnate|alpha slot|destiny|judgement|lore pet|hybrid|interface|level shift"),
        (
            "defense-resist",
            r"soft.?cap|defen[cs]e (cap|debuff|bonus|type)|resist(ance)? (cap|debuff|bonus|type)|positional|\bddr\b",
        ),
        ("recharge-endurance", r"recharge|perma|hasten|endurance (cost|reduction|management|drain)|recovery"),
        ("damage-dps", r"\bdps\b|damage (scale|bonus|buff|resist|type)|pylon|attack chain|rotation|\bcrit"),
        ("mechanics-formulas", r"formula|math|calculat|modifier|breakpoint|\bcap\b|diminish"),
        ("leveling-content", r"level(l)?ing|exemplar|task force|\btf\b|trial|farm"),
        ("tools", r"mids|hero designer|spreadsheet|planner|city of data"),
    )
)
# Section slugs and title words that name an archetype; the value is the folder name.
ARCHETYPES: tuple[tuple[str, str], ...] = (
    ("blaster", "blaster"),
    ("brute", "brute"),
    ("controller", "controller"),
    ("corruptor", "corruptor"),
    ("defender", "defender"),
    ("dominator", "dominator"),
    ("mastermind", "mastermind"),
    ("scrapper", "scrapper"),
    ("sentinel", "sentinel"),
    ("stalker", "stalker"),
    ("tanker", "tanker"),
    ("kheldian", "kheldian"),
    ("peacebringer", "peacebringer"),
    ("warshade", "warshade"),
    ("arachnos", "arachnos"),
    ("veat", "arachnos"),
    ("widow", "arachnos"),
)
_TABLE = re.compile(r"<table\b.*?</table>", re.I | re.S)
_ROW = re.compile(r"<tr\b.*?</tr>", re.I | re.S)
_CELL = re.compile(r"<t[dh]\b[^>]*>(.*?)</t[dh]>", re.I | re.S)
_LIST_OPEN = re.compile(r"<[uo]l\b[^>]*>", re.I)
_LI = re.compile(r"<li\b[^>]*>", re.I)
_STRONG_OPEN = re.compile(r"<(strong|b)\b[^>]*>", re.I)
_STRONG_CLOSE = re.compile(r"</(strong|b)>", re.I)
_HEADING = re.compile(r"<h[1-6]\b", re.I)
_TABLE_OPEN = re.compile(r"<table\b", re.I)
_SUMMARY_LINE = "- Summary pending review: captured from the opening post by the forum guide scraper."


def _table_to_rows(table_html: str) -> str:
    rows = []
    for row in _ROW.findall(table_html):
        cells = [" ".join(strip_html(cell).split()) for cell in _CELL.findall(row)]
        rows.append("| " + " | ".join(cells) + " |")
    return "\n" + "\n".join(rows) + "\n"


def render_html(fragment: str) -> str:
    """Plain text that keeps what a guide's layout carries: table rows, bullets, bold labels."""
    text = _TABLE.sub(lambda m: _table_to_rows(m.group(0)), fragment)
    text = _LIST_OPEN.sub("\n", text)
    text = _LI.sub("- ", text)
    text = _STRONG_OPEN.sub("**", text)
    text = _STRONG_CLOSE.sub("**", text)
    text = strip_html(text)
    # A bold run split by markup ("<strong>a</strong><strong> b</strong>") rejoins; a bold
    # label followed by a bold line stays two lines.
    text = re.sub(r"\*\*[ \t]*\*\*", "", text)
    return re.sub(r"\n{3,}", "\n\n", text).strip()


def structure_score(fragment: str) -> int:
    """Headings, bold runs and list items count one each; a table counts three."""
    return (
        len(_HEADING.findall(fragment))
        + len(_STRONG_OPEN.findall(fragment))
        + len(_LI.findall(fragment))
        + 3 * len(_TABLE_OPEN.findall(fragment))
    )


def is_guide(fragment: str, text: str) -> bool:
    """Long and structured, the two measures that separated guides from everything else."""
    return len(text.split()) >= GUIDE_MIN_WORDS and structure_score(fragment) >= GUIDE_MIN_STRUCTURE


def topic_tags(title: str, text: str) -> list[str]:
    blob = f"{title}\n{text[:4000]}"
    return [name for name, pattern in TOPICS if pattern.search(blob)]


def archetype_in(value: str) -> str:
    """The archetype folder a section slug or a title names, or "" when it names none."""
    lowered = value.lower()
    for word, folder in ARCHETYPES:
        if re.search(rf"\b{word}s?\b", lowered):
            return folder
    return ""


_PLAYER_ROLE_PREFIXES = (*dict.fromkeys(word for word, _ in ARCHETYPES), "pool", "epic")


def powersets_from_canonical(canonical: Path) -> frozenset[str]:
    """Every player powerset the canonical tree has a folder for, as lower-case words.

    Roles are the first folder level (``tanker_defense``, ``pool``); only player roles
    count, or NPC set names such as ``target`` would claim guide titles.
    """
    powers = canonical / "powers"
    if not powers.is_dir():
        return frozenset()
    return frozenset(
        child.name.replace("_", " ")
        for role in powers.iterdir()
        if role.is_dir() and role.name.startswith(_PLAYER_ROLE_PREFIXES)
        for child in role.iterdir()
        if child.is_dir()
    )


def build_relevance(text: str) -> float:
    """Build-vocabulary hits per 100 words: what separates guides about the game's systems from the rest."""
    words = len(text.split())
    return 100 * len(BUILD_VOCAB.findall(text)) / words if words else 0.0


def classify(title: str, section_slug: str, powersets: frozenset[str]) -> tuple[str, str]:
    """``(folder, leading tag)``: an archetype guide, else a powerset guide, else mechanics."""
    archetype = archetype_in(section_slug) or archetype_in(title)
    if archetype:
        return f"archetypes/{archetype}", archetype
    lowered = title.lower()
    for name in sorted(powersets, key=len, reverse=True):
        if re.search(rf"\b{re.escape(name)}\b", lowered):
            return "powersets", name.replace(" ", "-")
    return "mechanics", "mechanics"


def _quoted_list(values: list[str]) -> str:
    return "[" + ", ".join(json.dumps(v) for v in values) + "]"


def is_discussion(title: str) -> bool:
    """A request, showcase or roleplay thread: kept for its information, not presented as a guide."""
    return bool(REQUEST_TITLE.search(title) or DISCUSSION_TITLE.search(title))


def content_hash(text: str) -> str:
    """A short digest of the rendered opening post; equal digests mean the author changed nothing."""
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


def capture_markdown(post: Post, topic_url: str, title: str, tags: list[str], captured_on: str, digest: str) -> str:
    """The capture file: the manual workflow's frontmatter, a summary stub, the opening post verbatim."""
    posted = post.posted_at[:10]
    front = [
        "---",
        f"title: {json.dumps(title)}",
        f"url: {json.dumps(topic_url)}",
        'source: "forums.homecomingservers.com"',
        f"authors: {_quoted_list([post.author])}",
        f"date_posted: {json.dumps(posted)}",
        f"date_captured: {json.dumps(captured_on)}",
        'captured_by: "local-capture"',
        f"topic_tags: {_quoted_list(tags)}",
        "trust: single-author-opinion",
        'contradicts_data: "none"',
        f"content_hash: {json.dumps(digest)}",
        "---",
    ]
    body = "\n".join(
        [
            f"# {title}",
            "",
            "## Summary",
            "",
            _SUMMARY_LINE,
            "",
            _VERBATIM_HEADING,
            "",
            f"### Post 1 — {post.author}, {posted}",
            "",
            render_html(post.html),
            "",
        ]
    )
    return "\n".join(front) + "\n\n" + body


def _split(capture: str) -> tuple[dict[str, str], str]:
    """``(frontmatter key -> raw value, body)`` of a capture file; a file without frontmatter is all body."""
    if not capture.startswith("---\n"):
        return {}, capture
    end = capture.find("\n---", 4)
    if end < 0:
        return {}, capture
    keys: dict[str, str] = {}
    for line in capture[4:end].splitlines():
        key, sep, value = line.partition(":")
        if sep:
            keys[key.strip()] = value.strip()
    return keys, capture[end + 4 :].lstrip("\n")


def _verbatim_section(body: str) -> tuple[int, int]:
    """Start and end offsets of the verbatim section in a body, or ``(-1, -1)`` when it has none."""
    start = body.find(_VERBATIM_HEADING)
    if start < 0:
        return -1, -1
    following = re.search(r"^## ", body[start + len(_VERBATIM_HEADING) :], re.M)
    end = start + len(_VERBATIM_HEADING) + following.start() if following else len(body)
    return start, end


def stored_hash(capture: str) -> str:
    """The ``content_hash`` a capture file records, or "" for a file written before hashes."""
    return str(json.loads(_split(capture)[0].get("content_hash", '""')))


def merge_capture(existing: str, fresh: str) -> str:
    """A refresh that keeps what the reviewer wrote.

    Source-derived frontmatter and the verbatim section come from ``fresh``; the reviewer's
    ``trust`` and ``contradicts_data`` values, the Summary and every other section come from
    ``existing``. The replaced hash is kept as ``superseded_hash`` so the change can be diffed.
    """
    old_keys, old_body = _split(existing)
    new_keys, new_body = _split(fresh)
    lines = []
    for key, value in new_keys.items():
        if key in REVIEWER_KEYS and key in old_keys:
            value = old_keys[key]
        lines.append(f"{key}: {value}")
        if key == "content_hash" and old_keys.get("content_hash") not in (None, value):
            lines.append(f"superseded_hash: {old_keys['content_hash']}")
    new_start, new_end = _verbatim_section(new_body)
    old_start, old_end = _verbatim_section(old_body)
    if old_start < 0:
        body = old_body.rstrip("\n") + "\n\n" + new_body[new_start:new_end]
    else:
        body = old_body[:old_start] + new_body[new_start:new_end] + old_body[old_end:]
    return "---\n" + "\n".join(lines) + "\n---\n\n" + body


def _ledger(out: Path) -> dict[str, dict[str, object]]:
    path = out / "guides.jsonl"
    rows: dict[str, dict[str, object]] = {}
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                row = json.loads(line)
                rows[str(row["topic_url"])] = row
    return rows


def _walk_listing(fetcher: Fetcher, forum_url: str, pages: int | None) -> dict[str, str]:
    first = fetcher.get(forum_url)
    if first is None:
        print(f"E61: listing unavailable: {forum_url}", file=sys.stderr)
        return {}
    base = listing_base(first, forum_url)
    total = last_page_number(first)
    rows = dict(topic_rows(first))
    for number in range(2, (total if pages is None else min(total, pages)) + 1):
        listing = fetcher.get(f"{base}/page/{number}/")
        if listing is not None:
            for topic, last in topic_rows(listing).items():
                rows.setdefault(topic, last)
    return rows


def _capture(
    fetcher: Fetcher, topic_url: str, section_slug: str, out: Path, powersets: frozenset[str]
) -> tuple[str, str, str]:
    """Capture the thread's opening post if it is a build-related guide.

    Returns ``(file, digest, reason)``: the capture's path and content hash when the opener
    qualifies, else the reason it did not. ``reason`` is ``"unchanged"`` when the file already
    holds this content, in which case nothing is written.
    """
    page = fetcher.get(topic_url)
    if page is None:
        return "", "", "unavailable"
    posts = list(iter_posts(page))
    if not posts:
        return "", "", "no posts"
    opener = posts[0]
    text = render_html(opener.html)
    if not is_guide(opener.html, text):
        return "", "", "not guide-shaped"
    if build_relevance(text) < BUILD_VOCAB_MIN_PER_100:
        return "", "", "not build-related"
    title = page_title(page)
    folder, lead = classify(title, section_slug, powersets)
    tags = [lead, *(["discussion"] if is_discussion(title) else []), *[t for t in topic_tags(title, text) if t != lead]]
    digest = content_hash(text)
    path = out / folder / f"{slugify(title)}.md"
    relative = path.relative_to(out).as_posix()
    existing = path.read_text(encoding="utf-8") if path.exists() else ""
    if existing and stored_hash(existing) == digest:
        return relative, digest, "unchanged"
    fresh = capture_markdown(opener, topic_url, title, tags, datetime.now(UTC).date().isoformat(), digest)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(merge_capture(existing, fresh) if existing else fresh, encoding="utf-8", newline="\n")
    return relative, digest, ""


def main(argv: list[str] | None = None) -> int:
    """CLI entry point: capture the guide-shaped opening posts of the named sections."""
    parser = argparse.ArgumentParser(
        prog="python -m scripts.forum_scrape.guides",
        description="Capture forum guides as community reference files, with attribution.",
    )
    parser.add_argument(
        "--forum", action="append", default=[], metavar="URL", required=True, help="a forum listing URL (repeatable)"
    )
    parser.add_argument(
        "--pages", type=int, default=None, metavar="N", help="listing pages to walk per forum (default all)"
    )
    parser.add_argument("--out", type=Path, default=Path("community"), help="capture root (default community)")
    parser.add_argument(
        "--canonical", type=Path, default=Path("data/canonical"), help="canonical data, for powerset names"
    )
    parser.add_argument("--delay", type=float, default=2.0, help="minimum seconds between requests (default 2)")
    parser.add_argument("--user-agent", default=DEFAULT_UA)
    parser.add_argument("--force", action="store_true", help="refetch threads the ledger already covers")
    args = parser.parse_args(argv)

    fetcher = Fetcher(args.user_agent, args.delay)
    powersets = powersets_from_canonical(args.canonical)
    ledger = _ledger(args.out)
    args.out.mkdir(parents=True, exist_ok=True)
    examined = captured = streak = 0
    reasons: Counter[str] = Counter()
    tripped = False
    with (args.out / "guides.jsonl").open("a", encoding="utf-8", newline="\n") as ledger_file:
        for forum_url in args.forum:
            section_slug = forum_url.rstrip("/").rsplit("/", 1)[-1]
            for topic_url, last_activity in _walk_listing(fetcher, forum_url, args.pages).items():
                known = ledger.get(topic_url) or {}
                # A thread the last run could not fetch or parse is not covered: a timeout or a
                # layout change must not hide it.
                settled = bool(known) and known.get("reason") not in UNSETTLED
                if (
                    settled
                    and not args.force
                    and last_activity
                    and last_activity <= str(known.get("last_activity", ""))
                ):
                    continue
                file, digest, reason = _capture(fetcher, topic_url, section_slug, args.out, powersets)
                examined += 1
                captured += bool(file) and reason != "unchanged"
                reasons[reason or "written"] += 1
                streak = streak + 1 if reason in UNSETTLED else 0
                row = {
                    "topic_url": topic_url,
                    "last_activity": last_activity,
                    "captured": bool(file),
                    "file": file,
                    "content_hash": digest,
                    "reason": reason,
                }
                ledger[topic_url] = row
                ledger_file.write(json.dumps(row, ensure_ascii=False) + "\n")
                # On disk before the next thread: progress stays visible and an interrupted
                # run resumes from here instead of re-examining everything.
                ledger_file.flush()
                if streak >= BREAKER_STREAK:
                    # The site is down, blocking us, or changed its markup: every further
                    # request would fail the same way, so stop and leave the rest for a re-run.
                    tripped = True
                    print(
                        f"E63: {streak} consecutive threads unfetched or unparsed; stopping at {topic_url}",
                        file=sys.stderr,
                    )
                    break
            if tripped:
                break
    print(f"{captured} guide(s) written from {examined} thread(s) examined into {args.out}")
    if reasons:
        print("  " + ", ".join(f"{name}: {count}" for name, count in sorted(reasons.items())))
    # Unsettled threads are retried by the next run; exit 2 tells a scripted caller to schedule one.
    unsettled = sum(reasons[name] for name in UNSETTLED)
    if unsettled or tripped:
        print(f"E62: {unsettled} thread(s) could not be fetched or parsed; re-run to retry them", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
