"""Pull MidsReborn builds out of Homecoming forum threads, to disk, with attribution.

What it collects, per post, in descending order of usefulness:

1. **Attached build files** — a post that attaches a ``.mbd``/``.mxd`` is the best case:
    the attachment *is* the loadable artifact, downloaded byte-for-byte, and a ``.mbd``
    is parsed as JSON on the way in so a truncated download cannot pass as a build.
2. **Share blocks** — ``|MBD;u;c;e;BASE64;|`` (current Mids) and ``|MxDz;u;c;e;HEX;|``
    (legacy), saved verbatim as ``.txt``. That text is exactly what Mids' *paste build
    data* accepts, so it needs no decoding to be usable.
3. **cohplanner.com planner links** — ``download.php?uc=&c=&a=&f=HEX&dc=<hex>``. Older
    Mids exports emitted one of these instead of (or beside) the block; the query string
    carries the same four header fields, so it is rebuilt into a share block.
4. **Readable forum exports** — the ``Level 1: Power -- Enh(A), Enh(3)`` listing, saved
    as ``.export.txt``. Not machine-loadable, but it is what many posts actually contain.

Both share-block formats are decoded on the way in, so a truncated or mangled paste is
caught here rather than when someone tries to load it. An ``.mbd`` block (base64 +
Brotli) also yields its decoded ``.mbd`` JSON as a second file beside the verbatim block
— the block is what was posted, the ``.mbd`` is what Mids opens. An ``.mxd`` block
(hex + zlib) inflates to the legacy binary buffer, which is not a file Mids opens, so
only the verbatim block is kept for it.

Sections are crawled concurrently, one worker thread per forum section, but the request
pacer is shared: ``--delay`` is the floor between *any* two requests across the whole
run, so ``--workers`` overlaps latency without multiplying load on the site.

Spoiler / "hidden" content needs no special handling: Invision renders
``[spoiler]``/``[hidden]`` server-side into ``div.ipsSpoiler_contents``, so a plain
fetch already has the build text inside it. What a plain fetch does *not* get is content
gated behind "reply to see this" or a login — those posts come back empty, by design.

Politeness, which is not optional here:

- ``robots.txt`` is fetched and honoured through :mod:`urllib.robotparser`. Topic pages
    are allowed; ``/search/``, ``/discover/`` and friends are not, which is why thread
    discovery walks forum listing pages rather than the site search.
- The site's ``Content-Signal`` is ``search=yes, ai-train=no, use=reference``. Every
    sidecar records the author and source URL so the copy on disk stays a *reference* to
    their post. Do not feed what this collects into model training.
- One request every ``--delay`` seconds (default 2) **in total**, workers or not, and the
    user agent says plainly what this is — a named developer tool on ``Python-urllib`` —
    rather than posing as a browser. If that identifier gets blocked, that is an answer,
    not an obstacle to route around.

Run it::

    python -m scripts.forum_scrape --topic https://forums.homecomingservers.com/topic/12345-x/
    python -m scripts.forum_scrape --forum https://forums.homecomingservers.com/forum/19-mastermind/ --pages 3
    python -m scripts.forum_scrape --all-forums --pages 5 --workers 8 --group-by-author 10 --out builds/
"""

import argparse
import base64
import binascii
import gzip
import html as html_mod
import json
import re
import shutil
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from collections import Counter
from collections.abc import Collection, Iterable, Iterator, Mapping, Sequence
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import asdict, dataclass, field
from datetime import UTC, datetime
from pathlib import Path
from typing import Any
from urllib.robotparser import RobotFileParser

import brotli

from coh_engine.buildfile.mbd import parse_mbd
from coh_engine.buildfile.mxd import classify, decode_mxd, decode_share_block

BASE = "https://forums.homecomingservers.com"
# Identify honestly as what this is: a named developer tool, run by a person, built on
# Python's own HTTP client — the string ends with the stdlib's real `Python-urllib/x.y`
# token. That matters in both directions. The site's robots.txt names and blocks the
# autonomous AI crawlers (ClaudeBot, GPTBot, CCBot, …); this is not one of them, and
# claiming to be one would be as wrong as the opposite dodge of posing as Chrome to slip
# past a filter. Override with --user-agent to add a contact address, which is good
# manners for anything beyond a handful of threads.
DEFAULT_UA = (
    f"coh-build-companion/0.1 (+operator-run developer tool; MidsReborn build archiver) "
    f"Python-urllib/{sys.version_info.major}.{sys.version_info.minor}"
)
LICENSE_NOTE = (
    "Posted publicly by the named author on forums.homecomingservers.com and copied here "
    "for personal reference with attribution. Not for redistribution or model training "
    "(site Content-Signal: ai-train=no, use=reference)."
)

# --- HTML shapes, all verified against live pages (see module docstring) ---
_ARTICLE = re.compile(r"<article\s[^>]*id=['\"]elComment_(\d+)['\"]", re.I)
_CONTENT = re.compile(r"<div\s[^>]*data-role=['\"]commentContent['\"][^>]*>", re.I)
_DIV_TAG = re.compile(r"<(/?)div\b", re.I)
_PROFILE = re.compile(r"""href=['\"](https://forums\.homecomingservers\.com/profile/(\d+)-[^'\"]*)['\"]""", re.I)
_PHOTO_TITLE = re.compile(r"""title=['\"]Go to (.+?)'s profile['\"]""", re.I)
_TIME = re.compile(r"""<time\s[^>]*datetime=['\"]([^'\"]+)['\"]""", re.I)
_TITLE = re.compile(r"<title>(.*?)</title>", re.I | re.S)
# The slug excludes "/" so a link to a topic's page 2 cannot be absorbed as if it were a
# separate thread. Listings normally append "#comments" to those, which the old pattern
# happened to reject, but relying on the fragment made it one markup change from
# harvesting every paginated thread twice.
_TOPIC_LINK = re.compile(r"""['\"](https://forums\.homecomingservers\.com/topic/(\d+)-[^'\"#?/]*/)['\"]""")
_LINE_BREAK = re.compile(r"<br\s*/?>|</(?:p|li|div|tr|h[1-6]|pre)\s*>", re.I)
_TAG = re.compile(r"<[^>]+>")

