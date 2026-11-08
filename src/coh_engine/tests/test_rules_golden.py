"""The ``rules`` listing is pinned to the golden file the C# CLI test compares against.

Both engines must print ``tests/fixtures/rules-golden.txt`` until the Python tree is removed, so a rule added
or renamed on one side fails a test on that side.
"""

from pathlib import Path

import pytest

from coh_engine.__main__ import main

GOLDEN = Path(__file__).parents[3] / "tests" / "fixtures" / "rules-golden.txt"


def test_rules_output_matches_the_shared_golden(capsys: pytest.CaptureFixture[str]) -> None:
    assert main(["rules"]) == 0
    assert capsys.readouterr().out == GOLDEN.read_text(encoding="utf-8").replace("\r\n", "\n")
