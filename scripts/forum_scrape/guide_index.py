"""Index the captured guides by subject and report which powersets and archetypes they cover.

Stage 0 freezes the input: every capture under ``community/`` and ``community/pre-sunset/``
with its ``content_hash``, duplicate hashes and URLs folded. Stage 1 labels each capture with
no model: document type, archetype, the powersets and powers it names, and the issue live
when it was posted. The coverage report then says, per powerset and per (powerset, archetype)
pair, which captures cover it (docs/design/guides-to-engine.md, sections 2.5, 2.6 and 4).

A capture covers a powerset when its title names the set (display name, internal name, alias,
or a title combo such as ``Fire/Kin``) or its body names it three or more times. The match
is a lower bound for shorthand outside ``ALIASES`` and an upper bound for sets whose name is
also a common word or a power name (Regeneration, Ice Blast, Fire Blast, Force Field,
Invulnerability); those over-matches are reported as they fall, not tuned away.
Pool sets are left out of the subject match: their names are ordinary words ("Speed", "Flight").

Usage::

    python -m scripts.forum_scrape.guide_index

Writes ``guides.jsonl``, ``folded.jsonl`` and ``coverage.json`` under ``knowledge/coh/_work``.
Output is sorted, so a re-run on the same captures writes the same bytes.

Errors: E23 no captures under the root, E24 a capture skipped for unreadable front matter,
E25 no dated issue records in the patch index.
"""

from __future__ import annotations

import argparse
import bisect
import json
import re
import sys
from collections import defaultdict
from collections.abc import Iterable, Mapping
from functools import cache
from pathlib import Path
from typing import Any

from coh_engine.power_names import PowerNameResolver, PowersetName
from scripts.forum_scrape.guides import (
    _capture_url,
    _ledger,
    _split,
    archetype_in,
    content_hash,
    is_discussion,
    is_theme,
    stored_hash,
)
from scripts.forum_scrape.scoring import REQUEST_TITLE
from scripts.forum_scrape.scrape import find_forum_export

BODY_MIN_MENTIONS = 3
TRANSFER_MIN_GUIDES = 3
TRANSFER_MIN_ARCHETYPES = 2
PRE_SUNSET = "pre-sunset"
# Pool names are ordinary words; counting them would index every mention of "speed".
UNMATCHED_CATEGORIES = frozenset({"Pool"})
# Shorthand the forum uses for a set; values are display names. Small on purpose: a long
# list is hand-tuning, and the resolver already supplies every display and internal name.
ALIASES: Mapping[str, str] = {
    "bs": "Broad Sword",
    "dm": "Dark Melee",
    "ea": "Energy Aura",
    "ff": "Force Field",
    "inv": "Invulnerability",
    "kin": "Kinetics",
    "sj": "Street Justice",
    "sr": "Super Reflexes",
    "ss": "Super Strength",
    "tw": "Titan Weapons",
    "wp": "Willpower",
}
_ISSUE_TITLE = re.compile(r"\b(?:issue\s*|i)(\d{1,2})\b", re.I)
_COMBO = re.compile(r"\b(\w+)\s*/\s*(?=(\w+)\b)")


def _json_line(value: object) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True)


def _unquote(raw: str) -> str:
    try:
        return str(json.loads(raw))
    except json.JSONDecodeError:
        return raw.strip("\"' ")


def issue_timeline(patches: Path) -> list[tuple[str, str]]:
    """``(date, issue)`` per dated patch record that names an issue, oldest first."""
    index = patches / "index.jsonl"
    rows = (
        [json.loads(line) for line in index.read_text(encoding="utf-8").splitlines() if line.strip()]
        if index.exists()
        else []
    )
    return sorted({(str(r["published"])[:10], str(r["issue"])) for r in rows if r.get("issue")})


def issue_on(timeline: list[tuple[str, str]], date: str, title: str) -> str:
    """The issue live on ``date``; an Issue marker in the title overrides it. "" when neither applies."""
    marker = _ISSUE_TITLE.search(title)
    if marker:
        return f"issue-{int(marker.group(1))}"
    at = bisect.bisect_right(timeline, (date, "￿"))
    return timeline[at - 1][1] if at else ""


def tags_from(value: str) -> list[str]:
    """``topic_tags`` as the scraper writes it (JSON) or as a hand-written capture does (``[a, b]``).

    A block list leaves the key's own line empty, so it reads as no tags.
    """
    return [tag.strip().strip("\"'") for tag in value.strip().strip("[]").split(",") if tag.strip()]


def document_type(title: str, tags: Iterable[str], body: str) -> str:
    """Theme, build post (a Mids export under a help or critique title), discussion, else guide."""
    tagged = set(tags)
    if is_theme(title) or "theme-rp" in tagged:
        return "theme"
    if find_forum_export(body) and REQUEST_TITLE.search(title):
        return "build post"
    if "discussion" in tagged or is_discussion(title):
        return "discussion"
    return "guide"