_SHARE_HEADER = re.compile(r"^\|(MxDz|MBD);(\d+);(\d+);(\d+);(HEX|BASE64);\|$")
_SHARE_PAYLOAD = re.compile(r"^\|([A-Za-z0-9+/=]+)\|?$")
_PLANNER = re.compile(
    r"""cohplanner\.com/mids/download\.php\?uc=(\d+)&(?:amp;)?c=(\d+)&(?:amp;)?a=(\d+)"""
    r"""&(?:amp;)?f=(HEX|BASE64)&(?:amp;)?dc=([A-Za-z0-9+/=]+)""",
    re.I,
)
_EXPORT_LINE = re.compile(r"^Level\s+\d+:\s+\S", re.M)
# Invision states the real page count on the pagination element and links its pages off
# the listing's canonical URL, which for an alias section is not the URL you requested.
_PAGINATION_COUNT = re.compile(r"""<ul[^>]*class=['\"]ipsPagination['\"][^>]*data-pages=['\"](\d+)['\"]""", re.I)
_PAGINATION_HREF = re.compile(
    r"""class=['\"]ipsPagination['\"].{0,4000}?href=['\"]"""
    r"""(https://forums\.homecomingservers\.com/forum/\d+-[^'\"]*?)/page/\d+/['\"]""",
    re.I | re.S,
)
# A forum section link, as opposed to its .xml feed or a /page/N/ variant of it.
_FORUM_LINK = re.compile(r"""['\"](https://forums\.homecomingservers\.com/forum/\d+-[a-z0-9-]+/)['\"]""", re.I)
# Sections where regular players cannot post, so no player build was ever going to be in
# them. Matched as substrings of the URL slug; override with --exclude-forum.
DEFAULT_FORUM_EXCLUDES = (
    "announcements",
    "patch-notes",
    "terms-of-service",
    "code-of-conduct",
    "privacy-policy",
    "maintenance-notices",
    "staging-patch-notes",
)
# Every worker appends to one index file.
_INDEX_LOCK = threading.Lock()
_PRINT_LOCK = threading.Lock()
# Attachment anchors carry href and data-fileext in either order, so they are matched
# on the whole tag and the two attributes pulled out of it separately.
_ATTACH_TAG = re.compile(r"""<a\s[^>]*class=['\"]ipsAttachLink[^'\"]*['\"][^>]*>""", re.I)
_ATTACH_HREF = re.compile(r"""href=['\"]([^'\"]+)['\"]""", re.I)
_ATTACH_EXT = re.compile(r"""data-fileext=['\"]([A-Za-z0-9]+)['\"]""", re.I)
_DISPOSITION = re.compile(r"""filename=['\"]?([^'\";]+)""", re.I)
BUILD_EXTENSIONS = ("mbd", "mxd")
# The carriers a run can ask for. "mbd" and "mxd" are the two build formats — current and
# legacy, both of which MidsReborn opens — and "text" is the readable forum export, which
# is no format at all but is what many posts actually contain.
ALL_FORMATS = ("mbd", "mxd", "text")
_TOPIC_LEDGER = "topics.jsonl"
_SECTION_LEDGER = "section_sizes.jsonl"
# One row of a forum listing, which pairs a topic with its started/last-posted times.
_LISTING_ROW = re.compile(r"(?=<li[^>]*class=['\"][^'\"]*ipsDataItem)", re.I)
# Mids wraps share-block payloads at this width; matching it keeps a rebuilt block
# byte-comparable with one Mids itself would have emitted.
_WRAP = 67


@dataclass(frozen=True, slots=True)
class Post:
    """One forum post: who wrote it, when, and its rendered body as plain text."""

    comment_id: str
    author: str
    author_profile: str
    posted_at: str
    text: str
    html: str = field(default="", repr=False)


@dataclass
class Artifact:
    """One build found in one post, plus everything needed to credit its author."""

    kind: str  # "share-block" | "planner-link" | "forum-export" | "attachment"
    chunk_format: str  # "mbd" | "mxd" | "text"
    author: str
    author_profile: str
    post_url: str
    posted_at: str
    topic_title: str
    topic_url: str
    fetched_at: str
    source: str = "forums.homecomingservers.com"
    license_note: str = LICENSE_NOTE
    decoded_ok: bool | None = None
    decode_error: str = ""
    attached_filename: str = ""
    suffix: str = ".txt"
    body: str = field(default="", repr=False)
    data: bytes | None = field(default=None, repr=False)
    # A share block that decodes yields a second file next to the verbatim one: the
    # loadable .mbd. Both are kept — the block is what was actually posted (provenance),
    # the .mbd is what Mids opens.
    decoded: bytes | None = field(default=None, repr=False)


# --- Pure extraction: no network, every one of these is unit-tested ---


def strip_html(fragment: str) -> str:
    """Render an HTML fragment to plain text, preserving line structure.

    Block-level closers and ``<br>`` become newlines so that a share block's
    ``|...|`` lines survive; everything else is dropped and entities are unescaped.
    Spoiler contents are ordinary nested markup, so they come through like any other
    text rather than needing a special case.
    """
    text = _LINE_BREAK.sub("\n", fragment)
    text = _TAG.sub("", text)
    text = html_mod.unescape(text)
    text = text.replace("\xa0", " ").replace("\r\n", "\n").replace("\r", "\n")
    lines = [line.strip() for line in text.split("\n")]
    return "\n".join(lines)


def _extract_div(page: str, start: int) -> str:
    """Return the inner HTML of the ``<div>`` whose opening tag ends at ``start``.

    Counts nesting rather than matching the first ``</div>``: a post that wraps its
    build in a spoiler has nested divs, and the naive match would truncate the build
    at the spoiler's close.
    """
    depth = 1
    pos = start
    while True:
        match = _DIV_TAG.search(page, pos)
        if match is None:
            return page[start:]
        depth += -1 if match.group(1) else 1
        if depth == 0:
            return page[start : match.start()]
        pos = match.end()


def iter_posts(page: str) -> Iterator[Post]:
    """Yield one :class:`Post` per ``<article id='elComment_N'>`` on a topic page."""
    bounds = [(m.group(1), m.start()) for m in _ARTICLE.finditer(page)]
    for index, (comment_id, start) in enumerate(bounds):
        end = bounds[index + 1][1] if index + 1 < len(bounds) else len(page)
        article = page[start:end]

        profile = _PROFILE.search(article)
        photo = _PHOTO_TITLE.search(article)
        author = photo.group(1) if photo else ""
        if not author and profile:
            # Fall back to the profile slug ("6090-greenbone" -> "greenbone").
            author = urllib.parse.unquote(profile.group(1).rstrip("/").rsplit("-", 1)[-1])
        posted = _TIME.search(article)

        content = _CONTENT.search(article)
        body = _extract_div(article, content.end()) if content else ""
        yield Post(
            comment_id=comment_id,
            author=html_mod.unescape(author) or "unknown",
            author_profile=profile.group(1) if profile else "",
            posted_at=posted.group(1) if posted else "",
            text=strip_html(body),
            html=body,
        )


