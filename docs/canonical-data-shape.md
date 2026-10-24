# Canonical data shape

Canonical data is the City of Data v3 JSON export under `data/canonical/`, recorded in `data/canonical/manifest.json`.

## Power file naming

A power file lives at `data/canonical/powers/<powerset path>/<power>.json`.
Each dot-separated segment of the power's `full_name` becomes one lower-cased path segment.
Every `:` in a segment becomes `_`.
For example, `Training_Gadgets.Training_and_Gadgets.Tactical_Training:_Assault` is stored at `powers/training_gadgets/training_and_gadgets/tactical_training__assault.json`.

`coh_engine.attack_profile.canonical_power_path` is the single source of this rule.
`scripts/lane-b/run.ps1` and `scripts/check-overrides.ps1` apply the same mapping.

The zip distribution names these files with a literal `:`.
On NTFS, `name:stream` is an alternate data stream, so extracting them left empty files and the real JSON never reached the repository.
The exporter now renames them on extraction, and `src/coh_engine/tests/test_canonical_files.py` fails on any zero-byte canonical file.
