"""Tests for check_commit_message hook."""

from __future__ import annotations

from pathlib import Path

import pytest

import scripts.testing.hooks.check_commit_message as mod

# ---------------------------------------------------------------------------
# validate_commit_message
# ---------------------------------------------------------------------------


class TestValidateCommitMessage:
    """Tests for validate_commit_message()."""

    def test_empty_message(self) -> None:
        """Test empty message."""
        is_valid, reason = mod.validate_commit_message("")
        assert not is_valid
        assert "empty" in reason.lower()

    def test_whitespace_only_message(self) -> None:
        """Test whitespace only message."""
        is_valid, reason = mod.validate_commit_message("   \n\n  ")
        assert not is_valid
        assert "empty" in reason.lower()

    def test_leading_newlines_stripped(self) -> None:
        """Leading whitespace is stripped; first real line becomes subject."""
        is_valid, reason = mod.validate_commit_message("\nfeat: real subject")
        assert is_valid
        assert reason == "Conventional commit"

    def test_merge_commit(self) -> None:
        """Test merge commit."""
        is_valid, reason = mod.validate_commit_message("Merge branch 'feature' into main")
        assert is_valid
        assert reason == "Merge commit"

    def test_merge_pull_request(self) -> None:
        """Test merge pull request."""
        is_valid, reason = mod.validate_commit_message("Merge pull request #42 from owner/branch")
        assert is_valid
        assert reason == "Merge commit"

    def test_dependabot_bump_the(self) -> None:
        """Test dependabot bump the."""
        is_valid, reason = mod.validate_commit_message("Bump the dependency-updates group with 1 updates")
        assert is_valid
        assert reason == "Dependabot commit"

    def test_dependabot_bump_package(self) -> None:
        """Test dependabot bump package."""
        is_valid, reason = mod.validate_commit_message("Bump actions/checkout from 3 to 4")
        assert is_valid
        assert reason == "Dependabot commit"

    def test_conventional_type_only(self) -> None:
        """Test conventional type only."""
        is_valid, reason = mod.validate_commit_message("fix: resolve crash on startup")
        assert is_valid
        assert reason == "Conventional commit"

    def test_conventional_type_with_scope(self) -> None:
        """Test conventional type with scope."""
        is_valid, reason = mod.validate_commit_message("feat(ci): add auto-summary generator")
        assert is_valid
        assert reason == "Conventional commit"

    def test_conventional_with_body(self) -> None:
        """Test conventional with body."""
        msg = "docs: update README\n\nAdded new section for auto-summary."
        is_valid, reason = mod.validate_commit_message(msg)
        assert is_valid
        assert reason == "Conventional commit"

    def test_non_conventional_rejected(self) -> None:
        """Test non conventional rejected."""
        is_valid, reason = mod.validate_commit_message("Update README")
        assert not is_valid
        assert "conventional commit format" in reason.lower()
        assert "'Update README'" in reason

    def test_non_conventional_shows_examples(self) -> None:
        """Test non conventional shows examples."""
        is_valid, reason = mod.validate_commit_message("Fix bug")
        assert not is_valid
        assert "feat(ci):" in reason
        assert "fix:" in reason

    def test_uppercase_type_rejected(self) -> None:
        """Conventional commit types must be lowercase."""
        is_valid, _reason = mod.validate_commit_message("Fix: resolve crash")
        assert not is_valid

    @pytest.mark.parametrize(
        "commit_type",
        ["feat", "fix", "perf", "security", "revert", "docs", "test", "ci", "chore", "style", "refactor", "build"],
    )
    def test_all_gitops_types_accepted(self, commit_type: str) -> None:
        """Every type gitops.md permits is accepted, with and without scope."""
        assert mod.validate_commit_message(f"{commit_type}: do the thing")[0]
        assert mod.validate_commit_message(f"{commit_type}(scope): do the thing")[0]

    def test_allowlist_matches_gitops_rule(self) -> None:
        """The allowlist is the ci-templates SSOT plus the local `build` extra.

        Guards the drift this list already suffered once: it was hand-transcribed from
        prose and silently lost four standard types.
        """
        ssot = {"feat", "fix", "perf", "security", "revert", "docs", "test", "ci", "chore", "style", "refactor"}
        assert set(mod._ALLOWED_TYPES) == ssot | {"build"}

    @pytest.mark.parametrize("commit_type", ["feature", "wip", "hotfix", "improvement"])
    def test_unlisted_type_rejected(self, commit_type: str) -> None:
        """Types outside the gitops.md allowlist are rejected (local adaptation)."""
        is_valid, reason = mod.validate_commit_message(f"{commit_type}: something")
        assert not is_valid
        assert "Allowed types:" in reason

    @pytest.mark.parametrize(
        "subject",
        [
            "feat!: drop the legacy loader",
            "feat(engine)!: drop the legacy loader",
            "fix(ci)!: change the required check names",
        ],
    )
    def test_breaking_change_marker_accepted(self, subject: str) -> None:
        """The `!` breaking-change marker is valid after the type and after the scope.

        cliff.toml and the release flow both key the version bump off this marker, so
        rejecting it would block the one message shape the release process asks for.
        """
        assert mod.validate_commit_message(subject)[0]

    def test_bare_bang_without_type_rejected(self) -> None:
        """The marker does not make an otherwise invalid subject valid."""
        assert not mod.validate_commit_message("!: do a thing")[0]
        assert not mod.validate_commit_message("wip!: do a thing")[0]

    def test_git_revert_default_message_rejected(self) -> None:
        """`git revert`'s generated `Revert "..."` message is not a conventional commit.

        Adding the `revert` type accepts a hand-written `revert(scope): summary`; it must
        not quietly let the auto-generated message through, which explains nothing.
        """
        is_valid, _reason = mod.validate_commit_message('Revert "ci(gitlab): remove the pipeline"')
        assert not is_valid


