"""The rubric: request vs response, near-duplicate collapsing, and the breakpoint."""

import json
from pathlib import Path

import pytest

from scripts.forum_scrape.scoring import (
    SIMILARITY_THRESHOLD,
    ScoredBuild,
    _title_signal,
    build_fingerprint,
    cluster_similar,
    find_breakpoint,
    jaccard,
    refinement_chains,
    score_build,
    score_builds,
)


def scored(identity: str, author: str, *, seed: bool, volume: int, topic: str = "t") -> ScoredBuild:
    """A minimal ScoredBuild for exercising chain-building directly."""
    return ScoredBuild(
        identity=identity,
        author=author,
        posted_at="",
        post_url="",
        topic_url=topic,
        topic_title="",
        path="",
        role="reference",
        score=0.0,
        repost_count=0,
        reposted_by_others=0,
        author_volume=volume,
        is_thread_seed=seed,
        title_signal="neutral",
    )


def make_build(powers: dict[str, list[str]], sets: list[str] | None = None) -> dict:
    """A .mbd-shaped build from {power name: [enhancement uids]}.

    Carries the BuiltWith block a real Mids save always writes, because the engine's
    reader requires it.
    """
    return {
        "BuiltWith": {"App": "Mids Reborn", "Version": "3.8.6.0", "Database": "Homecoming"},
        "Level": "49",
        "Class": "Class_Scrapper",
        "PowerSets": sets or ["Scrapper_Melee.Dark_Melee", "Scrapper_Defense.Shield_Defense"],
        "PowerEntries": [
            {
                "PowerName": name,
                "Level": 1,
                "SlotEntries": [{"Level": 1, "Enhancement": {"Uid": uid}} for uid in enhancements],
            }
            for name, enhancements in powers.items()
        ],
    }


def write(tmp_path: Path, name: str, build: dict) -> Path:
    path = tmp_path / name
    path.write_text(json.dumps(build), encoding="utf-8")
    return path


# --- title reading -----------------------------------------------------------------


def test_title_signal() -> None:
    assert _title_signal("Dark/Shield build help please") == "request"
    assert _title_signal("The definitive Dark Melee guide") == "reference"
    assert _title_signal("Dark/Shield") == "neutral"
    # Reference wins when a title claims both, since a guide thread is reference material
    # even when its title invites feedback.
    assert _title_signal("Guide: Dark Melee - thoughts welcome") == "reference"


# --- fingerprints and similarity ---------------------------------------------------


def test_fingerprint_pairs_each_power_with_its_enhancements(tmp_path: Path) -> None:
    path = write(tmp_path, "a.mbd", make_build({"Set.A.Smite": ["Enh_1", "Enh_2"]}))
    assert build_fingerprint(path) == frozenset({"Set.A.Smite|", "Set.A.Smite|Enh_1", "Set.A.Smite|Enh_2"})


def test_fingerprint_is_empty_for_unreadable_or_non_builds(tmp_path: Path) -> None:
    assert build_fingerprint(tmp_path / "missing.mbd") == frozenset()
    junk = tmp_path / "junk.mbd"
    junk.write_text("not json", encoding="utf-8")
    assert build_fingerprint(junk) == frozenset()
    empty = tmp_path / "empty.mbd"
    empty.write_text('{"Class": "X"}', encoding="utf-8")
    assert build_fingerprint(empty) == frozenset()


def test_fingerprint_drops_a_build_the_engine_reader_rejects(tmp_path: Path) -> None:
    """Missing BuiltWith is fatal to the reader; that build leaves clustering, quietly."""
    malformed = make_build({"Set.A.Smite": ["Enh_1"]})
    del malformed["BuiltWith"]
    assert build_fingerprint(write(tmp_path, "bad.mbd", malformed)) == frozenset()


def test_fingerprint_skips_unnamed_powers_and_empty_slots(tmp_path: Path) -> None:
    build = make_build({"Set.A.Smite": ["Enh_1"]})
    build["PowerEntries"].append({"PowerName": "", "Level": 1, "SlotEntries": []})
    # An empty slot between two filled ones must not break the walk or add an entry.
    build["PowerEntries"][0]["SlotEntries"] = [
        {"Level": 1, "Enhancement": None},
        {"Level": 3, "Enhancement": {"Uid": "Enh_1"}},
    ]
    assert build_fingerprint(write(tmp_path, "a.mbd", build)) == frozenset({"Set.A.Smite|", "Set.A.Smite|Enh_1"})