def find_share_blocks(text: str) -> list[str]:
    """Return every ``|MxDz;…|`` / ``|MBD;…|`` share block in a post's plain text.

    A block is a header line followed by ``|payload|`` lines; the first line that is
    neither ends it (the trailing dashed banner, or ordinary prose). Blank lines are
    skipped rather than treated as the end — a post that separates payload lines with
    ``<br />`` inside an indented ``<p>`` renders one between every pair of them.
    """
    lines = text.split("\n")
    blocks: list[str] = []
    index = 0
    while index < len(lines):
        if not _SHARE_HEADER.match(lines[index].strip()):
            index += 1
            continue
        collected = [lines[index].strip()]
        index += 1
        while index < len(lines):
            line = lines[index].strip()
            if line == "":
                index += 1
                continue
            if not _SHARE_PAYLOAD.match(line):
                break
            collected.append(line)
            index += 1
        if len(collected) > 1:
            blocks.append("\n".join(collected))
    return blocks


def find_planner_links(page_or_text: str) -> list[str]:
    """Rebuild share blocks from any cohplanner.com ``download.php`` links.

    The link's ``uc``/``c``/``a``/``f`` query fields are the share header's four
    values and ``dc`` is the payload, so the block is recoverable exactly.
    """
    blocks = []
    for uncompressed, compressed, encoded, encoding, payload in _PLANNER.findall(page_or_text):
        header = f"|{'MxDz' if encoding.upper() == 'HEX' else 'MBD'};{uncompressed};{compressed};{encoded};{encoding.upper()};|"
        wrapped = [f"|{payload[i : i + _WRAP]}|" for i in range(0, len(payload), _WRAP)]
        blocks.append("\n".join([header, *wrapped]))
    return blocks


def find_forum_export(text: str) -> str:
    """Return the post's readable Mids forum export, or "" if it does not contain one.

    Keyed on the export's one reliable signature — a run of ``Level N: <power>`` lines.
    Six is enough to clear posts that merely mention a level in prose.
    """
    matches = list(_EXPORT_LINE.finditer(text))
    if len(matches) < 6:
        return ""
    # The export's last power still has slot lines under it, so the tail is included —
    # but stopped at the share block that usually follows, or the export would swallow
    # the block's header and then classify as a (broken) share block itself.
    tail = text[matches[-1].start() : matches[-1].start() + 400]
    for index, line in enumerate(tail.split("\n")):
        if _SHARE_HEADER.match(line.strip()) or line.strip().startswith(("|---", "| Copy")):
            tail = "\n".join(tail.split("\n")[:index])
            break
    return (text[matches[0].start() : matches[-1].start()] + tail).strip()


def find_attachments(post_html: str) -> list[tuple[str, str]]:
    """Return ``(url, extension)`` for every attached ``.mbd``/``.mxd`` on a post.

    Plenty of posters skip the share block entirely and attach the build file. That is
    the best carrier of the four — an attached ``.mbd`` is the loadable artifact itself,
    no decoding involved — so images and other attachments are filtered out and these
    are kept.
    """
    found = []
    for tag in _ATTACH_TAG.findall(post_html):
        href = _ATTACH_HREF.search(tag)
        ext = _ATTACH_EXT.search(tag)
        if href and ext and ext.group(1).lower() in BUILD_EXTENSIONS:
            found.append((html_mod.unescape(href.group(1)), ext.group(1).lower()))
    return found


def check_attachment(data: bytes, extension: str) -> tuple[bool | None, str]:
    """Sanity-check a downloaded build file so a truncated or error page is not kept.

    A ``.mbd`` goes through the engine's own reader
    (:func:`coh_engine.buildfile.mbd.parse_mbd`), so "valid" means the same thing here as
    everywhere else in the repo rather than being a second, looser opinion. A ``.mxd`` is
    the legacy binary blob, which this layer does not attempt to interpret.
    """
    if extension != "mbd":
        return None, "legacy .mxd attachment stored as-is; not parsed"
    try:
        document = json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        return False, f"attachment is not valid .mbd JSON: {exc}"
    if not isinstance(document, dict) or "PowerEntries" not in document:
        return False, "attachment JSON has no PowerEntries"
    try:
        parse_mbd(document)
    except (ValueError, KeyError, TypeError) as exc:
        return False, f"attachment does not parse as a build: {exc}"
    return True, ""


def decode_mbd(text: str) -> bytes:
    """Decode a ``|MBD;…BASE64;|`` share block to its ``.mbd`` JSON bytes.

    Mirrors ``Compression.DecompressFromBase64`` (``Core/Utils/Compression.cs``):
    base64 -> Brotli -> UTF-8, with the header's compressed size checked against the
    decoded byte count the way ``BuildManager.LoadShareData`` does.

    This lives here rather than beside :func:`decode_mxd` in ``coh_engine`` on purpose:
    Brotli is a third-party wheel and the engine is deliberately stdlib-only. The
    envelope parsing is still the engine's :func:`decode_share_block`.

    Raises:
        ValueError: on wrong classification, invalid base64, a compressed-size
            mismatch, or a Brotli decompression failure.
    """
    if classify(text) != "mbd":
        raise ValueError("not an mbd share block")
    block = decode_share_block(text)
    try:
        raw = base64.b64decode(block.payload, validate=True)
    except binascii.Error as exc:
        raise ValueError(f"mbd payload is not valid base64: {exc}") from exc
    if len(raw) != block.compressed_size:
        raise ValueError(f"mbd compressed size mismatch: header {block.compressed_size}, actual {len(raw)}")
    try:
        decompressed: bytes = brotli.decompress(raw)
    except brotli.error as exc:
        raise ValueError(f"mbd brotli decompression failed: {exc}") from exc
    return decompressed


def check_block(block: str) -> tuple[str, bool | None, str, bytes | None]:
    """Classify a share block, prove it decodes, and hand back the decoded build.

    Both formats round-trip here — ``.mxd`` is hex + zlib, ``.mbd`` is base64 + Brotli —
    so a truncated or mangled paste is caught at collection time rather than when someone
    tries to load it. The fourth element is the decoded ``.mbd`` JSON when there is one;
    an ``.mxd`` inflates to the legacy binary buffer, which is not a file Mids opens, so
    only its integrity is reported and the verbatim block stays the artifact.
    """
    kind = classify(block)
    if kind == "mxd":
        try:
            decode_mxd(block)
        except ValueError as exc:
            return "mxd", False, str(exc), None
        return "mxd", True, "", None
    if kind == "mbd":
        try:
            decoded = decode_mbd(block)
        except ValueError as exc:
            return "mbd", False, str(exc), None
        ok, error = check_attachment(decoded, "mbd")
        return "mbd", ok, error, decoded if ok else None
    return kind or "unknown", False, "not a recognised share block", None