# ---------------------------------------------------------------------------
# main (CLI entry point)
# ---------------------------------------------------------------------------


class TestMain:
    """Tests for main() CLI entry point."""

    def test_missing_argument(self, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]) -> None:
        """Test missing argument."""
        monkeypatch.setattr("sys.argv", ["check_commit_message.py"])
        assert mod.main() == 2
        err = capsys.readouterr().err
        assert "Usage" in err

    def test_missing_file(
        self, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str], tmp_path: Path
    ) -> None:
        """Test missing file."""
        missing = tmp_path / "nonexistent"
        monkeypatch.setattr("sys.argv", ["check_commit_message.py", str(missing)])
        assert mod.main() == 2
        err = capsys.readouterr().err
        assert "not found" in err

    def test_valid_message_returns_zero(self, monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
        """Test valid message returns zero."""
        msg_file = tmp_path / "COMMIT_EDITMSG"
        msg_file.write_text("feat: add new feature\n", encoding="utf-8")
        monkeypatch.setattr("sys.argv", ["check_commit_message.py", str(msg_file)])
        assert mod.main() == 0

    def test_invalid_message_returns_one(
        self, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str], tmp_path: Path
    ) -> None:
        """Test invalid message returns one."""
        msg_file = tmp_path / "COMMIT_EDITMSG"
        msg_file.write_text("Bad commit message\n", encoding="utf-8")
        monkeypatch.setattr("sys.argv", ["check_commit_message.py", str(msg_file)])
        assert mod.main() == 1
        err = capsys.readouterr().err
        assert "FAIL" in err

    def test_dependabot_message_returns_zero(self, monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
        """Test dependabot message returns zero."""
        msg_file = tmp_path / "COMMIT_EDITMSG"
        msg_file.write_text("Bump the dependency-updates group with 1 updates\n", encoding="utf-8")
        monkeypatch.setattr("sys.argv", ["check_commit_message.py", str(msg_file)])
        assert mod.main() == 0
