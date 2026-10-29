"""Power-name resolution — internal names to display names and back, scoped to player powersets."""

import json
from pathlib import Path

import pytest

from coh_engine.power_names import PowerNameResolver

CANONICAL = Path(__file__).parents[3] / "data" / "canonical"

# (internal, display) for every pair where the two differ, from the issue's table. The issue's
# last row names `Illusion_Control.Decoy`; the data's full names carry the category prefix and
# exist once for Controllers and once for Dominators.
DIVERGENT = [
    ("Epic.VEAT_Mace_Mastery.Focused_Accuracy", "Shatter Armor"),
    ("Pool.Leaping.Long_Jump", "Super Jump"),
    ("Pool.Leaping.Leap", "Acrobatics"),
    ("Pool.Flight.Combat_Flight", "Hover"),
    ("Pool.Flight.Afterburner", "Evasive Maneuvers"),
    ("Pool.Flight.Fly_Boost", "Afterburner"),
    ("Pool.Leadership.Defense", "Maneuvers"),
    ("Pool.Teleportation.Long_Range_Teleport", "Fold Space"),
    ("Pool.Teleportation.Teleport_Foe", "Combat Teleport"),
    ("Pool.Teleportation.Recall_Friend", "Teleport Target"),
    ("Training_Gadgets.Bane_Spider_Training.Hide", "Cloaking Device"),
    ("Training_Gadgets.Bane_Spider_Training.Bane_Spider_Armor", "Bane Spider Armor Upgrade"),
    ("Controller_Control.Illusion_Control.Decoy", "Phantom Army"),
    ("Dominator_Control.Illusion_Control.Decoy", "Phantom Army"),
]


@pytest.fixture(scope="module")
def resolver() -> PowerNameResolver:
    return PowerNameResolver.from_canonical(CANONICAL)


@pytest.mark.parametrize(("internal", "display"), DIVERGENT)
def test_internal_resolves_to_display(resolver: PowerNameResolver, internal: str, display: str) -> None:
    assert resolver.display_name(internal) == display


@pytest.mark.parametrize(("internal", "display"), DIVERGENT)
def test_display_resolves_back_to_internal(resolver: PowerNameResolver, internal: str, display: str) -> None:
    powerset = internal.rsplit(".", 1)[0]
    assert resolver.resolve(display, powerset=powerset) == internal
    assert resolver.resolve(display.upper(), powerset=powerset) == internal
    assert internal in resolver.internal_names(display)


def test_display_names_repeat_across_archetype_versions_of_a_set(resolver: PowerNameResolver) -> None:
    """Phantom Army exists in both the Controller and the Dominator Illusion Control sets."""
    assert resolver.internal_names("phantom army") == (
        "Controller_Control.Illusion_Control.Decoy",
        "Dominator_Control.Illusion_Control.Decoy",
    )
    assert resolver.internal_names("Phantom Army", powerset="Dominator_Control.Illusion_Control") == (
        "Dominator_Control.Illusion_Control.Decoy",
    )


def test_unknown_display_has_no_internal_names(resolver: PowerNameResolver) -> None:
    assert resolver.internal_names("Not A Power") == ()
    assert resolver.internal_names("Fold Space", powerset="Pool.Leaping") == ()


@pytest.mark.parametrize("leaf", ["Mace_Beam", "Pulverize", "Shatter"])
def test_player_powers_win_over_npc_and_proxy_copies(resolver: PowerNameResolver, leaf: str) -> None:
    """A bare leaf lookup matches V_Arachnos_Proxy and Warriors_New copies; only player sets are indexed."""
    internals = {p.internal for ps in resolver.powersets() for p in ps.powers}
    leaves = {name for name in internals if name.rsplit(".", 1)[1].casefold() == leaf.casefold()}
    assert leaves
    assert not {name for name in leaves if name.startswith(("V_Arachnos_Proxy.", "Warriors_New.", "Widow_Training_"))}


def test_npc_sets_are_not_player_powersets(resolver: PowerNameResolver) -> None:
    names = {ps.internal for ps in resolver.powersets()}
    assert not {n for n in names if n.startswith(("V_Arachnos_Proxy.", "Warriors_New.", "Warriors."))}
    assert "Pool.Teleportation" in names
    assert "Scrapper_Melee.Claws" in names
    assert "Epic.VEAT_Mace_Mastery" in names
    with pytest.raises(ValueError, match="E22"):
        resolver.display_name("Warriors_New.Warrior_Mace_Lt.Mace_Beam")


def test_powersets_are_sorted_and_carry_their_archetypes(resolver: PowerNameResolver) -> None:
    sets = resolver.powersets()
    assert [s.internal for s in sets] == sorted(s.internal for s in sets)
    by_name = {s.internal: s for s in sets}
    pool = by_name["Pool.Teleportation"]
    assert (pool.display, pool.category) == ("Teleportation", "Pool")
    assert "scrapper" in pool.archetypes
    assert list(pool.archetypes) == sorted(pool.archetypes)
    assert pool.powers
    assert all(p.powerset == pool.internal for p in pool.powers)
    assert by_name["Scrapper_Melee.Claws"].archetypes == ("scrapper",)
    assert by_name["Epic.VEAT_Mace_Mastery"].archetypes == ("arachnos_soldier", "arachnos_widow")


def test_resolve_needs_exactly_one_match(resolver: PowerNameResolver) -> None:
    with pytest.raises(ValueError, match=r"E22.*matches 0"):
        resolver.resolve("Fold Space", powerset="Pool.Leaping")


def test_resolve_refuses_a_display_name_that_repeats_within_a_set(tmp_path: Path) -> None:
    powers = tmp_path / "powers"
    (powers / "pool" / "twins").mkdir(parents=True)
    (tmp_path / "archetypes").mkdir()
    (tmp_path / "archetypes" / "blaster.json").write_text(
        json.dumps({"primary_category": "Pool", "secondary_category": "Pool", "power_pool_category": "Pool"})
    )
    (tmp_path / "powers_by_at.json").write_text(json.dumps({"blaster": []}))
    (powers / "pool" / "index.json").write_text(json.dumps({"name": "Pool"}))
    (powers / "pool" / "twins" / "index.json").write_text(
        json.dumps(
            {
                "name": "Twins",
                "display_name": "Twins",
                "power_names": ["Pool.Twins.A", "Pool.Twins.B"],
                "power_display_names": ["Same", "Same"],
            }
        )
    )
    twins = PowerNameResolver.from_canonical(tmp_path)
    assert twins.internal_names("Same") == ("Pool.Twins.A", "Pool.Twins.B")
    with pytest.raises(ValueError, match=r"E22.*matches 2"):
        twins.resolve("Same", powerset="Pool.Twins")


def test_display_name_of_unknown_internal_fails_loud(resolver: PowerNameResolver) -> None:
    with pytest.raises(ValueError, match=r"E22: internal power name 'Pool.Teleportation.Nope'"):
        resolver.display_name("Pool.Teleportation.Nope")


def test_render_forms(resolver: PowerNameResolver) -> None:
    internal = "Pool.Teleportation.Long_Range_Teleport"
    assert resolver.render(internal) == "Fold Space"
    assert resolver.render(internal, "display") == "Fold Space"
    assert resolver.render(internal, "internal") == internal
    assert resolver.render(internal, "both") == "Fold Space (Pool.Teleportation.Long_Range_Teleport)"


def test_render_of_unknown_internal_fails_loud(resolver: PowerNameResolver) -> None:
    with pytest.raises(ValueError, match="E22"):
        resolver.render("Pool.Nope.Nope", "both")
