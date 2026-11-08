"""Source root resolution: flag beats environment beats default, and a missing directory is E04."""

from __future__ import annotations

from pathlib import Path

import pytest

from scripts.forum_scrape import guide_index, guides, manifest, scrape, source_root
from scripts.forum_scrape.source_root import ENV_VAR
from scripts.forum_scrape.source_root import source_root as resolve


def test_flag_beats_env_beats_default(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    flag, env, default = tmp_path / "flag", tmp_path / "env", tmp_path / "default"
    for folder in (flag, env, default):
        folder.mkdir()
    monkeypatch.setattr(source_root, "DEFAULT_ROOT", default)
    assert resolve(flag, {ENV_VAR: str(env)}) == flag
    assert resolve(None, {ENV_VAR: str(env)}) == env
    assert resolve(None, {}) == default
    assert resolve(None, {ENV_VAR: ""}) == default


def test_reads_the_process_environment_by_default(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(ENV_VAR, str(tmp_path))
    assert resolve() == tmp_path


def test_default_sits_beside_the_repository() -> None:
    assert source_root.REPO_ROOT.parent / "homecoming-source-material" == source_root.DEFAULT_ROOT
    assert (source_root.REPO_ROOT / "scripts").is_dir()


def test_missing_root_exits_with_the_named_error(tmp_path: Path) -> None:
    absent = tmp_path / "absent"
    with pytest.raises(SystemExit) as raised:
        resolve(absent, {})
    assert str(raised.value) == f"E04: expected a directory at {absent}; set --source-root or {ENV_VAR}"


@pytest.mark.parametrize(
    ("main", "extra"),
    [
        (guide_index.main, []),
        (manifest.main, []),
        (guides.main, ["--forum", "https://forums.example/forum/1-x/"]),
        (scrape.main, ["--topic", "https://forums.example/topic/1-x/"]),
    ],
)
def test_every_cli_exits_on_a_missing_root(
    main, extra: list[str], tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.delenv(ENV_VAR, raising=False)
    with pytest.raises(SystemExit, match="E04: expected a directory at"):
        main(["--source-root", str(tmp_path / "absent"), *extra])


def test_scrape_and_guides_default_their_output_inside_the_root(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    seen: dict[str, Path] = {}

    def stop(out: Path) -> int:
        seen["out"] = out
        return 0

    monkeypatch.setattr(scrape, "_post_process", lambda args, _parser: stop(args.out))
    assert scrape.main(["--source-root", str(tmp_path), "--dedupe"]) == 0
    assert seen["out"] == tmp_path / "build-archives"

    monkeypatch.setattr(guides, "powersets_from_canonical", lambda _path: [])
    monkeypatch.setattr(guides, "_reclassify", lambda out, _powersets: stop(out))
    assert guides.main(["--source-root", str(tmp_path), "--reclassify"]) == 0
    assert seen["out"] == tmp_path / "community"
