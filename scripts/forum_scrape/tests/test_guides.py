"""Guide capture: shape detection, topic tags, placement, the capture file, and the crawl."""

import json
import re
from pathlib import Path

import pytest

from scripts.forum_scrape import scrape
from scripts.forum_scrape.guides import (
    BUILD_VOCAB_MIN_PER_100,
    GUIDE_MIN_WORDS,
    archetype_in,
    build_relevance,
    capture_markdown,
    classify,
    content_hash,
    is_discussion,
    is_guide,
    is_theme,
    main,
    merge_capture,
    powersets_from_canonical,
    render_html,
    stored_hash,
    structure_score,
    topic_tags,
)
from scripts.forum_scrape.scrape import Post, iter_posts

BASE = "https://forums.homecomingservers.com"
TOPIC = f"{BASE}/topic/16050-movement-speed-a-quick-guide/"
FORUM = f"{BASE}/forum/44-guides/"

TABLE = (
    "<table><tbody><tr><td><p>&nbsp;</p></td><td><p>Fly Speed (mph)</p></td><td><p>Run Speed (mph)</p></td></tr>"
    "<tr><td>Base</td><td>21.48</td><td>14.32</td></tr></tbody></table>"
)
PROSE = (
    "<p>"
    + " ".join(f"Point {n}: the power's recharge and endurance change with each slot you add." for n in range(80))
    + "</p>"
)
HOWTO = (
    "<p><strong>Keybinds</strong></p><ul><li>bind one</li><li>bind two</li><li>bind three</li></ul><p><strong>Fonts</strong></p>"
    "<p><strong>Sounds</strong></p><p>"
    + " ".join(f"Step {n}: open the folder and copy the file into place." for n in range(80))
    + "</p>"
)
GUIDE_BODY = (
    "<p><strong>Quick Guide to Movement Speed</strong></p>"
    "<p><strong>Base and Max Movement Speeds:</strong></p>" + TABLE + PROSE + "<ul><li>Hurdle adds jump height</li>"
    "<li>Swift adds run speed</li><li>Sprint stacks with both</li></ul>"
    "<p><strong>+X% to Movement Speed</strong> versus <strong>Strength</strong></p>"
)


def article(comment_id: str, author: str, when: str, body: str) -> str:
    return (
        f"<article id='elComment_{comment_id}' class='cPost'>"
        f"<a href='{BASE}/profile/1-{author.lower()}/' title=\"Go to {author}'s profile\" class='ipsUserPhoto'></a>"
        f"<div>Posted <time datetime='{when}'>x</time></div>"
        f"<div data-role='commentContent' class='ipsType_richText'>{body}</div></article>"
    )


def page(title: str, *articles: str) -> str:
    return f"<html><head><title>{title} - Guides - Homecoming</title></head><body>{''.join(articles)}</body></html>"


GUIDE_PAGE = page(
    "Movement Speed: A Quick Guide",
    article("1", "Bopper", "2020-03-01T10:00:00Z", GUIDE_BODY),
    article("2", "Reader", "2020-03-02T10:00:00Z", "<p>Thanks, this helped.</p>"),
)
CHATTER_PAGE = page("Build advice", article("9", "Newbie", "2024-01-01T00:00:00Z", "<p>Which armor is best?</p>"))
HOWTO_PAGE = page("Modding your client", article("5", "Tinker", "2024-01-01T00:00:00Z", HOWTO))


def test_build_relevance_separates_system_guides_from_client_howtos() -> None:
    assert build_relevance(render_html(GUIDE_BODY)) >= BUILD_VOCAB_MIN_PER_100
    assert build_relevance(render_html(HOWTO)) < BUILD_VOCAB_MIN_PER_100
    assert build_relevance("") == 0.0


def test_render_html_keeps_tables_as_rows_and_lists_as_bullets() -> None:
    text = render_html(GUIDE_BODY)
    assert "| Fly Speed (mph) | Run Speed (mph) |" in text
    assert "| Base | 21.48 | 14.32 |" in text
    assert "- Hurdle adds jump height" in text
    assert "**Quick Guide to Movement Speed**" in text


def test_structure_and_shape_thresholds() -> None:
    assert structure_score(GUIDE_BODY) >= 6
    assert is_guide(GUIDE_BODY, render_html(GUIDE_BODY))
    assert not is_guide("<p>short</p>", "short")
    long_flat = "<p>" + "word " * (GUIDE_MIN_WORDS + 10) + "</p>"
    assert not is_guide(long_flat, render_html(long_flat))  # long but unstructured prose is not a guide