def _group(resolver: PowerNameResolver) -> dict[str, tuple[PowersetName, ...]]:
    """Powersets by display name: one set on several archetypes is one subject."""
    groups: dict[str, list[PowersetName]] = defaultdict(list)
    for powerset in resolver.powersets():
        groups[powerset.display].append(powerset)
    return {display: tuple(members) for display, members in sorted(groups.items())}


def _names(display: str, members: tuple[PowersetName, ...], aliases: Mapping[str, str]) -> list[str]:
    internal = (m.internal.rsplit(".", 1)[-1].replace("_", " ") for m in members)
    short = (alias for alias, target in aliases.items() if target == display)
    return sorted({display.lower(), *(i.lower() for i in internal), *short}, key=len, reverse=True)


def _archetypes(members: tuple[PowersetName, ...]) -> frozenset[str]:
    return frozenset(a for m in members for a in m.archetypes)


def _combo_sets(
    title: str, groups: Mapping[str, tuple[PowersetName, ...]], archetype: str, aliases: Mapping[str, str]
) -> set[str]:
    """Sets named by a title combo (``Fire/Kin``): both halves must sit together on one archetype."""

    def matches(token: str) -> list[str]:
        token = token.lower()
        if token in aliases:
            return [aliases[token]] if aliases[token] in groups else []
        return [d for d in groups if d.lower() == token or d.lower().startswith(token + " ")]

    found: set[str] = set()
    for left, right in _COMBO.findall(title):
        for a in matches(left):
            for b in matches(right):
                shared = _archetypes(groups[a]) & _archetypes(groups[b])
                if shared and (not archetype or any(_same_archetype(archetype, s) for s in shared)):
                    found |= {a, b}
    return found


def subjects(
    title: str,
    body: str,
    archetype: str,
    groups: Mapping[str, tuple[PowersetName, ...]],
    aliases: Mapping[str, str] = ALIASES,
) -> list[str]:
    """Display names of the sets the title names or the body names ``BODY_MIN_MENTIONS`` times."""
    found = _combo_sets(title, groups, archetype, aliases)
    for display, members in groups.items():
        if members[0].category in UNMATCHED_CATEGORIES:
            continue
        pattern = _pattern(tuple(_names(display, members, aliases)))
        if pattern.search(title) or len(pattern.findall(body)) >= BODY_MIN_MENTIONS:
            found.add(display)
    return sorted(found)


@cache
def _pattern(names: tuple[str, ...]) -> re.Pattern[str]:
    return re.compile(r"\b(?:" + "|".join(re.escape(n) for n in names) + r")\b", re.I)


def _on_archetype(members: tuple[PowersetName, ...], archetype: str) -> tuple[PowersetName, ...]:
    """The members held by the capture's archetype, or all of them when none is or none matches."""
    held = tuple(m for m in members if any(_same_archetype(archetype, a) for a in m.archetypes))
    return held if archetype and held else members


def _same_archetype(guide: str, key: str) -> bool:
    return bool(guide) and (key == guide or key.startswith(guide))


def powers_named(body: str, members: tuple[PowersetName, ...]) -> set[str]:
    """Internal names of the powers in ``members`` whose display name appears in ``body``."""
    return {p.internal for m in members for p in m.powers if _pattern((p.display.lower(),)).search(body)}


def _archetype_of(parts: tuple[str, ...], title: str) -> str:
    folder = parts[1] if len(parts) > 1 and parts[0] == "archetypes" else ""
    return folder or archetype_in(title)


def build_index(
    community: Path,
    resolver: PowerNameResolver,
    timeline: list[tuple[str, str]],
    aliases: Mapping[str, str] = ALIASES,
) -> tuple[list[dict[str, Any]], list[dict[str, str]]]:
    """``(rows, folded)``: one row per distinct capture, and the captures folded into another."""
    groups = _group(resolver)
    ledgers = {
        era: {str(r["file"]): str(u) for u, r in _ledger(root).items() if r.get("file")}
        for era, root in _roots(community)
    }
    captures: list[tuple[str, str, str, dict[str, str], str, str]] = []
    for path in sorted(community.rglob("*.md")):
        relative = path.relative_to(community).as_posix()
        text = path.read_text(encoding="utf-8")
        keys, body = _split(text)
        if not keys:
            continue
        if "title" not in keys:
            print(f"E24: {relative}: front matter has no title; capture skipped", file=sys.stderr)
            continue
        era = PRE_SUNSET if relative.startswith(PRE_SUNSET + "/") else "live"
        captures.append((relative, era, relative.removeprefix(PRE_SUNSET + "/"), keys, body, text))
    # Live captures first, so a duplicate keeps the live one.
    captures.sort(key=lambda c: (c[1] == PRE_SUNSET, c[0]))

    rows: list[dict[str, Any]] = []
    folded: list[dict[str, str]] = []
    seen_hash: dict[str, str] = {}
    seen_url: dict[str, str] = {}
    for relative, era, inner, keys, body, text in captures:
        digest = stored_hash(text) or content_hash(body)
        url = _capture_url(text) or ledgers[era].get(inner, "")
        kept = seen_hash.get(digest) or (seen_url.get(url) if url else None)
        if kept:
            folded.append(
                {"file": relative, "kept": kept, "reason": "content_hash" if digest in seen_hash else "topic_url"}
            )
            continue
        seen_hash[digest] = relative
        if url:
            seen_url[url] = relative
        title = _unquote(keys["title"])
        archetype = _archetype_of(Path(inner).parts[:-1], title)
        names = subjects(title, body, archetype, groups, aliases)
        scoped = [m for n in names for m in _on_archetype(groups[n], archetype)]
        posted = _unquote(keys.get("date_posted", '""'))
        rows.append(
            {
                "file": relative,
                "content_hash": digest,
                "topic_url": url,
                "title": title,
                "era": era,
                "document_type": document_type(title, tags_from(keys.get("topic_tags", "")), body),
                "archetype": archetype,
                "powerset_names": names,
                "powersets": sorted({m.internal for m in scoped}),
                "powers": sorted(powers_named(body, tuple(scoped))),
                "date_posted": posted,
                "issue": issue_on(timeline, posted, title),
            }
        )
    rows.sort(key=lambda r: str(r["file"]))
    folded.sort(key=lambda r: r["file"])
    return rows, folded


