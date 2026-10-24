"""Guards on the committed canonical data: no empty placeholders, every colon power resolvable."""

import json
from pathlib import Path

from coh_engine.attack_profile import canonical_power_path, load_canonical_powers

CANONICAL = Path(__file__).parents[3] / "data" / "canonical"


def test_no_canonical_file_is_zero_bytes() -> None:
    empty = [str(p.relative_to(CANONICAL)) for p in CANONICAL.rglob("*") if p.is_file() and p.stat().st_size == 0]
    assert not empty, f"E40: {len(empty)} zero-byte canonical files (NTFS stream placeholders?): {empty}"


def test_every_colon_power_resolves() -> None:
    rows = json.loads((CANONICAL / "all_power_search.json").read_text(encoding="utf-8"))
    powers = CANONICAL / "powers"
    # Row is [display name, lower-cased full_name]; the second form is the one that maps to a path.
    lowered = [row[1] for row in rows if ":" in row[1]]
    assert lowered, "expected colon-bearing power names in all_power_search.json"
    names = [json.loads(canonical_power_path(powers, low).read_text(encoding="utf-8"))["full_name"] for low in lowered]
    assert [n.lower() for n in names] == lowered
    assert set(load_canonical_powers(powers, names)) == set(names)
