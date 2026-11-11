# Notice

This project is licensed under GPL-3.0-only; the text is in [LICENSE](LICENSE).
This file records where the engine and the data come from.

## Engine

The engine reproduces the arithmetic of [Mids Reborn](https://github.com/LoadedCamel/MidsReborn), which is licensed under GPL-3.0.
The Mids reference dumps under `tests/fixtures/mids/` and `tests/fixtures/mids4/` are output of its database.

## Game data

`data/canonical/` is a City of Data v3 raw export of Homecoming game data.
`data/canonical/manifest.json` records the source: `raw_data_homecoming-20251209_7415.zip`, SHA-256 `B32E1ED805538F5D45F12FBBCB00771007AE54474B881899845B78418CB86083`.

Two power sets, Light Affinity and Sonic Aura, are not in that export.
They were produced with the [CoH Sidekick](https://github.com/wednesdaywoe/CoH-Sidekick) decoder, which is licensed under AGPL-3.0.
The `supplements` entry in `data/canonical/manifest.json` records these facts:

- Commit: `fd4a2ee0ce896d99e197fc41ca4453574b6d1b1d`.
- Source paths: `exported_powers/<category>/light_affinity`, `exported_powers/redirects/light_affinity` and `exported_powers/<category>/sonic_aura`.
- Result: the directories listed there, 55 files for each set.

[docs/canonical-data-shape.md](docs/canonical-data-shape.md) describes the conversion to the City of Data form.

## Trademarks

City of Heroes and Homecoming game data, names and assets belong to their owners; this project is not affiliated with them.
