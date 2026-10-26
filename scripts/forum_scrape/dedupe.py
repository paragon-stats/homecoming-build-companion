"""Collapse a scrape down to distinct *builds*, and credit each to whoever posted it first.

A scrape counts artifacts, but the interesting unit is the build. The same one shows up
many times over, for reasons that have nothing to do with how good it is:

- Someone who likes their Titan Weapons build reposts it every time a Titan Weapons
    question comes up, even when the asker wanted a different secondary.
- Someone else answers a question by reposting *another* person's build, which credits
    the reposter unless identity is worked out first.
- The same build gets posted twice in one thread as an attachment and as a share block —
    different bytes, same build.
- A build re-saved under a different character name or comment is different bytes again.

So identity is computed from what the build *is* — archetype, power sets, and the ordered
(power, slot, enhancement) triples — and never from the bytes. Name, comment, the
``BuiltWith`` version stamp, and the per-power display toggles are all excluded, because
none of them change the build.

Attribution goes to the **earliest** post carrying a given build; everyone later is
recorded as a reposter. That is a best guess, not proof of authorship — an earlier posting
may simply not be in the crawl — so both the original and the repost list are kept and the
caller can see the evidence rather than take the verdict on faith.
"""

import hashlib
import json
from collections.abc import Iterable, Mapping
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from coh_engine.buildfile.dbindex import EnhIndexEntry, load_enhancement_index, load_power_index
from coh_engine.buildfile.mbd import parse_mbd
from coh_engine.buildfile.mxd import read_mxd

# Excluded from identity because they differ between two saves of the same build:
# StatInclude/ProcInclude (totals-pane toggles), Obtained/Grade (inventory bookkeeping),
# and Name/Comment/BuiltWith/LastPower (metadata about the file, not the build).


@dataclass(frozen=True, slots=True)
class BuildRecord:
    """One distinct build, with its first poster and everyone who reposted it."""

    identity: str
    first_author: str
    first_posted_at: str
    first_post_url: str
    path: str
    reposts: tuple[tuple[str, str], ...] = field(default=())  # (author, post_url)

    @property
    def repost_count(self) -> int:
        """How many later postings of this same build the crawl saw."""
        return len(self.reposts)


def _slot_key(slot: Iterable[Any]) -> str:
    """Flatten one slot to a string so mixed None/str/int fields stay sortable."""
    return "|".join("" if value is None else str(value) for value in slot)


def _canonical(class_name: str, power_sets: Iterable[str], entries: Iterable[tuple[str, int, tuple]]) -> str:
    """Hash the build-defining shape. Stable across saves, orderings and file formats."""
    payload = {
        "class": class_name,
        "sets": [s for s in power_sets if s],
        "powers": sorted([name, level, [_slot_key(slot) for slot in slots]] for name, level, slots in entries if name),
    }
    blob = json.dumps(payload, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()[:16]


def identity_from_mbd(data: bytes) -> str:
    """Identity of a ``.mbd`` JSON build (an attachment, or a decoded share block).

    Parsing goes through :func:`coh_engine.buildfile.mbd.parse_mbd` — the engine's own
    reader — rather than picking the JSON apart here. That reader already tracks
    MidsReborn's format: the ``-1`` level offset, the legacy ``Enhancement`` key standing
    in for ``Uid``, a JSON ``null`` meaning an empty slot. A second parser living in this
    file would quietly stop agreeing with it the first time any of that moved.

    Raises:
        ValueError: if the bytes are not a ``.mbd`` document.
    """
    try:
        document = json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"not .mbd JSON: {exc}") from exc
    if not isinstance(document, dict) or "PowerEntries" not in document:
        raise ValueError("no PowerEntries")
    build = parse_mbd(document)
    entries = [
        (
            power.power_name,
            power.level,
            tuple(
                (
                    slot.level,
                    slot.enhancement.uid if slot.enhancement else None,
                    slot.enhancement.io_level if slot.enhancement else None,
                    slot.enhancement.relative_level if slot.enhancement else None,
                )
                for slot in power.slot_entries
            ),
        )
        for power in build.power_entries
    ]
    return _canonical(build.class_name, build.power_sets, entries)