def slugify(value: str, limit: int = 60) -> str:
    """Reduce a title or author name to a safe, stable filename component."""
    cleaned = re.sub(r"[^A-Za-z0-9]+", "-", value).strip("-").lower()
    return (cleaned[:limit].rstrip("-")) or "unknown"


def topic_rows(listing_html: str) -> dict[str, str]:
    """Map each listed topic to when it was last posted in.

    A forum thread is not finished when it is crawled. A build thread from 2019 gets
    necroed — someone finds it, asks a follow-up, and a builder answers with a fresh
    build, which is exactly the request-and-answer pair worth having. A ledger that marks
    a thread "done" forever would never see any of it.

    The listing row carries two ``<time>`` elements, started and last-posted, so the
    freshness check costs nothing: the page has already been fetched to find the topics at
    all. Returns ``{}`` entries with an empty string where a row has no second timestamp,
    which reads downstream as "unknown, so look again" rather than as "unchanged".
    """
    rows: dict[str, str] = {}
    for chunk in _LISTING_ROW.split(listing_html)[1:]:
        match = _TOPIC_LINK.search(chunk)
        if not match:
            continue
        stamps = _TIME.findall(chunk)
        rows.setdefault(match.group(1), stamps[-1] if len(stamps) > 1 else "")
    return rows


def page_title(page: str) -> str:
    """The thread title, taken from ``<title>`` with the site suffix trimmed."""
    match = _TITLE.search(page)
    if not match:
        return ""
    title = html_mod.unescape(match.group(1)).strip()
    return re.sub(r"\s*-\s*[^-]+\s*-\s*Homecoming$", "", title).strip()


def last_page_number(page: str) -> int:
    """The page count Invision declares for this listing, or 1 if it does not paginate.

    Read off the pagination element's ``data-pages`` rather than by scanning the page
    for the highest ``/page/N/`` link. The scan over-reports — any linked topic or
    sub-forum contributes its own pagination to the match set — and since an
    out-of-range page silently re-serves page 1 instead of 404ing, the over-report is
    invisible: it just spends a request per phantom page.
    """
    declared = _PAGINATION_COUNT.search(page)
    return int(declared.group(1)) if declared else 1


def listing_base(page: str, requested_url: str) -> str:
    """The URL the listing's own pagination links use, which is not always the one asked for.

    Some sections are aliases: ``/forum/76-mids-reborn-hero-designer/`` renders forum 74's
    topics and paginates as ``/forum/74-forum/page/N/``. Appending ``/page/2/`` to the
    requested URL there lands out of range and re-serves page 1, so the crawl silently
    stops after 25 topics. Following the pagination's own base fixes it; when there is no
    pagination the requested URL stands.
    """
    match = _PAGINATION_HREF.search(page)
    return match.group(1) if match else requested_url.rstrip("/")


def discover_forums(index_html: str, exclude: Iterable[str] = DEFAULT_FORUM_EXCLUDES) -> list[str]:
    """Return every forum section linked from the board index, minus the excluded ones.

    Guests cannot see posting permissions — Invision renders no "Start new topic"
    control for them either way — so a section is filtered on what it is rather than on
    a permission bit that is not in the HTML: the administrative, staff-post-only
    sections named in :data:`DEFAULT_FORUM_EXCLUDES` (announcements, patch notes, the
    policy documents), plus anything the caller names. Sections that turn out to hold no
    topics are skipped at crawl time by :func:`crawl_forum` instead, which catches the
    container-only ones without having to list them.
    """
    patterns = [p.lower() for p in exclude]
    seen: dict[str, None] = {}
    for url in _FORUM_LINK.findall(index_html):
        slug = url.rstrip("/").rsplit("/", 1)[-1].lower()
        if any(pattern in slug for pattern in patterns):
            continue
        seen.setdefault(url, None)
    return list(seen)


# --- Network + orchestration ---


class Fetcher:
    """Rate-limited, robots-respecting GET, shared by every worker thread.

    The pacer is **global**, not per thread: ``delay`` is the minimum gap between the
    start of any two requests across the whole crawl, however many workers are running.
    That is the point — threads exist to overlap network latency and parsing, not to
    multiply the request rate. Adding workers fills the gaps; the only knob that changes
    the load on the site is ``delay``.
    """

    def __init__(self, user_agent: str, delay: float) -> None:
        self.user_agent = user_agent
        self.delay = delay
        self._last = 0.0
        self._pace_lock = threading.Lock()
        self._robots = RobotFileParser()
        self._robots.parse(self._raw(f"{BASE}/robots.txt")[0].decode("utf-8", "replace").splitlines())

    def _pace(self) -> None:
        """Block until this thread is allowed to start the next request."""
        with self._pace_lock:
            wait = self.delay - (time.monotonic() - self._last)
            if wait > 0:
                time.sleep(wait)
            # Stamped before the request, so the gap is measured start-to-start and a
            # slow response does not let the other threads bunch up behind it.
            self._last = time.monotonic()

    def _raw(self, url: str) -> tuple[bytes, str]:
        """Fetch ``url``, returning its decompressed body and any attachment filename."""
        self._pace()
        request = urllib.request.Request(url, headers={"User-Agent": self.user_agent, "Accept-Encoding": "gzip"})
        with urllib.request.urlopen(request, timeout=30) as response:
            body: bytes = response.read()
            if response.headers.get("Content-Encoding") == "gzip":
                body = gzip.decompress(body)
            disposition = response.headers.get("Content-Disposition") or ""
        self._last = time.monotonic()
        filename = _DISPOSITION.search(disposition)
        return body, filename.group(1).strip() if filename else ""

    def fetch(self, url: str) -> tuple[bytes, str] | None:
        """Fetch ``url`` as bytes, or ``None`` if robots.txt disallows it or it errors."""
        if not self._robots.can_fetch(self.user_agent, url):
            print(f"skip (robots.txt disallows): {url}", file=sys.stderr)
            return None
        try:
            return self._raw(url)
        except (urllib.error.URLError, TimeoutError) as exc:
            print(f"E01: fetch failed for {url}: {exc}", file=sys.stderr)
            return None

    def get(self, url: str) -> str | None:
        """Fetch ``url`` and decode it as a page of text."""
        result = self.fetch(url)
        return None if result is None else result[0].decode("utf-8", "replace")


