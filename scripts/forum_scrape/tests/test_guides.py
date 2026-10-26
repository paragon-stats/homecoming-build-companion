"""Guide capture: shape detection, topic tags, placement, the capture file, and the crawl."""

import json
from pathlib import Path

from scripts.forum_scrape.guides import (
    BUILD_VOCAB_MIN_PER_100,
    GUIDE_MIN_WORDS,
    archetype_in,
    build_relevance,
    capture_markdown,
    classify,
    is_guide,
    main,
    powersets_from_canonical,
    render_html,
    structure_score,
    topic_tags,
)
from scripts.forum_scrape.scrape import Post

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
        post, TOPIC, "Movement Speed: A Quick Guide", ["mechanics", "recharge-endurance"], "2026-10-26"
    )
    head, body = text.split("---\n", 2)[1:]
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
    assert main(args) == 0
    assert "1 guide(s) captured from 5 thread(s)" in capsys.readouterr().out
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

    fetched = calls.count(TOPIC)
    assert main(args) == 0
    # Unchanged threads are not refetched; the one with no last-activity stamp is looked at again.
    assert "0 guide(s) captured from 1 thread(s)" in capsys.readouterr().out
    assert calls.count(TOPIC) == fetched

    assert main([*args, "--force"]) == 0
    assert calls.count(TOPIC) == fetched + 1


def test_main_reports_an_unreadable_listing(tmp_path: Path, fake_net, capsys) -> None:
    assert main(["--forum", FORUM, "--out", str(tmp_path), "--canonical", str(tmp_path), "--delay", "0"]) == 0
    assert "E61" in capsys.readouterr().err
