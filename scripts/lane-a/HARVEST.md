# Lane A — Manual Harvest Checklist

Procedure for harvesting MidsReborn-derived data from the running app. Walk it once to refresh the snapshot. Step 2 (JSON Exporter) is the biggest action — produces 12 JSON files in one click.

> **Lane B is separate and faster.** Canonical game data comes from the City of Data zip (`raw_data_homecoming-*.zip`, ~92 MB → 411 MB / 44k JSON files). Extract via `coh-engine extract-canonical [--zip ZIP] [--out-dir DIR] [--force]`.
>
> Lane A and Lane B run independently; both give Claude cross-checkable sources.

**Install path:** `%APPDATA%\LoadedCamel\MidsReborn\`

**Output path:** `%USERPROFILE%\repos\homecoming-build-companion\data\mids\` (everything we collect lands here)

---

## Step 1. Pre-flight — confirm install/repo parity

The hash diff has been run; results in `docs/_archive/MERGE_PLAN-2026.md`. As of 2026-04-27: **install has drifted on 11 of 19 files**.

Big shared databases (`I12.mhd` powers, `I9.mhd` set bonuses, `EnhDB.mhd`, `Recipe.mhd`, `Salvage.mhd`) match — JSON exports from either side are equivalent.

Smaller config files differ; **we harvest from the install**.

Re-run the diff if you've updated MidsReborn since: see the PowerShell one-liner in the plan, Phase 0a.

---

## Step 2. Run the JSON Exporter (the headline action)

This single button exports the entire MidsReborn database to a 12-file zip.

1. Launch **MidsReborn** (double-click `MidsReborn.exe` in the install dir, or use the launcher).
2. From the menu bar: **Advanced → Database Menu**. The **DB Editor** form opens.
3. Click the **JSON Exporter** button.
4. A progress dialog appears titled _"DB Export progress"_. Wait for it to finish (~5–30 sec depending on disk).
5. Output file: `%APPDATA%\LoadedCamel\MidsReborn\Databases\Mids.zip`

**Copy the zip and unpack it:**

```powershell
$src = "$env:APPDATA\LoadedCamel\MidsReborn\Databases\Mids.zip"
$dst = "$env:USERPROFILE\repos\homecoming-build-companion\data\mids\database"
if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst | Out-Null }
Expand-Archive -Path $src -DestinationPath $dst -Force
Copy-Item $src "$env:USERPROFILE\repos\homecoming-build-companion\data\mids\Mids.zip" -Force
Get-ChildItem $dst | Select-Object Name, @{N='SizeKB';E={[math]::Round($_.Length/1KB,1)}} | Format-Table -AutoSize
```

Expected files in `homecoming-build-companion\data\mids\database\`:

<!-- markdownlint-disable MD013 -->

| File                      | What it is                                                                                                                                                                                  |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Database.json`           | The whole `Database` object — meta-snapshot                                                                                                                                                 |
| `Archetypes.json`         | All playable classes: Tanker, Brute, Scrapper, Stalker, Sentinel, Blaster, Corruptor, Defender, Controller, Dominator, Mastermind, Peacebringer, Warshade, Arachnos Soldier, Arachnos Widow |
| `AttribMods.json`         | Attribute modifier table (effect types, magnitudes)                                                                                                                                         |
| `Enhancement.json`        | Every enhancement (TO/DO/SO/HO/IO/SetO)                                                                                                                                                     |
| `EnhancementClasses.json` | Enhancement classification tree                                                                                                                                                             |
| `Entities.json`           | Pets, summons, MM henchmen                                                                                                                                                                  |
| `Levels.json`             | LevelMap — power picks + slot grants per level 1–50                                                                                                                                         |
| `Powers.json`             | Every power record with effects                                                                                                                                                             |
| `PowerSets.json`          | Every powerset (primaries, secondaries, ancillaries, pools)                                                                                                                                 |
| `PowerSetGroups.json`     | Powerset groupings (which sets belong to which AT)                                                                                                                                          |
| `Recipes.json`            | Crafting recipes                                                                                                                                                                            |
| `Salvage.json`            | Salvage materials                                                                                                                                                                           |

<!-- markdownlint-enable MD013 -->

If any file is empty or missing, re-run the JSON Exporter with the app fully restarted.

---

## Step 3. Run the AttribMod Exporter (3 extra slices)

The AttribMod editor has its own export button that produces three additional ordered/indexed views of the attribmod data — useful for cross-referencing.