def test_topic_tags_come_from_title_and_text_in_fixed_order() -> None:
    tags = topic_tags("Procs Per Minute (PPM) Information Guide", "set bonus math and incarnate alpha")
    assert tags[:3] == ["procs-ppm", "set-bonuses-io", "incarnate"]
    assert topic_tags("Hello", "nothing relevant") == []


def test_archetype_in_matches_section_slug_or_title() -> None:
    assert archetype_in("13-blaster") == "blaster"
    assert archetype_in("The (Almost) Complete guide to Kheldians") == "kheldian"
    assert archetype_in("Peacebringers are fun") == "peacebringer"
    assert archetype_in("44-guides") == ""


def test_powersets_from_canonical_reads_set_folder_names(tmp_path: Path) -> None:
    (tmp_path / "powers" / "tanker_defense" / "dark_armor").mkdir(parents=True)
    (tmp_path / "powers" / "tanker_defense" / "index.json").write_text("{}", encoding="utf-8")
    (tmp_path / "powers" / "pool" / "fighting").mkdir(parents=True)
    (tmp_path / "powers" / "hellions" / "target").mkdir(parents=True)  # an NPC role: not a player powerset
    (tmp_path / "powers" / "README.md").write_text("", encoding="utf-8")
    assert powersets_from_canonical(tmp_path) == frozenset({"dark armor", "fighting"})
    assert powersets_from_canonical(tmp_path / "missing") == frozenset()


def test_classify_places_archetype_then_powerset_then_mechanics() -> None:
    sets = frozenset({"dark armor", "time manipulation"})
    assert classify("Invulnerability Tankers - Soft Cap Defense", "44-guides", sets) == ("archetypes/tanker", "tanker")
    assert classify("Time Manipulation Guide", "44-guides", sets) == ("powersets", "time-manipulation")
    assert classify("How to Calculate Debuffs", "44-guides", sets) == ("mechanics", "mechanics")
    assert classify("Anything", "22-brute", sets) == ("archetypes/brute", "brute")
    assert classify("Weekly Strike Target guide", "44-guides", frozenset({"target"})) == ("powersets", "target")
    assert classify("Targeting macros", "44-guides", frozenset({"target"})) == (
        "mechanics",
        "mechanics",
    )  # whole word only


def test_capture_markdown_matches_the_manual_capture_format() -> None:
    post = Post("1", "Bopper", f"{BASE}/profile/1-bopper/", "2020-03-01T10:00:00Z", render_html(GUIDE_BODY), GUIDE_BODY)
    text = capture_markdown(
        post, TOPIC, "Movement Speed: A Quick Guide", ["mechanics", "recharge-endurance"], "2026-10-26", "abc123"
    )
    head, body = text.split("---\n", 2)[1:]
    assert 'content_hash: "abc123"' in head and "superseded_hash" not in head
    assert 'title: "Movement Speed: A Quick Guide"' in head
    assert f'url: "{TOPIC}"' in head
    assert 'authors: ["Bopper"]' in head
    assert 'date_posted: "2020-03-01"' in head and 'date_captured: "2026-10-26"' in head
    assert 'captured_by: "local-capture"' in head
    assert 'topic_tags: ["mechanics", "recharge-endurance"]' in head
    assert "trust: single-author-opinion" in head
    assert "## Summary" in body and "## Verbatim excerpt" in body
    assert "### Post 1 — Bopper, 2020-03-01" in body
    assert "| Base | 21.48 | 14.32 |" in body


def test_is_discussion_matches_requests_showcases_and_roleplay() -> None:
    assert is_discussion("Post your favourite builds")
    assert is_discussion("Need advice on my Brute")
    assert is_discussion("RP crossover build: Fallout meets Paragon City")
    assert not is_discussion("Movement Speed: A Quick Guide")


