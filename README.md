# MidsReborn Build Companion — Claude Project Snapshot

Orientation for a Claude project that designs **City of Heroes (Homecoming)** builds against **MidsReborn**'s data. Snapshot from a forked MidsReborn repo plus the local Homecoming client.

---

## Getting started — bootstrap the venv

Python 3.14+ is required. The bootstrap creates `.venv/`, installs dev dependencies from `pyproject.toml`, and wires up pre-commit hooks.

```bash
make bootstrap
```

`make bootstrap` detects the platform and dispatches to `scripts/dev/Bootstrap-Venv.ps1` on Windows or `scripts/dev/bootstrap_venv.sh` elsewhere. For flags or direct invocation:

```bash
python -m scripts.dev.bootstrap_venv --dry-run  # preview without changes
python -m scripts.dev.bootstrap_venv --verify   # verify installed packages match pyproject.toml
```

The bootstrap is idempotent — hashes `pyproject.toml` and `.pre-commit-config.yaml` and skips when unchanged. Re-run when dependencies change.

After the bootstrap, activate the venv:

```powershell
.venv\Scripts\Activate.ps1     # Windows PowerShell
```

```bash
source .venv/bin/activate      # Linux / macOS / WSL
```

Verify with `make help` — should list `lint`, `fix`, `test`, `cov`, `cov-html`, `validate`.

---

## What's in this project

```text
homecoming-build-companion/
├── README.md                  ← you are here
├── manifest.json              ← every file's SHA256 + which lane produced it
├── data/
│   ├── mids/                  Lane A — MidsReborn-derived (manual harvest)
│   │   ├── database/          12 JSON files unzipped from MidsReborn's "JSON Exporter"
│   │   ├── attribmod/         AttribMod editor's 3 export files
│   │   ├── repo-shipped/      AttribMod.json, Compare.json, TypeGrades.json from the repo
│   │   └── derived/           Hand-transcribed constants (server caps, level rules, multipliers)
│   ├── canonical/             Lane B — direct from C:\Games\Homecoming\ via Pigg/Bin Crawler
│   │   ├── powers.json
│   │   ├── archetypes.json
│   │   ├── attrib_descriptions.json
│   │   ├── villain_def.json
│   │   ├── text_strings.json
│   │   ├── boostsets.json
│   │   └── manifest.json
│   └── diff/                  Lane A vs Lane B vs City of Data spot-checks
├── docs/                      Hand-authored reference + strategy docs (start here for mechanics)
├── build-format/
│   ├── README.md              .mxd binary layout + JSON build schema
│   ├── schema.json            JSON Schema for CharacterBuildData
│   └── samples/               Sample builds: .mxd + .json + .forum.txt + .datalink.txt + sidecar
├── strategy/                  Curated tables: breakpoints, attack chains, set-bonus stacking rules
└── data/source-material/      Manifest of the forum captures; the captures live in a private repository
    └── manifest.json          One row per capture: URL, title, date, archetype, content hash
```

---

## How to use this project (decision rules for Claude)

When a build question comes up:

1. **For game-truth facts** ("what does this power actually do?", "what's the recharge of Foot Stomp?") — cite [data/canonical/](data/canonical/).
2. **For build math and slotting validity** ("does this enhancement go in this power?", "what's the level rule?", "what's the set bonus piece count?") — cite [data/mids/](data/mids/).
3. **For mechanics and strategy** ("how do procs scale?", "what's a soft-cap?", "how do attack chains work?") — cite [docs/](docs/).
4. **For player-validated wisdom** ("what's the meta?") — cite the captures listed in [data/source-material/](data/source-material/), weighed by their `trust` and `date_posted` fields.
5. **When sources disagree** — say so explicitly. Prefer canonical for raw numbers, prefer Mids for slotting validity, prefer community for strategy. Never hide a discrepancy.
6. **When proposing a build** — produce JSON conforming to [build-format/schema.json](build-format/schema.json). Paste into MidsReborn's File → Open or the Modern JSON load path.

---

## Hard limits Claude must respect

These come from `data/mids/database/Levels.json` and the constants in [docs/slotting-and-leveling.md](docs/slotting-and-leveling.md):

- **Max 50 levels.** Powers and slots are granted by a fixed schedule.
- **Max 6 enhancement slots per power.** First slot is inherent (free with the power); up to 5 additional slots can be added.
- **Max 67 added enhancement slots per character** (server constant `MaxSlots`).
- **Power picks are gated.** Tier-9 primaries unlock at level 32 (give or take per powerset). Ancillary/Patron pools unlock at 35. Travel powers traditionally at 4 (now relaxed).
- **Enhancement Diversification (ED).** Stacking same-aspect enhancements past ~95% gets sharply reduced returns. See [docs/ed-and-multipliers.md](docs/ed-and-multipliers.md).
- **Rule of Five.** No more than 5 instances of any *exact* set-bonus value stack. See [docs/min-maxing.md](docs/min-maxing.md).
- **AT-specific caps.** Each archetype has its own resist, damage, regen, recharge, and HP caps. See [docs/archetypes.md](docs/archetypes.md) and the AT JSONs under `data/canonical/archetypes/`.
- **Exemplar-to-10 *playability*** (universal). Every build must remain *playable* when exemplared to level 10.
  - "Playable" = can attack, defensive layer running, travel power picked, not dead weight.
  - **Level-50 endgame is the primary target; never trade level-50 quality for exemplar performance.**
  - See [docs/slotting-and-leveling.md](docs/slotting-and-leveling.md#exemplar-mechanics-and-the-universal-exemplar-10-rule) and [.claude/rules/exemplar-10.md](.claude/rules/exemplar-10.md).

A build that violates any of these is invalid. If the user asks for something that requires breaking a hard limit, explain why it's impossible and propose the closest valid alternative.

---

## How a build conversation usually goes

1. User picks an archetype + primary + secondary (or asks Claude to recommend).
2. Decide the **build goal**: soft-cap defense, resist cap, perma-Hasten, procmonster, farmer, theme — see [docs/build-types-and-goals.md](docs/build-types-and-goals.md).
3. Pick power picks per level, respecting the level-rule schedule and powerset tier gating.
4. Slot enhancements toward the goal, respecting ED, the Rule of Five, and slot budgets.
5. Validate against the breakpoints in [strategy/breakpoints.json](strategy/breakpoints.json) — does it actually hit the soft-cap / recharge target / etc.?
6. Output the build as JSON (or as MidsReborn forum text), name the trade-offs, suggest one or two follow-ups.

When in doubt: prefer survival → recharge → damage → utility for endgame builds; prefer theme/RP for early levels.

---

## Provenance and freshness

Each file's lane is recorded in [manifest.json](manifest.json). Lanes mean:

- `mids-manual` — harvested from the running MidsReborn app at snapshot time.
- `mids-repo-shipped` — copied verbatim from the MidsReborn repo's `Databases/Homecoming/`.
- `mids-derived` — hand-transcribed from MidsReborn C# source. Sources cited in `data/mids/derived/sources.md`.
- `canonical-binc` — produced by Bin Crawler against `C:\Games\Homecoming\assets\*.pigg`.
- `hand-authored` — markdown reference + strategy docs (the `docs/`, `strategy/`, `build-format/` content).
- `community-capture` — manually captured forum/wiki content with attribution.
- `samples` — sample builds.

Snapshot date: see `manifest.json::generated_at`. If it's been a while since a Homecoming patch, re-harvest (see `scripts/lane-a/HARVEST.md` and `scripts/lane-b/run.ps1`).
