"""Separate reference builds from learning builds, and score what is left.

A raw scrape counts every build equally, which buries the useful ones. Two distinct
distortions do the burying, and they need opposite treatment:

**Request builds.** Someone posts their own attempt and asks for help. These dominate by
count — most authors post exactly one — and they are not reference material: they are
snapshots of a person reasoning about a complicated system, often with the mistakes still
in. That makes them valuable for a different purpose (what do people get wrong, and
where), so they are separated rather than discarded.

**Response builds.** Someone answers with a worked build. These are the community's
accumulated answer to "how should this actually be slotted", and when one is reposted by
other people it has been backed by someone other than its author. These are the builds
worth mining for how to build well.

**Near-duplicates.** Exact identity (:mod:`scripts.forum_scrape.dedupe`) still counts a
build and the same build with two slots moved as two builds. A prolific poster answering
five similar questions with five lightly-edited variants therefore scores five times.
Clustering by similarity collapses those to one, keeping the earliest as representative.

The volume threshold that separates creators from requesters is **measured, not chosen**:
authors are binned by output and the bin where their behaviour flips from mostly-asking to
mostly-answering is the breakpoint. See :func:`find_breakpoint`.

Every call made here is a best guess from forum text and posting order. The evidence
behind each one is kept on the record so it can be checked rather than trusted.
"""

import json
import re
import statistics
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from coh_engine.buildfile.mbd import parse_mbd

# Thread titles that announce a request for help. Matched on the topic title, which is the
# only reliable statement of what a thread is *for* that the crawl captures.
REQUEST_TITLE = re.compile(
    r"\b(help|feedback|critique|advice|thoughts|review|suggestions?|improve|tweak|"
    r"what am i|am i doing|new to|newbie|first build|opinions?|any good|better)\b",
    re.I,
)
# Titles that announce reference material rather than a question.
REFERENCE_TITLE = re.compile(
    r"\b(guide|primer|compendium|compilation|definitive|comprehensive|how to|faq|"
    r"handbook|reference|showcase|collection)\b",
    re.I,
)
# Two builds are "the same build, edited" above this Jaccard overlap of their
# (power, enhancement) content. 0.90 keeps genuine variants apart — swapping a whole
# set or an epic pool moves well below it — while collapsing slot-shuffle edits.
SIMILARITY_THRESHOLD = 0.90


@dataclass(frozen=True, slots=True)
class ScoredBuild:
    """One build with its classification, score, and the evidence for both."""

    identity: str
    author: str
    posted_at: str
    post_url: str
    topic_url: str
    topic_title: str
    path: str
    role: str  # "reference" | "learning" | "unclassified"
    score: float
    repost_count: int
    reposted_by_others: int
    author_volume: int
    is_thread_seed: bool
    title_signal: str  # "request" | "reference" | "neutral"
    duplicate_of: str = ""
    reasons: tuple[str, ...] = field(default=())


def _title_signal(title: str) -> str:
    """Classify a thread title as asking for help, offering reference, or neither."""
    if REFERENCE_TITLE.search(title):
        return "reference"
    if REQUEST_TITLE.search(title):
        return "request"
    return "neutral"


def build_fingerprint(path: Path) -> frozenset[str]:
    """The set of (power, enhancement) pairs in a ``.mbd`` build, for similarity.

    Slot *order* is deliberately ignored: moving an enhancement between slots of the same
    power does not make it a different build. Returns an empty set for anything that will
    not parse, which excludes it from clustering rather than mis-clustering it.
    """
    try:
        document = json.loads(path.read_bytes().decode("utf-8"))
    except OSError, UnicodeDecodeError, json.JSONDecodeError:
        return frozenset()
    if not isinstance(document, dict) or "PowerEntries" not in document:
        return frozenset()
    try:
        # The engine's reader, not a second parser here. It is stricter than a hand-rolled
        # walk — it requires the BuiltWith block, for instance — so a file that fails it
        # drops out of clustering rather than taking the whole scoring run down.
        build = parse_mbd(document)
    except ValueError, KeyError, TypeError:
        return frozenset()
    pairs = set()
    for power in build.power_entries:
        if not power.power_name:
            continue
        pairs.add(f"{power.power_name}|")
        for slot in power.slot_entries:
            if slot.enhancement and slot.enhancement.uid:
                pairs.add(f"{power.power_name}|{slot.enhancement.uid}")
    return frozenset(pairs)


