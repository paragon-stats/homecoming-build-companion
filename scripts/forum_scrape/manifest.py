"""Write the manifest of the forum source material: one row per capture, no post text.

The captures live in a private checkout (see ``source_root``). This repository keeps only what
was read: URL, title, date, archetype, content hash and path. Rows are sorted by path, so a
re-run on the same captures writes the same bytes.

Usage::

    python -m scripts.forum_scrape.manifest [--source-root PATH] [--out data/source-material/manifest.json]

Errors: E04 source root missing, E24 a capture skipped for unreadable front matter,
E05 no captures under the root.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from scripts.forum_scrape.guide_index import archetype_of, ledger_urls, load_captures, unquote
from scripts.forum_scrape.guides import _capture_url, content_hash, stored_hash
from scripts.forum_scrape.source_root import add_source_root_argument, source_root


def build_manifest(community: Path) -> list[dict[str, str]]:
    """One row per capture under ``community``, sorted by path."""
    ledgers = ledger_urls(community)
    rows = []
    for relative, era, inner, keys, body, text in load_captures(community):
        title = unquote(keys["title"])
        rows.append(
            {
                "url": _capture_url(text) or ledgers[era].get(inner, ""),
                "title": title,
                "date_posted": unquote(keys.get("date_posted", '""')),
                "archetype": archetype_of(Path(inner).parts[:-1], title),
                "content_hash": stored_hash(text) or content_hash(body),
                "path": f"community/{relative}",
                "era": era,
            }
        )
    return sorted(rows, key=lambda r: r["path"])


def render(rows: list[dict[str, Any]]) -> str:
    """The manifest file's text."""
    return json.dumps(rows, ensure_ascii=False, sort_keys=True, indent=2) + "\n"


def main(argv: list[str] | None = None) -> int:
    """CLI entry point: write the manifest for the source root."""
    parser = argparse.ArgumentParser(
        prog="python -m scripts.forum_scrape.manifest",
        description="Write the manifest of the forum source material, without post text.",
    )
    add_source_root_argument(parser)
    parser.add_argument(
        "--out", type=Path, default=Path("data/source-material/manifest.json"), help="manifest file to write"
    )
    args = parser.parse_args(argv)
    community = source_root(args.source_root) / "community"
    rows = build_manifest(community)
    if not rows:
        print(
            f"E05: no captures under {community}; expected community/**/*.md with front matter;"
            " check the checkout is current, or pass --source-root",
            file=sys.stderr,
        )
        return 1
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(render(rows), encoding="utf-8", newline="\n")
    print(f"{len(rows)} capture(s) listed in {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