def _roots(community: Path) -> list[tuple[str, Path]]:
    return [("live", community), (PRE_SUNSET, community / PRE_SUNSET)]


def build_coverage(rows: list[dict[str, Any]], resolver: PowerNameResolver) -> dict[str, Any]:
    """Guides per powerset and per (powerset, archetype) pair, the four pair groups, unguided and transfer sets."""
    groups = {d: m for d, m in _group(resolver).items() if m[0].category not in UNMATCHED_CATEGORIES}
    covering: dict[str, list[tuple[str, str]]] = defaultdict(list)
    for row in rows:
        for name in row["powerset_names"]:
            covering[name].append((str(row["file"]), str(row["archetype"])))
    powersets = {
        display: {
            "internal": sorted(m.internal for m in members),
            "archetypes": sorted(_archetypes(members)),
            "guides": sorted(f for f, _ in covering[display]),
        }
        for display, members in groups.items()
    }
    pairs: list[dict[str, Any]] = []
    for display, members in groups.items():
        for archetype in sorted(_archetypes(members)):
            own = sorted(f for f, a in covering[display] if _same_archetype(a, archetype))
            others = [a for _, a in covering[display] if a and not _same_archetype(a, archetype)]
            group = "own" if own else "transfer" if others else "unknown_source" if covering[display] else "none"
            pairs.append({"powerset": display, "archetype": archetype, "group": group, "own_guides": own})
    counts = {g: sum(p["group"] == g for p in pairs) for g in ("own", "transfer", "unknown_source", "none")}
    strong: defaultdict[str, int] = defaultdict(int)
    for p in pairs:
        strong[p["powerset"]] += len(p["own_guides"]) >= TRANSFER_MIN_GUIDES
    return {
        "powersets": powersets,
        "pairs": pairs,
        "pair_group_counts": counts,
        "unguided": sorted(d for d in groups if not covering[d]),
        "transfer_test_set": sorted(d for d, n in strong.items() if n >= TRANSFER_MIN_ARCHETYPES),
    }


def write_outputs(
    out: Path, rows: list[dict[str, Any]], folded: list[dict[str, str]], coverage: dict[str, Any]
) -> None:
    out.mkdir(parents=True, exist_ok=True)
    for name, lines in (("guides.jsonl", rows), ("folded.jsonl", folded)):
        (out / name).write_text("".join(_json_line(r) + "\n" for r in lines), encoding="utf-8", newline="\n")
    (out / "coverage.json").write_text(
        json.dumps(coverage, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8", newline="\n"
    )


def main(argv: list[str] | None = None) -> int:
    """CLI entry point: index the captures and write the coverage report."""
    parser = argparse.ArgumentParser(
        prog="python -m scripts.forum_scrape.guide_index",
        description="Index the captured guides by subject and report powerset coverage.",
    )
    parser.add_argument("--community", type=Path, default=Path("community"), help="capture root (default community)")
    parser.add_argument("--out", type=Path, default=Path("knowledge/coh/_work"), help="output folder")
    parser.add_argument("--canonical", type=Path, default=Path("data/canonical"), help="canonical data")
    parser.add_argument("--patches", type=Path, default=Path("data/patches"), help="dated patch records")
    args = parser.parse_args(argv)

    timeline = issue_timeline(args.patches)
    if not timeline:
        print(
            f"E25: {args.patches}/index.jsonl has no issue-dated records; the issue column will be empty",
            file=sys.stderr,
        )
    resolver = PowerNameResolver.from_canonical(args.canonical)
    rows, folded = build_index(args.community, resolver, timeline)
    if not rows:
        print(f"E23: no captures under {args.community}; expected community/**/*.md with front matter", file=sys.stderr)
        return 1
    write_outputs(args.out, rows, folded, build_coverage(rows, resolver))
    print(f"{len(rows)} capture(s) indexed, {len(folded)} folded, written to {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