def test_is_theme_matches_concept_costume_and_roleplay_threads() -> None:
    assert is_theme("RP crossover build: Fallout meets Paragon City")
    assert is_theme("Power sets that can be flavored as light elemental")
    assert is_theme("What would Spider-Man be?")
    assert is_theme("A guide to costume design for villains")
    assert is_theme("Power colour customization: matching auras to your look")
    assert not is_theme("Movement Speed: A Quick Guide")
    # Aura powersets and Lore pets are build subjects, not themes.
    assert not is_theme("Maximal survival guide for Fiery Melee/Energy Aura brutes")
    assert not is_theme("Memphis Bill's Firey Aura overview 2.0 (i18)")
    assert not is_theme("A mostly comprehensive compendium of Lore pet performance")


def test_main_captures_theme_guides_without_build_vocabulary(tmp_path: Path, fake_net) -> None:
    routes, _ = fake_net
    costume = f"{BASE}/topic/8-a-guide-to-costume-design/"
    routes[FORUM] = (
        f"<li class='ipsDataItem'><a href='{costume}'>t</a><time datetime='2024-01-01T00:00:00Z'></time>"
        f"<time datetime='2024-01-01T00:00:00Z'></time></li>"
    ).encode()
    routes[costume] = page("A guide to costume design", article("1", "Tailor", "2024-01-01T00:00:00Z", HOWTO)).encode()
    out = tmp_path / "community"
    assert main(["--forum", FORUM, "--out", str(out), "--canonical", str(tmp_path), "--delay", "0"]) == 0
    text = (out / "theme" / "a-guide-to-costume-design.md").read_text(encoding="utf-8")
    assert 'topic_tags: ["theme-rp"' in text and "discussion" not in text.split("---")[1]


def test_content_hash_is_stable_and_short() -> None:
    assert content_hash("a") == content_hash("a") and content_hash("a") != content_hash("b")
    assert len(content_hash("anything")) == 16


def test_merge_capture_keeps_reviewer_work_and_replaces_the_verbatim_section() -> None:
    post = Post("1", "Bopper", "", "2020-03-01T10:00:00Z", "", GUIDE_BODY)
    old = capture_markdown(post, TOPIC, "T", ["mechanics"], "2026-10-26", "old1")
    reviewed = (
        old.replace("trust: single-author-opinion", "trust: community-consensus")
        .replace('contradicts_data: "none"', 'contradicts_data: "Claim: 21 mph; data: 21.48"')
        .replace(_summary_stub(old), "- Reviewed: base fly speed is 21.48 mph.")
        + "\n## Useful points\n\n- distilled by the reviewer\n"
    )
    edited = Post("1", "Bopper", "", "2020-03-01T10:00:00Z", "", GUIDE_BODY.replace("21.48", "21.50"))
    fresh = capture_markdown(edited, TOPIC, "T", ["mechanics"], "2026-11-01", "new2")
    merged = merge_capture(reviewed, fresh)
    head, body = merged.split("---\n", 2)[1:]
    assert "trust: community-consensus" in head and 'contradicts_data: "Claim: 21 mph; data: 21.48"' in head
    assert 'content_hash: "new2"' in head and 'superseded_hash: "old1"' in head
    assert 'date_captured: "2026-11-01"' in head
    assert "- Reviewed: base fly speed is 21.48 mph." in body and "## Useful points" in body
    assert "| Base | 21.50 | 14.32 |" in body and "| Base | 21.48 | 14.32 |" not in body
    # The reviewer's section after the verbatim one survives in place.
    assert body.index("## Verbatim excerpt") < body.index("## Useful points")


def test_merge_capture_appends_the_verbatim_section_when_the_old_file_lacks_one() -> None:
    post = Post("1", "Bopper", "", "2020-03-01T10:00:00Z", "", GUIDE_BODY)
    fresh = capture_markdown(post, TOPIC, "T", ["mechanics"], "2026-11-01", "new2")
    merged = merge_capture('---\ntitle: "T"\n---\n\n# T\n\n## Summary\n\n- hand written\n', fresh)
    assert "- hand written" in merged and "## Verbatim excerpt" in merged and "superseded_hash" not in merged
    assert stored_hash(merged) == "new2"
    assert stored_hash("no frontmatter at all") == "" and stored_hash("---\nunterminated") == ""
    assert stored_hash('---\nbare line without a colon\ncontent_hash: "h"\n---\n') == "h"
    assert stored_hash("---\ncontent_hash: abc\n---\n") == "abc"  # hand-edited without quotes