def test_jaccard() -> None:
    assert jaccard(frozenset({"a", "b"}), frozenset({"a", "b"})) == 1.0
    assert jaccard(frozenset({"a", "b"}), frozenset({"a", "c"})) == pytest.approx(1 / 3)
    assert jaccard(frozenset(), frozenset({"a"})) == 0.0


def test_clustering_collapses_a_lightly_edited_build(tmp_path: Path) -> None:
    """Twenty identical slots and one changed is the same build, edited."""
    shared = {f"Set.A.Power{i}": [f"Enh_{i}"] for i in range(20)}
    original = make_build(shared)
    tweaked = make_build({**shared, "Set.A.Power19": ["Enh_changed"]})
    paths = {
        "orig": write(tmp_path, "orig.mbd", original),
        "tweak": write(tmp_path, "tweak.mbd", tweaked),
    }
    fingerprints = {k: build_fingerprint(v) for k, v in paths.items()}
    records = [{"identity": "orig"}, {"identity": "tweak"}]
    assert cluster_similar(records, fingerprints) == {"tweak": "orig"}


def test_clustering_keeps_a_different_secondary_apart(tmp_path: Path) -> None:
    """Swapping a whole power set is a different build, not an edit."""
    primary = {f"Scrapper_Melee.Dark_Melee.P{i}": [f"Enh_{i}"] for i in range(12)}
    one = make_build({**primary, **{f"Scrapper_Defense.Shield_Defense.S{i}": [f"E{i}"] for i in range(12)}})
    two = make_build({**primary, **{f"Scrapper_Defense.Bio_Armor.S{i}": [f"E{i}"] for i in range(12)}})
    fingerprints = {
        "one": build_fingerprint(write(tmp_path, "one.mbd", one)),
        "two": build_fingerprint(write(tmp_path, "two.mbd", two)),
    }
    assert cluster_similar([{"identity": "one"}, {"identity": "two"}], fingerprints) == {}


def test_clustering_ignores_builds_it_cannot_fingerprint() -> None:
    assert cluster_similar([{"identity": "x"}], {"x": frozenset()}) == {}


def test_clustering_checks_every_earlier_leader_not_just_the_first(tmp_path: Path) -> None:
    """A build may match the second cluster in its bucket rather than the first."""
    shared = {f"Set.A.Power{i}": [f"Enh_{i}"] for i in range(20)}
    first = make_build({**shared, **{f"Set.A.Extra{i}": [f"X_{i}"] for i in range(20)}})
    second = make_build(shared)
    third = make_build({**shared, "Set.A.Power19": ["Enh_changed"]})  # close to `second`
    fingerprints = {
        "first": build_fingerprint(write(tmp_path, "1.mbd", first)),
        "second": build_fingerprint(write(tmp_path, "2.mbd", second)),
        "third": build_fingerprint(write(tmp_path, "3.mbd", third)),
    }
    records = [{"identity": "first"}, {"identity": "second"}, {"identity": "third"}]
    assert cluster_similar(records, fingerprints) == {"third": "second"}


# --- the breakpoint ----------------------------------------------------------------


def test_breakpoint_is_the_first_bin_that_mostly_answers() -> None:
    authors = {
        **{f"asker{i}": (1, 0.1) for i in range(10)},
        **{f"middling{i}": (5, 0.4) for i in range(10)},
        **{f"answerer{i}": (21, 0.8) for i in range(10)},
    }
    breakpoint_value, table = find_breakpoint(authors)
    assert breakpoint_value == 21
    assert [floor for floor, _, _ in table] == [1, 5, 21]
    assert [count for _, count, _ in table] == [10, 10, 10]


def test_breakpoint_defaults_high_when_nobody_mostly_answers() -> None:
    breakpoint_value, _ = find_breakpoint({f"a{i}": (2, 0.1) for i in range(5)})
    assert breakpoint_value == 144


# --- classification ----------------------------------------------------------------


