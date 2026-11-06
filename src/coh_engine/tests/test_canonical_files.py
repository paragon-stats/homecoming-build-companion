"""Guards on the committed canonical data: no empty placeholders, every colon power resolvable."""

import json
from pathlib import Path

import pytest

from coh_engine.attack_profile import canonical_power_path, canonical_resolver, load_canonical_powers
from coh_engine.power_names import PowerNameResolver

CANONICAL = Path(__file__).parents[3] / "data" / "canonical"
SUPPLEMENTS = json.loads((CANONICAL / "manifest.json").read_text(encoding="utf-8")).get("supplements", [])
SUPPLEMENT_DIRS = [d for s in SUPPLEMENTS for d in s["directories"]]


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


@pytest.mark.parametrize("directory", SUPPLEMENT_DIRS)
def test_supplemented_set_loads_and_its_links_resolve(directory: str) -> None:
    index = json.loads((CANONICAL / directory / "index.json").read_text(encoding="utf-8"))
    names = index["power_names"]
    powers = load_canonical_powers(CANONICAL / "powers", names)
    unlisted = sorted(n for n, p in powers.items() if not isinstance(p["allowed_boostset_cats"], list))
    assert not unlisted, f"E97: allowed_boostset_cats is not a list in {unlisted}; City of Data writes [] when empty"
    category = directory.split("/")[1]
    if category not in ("redirects", "pets"):
        internal = names[0].rsplit(".", 1)[0]
        listed = {ps.internal: ps for ps in PowerNameResolver.from_canonical(CANONICAL).powersets()}
        assert internal in listed, (
            f"E95: {internal} from {directory} is not a player powerset; check its category index"
        )
        assert [p.internal for p in listed[internal].powers] == names
    resolve = canonical_resolver(CANONICAL / "powers")
    params = [t.get("params") or {} for p in powers.values() for e in p["effects"] for t in e["templates"]]
    links = {link for p in params for key in ("power_names", "redirects") for link in p.get(key) or ()}
    missing = sorted(link for link in links if resolve(link) is None)
    assert not missing, f"E96: {directory} links to {len(missing)} powers with no canonical file: {missing}"