def test_merge_capture_keeps_keys_and_tags_the_tool_does_not_own() -> None:
    post = Post("1", "Bopper", "", "2020-03-01T10:00:00Z", "", GUIDE_BODY)
    old = capture_markdown(post, TOPIC, "T", ["mechanics"], "2026-10-26", "h2")
    reviewed = old.replace(
        'content_hash: "h2"', 'content_hash: "h2"\nsuperseded_hash: "h1"\nreviewed_by: "local-capture"'
    )
    reviewed = reviewed.replace('topic_tags: ["mechanics"]', 'topic_tags: ["mechanics", "procs-ppm"]')
    # An unchanged hash: the earlier superseded hash and the reviewer's own key and tags stay.
    same = merge_capture(reviewed, capture_markdown(post, TOPIC, "T", ["mechanics"], "2026-11-01", "h2"))
    head = same.split("---\n", 2)[1]
    assert 'superseded_hash: "h1"' in head and 'reviewed_by: "local-capture"' in head
    assert 'topic_tags: ["mechanics", "procs-ppm"]' in head
    # A changed hash replaces the superseded one rather than listing two.
    moved = merge_capture(reviewed, capture_markdown(post, TOPIC, "T", ["mechanics"], "2026-11-02", "h3"))
    head = moved.split("---\n", 2)[1]
    assert head.count("superseded_hash") == 1 and 'superseded_hash: "h2"' in head


def test_capture_markdown_escapes_post_lines_that_would_read_as_headings() -> None:
    body = GUIDE_BODY + "<p>## Not a heading, just the author's text</p>"
    post = Post("1", "Bopper", "", "2020-03-01T10:00:00Z", "", body)
    text = capture_markdown(post, TOPIC, "T", ["mechanics"], "2026-10-26", "h")
    assert "\n\\## Not a heading" in text and "\n## Not a heading" not in text


def _summary_stub(capture: str) -> str:
    start = capture.index("## Summary") + len("## Summary")
    return capture[start : capture.index("## Verbatim excerpt")].strip()


def test_main_captures_guides_skips_chatter_and_resumes(tmp_path: Path, fake_net, capsys) -> None:
    routes, calls = fake_net
    chatter = f"{BASE}/topic/1-build-advice/"
    gone = f"{BASE}/topic/2-deleted/"  # no route: the fetch fails
    empty = f"{BASE}/topic/3-empty/"  # a page with no posts on it
    howto = f"{BASE}/topic/4-modding/"  # guide-shaped, but about the client, not the game's systems

    def row(topic: str, when: str) -> str:
        return f"<li class='ipsDataItem'><a href='{topic}'>t</a><time datetime='{when}'></time><time datetime='{when}'></time></li>"

    # Three listing pages: the guide on page 1, the rest on page 2, page 3 unreachable.
    pagination = f"<ul class='ipsPagination' data-pages='3'><li><a href='{FORUM}page/2/'>2</a></li></ul>"
    routes[FORUM] = (pagination + row(TOPIC, "2020-03-02T10:00:00Z")).encode()
    routes[f"{FORUM}page/2/"] = "".join(
        [
            row(chatter, "2024-01-01T00:00:00Z"),
            row(gone, "2024-01-01T00:00:00Z"),
            row(empty, ""),
            row(howto, "2024-01-01T00:00:00Z"),
        ]
    ).encode()
    routes[TOPIC] = GUIDE_PAGE.encode()
    routes[chatter] = CHATTER_PAGE.encode()
    routes[empty] = page("Nothing here").encode()
    routes[howto] = HOWTO_PAGE.encode()
    canonical = tmp_path / "canonical"
    (canonical / "powers" / "x" / "fly").mkdir(parents=True)
    out = tmp_path / "community"
    out.mkdir()
    (out / "guides.jsonl").write_text("\n", encoding="utf-8")  # a blank line in the ledger is ignored

    args = ["--forum", FORUM, "--out", str(out), "--canonical", str(canonical), "--delay", "0"]
    assert main(args) == 2  # one thread was unavailable: a re-run is wanted
    out_text, err_text = capsys.readouterr()
    assert "1 guide(s) written from 5 thread(s)" in out_text
    assert "no posts: 1, not build-related: 1, not guide-shaped: 1, unavailable: 1, written: 1" in out_text
    assert "E62: 2 thread(s) could not be fetched or parsed" in err_text  # the 404 and the empty page
    files = sorted(p.relative_to(out).as_posix() for p in out.rglob("*.md"))
    assert files == ["mechanics/movement-speed-a-quick-guide.md"]
    ledger = [
        json.loads(line) for line in (out / "guides.jsonl").read_text(encoding="utf-8").splitlines() if line.strip()
    ]
    assert {r["topic_url"]: r["reason"] for r in ledger} == {
        TOPIC: "",
        chatter: "not guide-shaped",
        gone: "unavailable",
        empty: "no posts",
        howto: "not build-related",
    }
    assert ledger[0]["content_hash"] == stored_hash((out / files[0]).read_text(encoding="utf-8"))

    fetched = calls.count(TOPIC)
    assert main(args) == 2
    # Unchanged threads are not refetched; the unfetched and the unparsed ones are looked at again.
    assert "0 guide(s) written from 2 thread(s)" in capsys.readouterr().out
    assert calls.count(TOPIC) == fetched

    # A reply moves the listing's last-post time: the thread is refetched, the file is not rewritten.
    routes[FORUM] = (pagination + row(TOPIC, "2020-03-03T10:00:00Z")).encode()
    before = (out / files[0]).read_bytes()
    assert main(args) == 2
    fetched += 1
    assert calls.count(TOPIC) == fetched
    assert (out / files[0]).read_bytes() == before
    assert "0 guide(s) written" in capsys.readouterr().out

    # --force refetches, but an unchanged opener rewrites nothing.
    before = (out / files[0]).read_bytes()
    assert main([*args, "--force"]) == 2
    assert calls.count(TOPIC) == fetched + 1
    assert (out / files[0]).read_bytes() == before
    assert "0 guide(s) written" in capsys.readouterr().out

    # The author edits the opener: the verbatim section is replaced, the reviewer's work kept.
    reviewed = (
        (out / files[0])
        .read_text(encoding="utf-8")
        .replace("trust: single-author-opinion", "trust: community-consensus")
    )
    (out / files[0]).write_text(reviewed, encoding="utf-8", newline="\n")
    routes[TOPIC] = GUIDE_PAGE.replace("21.48", "21.50").encode()
    assert main([*args, "--force"]) == 2
    refreshed = (out / files[0]).read_text(encoding="utf-8")
    assert (
        "trust: community-consensus" in refreshed and "superseded_hash" in refreshed and "| Base | 21.50 |" in refreshed
    )
    assert "1 guide(s) written" in capsys.readouterr().out