def test_a_low_volume_opening_post_in_a_help_thread_is_a_learning_build() -> None:
    role, score, reasons = score_build({}, author_volume=1, is_seed=True, title_signal="request", breakpoint_value=34)
    assert (role, score) == ("learning", 0.0)
    assert "low-volume author" in reasons[0]


def test_an_unreposted_opening_question_is_a_learning_build_whoever_asked() -> None:
    """A prolific builder asking for help on their own build is still asking."""
    role, _, reasons = score_build(
        {"repost_count": 0, "reposted_by_others": 0},
        author_volume=500,
        is_seed=True,
        title_signal="request",
        breakpoint_value=34,
    )
    assert role == "learning"
    assert "never reposted" in reasons[0]


def test_a_reply_with_no_endorsement_is_left_unclassified() -> None:
    role, score, reasons = score_build(
        {"repost_count": 0, "reposted_by_others": 0},
        author_volume=2,
        is_seed=False,
        title_signal="neutral",
        breakpoint_value=34,
    )
    assert (role, score) == ("unclassified", 0.0)
    assert "no endorsement signal" in reasons[0]


def test_a_third_party_repost_carries_the_most_weight() -> None:
    role, score, reasons = score_build(
        {"repost_count": 4, "reposted_by_others": 3},
        author_volume=2,
        is_seed=False,
        title_signal="neutral",
        breakpoint_value=34,
    )
    assert role == "reference"
    assert score == 3.0 * 3 + 0.5 * 1 + 1.0  # others, self-repost remainder, reply bonus
    assert any("reposted by 3 other" in r for r in reasons)


def test_a_prolific_author_in_a_guide_thread_qualifies() -> None:
    role, score, reasons = score_build(
        {"repost_count": 0, "reposted_by_others": 0},
        author_volume=50,
        is_seed=True,
        title_signal="reference",
        breakpoint_value=34,
    )
    assert role == "reference"
    assert score == 4.0  # above breakpoint + guide thread, no reply bonus for a seed
    assert any("guide/reference thread" in r for r in reasons)


# --- end to end --------------------------------------------------------------------


def scored_fixture(tmp_path: Path):
    """A help thread: a newcomer asks, a prolific builder answers with a rework."""
    asked = make_build({f"Scrapper_Melee.Dark_Melee.P{i}": [f"Enh_{i}"] for i in range(12)})
    answered = make_build(
        {**{f"Scrapper_Melee.Dark_Melee.P{i}": [f"Enh_{i}"] for i in range(11)}, "Pool.Fighting.Tough": ["Enh_new"]}
    )
    records = [
        {
            "identity": "ask",
            "first_author": "Newbie",
            "first_posted_at": "2026-01-01T00:00:00Z",
            "first_post_url": "https://f/topic/1-dark-shield-build-help/#comment-1",
            "path": str(write(tmp_path, "ask.mbd", asked)),
            "repost_count": 0,
            "reposts": [],
        },
        {
            "identity": "answer",
            "first_author": "Veteran",
            "first_posted_at": "2026-01-02T00:00:00Z",
            "first_post_url": "https://f/topic/1-dark-shield-build-help/#comment-2",
            "path": str(write(tmp_path, "answer.mbd", answered)),
            "repost_count": 1,
            "reposts": [{"author": "ThirdParty", "post_url": "https://f/topic/9-other/#comment-9"}],
        },
    ]
    rows = [{"topic_url": "https://f/topic/1-dark-shield-build-help/", "topic_title": "Dark/Shield build help"}]
    return records, rows


def test_score_builds_splits_the_thread_into_ask_and_answer(tmp_path: Path) -> None:
    records, rows = scored_fixture(tmp_path)
    result = score_builds(records, rows)
    by_identity = {b.identity: b for b in result["builds"]}

    assert by_identity["ask"].role == "learning"
    assert by_identity["ask"].is_thread_seed
    assert by_identity["ask"].score == 0.0  # learning builds are deliberately unscored

    answer = by_identity["answer"]
    assert answer.role == "reference"
    assert answer.reposted_by_others == 1
    assert not answer.is_thread_seed
    assert answer.title_signal == "request"  # the thread asked, the reply still answers