def jaccard(left: frozenset[str], right: frozenset[str]) -> float:
    """Overlap of two fingerprints, 0.0 when either is empty."""
    if not left or not right:
        return 0.0
    return len(left & right) / len(left | right)


def cluster_similar(
    records: Sequence[Mapping[str, Any]],
    fingerprints: Mapping[str, frozenset[str]],
    threshold: float = SIMILARITY_THRESHOLD,
) -> dict[str, str]:
    """Map each build identity to its cluster representative (the earliest posting).

    Comparison is bucketed by archetype + power sets, taken from the fingerprint's power
    names, because builds that do not share a powerset can never clear the threshold.
    Without the bucketing this is 200M pairwise comparisons; with it, it is a few hundred
    per bucket.
    """
    buckets: dict[frozenset[str], list[str]] = {}
    for record in records:
        identity = record["identity"]
        fingerprint = fingerprints.get(identity, frozenset())
        if not fingerprint:
            continue
        # Powerset prefix of each power name ("Scrapper_Melee.Dark_Melee.Smite" -> the set).
        key = frozenset(pair.split(".")[0] + "." + pair.split(".")[1] for pair in fingerprint if pair.count(".") >= 2)
        buckets.setdefault(key, []).append(identity)

    representative: dict[str, str] = {}
    for members in buckets.values():
        chosen: list[str] = []
        for identity in members:  # records arrive earliest-first, so the first wins
            fingerprint = fingerprints[identity]
            for leader in chosen:
                if jaccard(fingerprint, fingerprints[leader]) >= threshold:
                    representative[identity] = leader
                    break
            else:
                chosen.append(identity)
    return representative


def find_breakpoint(authors: Mapping[str, tuple[int, float]]) -> tuple[int, list[tuple[int, int, float]]]:
    """Find the output level where authors flip from mostly-asking to mostly-answering.

    ``authors`` maps name -> (distinct builds, fraction of them that were responses).
    Authors are binned by output on a log-ish scale and each bin's mean response fraction
    is computed; the breakpoint is the lowest bin whose mean clears 0.5, i.e. where the
    typical author in that bin is answering more often than asking.

    This is why the threshold is not a round number picked by eye: it is the point where
    the population's *behaviour* changes, which is the thing actually being separated.

    Returns ``(breakpoint, [(bin_floor, author_count, mean_response_ratio), ...])``.
    """
    bins = [1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144]
    table: list[tuple[int, int, float]] = []
    breakpoint_value = bins[-1]
    found = False
    for index, floor in enumerate(bins):
        ceiling = bins[index + 1] if index + 1 < len(bins) else 10**9
        members = [ratio for volume, ratio in authors.values() if floor <= volume < ceiling]
        if not members:
            continue
        mean = statistics.mean(members)
        table.append((floor, len(members), mean))
        if not found and mean > 0.5:
            breakpoint_value = floor
            found = True
    return breakpoint_value, table