1. Still in the **DB Editor** (from step 2), click **AttribMod Editor**.
2. The AttribMod editor form opens. Find the **Export JSON** button (toward the bottom of the form) and click it.
3. Three files are written to `%APPDATA%\LoadedCamel\MidsReborn\Data\Export\`:
   - `attribMod.json`
   - `attribModTables.json`
   - `attribModOrdered.json`

**Copy:**

```powershell
$src = "$env:APPDATA\LoadedCamel\MidsReborn\Data\Export"
$dst = "$env:USERPROFILE\repos\homecoming-build-companion\data\mids\attribmod"
if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst | Out-Null }
Copy-Item "$src\*.json" $dst -Force
```

---

## Step 4. Repo-shipped JSON (free data, no UI walk needed)

Three files already ship pre-extracted in the repo. Copy them as-is.

```powershell
$src = "$env:USERPROFILE\repos\MidsReborn\MidsReborn\Databases\Homecoming"
$dst = "$env:USERPROFILE\repos\homecoming-build-companion\data\mids\repo-shipped"
if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst | Out-Null }
Copy-Item "$src\AttribMod.json"  $dst -Force
Copy-Item "$src\Compare.json"    $dst -Force
Copy-Item "$src\TypeGrades.json" $dst -Force
```

`Compare.json` is **only in the repo** — install doesn't ship it. AttribMod and TypeGrades JSONs are slightly older than the install's; install is preferred, repo copies kept as fallback.

---

## Step 5. Hand-transcribed constants (fields the app does not export)

A few constants aren't in the JSON Exporter output — they're hard-coded in MidsReborn's C# source. Transcribe to `data/mids/derived/` once; they don't change unless MidsReborn ships a release.

- **`derived/server_constants.json`** — copy from [ServerData.cs](../../../MidsReborn/MidsReborn/Core/ServerData.cs) constructor:
  - `BaseToHit`, `MaxSlots`
  - `HealthSlot1Level=8`, `HealthSlot2Level=16`
  - `StaminaSlot1Level=12`, `StaminaSlot2Level=22`
  - `BaseRunSpeed`, `MaxRunSpeed`, plus the `EnabledIncarnates` map
- **`derived/level_rules_summary.json`** — derive from `Levels.json` (step 2 output): for each level 1–50, list `{ level, powers_granted, slots_granted, milestones[] }`.
  - Milestones include the inherent slot grants (Health@8, Health@16, Stamina@12, Stamina@22).
- **`derived/effect_tables.json`** — the `MultED`, `MultTO`, `MultDO`, `MultSO`, `MultIO` jagged arrays from `Database.json` (step 2 output).
  - Already there, buried inside the `Database` object; pull up to a top-level file for easier Claude lookup.
- **`derived/sources.md`** — for every transcribed file above, record the source class + line + date transcribed. Auditable provenance.

---

## Step 6. Sample builds

For each `.mxd` build sample (sourced from forums, see [community/CAPTURE.md](../../../homecoming-build-companion/community/CAPTURE.md)):

1. **Open** the `.mxd` in MidsReborn (File → Open) — verify it loads cleanly.
2. **Save As JSON**: File → Save As → set the file type filter to JSON and save next to the `.mxd`.
3. **Export Forum text**: File → Export → Forum Build (BBCode) → save as `<name>.forum.txt`.
4. **Export DataLink**: Menu bar → tsShareLegacy ("Export Datalink") → save as `<name>.datalink.txt`.
5. **Drop everything** into `homecoming-build-companion/build-format/samples/<name>/`:
   - `<name>.mxd`
   - `<name>.json`
   - `<name>.forum.txt`
   - `<name>.datalink.txt`
   - `<name>.source.md` (frontmatter: source URL, author handle, date posted, date captured, AT, primary, secondary, build-type tags)

**Coverage target** — pick samples that span the space:

- defensive AT (Tanker), offensive (Blaster)
- support (Defender or Corruptor), control (Controller)
- Epic (Peacebringer/Warshade or Arachnos Soldier/Widow)

Aim for level-50, fully-slotted, IO-heavy. Tag at least one as "procmonster" and one as "soft-cap defense".

---

## When to re-harvest

- **MidsReborn updated** (the launcher pulls a new version): re-run the hash diff, then steps 2–4.
- **Adding new sample builds**: just step 6 for the affected build.
- **Quarterly sanity check** (otherwise): re-run the full procedure to catch silent dataset shifts.
