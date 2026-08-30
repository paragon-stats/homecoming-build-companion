# Build File Format

MidsReborn uses three serialization formats for builds. They're roundtrip-equivalent — any one converts to the others through the running app's File menu.

| Format             | Extension                                                                | When it's used                                                                        |
| ------------------ | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------- |
| Modern JSON        | `.json` (or no extension in `build-format/samples/`)                     | Human-readable. What Claude reads and writes.                                         |
| Legacy MXD binary  | `.mxd`                                                                   | Native MidsReborn save format. Compact. Requires the app to read.                     |
| DataLink hyperlink | `.txt` (or pasted)                                                       | URL-encoded compressed payload, max ~2048 chars. Used for forum signatures and links. |
| MBD chunk          | `.txt`                                                                   | Multi-line base64 chunk with `\|MBD;...\|` headers. Used for forum posts.             |

For the project, **the JSON format is canonical**. We keep `.mxd` files alongside as binary backups; users / Claude convert through the app.

---

## Modern JSON (the format Claude operates on)

Schema: [build-format/schema.json](../build-format/schema.json) (JSON Schema 2020-12).
Authoritative source: [CharacterBuildData.cs](../../MidsReborn/MidsReborn/Core/BuildFile/CharacterBuildData.cs) and the data-model classes.

### Top-level shape

```jsonc
{
  "BuiltWith": {
    "App": "Mids Reborn",
    "Version": "3.6.7.0",
    "Database": "Homecoming",
    "DatabaseVersion": "...",
  },
  "Level": "50",
  "Class": "Class_Tanker",
  "Origin": "Magic",
  "Alignment": "Hero",
  "Name": "Atomic Pretzel",
  "Comment": "Build notes go here.",
  "PowerSets": [
    "Tanker_Defense.Invulnerability",
    "Tanker_Melee.Super_Strength",
    "Pool.Speed",
    "Pool.Leaping",
    "Pool.Fighting",
    "Pool.Leadership",
    "Tanker_Ancillary.Energy_Mastery",
  ],
  "LastPower": 23,
  "PowerEntries": [
    /* one entry per power pick + inherents + Incarnates */
  ],
}
```

**Critical fields:**

- `Class` — archetype `ClassName` (e.g. `Class_Tanker`). Resolves through `data/mids/database/Archetypes.json`. Hard requirement.
- `Origin` — Magic / Mutation / Natural / Science / Technology. Required.
- `Alignment` — Hero / Vigilante / Rogue / Villain / Loyalist / Resistance.
- `PowerSets[]` — fixed slot order: `[Primary, Secondary, Pool1, Pool2, Pool3, Pool4, Ancillary]`. Empty strings for unfilled slots. Each is a powerset full name.
- `PowerEntries[]` — **every** power slot, including inherents (Brawl, Sprint, Rest, Health, Stamina, AT inherent like Gauntlet) and Incarnate slots. Order matters; Mids uses index positions.

### PowerData shape

Each entry in `PowerEntries[]`:

```jsonc
{
  "PowerName": "Tanker.Invulnerability.Unstoppable",
  "Level": 32, // when picked (1-50); -1 for unfilled
  "StatInclude": true,
  "ProcInclude": true,
  "VariableValue": 0, // for powers with a stack count
  "InherentSlotsUsed": 0,
  "SubPowerEntries": [],
  "SlotEntries": [
    /* 1..6 slots */
  ],
}
```

### SlotData shape

```jsonc
{
  "Level": 32, // character level when this slot was added
  "IsInherent": true, // true for the slot that comes free with the power
  "Enhancement": {
    /* EnhancementData or null */
  },
  "FlippedEnhancement": null, // alternate-form (Attuned vs Crafted toggle)
}
```

### EnhancementData shape

```jsonc
{
  "Uid": "Attuned_Luck_of_the_Gambler_E", // see Enhancement.json
  "Grade": "None", // for set IOs; TO/DO/SO/HO/IO have specific grades
  "IoLevel": 50, // crafted IO level (10-50); ignored for TO/DO/SO/HO/Attuned
  "RelativeLevel": "Even", // MinusThree..PlusFive; boosters move toward PlusFive
  "Obtained": false, // true = actually owned, false = planning
}
```

---

## Legacy MXD binary

Header: 4 bytes magic + version int.

| Magic  | Meaning                         |
| ------ | ------------------------------- |
| `MxDz` | zlib-compressed payload follows |
| `MxDu` | uncompressed payload            |