def score_build(
    record: Mapping[str, Any],
    author_volume: int,
    is_seed: bool,
    title_signal: str,
    breakpoint_value: int,
) -> tuple[str, float, tuple[str, ...]]:
    """Classify one build and score it. Returns ``(role, score, reasons)``.

    The score is only meaningful for reference builds; it ranks them by how much the
    community leaned on them. Learning builds are deliberately left unscored — they are
    not competing on quality, and rating a beginner's first attempt against a veteran's
    worked answer would be both meaningless and unkind.
    """
    reasons: list[str] = []
    others = record.get("reposted_by_others", 0)
    reposts = record.get("repost_count", 0)

    # A build in the post that opens a help thread, by someone who rarely posts builds,
    # is the archetypal request: "here is mine, what am I doing wrong".
    if is_seed and title_signal == "request" and author_volume < breakpoint_value:
        reasons.append("opens a help thread, low-volume author")
        return "learning", 0.0, tuple(reasons)
    if is_seed and title_signal == "request" and others == 0:
        reasons.append("opens a help thread, never reposted")
        return "learning", 0.0, tuple(reasons)

    # Being a reply is necessary but nowhere near sufficient — most replies are a second
    # person asking, or the original poster iterating. A build only earns the reference
    # reference set on a positive signal that someone other than chance put it there: an author
    # who demonstrably answers more than they ask, a repost by a third party, or a thread
    # that exists to be reference material.
    qualifies = author_volume >= breakpoint_value or others > 0 or title_signal == "reference"
    if not qualifies:
        reasons.append("no endorsement signal: low-volume author, never reposted, not a guide thread")
        return "unclassified", 0.0, tuple(reasons)

    score = 0.0
    if others:
        score += 3.0 * others
        reasons.append(f"reposted by {others} other author(s)")
    if reposts > others:
        score += 0.5 * (reposts - others)
        reasons.append("reposted by its own author")
    if author_volume >= breakpoint_value:
        score += 2.0
        reasons.append(f"author is above the {breakpoint_value}-build breakpoint")
    if title_signal == "reference":
        score += 2.0
        reasons.append("posted in a guide/reference thread")
    if not is_seed:
        score += 1.0
        reasons.append("posted as a reply, not the opening question")
    return "reference", round(score, 2), tuple(reasons)


@dataclass(frozen=True, slots=True)
class Refinement:
    """A request build and the worked answer it drew, in one thread.

    This pair is the point of the whole exercise. Neither half teaches much alone: the
    request is one person's guess, the answer is an assertion with no stated problem. Put
    them together and the delta *is* the lesson — these powers were dropped, those
    enhancements were moved, and a named community builder thought so in response to a
    stated goal. That is the shape the engine's own scoring needs in order to justify a
    pick rather than merely make one.
    """

    topic_url: str
    topic_title: str
    asked_by: str
    asked_path: str
    answered_by: str
    answered_path: str
    answerer_volume: int
    powers_added: tuple[str, ...]
    powers_removed: tuple[str, ...]
    enhancements_added: tuple[str, ...]
    enhancements_removed: tuple[str, ...]

    @property
    def change_size(self) -> int:
        """Total structural edits between the two builds."""
        return (
            len(self.powers_added)
            + len(self.powers_removed)
            + len(self.enhancements_added)
            + len(self.enhancements_removed)
        )


def _powers(fingerprint: frozenset[str]) -> set[str]:
    return {pair.split("|", 1)[0] for pair in fingerprint}


def _powersets(fingerprint: frozenset[str]) -> frozenset[str]:
    """The archetype power sets a build draws on, e.g. ``Scrapper_Melee.Dark_Melee``.

    Pools, epics and inherents are excluded on purpose. Two builds are "the same
    character" when their primary and secondary match; swapping the epic pool or dropping
    Fighting is precisely the kind of change a refinement makes, so counting those as a
    different character would throw away the chains worth having.
    """
    sets = {".".join(pair.split(".")[:2]) for pair in fingerprint if pair.count(".") >= 2}
    return frozenset(s for s in sets if not s.startswith(("Pool.", "Epic.", "Inherent.")))


def refinement_chains(
    scored: Sequence[ScoredBuild],
    fingerprints: Mapping[str, frozenset[str]],
    breakpoint_value: int,
) -> list[Refinement]:
    """Pair each request build with the answers it drew, and diff them.

    An answer counts only when it comes from a *different* author at or above the
    behavioural breakpoint — otherwise a second person asking in the same thread would be
    recorded as having taught the first.

    It must also be a rework of the *same* character: the two builds have to share their
    primary and secondary power sets. Without that the pair is a question about one build
    and an answer about a different one, which produces a large meaningless diff and
    teaches nothing about what was wrong with the original.
    """
    by_topic: dict[str, list[ScoredBuild]] = {}
    for build in scored:
        by_topic.setdefault(build.topic_url, []).append(build)

    chains: list[Refinement] = []
    for topic, builds in by_topic.items():
        seeds = [b for b in builds if b.is_thread_seed]
        if not seeds:
            continue
        request = seeds[0]
        asked = fingerprints.get(request.identity, frozenset())
        if not asked:
            continue
        for answer in builds:
            if answer.author == request.author or answer.is_thread_seed:
                continue
            if answer.author_volume < breakpoint_value:
                continue
            answered = fingerprints.get(answer.identity, frozenset())
            if not answered:
                continue
            # Same character, reworked — not a different build posted in the same thread.
            if _powersets(asked) != _powersets(answered):
                continue
            before, after = _powers(asked), _powers(answered)
            # A fingerprint carries a bare "power|" marker per power so that an
            # unslotted power still counts. Those belong in the power diff, not the
            # enhancement diff, or dropping one power reads as two enhancement changes.
            asked_enh = {p for p in asked if not p.endswith("|")}
            answered_enh = {p for p in answered if not p.endswith("|")}
            chains.append(
                Refinement(
                    topic_url=topic,
                    topic_title=request.topic_title,
                    asked_by=request.author,
                    asked_path=request.path,
                    answered_by=answer.author,
                    answered_path=answer.path,
                    answerer_volume=answer.author_volume,
                    powers_added=tuple(sorted(after - before)),
                    powers_removed=tuple(sorted(before - after)),
                    enhancements_added=tuple(sorted(answered_enh - asked_enh)),
                    enhancements_removed=tuple(sorted(asked_enh - answered_enh)),
                )
            )
    return chains