def test_main_tags_discussion_threads_retries_timeouts_and_flushes_per_thread(
    tmp_path: Path, fake_net, monkeypatch, capsys
) -> None:
    routes, calls = fake_net
    showcase = f"{BASE}/topic/7-post-your-favourite-builds/"
    gone = f"{BASE}/topic/2-timed-out/"
    routes[FORUM] = (
        f"<li class='ipsDataItem'><a href='{showcase}'>t</a><time datetime='2024-01-01T00:00:00Z'></time>"
        f"<time datetime='2024-01-01T00:00:00Z'></time></li>"
        f"<li class='ipsDataItem'><a href='{gone}'>t</a><time datetime='2024-01-01T00:00:00Z'></time>"
        f"<time datetime='2024-01-01T00:00:00Z'></time></li>"
        f"<li class='ipsDataItem'><a href='{TOPIC}'>t</a><time datetime='2020-03-02T10:00:00Z'></time>"
        f"<time datetime='2020-03-02T10:00:00Z'></time></li>"
    ).encode()
    routes[showcase] = page(
        "Post your favourite builds", article("1", "Host", "2024-01-01T00:00:00Z", GUIDE_BODY)
    ).encode()
    routes[TOPIC] = GUIDE_PAGE.encode()
    out = tmp_path / "community"
    rows_before_second_fetch: list[int] = []
    original_get = scrape.Fetcher.get

    def counting_get(self: scrape.Fetcher, url: str) -> str | None:
        if url == gone:  # second thread: the first thread's ledger row must already be on disk
            rows_before_second_fetch.append(len((out / "guides.jsonl").read_text(encoding="utf-8").splitlines()))
        return original_get(self, url)

    monkeypatch.setattr(scrape.Fetcher, "get", counting_get)
    args = ["--forum", FORUM, "--out", str(out), "--canonical", str(tmp_path), "--delay", "0"]
    assert main(args) == 2
    assert rows_before_second_fetch == [1]
    showcase_file = next(out.rglob("post-your-favourite-builds.md"))
    assert 'topic_tags: ["mechanics", "discussion"' in showcase_file.read_text(encoding="utf-8")
    assert "theme-rp" not in showcase_file.read_text(encoding="utf-8")

    # The timed-out thread is retried on the next run; the settled ones are not.
    fetched_gone, fetched_topic = calls.count(gone), calls.count(TOPIC)
    routes[gone] = page("Back", article("1", "Late", "2024-01-01T00:00:00Z", "<p>short</p>")).encode()
    assert main(args) == 0  # the retried thread answered this time
    assert calls.count(gone) == fetched_gone + 1 and calls.count(TOPIC) == fetched_topic
    assert "0 guide(s) written from 1 thread(s)" in capsys.readouterr().out


