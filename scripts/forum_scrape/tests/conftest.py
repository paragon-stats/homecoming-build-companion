"""Shared fixtures: a fake network so no test ever reaches the real forum."""

from __future__ import annotations

import gzip
import io
import urllib.error
import urllib.request
from pathlib import Path

import pytest

from scripts.forum_scrape import scrape

ROBOTS = "User-agent: *\nAllow: /\nDisallow: /search/\n"
FIXTURE = Path(__file__).parent / "fixtures" / "topic_sample.html"
TOPIC = "https://forums.homecomingservers.com/topic/99999-a-bots-build-thread/"
ATTACHMENT = "https://forums.homecomingservers.com/applications/core/interface/file/attachment.php?id=119180&key=abc123"


class FakeResponse(io.BytesIO):
    """Minimal stand-in for the object ``urlopen`` yields as a context manager."""

    def __init__(self, body: bytes, encoding: str | None = None) -> None:
        super().__init__(body)
        self.headers = {"Content-Encoding": encoding} if encoding else {}

    def __enter__(self) -> FakeResponse:
        return self

    def __exit__(self, *exc: object) -> None:
        self.close()


@pytest.fixture
def fake_net(monkeypatch: pytest.MonkeyPatch):
    """Route every urlopen through a dict of URL -> body, recording the order."""
    routes: dict[str, bytes | Exception] = {"https://forums.homecomingservers.com/robots.txt": ROBOTS.encode()}
    calls: list[str] = []

    def fake_urlopen(request, timeout=None):
        url = request.full_url
        calls.append(url)
        body = routes.get(url)
        if body is None:
            raise urllib.error.HTTPError(url, 404, "Not Found", {}, None)  # type: ignore[arg-type]
        if isinstance(body, Exception):
            raise body
        if url.endswith("#gzip"):
            return FakeResponse(gzip.compress(body), "gzip")
        return FakeResponse(body)

    monkeypatch.setattr(urllib.request, "urlopen", fake_urlopen)
    monkeypatch.setattr(scrape.time, "sleep", lambda _seconds: None)
    return routes, calls


@pytest.fixture
def topic_page() -> str:
    return FIXTURE.read_text(encoding="utf-8")