def identity_from_mxd(text: str, power_index: Mapping[int, str], enh_index: Mapping[int, EnhIndexEntry]) -> str:
    """Identity of a legacy ``.mxd`` share block, in the same shape as the ``.mbd`` path.

    Raises:
        ValueError: if the block does not decode or parse.
    """
    build = read_mxd(text, dict(power_index), dict(enh_index), None)
    entries = []
    for power in build.power_entries:
        slots = tuple(
            (
                slot.level,
                slot.enhancement.uid if slot.enhancement else None,
                slot.enhancement.io_level if slot.enhancement else None,
                slot.enhancement.relative_level if slot.enhancement else None,
            )
            for slot in power.slot_entries
        )
        entries.append((power.power_name, power.level, slots))
    return _canonical(build.class_name, build.power_sets, entries)


def artifact_identity(
    row: Mapping[str, Any],
    power_index: Mapping[int, str] | None = None,
    enh_index: Mapping[int, EnhIndexEntry] | None = None,
) -> str:
    """Best available identity for one index row, degrading rather than failing.

    A readable forum export, or anything that will not parse, falls back to a hash of its
    bytes. That still collapses verbatim reposts of it; it just cannot see through a
    re-save the way the parsed forms can, so those stay counted separately.
    """
    path = Path(row["path"])
    try:
        data = path.read_bytes()
    except OSError:
        return f"missing:{row['path']}"

    decoded = path.with_suffix(".mbd")
    if row.get("decoded_file") and decoded.exists():
        # A share block that decoded: identify from the build, not the envelope.
        try:
            return identity_from_mbd(decoded.read_bytes())
        except ValueError:
            pass
    if row["chunk_format"] == "mbd":
        try:
            return identity_from_mbd(data)
        except ValueError:
            pass
    if row["chunk_format"] == "mxd" and power_index is not None and enh_index is not None:
        try:
            return identity_from_mxd(data.decode("utf-8", "replace"), power_index, enh_index)
        except ValueError, KeyError, IndexError, OSError:
            pass
    return "bytes:" + hashlib.sha256(data).hexdigest()[:16]


def load_index_maps(dump_dir: Path) -> tuple[dict[int, str], dict[int, EnhIndexEntry]] | tuple[None, None]:
    """Load the StaticIndex maps a ``.mxd`` parse needs, or ``(None, None)`` if absent."""
    powers = dump_dir / "power_static_index.json"
    enhancements = dump_dir / "enhancements.json"
    if not (powers.exists() and enhancements.exists()):
        return None, None
    return load_power_index(powers), load_enhancement_index(enhancements)


def dedupe(rows: Iterable[Mapping[str, Any]], dump_dir: Path | None = None) -> list[BuildRecord]:
    """Reduce index rows to distinct builds, earliest poster first.

    Rows are sorted by post timestamp so "first" means first *seen by the crawl*; rows
    with no timestamp sort last rather than claiming priority they cannot support.
    """
    power_index, enh_index = load_index_maps(dump_dir) if dump_dir else (None, None)
    ordered = sorted(rows, key=lambda r: (r.get("posted_at") or "9999", r.get("post_url", "")))

    first: dict[str, BuildRecord] = {}
    reposts: dict[str, list[tuple[str, str]]] = {}
    for row in ordered:
        key = artifact_identity(row, power_index, enh_index)
        if key in first:
            entry = (row["author"], row["post_url"])
            if entry != (first[key].first_author, first[key].first_post_url):
                reposts.setdefault(key, []).append(entry)
            continue
        first[key] = BuildRecord(
            identity=key,
            first_author=row["author"],
            first_posted_at=row.get("posted_at", ""),
            first_post_url=row.get("post_url", ""),
            path=row["path"],
        )
    return [
        BuildRecord(
            identity=record.identity,
            first_author=record.first_author,
            first_posted_at=record.first_posted_at,
            first_post_url=record.first_post_url,
            path=record.path,
            reposts=tuple(reposts.get(key, ())),
        )
        for key, record in first.items()
    ]