def test_main_stops_after_a_streak_of_unfetchable_threads(tmp_path: Path, fake_net, capsys, monkeypatch) -> None:
    routes, calls = fake_net
    monkeypatch.setattr("scripts.forum_scrape.guides.BREAKER_STREAK", 3)
    dead = [f"{BASE}/topic/{n}-dead/" for n in range(1, 6)]  # none routed: every fetch fails
    routes[FORUM] = "".join(
        f"<li class='ipsDataItem'><a href='{t}'>t</a><time datetime='2024-01-01T00:00:00Z'></time>"
        f"<time datetime='2024-01-01T00:00:00Z'></time></li>"
        for t in [*dead, TOPIC]
    ).encode()
    routes[TOPIC] = GUIDE_PAGE.encode()
    out = tmp_path / "community"
    assert main(["--forum", FORUM, "--out", str(out), "--canonical", str(tmp_path), "--delay", "0"]) == 2
    err = capsys.readouterr().err
    assert "E63: 3 consecutive threads" in err and "E62" in err
    assert calls.count(dead[3]) == 0 and calls.count(TOPIC) == 0  # stopped after the third failure
    rows = [json.loads(line) for line in (out / "guides.jsonl").read_text(encoding="utf-8").splitlines()]
    assert len(rows) == 3  # the three examined threads are on disk for the re-run


def test_main_reads_an_archived_board_with_its_own_source_and_era_tag(tmp_path: Path, fake_net) -> None:
    routes, _ = fake_net  # the archive serves no robots.txt: no route for it
    archive = "https://forumarchive.cityofheroes.dev"
    forum = f"{archive}/index.php?/forum/44-player-guides/"
    topic = f"{archive}/index.php?/topic/1114-snipefus-definitive-guide-to-energy-blasters/"
    routes[forum] = (
        f"<li class='ipsDataItem'><a href='{topic}'>t</a><time datetime='2005-10-03T04:35:00Z'></time>"
        f"<time datetime='2012-10-21T19:23:00Z'></time></li>"
    ).encode()
    routes[topic] = page(
        "Snipefu's Definitive Guide to Energy Blasters", article("1", "Snipefu", "2005-10-03T04:35:00Z", GUIDE_BODY)
    ).encode()
    out = tmp_path / "pre-sunset"
    args = [
        "--forum",
        forum,
        "--base",
        archive,
        "--tag",
        "pre-sunset",
        "--out",
        str(out),
        "--canonical",
        str(tmp_path),
        "--delay",
        "0",
    ]
    assert main(args) == 0
    text = next(out.rglob("*.md")).read_text(encoding="utf-8")
    assert 'source: "forumarchive.cityofheroes.dev"' in text
    assert 'topic_tags: ["archetypes/blaster"' not in text  # the folder tag is the archetype name, not a path
    assert 'topic_tags: ["blaster", "pre-sunset"' in text
    assert 'date_posted: "2005-10-03"' in text


ARCHIVE_FIXTURE = Path(__file__).parent / "fixtures" / "archive_topic_sample.html"