def chain_fingerprints(tmp_path: Path) -> dict[str, frozenset[str]]:
    """Question and answer over the same primary/secondary, differing by one power."""
    asked = make_build({f"Scrapper_Melee.Dark_Melee.P{i}": [f"Enh_{i}"] for i in range(12)})
    answered = make_build(
        {**{f"Scrapper_Melee.Dark_Melee.P{i}": [f"Enh_{i}"] for i in range(11)}, "Pool.Fighting.Tough": ["Enh_new"]}
    )
    return {
        "ask": build_fingerprint(write(tmp_path, "ask.mbd", asked)),
        "answer": build_fingerprint(write(tmp_path, "answer.mbd", answered)),
    }


def test_refinement_chain_pairs_the_question_with_the_answer(tmp_path: Path) -> None:
    builds = [scored("ask", "Newbie", seed=True, volume=1), scored("answer", "Veteran", seed=False, volume=99)]
    chains = refinement_chains(builds, chain_fingerprints(tmp_path), breakpoint_value=34)
    assert len(chains) == 1
    chain = chains[0]
    assert (chain.asked_by, chain.answered_by) == ("Newbie", "Veteran")
    assert chain.powers_added == ("Pool.Fighting.Tough",)
    assert chain.powers_removed == ("Scrapper_Melee.Dark_Melee.P11",)
    # The bare "power exists" markers stay in the power diff; only real enhancements
    # appear in the enhancement diff.
    assert chain.enhancements_added == ("Pool.Fighting.Tough|Enh_new",)
    assert chain.enhancements_removed == ("Scrapper_Melee.Dark_Melee.P11|Enh_11",)
    assert chain.change_size == 4  # one power and one enhancement, each way


def test_no_chain_when_the_answer_is_a_different_character(tmp_path: Path) -> None:
    """A reply about another powerset is not a refinement of the question."""
    fingerprints = chain_fingerprints(tmp_path)
    other = make_build({f"Scrapper_Melee.Claws.P{i}": [f"Enh_{i}"] for i in range(12)})
    fingerprints["answer"] = build_fingerprint(write(tmp_path, "other.mbd", other))
    builds = [scored("ask", "Newbie", seed=True, volume=1), scored("answer", "Veteran", seed=False, volume=99)]
    assert refinement_chains(builds, fingerprints, breakpoint_value=34) == []


def test_no_chain_from_a_low_volume_replier(tmp_path: Path) -> None:
    """Below the breakpoint the replier is another asker, not a teacher."""
    builds = [scored("ask", "Newbie", seed=True, volume=1), scored("answer", "AlsoNew", seed=False, volume=2)]
    assert refinement_chains(builds, chain_fingerprints(tmp_path), breakpoint_value=34) == []


def test_no_chain_when_the_replier_is_the_asker(tmp_path: Path) -> None:
    """Someone iterating on their own build is not being taught."""
    builds = [scored("ask", "Newbie", seed=True, volume=1), scored("answer", "Newbie", seed=False, volume=99)]
    assert refinement_chains(builds, chain_fingerprints(tmp_path), breakpoint_value=34) == []


def test_near_duplicates_are_marked_and_not_scored(tmp_path: Path) -> None:
    records, rows = scored_fixture(tmp_path)
    twin = dict(records[1])
    twin["identity"] = "answer-twin"
    twin["first_posted_at"] = "2026-01-03T00:00:00Z"
    twin["first_post_url"] = "https://f/topic/1-dark-shield-build-help/#comment-3"
    records.append(twin)

    result = score_builds(records, rows)
    marked = {b.identity: b for b in result["builds"]}["answer-twin"]
    assert marked.role == "near-duplicate"
    assert marked.score == 0.0
    assert marked.duplicate_of == "answer"
    assert f"{SIMILARITY_THRESHOLD:.0%}" in marked.reasons[-1]


def test_refinement_chains_skip_builds_without_a_fingerprint() -> None:
    builds = [scored("a", "Asker", seed=True, volume=1), scored("b", "Answerer", seed=False, volume=99)]
    assert refinement_chains(builds, {"a": frozenset(), "b": frozenset({"x"})}, 34) == []
    assert refinement_chains(builds, {"a": frozenset({"x"}), "b": frozenset()}, 34) == []
    # And a thread with no seed at all yields nothing.
    assert refinement_chains([builds[1]], {"b": frozenset({"x"})}, 34) == []