def _artifact(
    context: tuple[Post, str, str, str],
    kind: str,
    chunk_format: str,
    body: str,
    ok: bool | None = None,
    error: str = "",
    suffix: str = ".txt",
    data: bytes | None = None,
    attached_filename: str = "",
    decoded: bytes | None = None,
) -> Artifact:
    """Stamp one found build with the post it came from, so credit travels with it."""
    post, topic_url, title, now = context
    return Artifact(
        kind=kind,
        chunk_format=chunk_format,
        decoded_ok=ok,
        decode_error=error,
        suffix=suffix,
        data=data,
        decoded=decoded,
        attached_filename=attached_filename,
        body=body,
        author=post.author,
        author_profile=post.author_profile,
        post_url=f"{topic_url}#comment-{post.comment_id}",
        posted_at=post.posted_at,
        topic_title=title,
        topic_url=topic_url,
        fetched_at=now,
    )


def harvest_post(
    fetcher: Fetcher,
    context: tuple[Post, str, str, str],
    formats: Collection[str] = ALL_FORMATS,
) -> list[Artifact]:
    """Return the builds one post carries, in the formats asked for.

    Filtering happens before the download, not after: an attachment declares its extension
    in the listing markup, so a run that only wants the legacy format never spends a
    request fetching a ``.mbd``.
    """
    post = context[0]
    artifacts: list[Artifact] = []

    blocks = find_share_blocks(post.text)
    # Planner links live in an href, which strip_html discards — read the raw body.
    planner = [b for b in find_planner_links(post.html) if b not in blocks]
    for kind, found in (("share-block", blocks), ("planner-link", planner)):
        for block in found:
            chunk, ok, error, decoded = check_block(block)
            if chunk not in formats:
                continue
            artifacts.append(_artifact(context, kind, chunk, block, ok, error, decoded=decoded))

    for url, extension in find_attachments(post.html):
        if extension not in formats:
            continue
        downloaded = fetcher.fetch(url)
        if downloaded is None:
            continue
        payload, filename = downloaded
        ok, error = check_attachment(payload, extension)
        artifacts.append(
            _artifact(
                context,
                "attachment",
                extension,
                "",
                ok,
                error,
                suffix=f".{extension}",
                data=payload,
                attached_filename=filename,
            )
        )

    export = find_forum_export(post.text) if "text" in formats else ""
    if export:
        artifacts.append(_artifact(context, "forum-export", "text", export, suffix=".export.txt"))
    return artifacts


@dataclass(frozen=True, slots=True)
class TopicCoverage:
    """What a prior run did to one thread, and how fresh the thread was at the time."""

    formats: frozenset[str]
    last_activity: str  # the thread's last-post time when crawled; "" if not recorded


def load_topic_ledger(out_dir: Path) -> dict[str, TopicCoverage]:
    """What each thread has already been *asked* for, and how current that answer is.

    Keyed on what was requested rather than what was found, because those differ in a way
    that matters for resuming: a thread crawled for ``mbd`` alone and holding no ``mbd``
    build looks identical, in the artifacts, to one never crawled at all. Recording the
    request means a later ``--formats mxd`` run knows it still has work to do there, and a
    repeat ``--formats mbd`` run knows it does not.

    ``last_activity`` is what keeps a thread from being marked done forever — see
    :func:`topic_rows`. Rows written before that field existed carry an empty string,
    which means "unknown", and those threads get looked at once more so they can pick one
    up. Across runs the formats union and the newest activity wins.
    """
    path = out_dir / _TOPIC_LEDGER
    if not path.exists():
        return {}
    formats: dict[str, set[str]] = {}
    activity: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        row = json.loads(line)
        url = row["topic_url"]
        formats.setdefault(url, set()).update(row.get("formats_requested", []))
        seen = row.get("last_activity", "")
        if seen > activity.get(url, ""):
            activity[url] = seen
    return {url: TopicCoverage(frozenset(f), activity.get(url, "")) for url, f in formats.items()}


def is_covered(coverage: TopicCoverage | None, wanted: Collection[str], last_activity: str) -> bool:
    """Whether a thread can be skipped: same formats, and nothing posted since.

    Skipping needs both halves to be *known*. An unknown timestamp on either side — a
    ledger row from before freshness was tracked, or a listing row with no last-post
    time — means look again. Guessing "unchanged" there would silently freeze a thread
    at whatever it held the first time it was seen.
    """
    if coverage is None or not set(wanted) <= coverage.formats:
        return False
    if not coverage.last_activity or not last_activity:
        return False
    return last_activity <= coverage.last_activity


def record_topic(
    out_dir: Path,
    topic_url: str,
    formats: Collection[str],
    artifacts: Sequence[Artifact],
    last_activity: str = "",
) -> None:
    """Append this thread's coverage to the ledger: what was asked for, what turned up."""
    _append_ledger(
        out_dir,
        _TOPIC_LEDGER,
        {
            "topic_url": topic_url,
            "formats_requested": sorted(formats),
            "formats_found": sorted({a.chunk_format for a in artifacts}),
            "artifacts": len(artifacts),
            "last_activity": last_activity,
            "fetched_at": datetime.now(UTC).isoformat(),
        },
    )


def record_section(out_dir: Path, forum_url: str, pages: int, topics: int) -> None:
    """Record how big a section is: its listing page count and how many threads it holds.

    Written as soon as the listing walk finishes and before any thread is fetched, so a
    run still in progress can be measured against it — the thread ledger is the numerator,
    this is the denominator. Without this row the only honest answer to "how far along is
    it?" is a live request to the site, which is the wrong way to ask.

    Both numbers are kept because they answer different questions: ``pages`` is what the
    crawl paid to enumerate the section, ``topics`` is what it still has to work through.

    Kept in its own file rather than mixed into the thread ledger. The two are read for
    different reasons at different times, and a per-thread file whose line count is not a
    thread count invites exactly the miscount this row exists to prevent.
    """
    _append_ledger(
        out_dir,
        _SECTION_LEDGER,
        {
            "forum_url": forum_url,
            "pages": pages,
            "topics": topics,
            "fetched_at": datetime.now(UTC).isoformat(),
        },
    )