def score_builds(records: Sequence[Mapping[str, Any]], index_rows: Iterable[Mapping[str, Any]]) -> dict[str, Any]:
    """Run the whole pipeline over deduped builds; returns the scored builds and stats."""
    ordered = sorted(records, key=lambda r: (r.get("first_posted_at") or "9999", r.get("first_post_url", "")))

    # The earliest build in a thread is treated as that thread's seed — the post the
    # conversation formed around. It is a proxy for "the opening post": the crawl records
    # no post ordinal, so a thread whose true first post carried no build will nominate
    # its earliest build-bearing post instead.
    seeds: dict[str, str] = {}
    for record in ordered:
        seeds.setdefault(record["first_post_url"].split("#")[0], record["first_post_url"])

    titles = {row["topic_url"]: row.get("topic_title", "") for row in index_rows}

    # Response fraction per author: how often they post into someone else's thread.
    volume: dict[str, int] = {}
    responses: dict[str, int] = {}
    for record in ordered:
        author = record["first_author"]
        topic = record["first_post_url"].split("#")[0]
        volume[author] = volume.get(author, 0) + 1
        if seeds.get(topic) != record["first_post_url"]:
            responses[author] = responses.get(author, 0) + 1
    author_stats = {a: (n, responses.get(a, 0) / n) for a, n in volume.items()}
    breakpoint_value, bin_table = find_breakpoint(author_stats)

    fingerprints = {r["identity"]: build_fingerprint(Path(r["path"])) for r in ordered}
    representative = cluster_similar(ordered, fingerprints)

    scored: list[ScoredBuild] = []
    for record in ordered:
        topic = record["first_post_url"].split("#")[0]
        is_seed = seeds.get(topic) == record["first_post_url"]
        signal = _title_signal(titles.get(topic, ""))
        others = sum(1 for rp in record.get("reposts", []) if rp["author"] != record["first_author"])
        enriched = {**record, "reposted_by_others": others}
        role, score, reasons = score_build(enriched, volume[record["first_author"]], is_seed, signal, breakpoint_value)
        duplicate_of = representative.get(record["identity"], "")
        if duplicate_of:
            role, score = "near-duplicate", 0.0
            reasons = (*reasons, f"within {SIMILARITY_THRESHOLD:.0%} of {duplicate_of}")
        scored.append(
            ScoredBuild(
                identity=record["identity"],
                author=record["first_author"],
                posted_at=record.get("first_posted_at", ""),
                post_url=record["first_post_url"],
                topic_url=topic,
                topic_title=titles.get(topic, ""),
                path=record["path"],
                role=role,
                score=score,
                repost_count=record.get("repost_count", 0),
                reposted_by_others=others,
                author_volume=volume[record["first_author"]],
                is_thread_seed=is_seed,
                title_signal=signal,
                duplicate_of=duplicate_of,
                reasons=reasons,
            )
        )
    chains = refinement_chains(scored, fingerprints, breakpoint_value)
    return {
        "builds": scored,
        "breakpoint": breakpoint_value,
        "bins": bin_table,
        "authors": author_stats,
        "refinements": chains,
    }
