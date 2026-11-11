"""Tests for scripts.common.frontmatter — YAML frontmatter parser."""

from __future__ import annotations

from pathlib import Path

import pytest

from scripts.common.frontmatter import FrontmatterParseError, parse_frontmatter


def write(tmp_path: Path, text: str) -> Path:
    path = tmp_path / "capture.md"
    path.write_text(text, encoding="utf-8")
    return path


class TestNoFrontmatter:
    def test_empty_file_returns_none(self, tmp_path: Path) -> None:
        path = write(tmp_path, "")
        assert parse_frontmatter(path) is None

    def test_whitespace_only_file_returns_none(self, tmp_path: Path) -> None:
        path = write(tmp_path, "  \n\n\t\n")
        assert parse_frontmatter(path) is None

    def test_plain_markdown_returns_none(self, tmp_path: Path) -> None:
        path = write(tmp_path, "# Title\n\nBody text.\n")
        assert parse_frontmatter(path) is None

    def test_delimiter_not_on_first_line_returns_none(self, tmp_path: Path) -> None:
        path = write(tmp_path, "\n---\nkey: value\n---\n")
        assert parse_frontmatter(path) is None

    def test_missing_file_raises(self, tmp_path: Path) -> None:
        with pytest.raises(FileNotFoundError):
            parse_frontmatter(tmp_path / "absent.md")


class TestParsedFrontmatter:
    def test_simple_mapping(self, tmp_path: Path) -> None:
        path = write(tmp_path, "---\ncaptured_by: local-capture\ntrust: first-party\n---\n# Body\n")
        assert parse_frontmatter(path) == {"captured_by": "local-capture", "trust": "first-party"}

    def test_nested_mapping_and_list(self, tmp_path: Path) -> None:
        path = write(
            tmp_path,
            "---\nmetadata:\n  type: project\ntags:\n  - builds\n  - engine\n---\n",
        )
        assert parse_frontmatter(path) == {
            "metadata": {"type": "project"},
            "tags": ["builds", "engine"],
        }

    def test_quoted_strings_preserved(self, tmp_path: Path) -> None:
        path = write(tmp_path, '---\ntitle: "Colon: in value"\n---\n')
        assert parse_frontmatter(path) == {"title": "Colon: in value"}

    def test_empty_frontmatter_block_returns_empty_dict(self, tmp_path: Path) -> None:
        path = write(tmp_path, "---\n---\nbody\n")
        assert parse_frontmatter(path) == {}

    def test_comment_only_frontmatter_returns_empty_dict(self, tmp_path: Path) -> None:
        path = write(tmp_path, "---\n# just a comment\n---\n")
        assert parse_frontmatter(path) == {}

    def test_delimiter_with_surrounding_spaces_recognized(self, tmp_path: Path) -> None:
        path = write(tmp_path, "---\nkey: value\n  ---  \nbody\n")
        assert parse_frontmatter(path) == {"key": "value"}


class TestMalformedFrontmatter:
    def test_unclosed_delimiter_raises(self, tmp_path: Path) -> None:
        path = write(tmp_path, "---\nkey: value\nno closing delimiter\n")
        with pytest.raises(FrontmatterParseError, match="never closed"):
            parse_frontmatter(path)

    def test_invalid_yaml_raises(self, tmp_path: Path) -> None:
        path = write(tmp_path, "---\nkey: [unclosed\n---\n")
        with pytest.raises(FrontmatterParseError, match="invalid YAML"):
            parse_frontmatter(path)

    def test_non_mapping_yaml_raises(self, tmp_path: Path) -> None:
        path = write(tmp_path, "---\n- just\n- a\n- list\n---\n")
        with pytest.raises(FrontmatterParseError, match="must be a mapping"):
            parse_frontmatter(path)

    def test_scalar_yaml_raises(self, tmp_path: Path) -> None:
        path = write(tmp_path, "---\njust a string\n---\n")
        with pytest.raises(FrontmatterParseError, match="must be a mapping"):
            parse_frontmatter(path)