def _append_ledger(out_dir: Path, name: str, row: dict[str, Any]) -> None:
    """Append one row to a ledger file, under the write lock."""
    out_dir.mkdir(parents=True, exist_ok=True)
    with _INDEX_LOCK, (out_dir / name).open("a", encoding="utf-8") as ledger:
        ledger.write(json.dumps(row) + "\n")


def harvest_topic(
    fetcher: Fetcher,
    topic_url: str,
    formats: Collection[str] = ALL_FORMATS,
) -> list[Artifact]:
    """Walk every page of one topic and return the builds found in it."""
    first = fetcher.get(topic_url)
    if first is None:
        return []
    title = page_title(first)
    pages = [first]
    for number in range(2, last_page_number(first) + 1):
        page = fetcher.get(f"{topic_url.rstrip('/')}/page/{number}/")
        if page is not None:
            pages.append(page)

    now = datetime.now(UTC).isoformat()
    return [
        artifact
        for page in pages
        for post in iter_posts(page)
        for artifact in harvest_post(fetcher, (post, topic_url, title, now), formats)
    ]


def write_artifacts(artifacts: list[Artifact], out_dir: Path, force: bool) -> int:
    """Write each build plus its attribution sidecar; return how many were new."""
    written = 0
    index_path = out_dir / "index.jsonl"
    out_dir.mkdir(parents=True, exist_ok=True)
    # One post can carry several builds (a block plus the planner link for a second
    # version, say); number them so the later ones are not mistaken for duplicates.
    seen_stems: dict[str, int] = {}
    for artifact in artifacts:
        topic_id = artifact.topic_url.rstrip("/").rsplit("/", 1)[-1]
        comment_id = artifact.post_url.rsplit("-", 1)[-1]
        folder = out_dir / slugify(topic_id, 80)
        folder.mkdir(parents=True, exist_ok=True)
        suffix = artifact.suffix
        stem = f"{comment_id}-{slugify(artifact.author, 30)}"
        seen_stems[stem + suffix] = seen_stems.get(stem + suffix, 0) + 1
        if seen_stems[stem + suffix] > 1:
            stem = f"{stem}-{seen_stems[stem + suffix]}"
        body_path = folder / f"{stem}{suffix}"
        if body_path.exists() and not force:
            continue
        if artifact.data is None:
            body_path.write_text(artifact.body + "\n", encoding="utf-8")
        else:
            body_path.write_bytes(artifact.data)
        meta = {k: v for k, v in asdict(artifact).items() if k not in ("body", "data", "decoded")}
        meta["file"] = body_path.name
        if artifact.decoded is not None:
            decoded_path = body_path.with_suffix(".mbd")
            decoded_path.write_bytes(artifact.decoded)
            meta["decoded_file"] = decoded_path.name
        (folder / f"{stem}{suffix.removesuffix('.txt')}.json").write_text(
            json.dumps(meta, indent=2) + "\n", encoding="utf-8"
        )
        # Threads crawl one forum each, so topic folders never collide — but every
        # worker appends to the one index, which has to be serialised.
        with _INDEX_LOCK, index_path.open("a", encoding="utf-8") as index:
            index.write(json.dumps({**meta, "path": str(body_path)}) + "\n")
        written += 1
    return written


@dataclass(frozen=True, slots=True)
class ForumResult:
    """What one forum section yielded, reported back to the main thread when it is done."""

    forum_url: str
    topics: int
    found: int
    written: int
    skipped_reason: str = ""


def crawl_forum(
    fetcher: Fetcher,
    forum_url: str,
    pages: int,
    out_dir: Path,
    force: bool,
    formats: Collection[str] = ALL_FORMATS,
    already: Mapping[str, TopicCoverage] | None = None,
) -> ForumResult:
    """Walk one forum section's listing pages and harvest every topic on them.

    This is the unit of work a worker thread owns end to end. A section whose listing
    pages carry no topics at all — a category container, or a policy section holding a
    single pinned article — is reported as skipped rather than crawled further.

    ``already`` is the topic ledger from prior runs. A thread whose requested formats have
    all been asked for before is skipped without fetching it, so a second pass for a
    different format only pays for the threads it has not covered.
    """
    first = fetcher.get(forum_url)
    if first is None:
        return ForumResult(forum_url, 0, 0, 0, "listing unreachable")
    targets: dict[str, str] = dict(topic_rows(first))

    # Page URLs come from the listing's own pagination, not from appending to the URL we
    # were handed: an alias section paginates under its canonical id, and asking the
    # alias for page 2 silently re-serves page 1 rather than 404ing. Both the base and
    # the count are taken from the pagination element for the same reason.
    base = listing_base(first, forum_url)
    page_count = min(pages, last_page_number(first))
    for number in range(2, page_count + 1):
        listing = fetcher.get(f"{base}/page/{number}/")
        if listing is None:
            continue
        for url, seen_at in topic_rows(listing).items():
            targets.setdefault(url, seen_at)

    if not targets:
        return ForumResult(forum_url, 0, 0, 0, "no topics visible")

    record_section(out_dir, forum_url, page_count, len(targets))
    wanted = set(formats)
    ledger = already or {}
    found = written = skipped = 0
    for topic_url, last_activity in targets.items():
        if not force and is_covered(ledger.get(topic_url), wanted, last_activity):
            skipped += 1
            continue
        artifacts = harvest_topic(fetcher, topic_url, formats)
        found += len(artifacts)
        written += write_artifacts(artifacts, out_dir, force)
        record_topic(out_dir, topic_url, wanted, artifacts, last_activity)
    note = f"{skipped} topic(s) unchanged since last crawl" if skipped else ""
    return ForumResult(forum_url, len(targets), found, written, note)


def crawl_forums(
    fetcher: Fetcher,
    forum_urls: list[str],
    pages: int,
    out_dir: Path,
    force: bool,
    workers: int,
    formats: Collection[str] = ALL_FORMATS,
) -> list[ForumResult]:
    """Run the forum queue across ``workers`` threads, one section per thread at a time.

    ``ThreadPoolExecutor`` is the queue: every section is submitted up front and workers
    pull the next one as they finish, so a huge section does not leave the others idle.
    The shared :class:`Fetcher` keeps the combined request rate at one per ``--delay``
    no matter how many threads are running.

    The topic ledger is read once here and shared: every worker needs the same view of
    what earlier runs already covered, and re-reading it per section would be both slower
    and inconsistent as the file grows underneath them.
    """
    already = load_topic_ledger(out_dir)
    results: list[ForumResult] = []
    with ThreadPoolExecutor(max_workers=max(1, workers), thread_name_prefix="forum") as pool:
        futures = {
            pool.submit(crawl_forum, fetcher, url, pages, out_dir, force, formats, already): url for url in forum_urls
        }
        for future in as_completed(futures):
            result = future.result()
            results.append(result)
            with _PRINT_LOCK:
                # A section with topics still reports its note (e.g. "12 already covered")
                # alongside the counts; only a section with nothing to crawl is a skip.
                if result.skipped_reason and not result.topics:
                    print(f"  skip  {result.skipped_reason:<24} {result.forum_url}")
                else:
                    note = f"  [{result.skipped_reason}]" if result.skipped_reason else ""
                    print(
                        f"{result.written:5d} new  {result.found:4d} found  "
                        f"{result.topics:3d} topics  {result.forum_url}{note}"
                    )
    return results


