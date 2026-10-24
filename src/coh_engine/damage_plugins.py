"""The single registration manifest for the layered damage model.

Importing this module imports every module that registers a ``@damage_source`` or
``@damage_modifier`` (``damage_sources``, ``damage_modifiers``, ``pet_damage``, ``at_modifiers``),
so the default registries in :mod:`coh_engine.damage_model` are fully populated. Adding a plugin
module means adding one import here, and nothing else (``.claude/rules/check-registry.md``).
"""

from coh_engine import at_modifiers, damage_modifiers, damage_sources, pet_damage

__all__ = ["at_modifiers", "damage_modifiers", "damage_sources", "pet_damage"]