def test_main_captures_a_real_archived_thread(tmp_path: Path, fake_net) -> None:
    """The archive's own markup, saved from one thread, gives the right title, author, date and body."""
    routes, _ = fake_net
    archive = "https://forumarchive.cityofheroes.dev"
    forum = f"{archive}/index.php?/forum/44-player-guides/"
    topic = f"{archive}/index.php?/topic/141229-clawsfire-brute-build/"
    routes[forum] = (
        f"<li class='ipsDataItem'><a href='{topic}'>t</a><time datetime='2012-08-13T15:30:00Z'></time>"
        f"<time datetime='2012-08-20T00:00:00Z'></time></li>"
    ).encode()
    page_html = ARCHIVE_FIXTURE.read_text(encoding="utf-8")
    routes[topic] = page_html.encode()
    (opener,) = iter_posts(page_html)
    assert opener.author == "Super_E_Man"
    assert opener.author_profile == f"{archive}/index.php?/profile/29433-super_e_man/"
    out = tmp_path / "pre-sunset"
    args = ["--forum", forum, "--base", archive, "--tag", "pre-sunset", "--out", str(out)]
    assert main([*args, "--canonical", str(tmp_path), "--delay", "0"]) == 0
    (capture,) = out.rglob("*.md")
    text = capture.read_text(encoding="utf-8")
    assert capture.relative_to(out).as_posix() == "archetypes/brute/claws-fire-brute-build.md"
    assert 'title: "Claws/Fire Brute build"' in text and 'authors: ["Super_E_Man"]' in text
    assert 'date_posted: "2012-08-13"' in text and 'source: "forumarchive.cityofheroes.dev"' in text
    assert "### Post 1 — Super_E_Man, 2012-08-13" in text and text.count("\n- ") > 50  # the build's slot lines


def test_main_names_a_colliding_title_by_topic_id_and_leaves_hand_made_captures(
    tmp_path: Path, fake_net, capsys
) -> None:
    routes, _ = fake_net
    twin = f"{BASE}/topic/20000-movement-speed-a-quick-guide/"  # another thread, the same title
    routes[FORUM] = (
        f"<li class='ipsDataItem'><a href='{TOPIC}'>t</a><time datetime='2024-01-01T00:00:00Z'></time>"
        f"<time datetime='2024-01-01T00:00:00Z'></time></li>"
        f"<li class='ipsDataItem'><a href='{twin}'>t</a><time datetime='2024-01-02T00:00:00Z'></time>"
        f"<time datetime='2024-01-02T00:00:00Z'></time></li>"
    ).encode()
    routes[TOPIC] = GUIDE_PAGE.encode()
    routes[twin] = GUIDE_PAGE.replace("21.48", "22.00").encode()
    out = tmp_path / "community"
    args = ["--forum", FORUM, "--out", str(out), "--canonical", str(tmp_path), "--delay", "0"]
    assert main(args) == 0
    first = (out / "mechanics" / "movement-speed-a-quick-guide.md").read_text(encoding="utf-8")
    second = (out / "mechanics" / "movement-speed-a-quick-guide-20000.md").read_text(encoding="utf-8")
    assert f'url: "{TOPIC}"' in first and "21.48" in first
    assert f'url: "{twin}"' in second and "22.00" in second
    assert "2 guide(s) written" in capsys.readouterr().out

    # A hand-made capture (no content_hash) at a thread's path is never overwritten.
    manual = '---\ntitle: "Movement Speed"\nurl: "' + TOPIC + '"\n---\n\n# Movement Speed\n\n### Post 2 — Reader\n'
    (out / "mechanics" / "movement-speed-a-quick-guide.md").write_text(manual, encoding="utf-8")
    assert main([*args, "--force"]) == 0
    out_text, err_text = capsys.readouterr()
    assert (out / "mechanics" / "movement-speed-a-quick-guide.md").read_text(encoding="utf-8") == manual
    assert "E69: mechanics/movement-speed-a-quick-guide.md has no content_hash" in err_text
    assert "kept manual capture: 1" in out_text
    # A capture whose url line was hand-edited without quotes still names its own thread.
    unquoted = first.replace(f'url: "{TOPIC}"', f"url: {TOPIC}")
    (out / "mechanics" / "movement-speed-a-quick-guide.md").write_text(unquoted, encoding="utf-8")
    assert main([*args, "--force"]) == 0
    assert (out / "mechanics" / "movement-speed-a-quick-guide.md").read_text(encoding="utf-8") == unquoted
    assert "0 guide(s) written" in capsys.readouterr().out  # recognised as its own file, unchanged