def write_section_report(out_dir: Path, results: list[ForumResult]) -> list[str]:
    """Record what each section yielded and return the slugs that yielded no builds.

    Which sections are worth crawling is a question only a crawl can answer, so this
    reports the measurement instead of guessing: a section that turned up zero builds is
    listed with its slug, ready to paste back as ``--exclude-forum``. Re-run it after a
    wide crawl and the exclusion list becomes evidence rather than a hunch.
    """
    out_dir.mkdir(parents=True, exist_ok=True)
    ranked = sorted(results, key=lambda r: (-r.found, r.forum_url))
    barren = [r.forum_url.rstrip("/").rsplit("/", 1)[-1] for r in ranked if r.found == 0]

    lines = [
        "# Builds by forum section",
        "",
        "Sections with 0 builds can be passed back as `--exclude-forum <slug>`.",
        "",
        "| Section | Topics | Builds found | Note |",
        "| --- | ---: | ---: | --- |",
    ]
    lines += [
        f"| {r.forum_url.rstrip('/').rsplit('/', 1)[-1]} | {r.topics} | {r.found} | {r.skipped_reason} |"
        for r in ranked
    ]
    (out_dir / "sections.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (out_dir / "sections.json").write_text(json.dumps([asdict(r) for r in ranked], indent=2) + "\n", encoding="utf-8")
    return barren


def read_index(out_dir: Path) -> list[dict[str, Any]]:
    """Load ``index.jsonl``, keeping one row per artifact path (the newest wins).

    The index is append-only across runs, so a ``--force`` re-scrape leaves stale rows
    behind it. Everything downstream reads through here rather than the raw file.
    """
    index_path = out_dir / "index.jsonl"
    if not index_path.exists():
        return []
    rows: dict[str, dict[str, Any]] = {}
    for line in index_path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            row = json.loads(line)
            rows[row["path"]] = row
    return list(rows.values())


def write_dedupe_report(out_dir: Path, dump_dir: Path | None) -> tuple[int, int]:
    """Collapse the index to distinct builds and write the repost ledger.

    Returns ``(distinct builds, duplicate postings collapsed)``. See
    :mod:`scripts.forum_scrape.dedupe` for why identity is computed from the build rather
    than from the file's bytes.
    """
    from scripts.forum_scrape.dedupe import dedupe

    rows = [r for r in read_index(out_dir) if r["kind"] != "forum-export"]
    records = dedupe(rows, dump_dir)
    collapsed = len(rows) - len(records)

    by_author = Counter(r.first_author for r in records)
    reposted = sorted((r for r in records if r.reposts), key=lambda r: -r.repost_count)

    (out_dir / "builds.jsonl").write_text(
        "".join(
            json.dumps(
                {
                    "identity": r.identity,
                    "first_author": r.first_author,
                    "first_posted_at": r.first_posted_at,
                    "first_post_url": r.first_post_url,
                    "path": r.path,
                    "repost_count": r.repost_count,
                    "reposts": [{"author": a, "post_url": u} for a, u in r.reposts],
                }
            )
            + "\n"
            for r in records
        ),
        encoding="utf-8",
    )

    lines = [
        "# Distinct builds, credited to the earliest posting",
        "",
        f"{len(rows)} postings collapsed to {len(records)} distinct builds "
        f"({collapsed} duplicate postings, {100 * collapsed / len(rows):.1f}%).",
        "",
        "Credit goes to the earliest post the crawl saw carrying each build. That is a",
        "best guess: an earlier posting may simply not have been crawled.",
        "",
        "## Authors by distinct build",
        "",
        "| Author | Distinct builds |",
        "| --- | ---: |",
        *(f"| {name} | {count} |" for name, count in by_author.most_common(100)),
        "",
        "## Most reposted builds",
        "",
        "| Build | First posted by | Reposts |",
        "| --- | --- | ---: |",
        *(f"| {r.identity} | {r.first_author} | {r.repost_count} |" for r in reposted[:50]),
    ]
    (out_dir / "builds.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return len(records), collapsed


def author_counts(rows: list[dict[str, Any]]) -> dict[str, int]:
    """Builds per author, highest first. A readable export is not counted as a build.

    The ``forum-export`` text is usually a second rendering of a build the same post
    already carries as a block or attachment, so counting it would double some authors
    and not others, which is exactly the distinction the threshold is trying to draw.
    """
    counts = Counter(row["author"] for row in rows if row["kind"] != "forum-export")
    return dict(counts.most_common())


def group_by_author(out_dir: Path, threshold: int) -> tuple[int, int]:
    """Materialise ``by-author/`` from the index; return (prolific authors, pooled ones).

    Authors at or above ``threshold`` builds get their own folder; everyone else lands in
    ``by-author/_pool/``, which is where the one-off "here's my build, any advice?" posts
    belong. Files are copied rather than moved, so the by-topic tree stays intact and the
    grouping can be rebuilt at a different threshold without re-crawling.
    """
    rows = read_index(out_dir)
    counts = author_counts(rows)
    root = out_dir / "by-author"
    if root.exists():
        shutil.rmtree(root)

    prolific = {name for name, count in counts.items() if count >= threshold}
    for row in rows:
        source = Path(row["path"])
        if not source.exists():
            continue
        author = row["author"]
        folder = root / (slugify(author, 40) if author in prolific else "_pool")
        folder.mkdir(parents=True, exist_ok=True)
        topic_id = row["topic_url"].rstrip("/").rsplit("/", 1)[-1].split("-", 1)[0]
        shutil.copy2(source, folder / f"{topic_id}-{source.name}")

    _write_author_report(root, counts, threshold, prolific)
    return len(prolific), len(counts) - len(prolific)


def _write_author_report(root: Path, counts: dict[str, int], threshold: int, prolific: set[str]) -> None:
    """Write the per-author tally so the threshold can be chosen from real numbers."""
    root.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Builds by author",
        "",
        f"Threshold: {threshold} builds. Authors at or above it get a folder; the rest are in `_pool/`.",
        "",
        "| Author | Builds | Group |",
        "| --- | ---: | --- |",
    ]
    lines += [
        f"| {name} | {count} | {slugify(name, 40) if name in prolific else '_pool'} |" for name, count in counts.items()
    ]
    (root / "authors.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (root / "authors.json").write_text(json.dumps(counts, indent=2) + "\n", encoding="utf-8")


def _post_process(args: argparse.Namespace, parser: argparse.ArgumentParser) -> int:
    """Run ``--dedupe`` / ``--group-by-author`` over an existing ``--out`` and exit.

    Both are pure local passes over what a crawl already wrote, so they cost no requests
    and can be re-run at different settings as often as you like.
    """
    if args.regroup_only and args.group_by_author < 1:
        parser.error("--regroup-only needs --group-by-author N")
    if args.dedupe:
        dump = args.dump_dir if args.dump_dir.exists() else None
        builds, collapsed = write_dedupe_report(args.out, dump)
        print(f"{builds} distinct build(s), {collapsed} duplicate posting(s) collapsed -> {args.out / 'builds.md'}")
    if args.group_by_author > 0:
        named, pooled = group_by_author(args.out, args.group_by_author)
        print(f"by-author/: {named} author folder(s), {pooled} author(s) pooled into _pool/")
    return 0


def main(argv: list[str] | None = None) -> int:
    """CLI entry point: crawl the requested topics/forums and write what was found."""
    parser = argparse.ArgumentParser(
        prog="python -m scripts.forum_scrape",
        description="Collect publicly posted MidsReborn builds from the Homecoming forums, with attribution.",
    )
    parser.add_argument("--topic", action="append", default=[], metavar="URL", help="a topic URL (repeatable)")
    parser.add_argument("--forum", action="append", default=[], metavar="URL", help="a forum listing URL (repeatable)")
    parser.add_argument(
        "--all-forums",
        action="store_true",
        help="queue every forum section on the board index, minus the staff-post-only ones",
    )
    parser.add_argument(
        "--exclude-forum",
        action="append",
        default=[],
        metavar="SLUG",
        help=f"extra URL-slug substring to skip with --all-forums (default skips: {', '.join(DEFAULT_FORUM_EXCLUDES)})",
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=4,
        metavar="N",
        help="forum sections crawled concurrently (default 4). Threads overlap latency; "
        "--delay still caps the combined request rate.",
    )
    parser.add_argument(
        "--formats",
        default=",".join(ALL_FORMATS),
        metavar="LIST",
        help="comma-separated carriers to collect: mbd (current), mxd (legacy, which "
        "MidsReborn still opens and converts), text (readable forum export). "
        f"Default {','.join(ALL_FORMATS)}. Attachments are filtered before download, so "
        "a narrowed run costs fewer requests as well as less disk.",
    )
    parser.add_argument("--pages", type=int, default=1, help="listing pages to walk per --forum (default 1)")
    parser.add_argument("--out", type=Path, required=True, help="output directory")
    parser.add_argument(
        "--delay",
        type=float,
        default=2.0,
        help="minimum seconds between requests across ALL workers (default 2.0)",
    )
    parser.add_argument("--user-agent", default=DEFAULT_UA, help="User-Agent to send")
    parser.add_argument("--force", action="store_true", help="rewrite artifacts that already exist on disk")
    parser.add_argument(
        "--group-by-author",
        type=int,
        default=0,
        metavar="N",
        help="also build by-author/, giving a folder to anyone with N+ builds and pooling the rest "
        "(N=0 disables). Rebuildable at any N from the index without re-crawling.",
    )
    parser.add_argument(
        "--regroup-only",
        action="store_true",
        help="skip crawling; just rebuild by-author/ from an existing --out at --group-by-author N",
    )
    parser.add_argument(
        "--dedupe",
        action="store_true",
        help="collapse the index to distinct builds (identity from powers/slots/enhancements, "
        "not bytes) and write builds.jsonl + builds.md crediting the earliest poster",
    )
    parser.add_argument(
        "--dump-dir",
        type=Path,
        default=Path("tools/mids-dump/out"),
        help="Mids dump dir, needed to parse legacy .mxd builds during --dedupe",
    )
    args = parser.parse_args(argv)

    formats = tuple(f.strip() for f in args.formats.split(",") if f.strip())
    unknown = sorted(set(formats) - set(ALL_FORMATS))
    if unknown:
        parser.error(f"unknown --formats value(s): {', '.join(unknown)}; choose from {', '.join(ALL_FORMATS)}")

    if args.regroup_only or args.dedupe:
        return _post_process(args, parser)

    if not args.topic and not args.forum and not args.all_forums:
        parser.error("give at least one --topic, --forum, or --all-forums")

    fetcher = Fetcher(args.user_agent, args.delay)
    forums: list[str] = list(args.forum)
    if args.all_forums:
        index = fetcher.get(f"{BASE}/")
        if index is None:
            print("E02: could not read the board index; nothing queued", file=sys.stderr)
            return 1
        discovered = discover_forums(index, [*DEFAULT_FORUM_EXCLUDES, *args.exclude_forum])
        forums.extend(url for url in discovered if url not in forums)
        print(f"queued {len(forums)} forum section(s) across {args.workers} worker(s)\n")

    total = 0
    if forums:
        results = crawl_forums(fetcher, forums, args.pages, args.out, args.force, args.workers, formats)
        total += sum(r.written for r in results)
        barren = write_section_report(args.out, results)
        if barren:
            print(f"\n{len(barren)} section(s) yielded no builds; see {args.out / 'sections.md'}")
            print("  skip them next run with: --exclude-forum " + " --exclude-forum ".join(barren))

    for topic_url in args.topic:
        artifacts = harvest_topic(fetcher, topic_url, formats)
        record_topic(args.out, topic_url, formats, artifacts)
        written = write_artifacts(artifacts, args.out, args.force)
        total += written
        kinds = ", ".join(sorted({a.chunk_format for a in artifacts})) or "none"
        print(f"{written:5d} new  {len(artifacts):4d} found  ({kinds})  {topic_url}")

    print(f"\n{total} artifact(s) written to {args.out}")

    if args.group_by_author > 0:
        named, pooled = group_by_author(args.out, args.group_by_author)
        print(f"by-author/: {named} author folder(s), {pooled} author(s) pooled into _pool/")
    return 0