Current version: `3.20`. Older versions (3.10, 3.00) supported via migration paths in `MidsCharacterFileFormat.cs`. Detailed binary layout (in source-order):

1. Magic (4 bytes ASCII)
2. Version (int)
3. Archetype (string by ClassName)
4. Origin (int index)
5. Alignment (int)
6. Name (string)
7. PowerSet array (length-prefixed array of powerset name strings)
8. Power picks (length-prefixed):
    - Per power: nIDPower, level, slot count, slots
9. Per slot: enhancement nID + level + IO level + RelativeLevel + Obtained flag

The C# binary reader/writer lives at `MidsReborn/Core/MidsCharacterFileFormat.cs`.
We do **not** parse this format ourselves — instead, the running app converts to/from JSON via File → Save As / File → Open.

---

## DataLink hyperlink

A compressed-base64 build encoded into a URL fragment, intended for forum signatures and links. Format:

```text
mrb://import/MBD;<size_uncompressed>;<size_compressed>;<size_encoded>;BASE64;<base64-data>
```

Maximum length ~2048 characters. Larger builds get truncated.

Generated by: `CharacterBuildData.GenerateChunkData()` → produces this URL.

Imported by: File → Import (Ctrl+I) in the app, paste the URL.

---

## MBD chunk

A line-wrapped compressed-base64 build, intended for pasting into forum posts. Format:

```text
|MBD;<size_uncompressed>;<size_compressed>;<size_encoded>;BASE64;|
<wrapped base64, 67 chars per line>
```

Generated alongside DataLink. Import via File → Import → MBD Chunk and paste.

The "MBD" wrapper is what `clsOutput.Build()` outputs as part of forum-formatted exports — the forum text contains a human-readable build summary plus the MBD chunk for re-import.

---

## Conversion workflow (summary)

| From         | To         | How                                                           |
| ------------ | ---------- | ------------------------------------------------------------- |
| `.mxd`       | JSON       | App: File → Open → File → Save As → set type to JSON          |
| `.mxd`       | Forum text | App: File → Open → File → Export → Forum                      |
| `.mxd`       | DataLink   | App: File → Open → File → Export → Datalink                   |
| JSON         | `.mxd`     | App: File → Open (JSON) → File → Save As (`.mxd`)             |
| Forum text   | `.mxd`     | App: Ctrl+I (Import From Forum Post) → paste → save as `.mxd` |
| DataLink URL | `.mxd`     | App: Ctrl+I → paste URL → save as `.mxd`                      |
| MBD chunk    | `.mxd`     | App: File → Import → MBD Chunk → save as `.mxd`               |

---

## How Claude should produce a build

1. Output the build as JSON conforming to [schema.json](../build-format/schema.json).
2. The user pastes it into MidsReborn via File → Open → set file filter to JSON → load.
3. Or saves it as a `.json` file and opens it.
4. From there the user can Save As `.mxd`, export to forum text, or generate a DataLink.

When proposing a build, **always include**:

- The full JSON build object (validate against the schema before posting).
- A summary table: AT, primary, secondary, pools, ancillary, build goal (soft-cap defense / perma-Hasten / etc.).
- Power picks per level (level 1, 2, 4, 6, 8, ..., 50) with the chosen powers.
- Slot allocation per power, with the chosen IOs / set pieces.
- Expected stats: defense to relevant types, resistance, +Recharge, anything else relevant to the build goal.
- Trade-offs: what the build sacrifices to hit its goal, and 1-2 alternative directions.

A tight build proposal should be ~200-400 lines of JSON plus a short narrative.

---

## Validation cheats

Quick checks before submitting a JSON build:

- `Class` is in `data/mids/database/Archetypes.json::ClassName`.
- `PowerSets[0]` is in the AT's `Primary[]` list. Same for `[1]` Secondary, `[6]` Ancillary.
- Every `PowerEntries[i].PowerName` is in `data/mids/database/Powers.json::FullName`.
- Every `Level` ≤ 50 and ≥ the power's `Level` field.
- Total non-inherent slots = `sum(SlotEntries.length where !IsInherent)` ≤ 67.
- Per-power slot count ≤ 6.
- Each enhancement `Uid` is in `data/mids/database/Enhancement.json::UID`.
- 4 distinct pool sets max in `PowerSets[2..5]`.

If any check fails, the build won't load — say so explicitly when proposing.