def test_reclassify_moves_only_captures_whose_folder_changes_and_fetches_nothing(
    tmp_path: Path, fake_net, capsys, monkeypatch
) -> None:
    routes, calls = fake_net
    routes[FORUM] = (
        f"<li class='ipsDataItem'><a href='{TOPIC}'>t</a><time datetime='2024-01-01T00:00:00Z'></time>"
        f"<time datetime='2024-01-01T00:00:00Z'></time></li>"
    ).encode()
    routes[TOPIC] = GUIDE_PAGE.encode()
    out = tmp_path / "community"
    capture_args = ["--forum", FORUM, "--out", str(out), "--canonical", str(tmp_path), "--delay", "0"]
    # An older classifier that read "speed" as a theme word sends the guide to theme/.
    monkeypatch.setattr("scripts.forum_scrape.guides.THEME_TITLE", re.compile(r"\bspeed\b", re.I))
    assert main(capture_args) == 0
    old = out / "theme" / "movement-speed-a-quick-guide.md"
    assert 'topic_tags: ["theme-rp"' in old.read_text(encoding="utf-8")
    rows = [json.loads(line) for line in (out / "guides.jsonl").read_text(encoding="utf-8").splitlines()]
    assert rows[-1]["section"] == "44-guides"  # the ledger names the section a thread came from
    reviewed = old.read_text(encoding="utf-8") + "\n## Useful points\n\n- kept by the reviewer\n"
    old.write_text(reviewed, encoding="utf-8", newline="\n")
    # A capture sitting under an archetype from its section, in a row written before sections were recorded.
    brute = out / "archetypes" / "brute" / "fire-fire-notes.md"
    brute.parent.mkdir(parents=True)
    brute.write_text(
        '---\ntitle: "Fire/Fire notes"\nurl: "u2"\ntopic_tags: ["brute"]\ncontent_hash: "h"\n---\n', encoding="utf-8"
    )
    (out / "nested").mkdir()
    (out / "nested" / "guides.jsonl").write_text("", encoding="utf-8")  # another capture root: left alone
    (out / "nested" / "speed-run.md").write_text('---\ntitle: "x"\ncontent_hash: "h"\n---\n', encoding="utf-8")
    (out / "hand.md").write_text('---\ntitle: "Movement speed notes"\n---\n', encoding="utf-8")  # no hash: skipped
    capsys.readouterr()

    monkeypatch.undo()
    before = len(calls)
    assert main(["--reclassify", "--out", str(out), "--canonical", str(tmp_path)]) == 0
    assert calls[before:] == []  # not even robots.txt
    assert "1 capture(s) moved, 1 unchanged" in capsys.readouterr().out
    new = out / "mechanics" / "movement-speed-a-quick-guide.md"
    assert not old.exists() and "- kept by the reviewer" in new.read_text(encoding="utf-8")
    assert 'topic_tags: ["mechanics"' in new.read_text(encoding="utf-8") and brute.exists()
    rows = [json.loads(line) for line in (out / "guides.jsonl").read_text(encoding="utf-8").splitlines()]
    assert rows[-1]["file"] == "mechanics/movement-speed-a-quick-guide.md"

    # A target already taken is named and the capture left where it is.
    monkeypatch.setattr("scripts.forum_scrape.guides.THEME_TITLE", re.compile(r"\bspeed\b", re.I))
    (out / "theme").mkdir(exist_ok=True)
    new.rename(out / "theme" / new.name)
    new.write_text("someone else's file\n", encoding="utf-8")
    monkeypatch.undo()
    assert main(["--reclassify", "--out", str(out), "--canonical", str(tmp_path)]) == 0
    assert "E70: mechanics/movement-speed-a-quick-guide.md already exists" in capsys.readouterr().err


def test_main_needs_a_forum_unless_it_reclassifies(tmp_path: Path) -> None:
    with pytest.raises(SystemExit):
        main(["--out", str(tmp_path)])


def test_main_reports_an_unreadable_listing_and_asks_for_a_re_run(tmp_path: Path, fake_net, capsys) -> None:
    assert main(["--forum", FORUM, "--out", str(tmp_path), "--canonical", str(tmp_path), "--delay", "0"]) == 2
    assert "E61" in capsys.readouterr().err

    # A later listing page that fails is named too, and the run still asks for a re-run.
    routes, _ = fake_net
    pagination = f"<ul class='ipsPagination' data-pages='2'><li><a href='{FORUM}page/2/'>2</a></li></ul>"
    routes[FORUM] = pagination.encode()
    assert main(["--forum", FORUM, "--out", str(tmp_path), "--canonical", str(tmp_path), "--delay", "0"]) == 2
    assert f"E65: listing page unavailable: {FORUM}page/2/" in capsys.readouterr().err
