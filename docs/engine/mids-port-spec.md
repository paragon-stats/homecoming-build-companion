# MidsReborn Engine Port Spec

> Generated 2026-07-02 from the MidsReborn fork at `<repos>/MidsReborn/MidsReborn` by the `mids-port-spec` extraction workflow (run `wf_04edd3dd-bb9`). Language-neutral SSOT the Python port implements
> against. Mids is the reference implementation: every numeric claim is validated against MidsReborn's own output via golden fixtures. Regenerate and re-baseline after any Homecoming DB update.

## Synthesis

### Implementation order

- **data-and-build-formats** — CP2 foundation: primitive encodings (LEB128 strings, length-1 array idiom, LE ints/floats), .mhd data container layout, and direct .mbd(JSON)/.mxd(binary) build-file
  parse with per-format +1/-1 offset and migration rules.
- **at-modifier-tables** — AttribMod tables + GetModifier lookup: table[nModifierTable].Table[level=49 fixed][Classes[iClass].Column], out-of-bounds->0, Damage cells negative; the base number under
  every effect magnitude.
- **enhancement-value-pipeline** — Slot->per-aspect magnitude: schedule-table lookup x relative-level x Superior x per-effect multiplier, summed across slots per aspect, then ED applied ONCE on the
  aggregate (slopes 1.0/0.9/0.7/0.15), gated by Slots[i].Level<ForceLevel.
- **effect-model** — Effect record + Mag=sign*Scale*nMagnitude*GetModifier, BuffedMag fallback, and GetEffectMagSum/GetEnhancementMagSum/GetDamageMagSum ToWho/probability/suppression/conditional
  filtering into ShortFX rollups.
- **expressions-language** — Expression evaluator for AttribType==Expression effect Mag/Duration/Probability: CommandsDict(26)+FunctionsDict(16) substitution then Jace infix math with 9 custom funcs,
  plus one hardcoded RPN special case.
- **gbpa-pass-pipeline** — GenerateBuffedPowerArray orchestration: Pass0 assemble, enhancement pass, Pass1-6 per-power (preED/ED/postED/add/caps/multiply), buff pass, Pass6 accuracy/ToHit, global-enh
  fold — the exact-ordering spine everything feeds.
- **gbd-totals-and-caps** — GBD_Totals: fold \_selfBuffs/\_selfEnhance into Totals (defense[0] fold, MezRes/DebuffRes x100, movement, BuffDam min/avg/max heuristic) then TotalsCapped applying
  AT/server caps with the cap-minus-1 convention.
- **set-bonuses-ruleof5** — Set-bonus virtual power: per-power I9SetData tally (SetO only), tier activation (>=2 pieces, PvMode), specials/uniques (>=1 piece), Rule-of-Five (count<6 on shared
  set_bonus power id), exemplar gate on power pick Level<=ForceLevel.
- **legality-predicates** — Enhancement placement legality: standard enh via IsEnhancementValid (ClassID in Power.Enhancements) + set enh via SetType in Power.SetTypes, plus EnhancementTest
  unique/mutex/duplicate build-wide rules; EnhancementTest alone is NOT a complete legality check for standard IOs.
- **updater-protocol** — DB-refresh pull: fetch primary midsreborn manifest, System.Version strict-greater compare, download .mru (zlib+LEB128 container) + .hash sidecar, SHA256-verify, drop into
  Databases/Homecoming/; the beta-channel math-update path.

### Resolved questions

- **Q: Does Mids implement a real-CoH set-level-minus-3 exemplar bonus retention, or only ForceLevel<=power-Level gating?**
  - **A:** Only ForceLevel gating. Mids does NOT implement the real-CoH (set min level - 3) set-bonus retention rule; there is no -3 offset and no consultation of the enhancement set's
    LevelMin/LevelMax anywhere in the bonus-activation path. The single exemplar gate is whether the POWER's pick level <= Config.ForceLevel (default 50); if so, ALL of that power's slotted set
    bonuses are retained at any exemplar depth. A faithful port must NOT add a -3 rule (doing so diverges from Mids, which is a known simplification vs live CoH).
  - Evidence: set-bonuses-ruleof5: Build.cs:1160 `if (Powers[index1] != null && Powers[index1].Level <= MidsContext.Config.ForceLevel)` is the sole gate; a repo-wide search for `- 3`/`LevelMin` found
    LevelMin only in IO-level display/clamping, never in bonus gating. Corroborated by enhancement-value-pipeline (slot value gate is a hard include/exclude at clsToonX.cs:1751
    `Slots[i].Level < ForceLevel`, explicitly noted as separate from set-bonus persistence), at-modifier-tables (base lookup hardcodes level index 49, no exemplar re-index), and data-and-build-formats
    (file readers enforce no exemplar/-3 legality; that is Character.Validate territory).
- **Q: Does current Mids reject Force Feedback in Soul Drain and a Recharge IO in One with the Shield, or accept them?**
  - **A:** Both are REJECTED, but by different mechanisms. Force Feedback (a Knockback-category set, including its +Recharge proc) in Soul Drain is rejected on every path: Soul Drain's Power.SetTypes
    = {MeleeAoE, Scrapper, ToHit, UniversalDamage} contains no Knockback, so EnhancementTest's SetO set-type gate returns false, the picker never renders a Knockback tab, GetValidEnhancementsFromSets
    excludes it, and load-time CheckAndFixAllEnhancements strips it. A common Recharge IO in One with the Shield is also rejected, but NOT by EnhancementTest: One with the Shield's Power.Enhancements
    has no Recharge class, so IsEnhancementValid (Predicate 1, GetValidEnhancements(InventO)) returns false -> the picker never offers it and any imported copy is stripped by
    CheckAndFixAllEnhancements. Critically EnhancementTest itself returns TRUE for the Recharge IO (its set-type gate only fires for SetO), so a naive port routing all placement solely through
    EnhancementTest (as MainWindow2:2762 literally does) would wrongly accept it.
  - Evidence: legality-predicates: Build.cs:1035-1044 set-type gate (setType=Knockback not in Power.SetTypes -> return false) for FF; soul_drain.json allowed_boostset_cats lacks Knockback and
    force_feedback.json group_name=Knockback (SetType 23). For One with the Shield: one_with_the_shield.json boosts_allowed lacks any Recharge class; Power.cs:2104-2124
    GetValidEnhancements/IsEnhancementValid=false; picker Ui.Io omits it and MainWindow2.cs:2600-2606 repeat path returns empty; CheckAndFixAllEnhancements (Build.cs:608) sets Enh=-1. Documented trap:
    EnhancementTest performs no Power.Enhancements class check for Normal/InventO/SpecialO.

### Expression language

- Exists: **True**
- Expressions.Parse exists (Core/Expressions.cs) and is definitively used: an effect with AttribType==eAttribType.Expression carries an Expressions object with three independent string fields
  (Magnitude, Duration, Probability), each evaluated on demand by the Effect getters — Effect.Probability (Parse then clamp [0,1], error->0), Effect.Mag (Parse, else Scale*nMagnitude;*-1 for Damage),
  Effect.Duration (Parse, else Math_Duration/nDuration). Real data uses it, e.g. the fast-snipe magnitude '@StdResult \* (0.211 \* minmax(4.54 \* source>cur.kToHit - 3.41, -1, 1) + 1)'. The evaluator
  the port needs is medium-sized, not a full language: a two-phase string-substitution pass (26 CommandsDict plain-string constant replacements in dict insertion order + 16 FunctionsDict regex
  replacements) that reduces the string to pure infix math, then a Jace4fc infix arithmetic engine (+ - \* / % ^, parens, comma args) with 9 registered custom functions
  (eq/ne/gt/gte/lt/lte/minmax/and/or), plus one hardcoded RPN special-case for a single postfix Magnitude string Jace cannot parse. Faithful behavior requires replicating C# quirks:
  dict-insertion-order replacement with substring-collision/residual-token failures that legitimately yield 0, rand() nondeterminism, and dependence on external subsystems (attrib-mod tables,
  BuffToHit, HPMax, VariableValue, combat-context config) for exact numeric match.

### Top fidelity risks

- Pass ordering and in-place mutation quirks in the GBPA pipeline (high risk): _selfBuffs is empty during Pass1-6 but read by Pass6; Pass5_ResyncEffects RemoveAt-while-iterating skips a comparison
  (must reproduce, not 'fix'); buffed.Accuracy is overwritten before AccuracyMult is computed. Naive reimplementation silently diverges on Accuracy/ToHit routing, the Defiance DamageBuff exclusion,
  generic-vs-typed defense folding (index 0 fold), and the BuffDam min/avg/max headline heuristic.
- GetModifier level index is hardcoded to 49 (character level 50), NOT the character's current/exemplar level, and the class column is an indirection Classes[iClass].Column (not iClass). A port that
  indexes by current level or by raw class index silently produces wrong base magnitudes for every effect.
- ED is applied ONCE on the per-aspect aggregate across all slots, not per slot; IOLevel is 0-based (level-1, so MultIO[49] for a level-50 IO); RelativeLevel None yields a 0.0 multiplier (kills the
  contribution, distinct from Even=1.0); Superior x1.25 is inside the per-effect multiplier before summation. Per-slot ED then summing over-values every enhanced power.
- The exemplar -3 divergence: Mids has NO set-min-minus-3 retention (only power-pick-Level<=ForceLevel gating). Porting the 'correct' live-CoH -3 rule from training memory would make the port diverge
  from the Mids reference exactly where a benchmark expects agreement.
- Legality single-gate trap: EnhancementTest does not class-check standard (Normal/InventO/SpecialO) enhancements — legality for those comes from IsEnhancementValid/picker population + load-time
  CheckAndFixAllEnhancements. A port routing all placement through EnhancementTest alone will wrongly accept illegal standard IOs (e.g. a Recharge IO in One with the Shield).
- Binary .mhd data-file drift (high): Power (~60 fields) and Effect (~90 fields) records are large sequential layouts with enums-as-Int32 and nested variable-length arrays that change across app
  versions; every array count is stored as length-1. Direct byte-for-byte transcription is high-defect — the recommended mitigation is a Mids-driven JSON export stamped with DB version.
- Build-file format asymmetries: .mxd stores power/slot Level raw while .mbd stores +1 (subtract on load); .mxd references by StaticIndex (with ReplTable.FetchAlternate remap + DB-specific migrations:
  Fitness->Invisibility, Fitness nID remap, Afterburner->Evasive_Maneuvers) while .mbd references by UID; IOLevel clamp<=49 only on .mxd. Compression differs: .mbd=Brotli+Base64,
  .mxd=zlib(Z_BEST)+Hex.
- Updater/patch container decode: the .mru payload is a zlib stream (RFC1950, 2-byte header + Adler-32), NOT gzip and NOT raw deflate (do not use wbits=-15/gzip), wrapping a .NET BinaryWriter stream
  with 7-bit LEB128 length-prefixed UTF-8 strings and LE ints in order dataLength,fileName,directory,data. Wrong assumptions silently fail decode.
- Expression evaluator C# quirks: dict-insertion-order plain-string replaces have substring collisions (source>cur.kMeter prefix of ...kMeterAbs) and residual unsubstituted tokens (e.g. leftover
  'source>') make Jace throw and legitimately return 0; rand() is re-rolled every getter access. A port that 'cleans up' these behaviors will mismatch.

### Recommended next (CP2)

Target CP2 = data ingestion, and adopt the sections' explicit recommendation: build a one-time Mids-driven C# JSON export harness (referencing DatabaseAPI) for the large binary .mhd data files rather
than a direct binary parser, stamped with a Homecoming DB-version. Concrete first deliverables, in order: (1) ingest the already-JSON payloads that gate every downstream number —
Databases/Homecoming/AttribMod.json (102 tables, 60 columns, index at fixed level row 49) and Maths.mhd tables (MultED[4]\[3], MultTO/DO/SO/HO, MultIO[53]\[4]); (2) export the archetype/class records
that the JSON files lack — per-AT Column (the class->column indirection) and caps (Hitpoints, HPCap, ResCap as a fraction, DamageCap/RechargeCap/RecoveryCap/RegenCap,
BaseRegen/BaseRecovery/BaseThreat, PerceptionCap) plus the StaticIndex->nID and EnhancementClass name->ID lookup tables and PowersReplTable, since these live in binary I12.mhd/EnhDB.mhd and are needed
for both magnitude math and .mxd build resolution; (3) write the direct build-file parsers for user inputs — the .mbd plain-JSON reader first (simplest, source of truth: CharacterBuildData with -1
Level offsets and the 5-key EnhancementData converter), then the .mxd binary reader (LEB128 strings, length-1 array idiom, zlib+Hex share wrapper, StaticIndex+migrations). Validate the CP2 slice
immediately against the two documented worked assertions (Hasten 3x/2x L50 recharge IOs = 99.08%/83.32%) to confirm the Maths tables + ED pipeline + AttribMod level-49 lookup wire together before
moving to CP3.

---

## data-and-build-formats

**Port risk:** high — Build formats (.mbd/.mxd) are fully specified and directly portable. The .mhd data files are the risk: Power (~60 fields) and Effect (~90 fields) records are large sequential
binary layouts with enums-as-Int32 and nested variable-length arrays that drift across app versions. Faithful direct binary parsing requires transcribing every ctor exactly; the recommended mitigation
is a one-time Mids-driven JSON export of the DB, which trades binary-drift coupling for a regenerate-on-DB-update step. Additional risk: several migrations/nID remaps are DB-specific (homecoming) and
the StaticIndex->nID + ReplTable mapping must be exported alongside the DB for .mxd builds to resolve.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/Core/AppDataPaths.cs` (11-129) — File name constants (I12.mhd, EnhDB.mhd, SData.mhd, GlobalMods.mhd, NLevels/RLevels, etc.) and all header magic strings
  (Db/EnhDb/ServerData/Save.*).
- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (1175-1459, 1628-1761, 2099-2130, 2163-2344, 2356-2585) — All Load*/Save* routines for the .mhd data files: main DB container (I12), ServerData
  (SData), EffectIds (GlobalMods), Levels (N/RLevels text), EnhancementDb, EnhancementClasses (EClasses text), TypeGrades (json), Maths (text), ReplacementTable.
- `<repos>/MidsReborn/MidsReborn/Core/MidsCharacterFileFormat.cs` (32-268, 338-750, 1353-1425) — .mxd (legacy) binary build format: write buffer (MxDBuildSaveBuffer), read (MxDReadSaveData), slot
  read/write, share-string wrapper, hardcoded migrations, IOLevel<=49 clamp, version/format gating.
- `<repos>/MidsReborn/MidsReborn/Core/BuildFile/BuildManager.cs` (37-218, 308-433) — Build-file load/save orchestration: .mbd plain-JSON file load/save, share-data header parsing (5-item ';' header),
  MXD hex path vs MBD base64 path, size-mismatch rejects, DB-name gating.
- `<repos>/MidsReborn/MidsReborn/Core/BuildFile/CharacterBuildData.cs` (37-71, 75-202, 273-494) — .mbd JSON model (source of truth): Update (serialize, +1 level offsets), LoadBuild (deserialize, -1
  offsets), slot/alt-slot mapping, GenerateChunkData/GenerateShareData (header + Brotli+Base64).
- `<repos>/MidsReborn/MidsReborn/Core/BuildFile/DataModels` (all) — JSON DTOs: MetaData, PowerData, SubPowerData, SlotData, EnhancementData (field names + defaults).
- `<repos>/MidsReborn/MidsReborn/Core/BuildFile/EnhancementDataConverter.cs` (1-80) — Custom Newtonsoft converter fixing EnhancementData JSON key order/names and legacy 'Enhancement'->Uid fallback.
- `<repos>/MidsReborn/MidsReborn/Core/Utils/Compression.cs` (12-37) — MBD compression = Brotli (SmallestSize) + Base64.
- `<repos>/MidsReborn/MidsReborn/Core/Utils/ModernZlib.cs` (13-120) — MXD compression = zlib (Z_BEST_COMPRESSION) + Hex; BreakString 67-char bookended line format; Hex encode/decode.
- `<repos>/MidsReborn/MidsReborn/Core/Utils/DataClassifier.cs` (36-54) — Regex classification of pasted share data into Mxd / Mbd / UnkBase64.
- `<repos>/MidsReborn/MidsReborn/Core/FileIO.cs` (21-79) — Text-file tokenizer for N/RLevels, EClasses, Maths: IOGrab (tab split + strip quotes), IOSeek/IOSeekReturn.
- `<repos>/MidsReborn/MidsReborn/Core/LevelMap.cs` (12-28) — Per-level Powers/Slots record parsed from Levels text rows.
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Power.cs` (213-374) — I12.mhd Power record binary reader (~60 sequential fields, enums as Int32, nested Requirement + Effect[]).
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Effect.cs` (87-172) — I12.mhd Effect record binary reader (nested inside each Power).
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Archetype.cs` (89-115) — I12.mhd Archetype record binary reader.
- `<repos>/MidsReborn/MidsReborn/Core/Powerset.cs` (66-94) — I12.mhd Powerset record binary reader.
- `<repos>/MidsReborn/MidsReborn/Core/Requirement.cs` (67-90) — Requirement sub-record binary reader embedded in Power.
- `<repos>/MidsReborn/MidsReborn/Core/Enhancement.cs` (86-142) — EnhDB.mhd Enhancement record binary reader.
- `<repos>/MidsReborn/MidsReborn/Core/EnhancementSet.cs` (61-107) — EnhDB.mhd EnhancementSet record binary reader.

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| I12.mhd header | AppDataPaths.cs:79 | "Mids Reborn Powers Database" |
| DB section markers | AppDataPaths.cs:80-83 | BEGIN:ARCHETYPES / BEGIN:POWERSETS / BEGIN:POWERS / BEGIN:SUMMONS |
| EnhDB.mhd header | AppDataPaths.cs:88 | "Mids Reborn Enhancement Database" |
| SData.mhd header | AppDataPaths.cs:108 | "Mids Reborn Server Data" |
| TypeGrades header | AppDataPaths.cs:113 | "Mids Reborn Types and Grades" |
| Save magic strings | AppDataPaths.cs:118-121 | Compressed=MRBz, Uncompressed=ToonDataVersion, LegacyCompressed=MHDz, LegacyUncompressed=HeroDataVersion |
| Save format version | AppDataPaths.cs:127 | 1.4f |
| Data file names | AppDataPaths.cs:11-23 | I12.mhd, NLevels.mhd, RLevels.mhd, Maths.mhd, EClasses.mhd, Origins.mhd, Salvage.mhd, Recipe.mhd, EnhDB.mhd, GlobalMods.mhd, SData.mhd |
| MxD MagicNumber | MidsCharacterFileFormat.cs:43-49 | bytes ['M','x','D',12] = [0x4D,0x78,0x44,0x0C] |
| MxD magic strings | MidsCharacterFileFormat.cs:32-33 | MagicCompressed="MxDz", MagicUncompressed="MxDu" |
| MxD version gates | MidsCharacterFileFormat.cs:34-35 | PriorVersion=3.10f, ThisVersion=3.20f |
| MxD write flags | MidsCharacterFileFormat.cs:39-41 | UseQualifiedNames=false, UseOldSubpowerFields=true |
| DataLinkMaxLength | MidsCharacterFileFormat.cs:37 | 2048 |
| IOLevel clamp | MidsCharacterFileFormat.cs:1414-1415 | if IOLevel > 49 then 49 |
| Powerset name migrations (.mxd) | MidsCharacterFileFormat.cs:459-467 | Pool.Leadership_beta->Pool.Leadership; Blaster_Support.Atomic_Manipulation->Blaster_Support.Radiation_Manipulation; Pool.Fitness->Pool.Invisibility |
| Fitness nID remap (.mxd, Level==0) | MidsCharacterFileFormat.cs:642-649 | 2553->1521, 2554->1523, 2555->1522, 2556->1524 |
| Afterburner migration (homecoming) | MidsCharacterFileFormat.cs:655-674 | Pool.Flight.Afterburner / Inherent.Inherent.Afterburner(idx<24) -> Pool.Flight.Evasive_Maneuvers |
| Inherent grid sort indices | MidsCharacterFileFormat.cs:283-324 | Brawl=0,Sprint=1,Rest=2,Swift=3,Hurdle=4,Health=5,Stamina=6; Accolade Level forced=49 |
| MBD compression | Compression.cs:16-24 | Brotli CompressionLevel.SmallestSize + Base64 |
| MXD compression | ModernZlib.cs:17, 85-92 | zlib Z_BEST_COMPRESSION (standard zlib stream) + uppercase Hex |
| BreakString params | CharacterBuildData.cs:482 / MidsCharacterFileFormat.cs:186 | line length 67, bookend '\|' |
| MBD share header | CharacterBuildData.cs:484 | \|MBD;{uncompressed};{compressed};{encoded};BASE64;\| |
| MXD share header | MidsCharacterFileFormat.cs:264-267 | \|MxDz;{SzUncompressed};{SzCompressed};{SzEncoded};HEX;\| |
| Classifier regexes | DataClassifier.cs:36-38 | `Mxd=^\\\|MxDz;\d+;\d+;\d+;HEX;\\\| ; Mbd=^\\\|MBD;\d+;\d+;\d+;BASE64;\\\| ; UnkB64=^[A-Za-z0-9+/]+={0,2}$` |
| Header item count | BuildManager.cs:333-342 | exactly 5 (';' split, RemoveEmptyEntries); items[1]=uncompressed, items[2]=compressed |
| EnhancementData JSON defaults | EnhancementData.cs | Uid="", Grade="None", IoLevel=1, RelativeLevel="Even", Obtained=false |
| Maths table dims | DatabaseAPI.cs:2356-2410, 1743 | MultED[4]\[3]; MultTO/DO/SO/HO[1]\[4]; MultIO[53]\[4]; Levels count=50 |
| EnhGrade label arrays | DatabaseAPI.cs:2336-2343 | Long=[None,Training Origin,Dual Origin,Single Origin]; Short=[None,TO,DO,SO] |
| Levels_MainPowers rule | DatabaseAPI.cs:1749-1758 | [0] + indices where LevelMap.Powers>0 |
| Array count idiom | DatabaseAPI.cs:1378, 1397, 1423; MidsCharacterFileFormat.cs:98,105,131 | stored as length-1, read as readInt32()+1 |

### Data and Build Formats — Port Spec

Two independent problem areas:

- **A. `.mhd` data files** (the game database Mids loads at startup). Very large, deeply nested binary/text records. **Recommendation: JSON export, not direct binary parse** (see §A.0).
- **B. `.mbd` / `.mxd` build files** (the user-supplied builds — a *required input*). Python must parse these directly. Full read/write spec in §B.

#### Primitive encoding conventions (apply to ALL binary `.mhd` and `.mxd`)

All binary files use .NET `BinaryReader`/`BinaryWriter`, little-endian:

- `Int32` = 4 bytes LE (signed). `Single` = 4 bytes IEEE-754 LE. `Int64` = 8 bytes LE. `Boolean` = 1 byte (0/1). `SByte` = 1 byte signed.
- **`String` = .NET length-prefixed:** a 7-bit-encoded (LEB128) unsigned length in bytes, then that many UTF-8 bytes. Reimplement `read_string`: accumulate 7-bit groups (each byte's low 7 bits, high
  bit = continue) to get byte length, then read/decode UTF-8. This is NOT a null-terminated or fixed-width string.
- **Enums are stored as `Int32`** (unless noted `SByte`). Read the int, map to the enum by ordinal.
- **Array-length idiom:** counts are written as `length - 1` (`UBound`) and read back as `count = readInt32() + 1`, then `count` elements. Every list in these files uses this `+1`/`-1` convention. A
  naive port that stores the true length will be off by one for every array.

---

#### A. `.mhd` data files

##### A.0 RESOLUTION — parse binary vs. JSON export

**Recommend a one-time Mids-driven JSON export for the data files, consumed by Python.** Rationale from the code:

- The `Power` record (`Power.cs:213-374`) reads ~60 sequential fields plus a nested `Requirement` and a variable-length `Effect[]`, and each `Effect` (`Effect.cs:87-172`) reads ~90 fields. Field sets
  change between app versions (the source has commented-out old field blocks, e.g. `Power.cs:302-306`). Transcribing every ctor byte-for-byte and keeping it in sync with Homecoming DB drift is
  high-risk.
- **Trade:** a JSON export decouples Python from binary layout drift but must be regenerated whenever the Homecoming DB updates (bundle the JSON with a DB-version stamp). Direct binary parsing keeps
  parity with whatever `.mhd` ships but requires exact transcription of every record ctor and per-version field tracking. For a numbers-must-match reimplementation, JSON export is the lower-defect
  path.
- A small C# harness that references `DatabaseAPI` (or Mids' own exporter) can dump `Database.Classes/Powersets/Power/Enhancements/EnhancementSets/…` to JSON once. The container/record locations below
  are the transcription spec if a direct parser is nonetheless required.

The **build files (§B) must still be parsed directly** because they are user inputs.

##### A.1 `I12.mhd` — main powers database container (`DatabaseAPI.cs:1461-1459` read; `:1244-1310` write)

Sequential read after opening:

1. `String` header, must equal `"Mids Reborn Powers Database"` (else abort).
2. `String` Version (`Version.Parse`).
3. `Int32` year. **If year > 0:** read `Int32` month, `Int32` day → `DateTime`. **Else** read `Int64` → `DateTime.FromBinary`. (Writer always writes `-1` then `Int64 ToBinary` —
    `DatabaseAPI.cs:1266-1267` — so the else branch is the live path; the year>0 branch is legacy.)
4. `Int32` Issue. `Int32` PageVol. `String` PageVolText.
5. `String` == `"BEGIN:ARCHETYPES"`. `Int32` (n-1). Then `n` × Archetype record (`Archetype.cs:89-115`).
6. `String` == `"BEGIN:POWERSETS"`. `Int32` (n-1). Then `n` × Powerset record (`Powerset.cs:66-94`).
7. `String` == `"BEGIN:POWERS"`. `Int32` (n-1). Then `n` × Power record (`Power.cs:213-374`). Each Power embeds a `Requirement` (`Requirement.cs:67-90`) at field position after `Available`, and an
    `Effect[]` near the end (`Int32` (n-1) then `n` × `Effect.cs:87-172`).
8. `String` == `"BEGIN:SUMMONS"`. Then `Database.LoadEntities(reader)` — summon/entity tables (leaf, not transcribed here).

**Archetype record** (`Archetype.cs:89-115`), in order: `String` DisplayName; `Int32` Hitpoints; `Single` HPCap; `String` DescLong; `Single` ResCap; `Int32` originCount then (originCount+1) × `String`
Origin; `String` ClassName; `Int32` ClassType(enum); `Int32` Column; `String` DescShort; `String` PrimaryGroup; `String` SecondaryGroup; `Boolean` Playable; `Single` RechargeCap; `Single` DamageCap;
`Single` RecoveryCap; `Single` RegenCap; `Single` BaseRecovery; `Single` BaseRegen; `Single` BaseThreat; `Single` PerceptionCap.

**Powerset record** (`Powerset.cs:66-94`), in order: `String` DisplayName; `Int32` nArchetype; `Int32` SetType(enum); `String` ImageName; `String` FullName (if empty →
`"Orphan."+DisplayName.replace(' ','_')`); `String` SetName; `String` Description; `String` SubName; `String` ATClass; `String` UIDTrunkSet; `String` UIDLinkSecondary; `Int32` num; then (num+1) × {
`String` UIDMutexSets[i], `Int32` nIDMutexSets[i] }.

**Power record** (`Power.cs:240-374`): too long to transcribe safely; parse the ctor in file order exactly. First fields: `Int32` StaticIndex; `String` FullName, GroupName, SetName, PowerName,
DisplayName; `Int32` Available; then `Requirement(reader)`; `Int32` ModesRequired, ModesDisallowed, PowerType (all enums); `Single` Accuracy; … continues through `Effects` array and ends with
`Boolean` HiddenPower, Active, Taken; `Int32` Stacks; `Int32` VariableStart. **Key field for builds: `StaticIndex` (Int32, first field)** — this is the stable ID `.mxd` build files reference.

##### A.2 `EnhDB.mhd` — enhancement database (`DatabaseAPI.cs:2163-2236` read; `:2132-2161` write)

1. `String` header == `"Mids Reborn Enhancement Database"`.
2. `Single` VersionEnhDb (read and discarded).
3. `Int32` (n-1). Then `n` × Enhancement record (`Enhancement.cs:86-142`). Enhancement carries `StaticIndex`, `UID`, `TypeID` (enum `eType`: None/Normal/InventO/SetO/SpecialO) — all three used by
    build I/O in §B.
4. `Int32` (n-1). Then `n` × EnhancementSet record (`EnhancementSet.cs:61-107`).

##### A.3 `SData.mhd` — server data (legacy binary; `DatabaseAPI.cs:1181-1242`)

Preferred source is `SData.json` (`ServerData.Load`, `DatabaseAPI.cs:1177-1178`); the `.mhd` proforma is fallback. Binary order: `String` header == `"Mids Reborn Server Data"`; `String` ManifestUri;
`Int32` MaxSlots; `Single` × 9: BaseFlySpeed, BaseJumpHeight, BaseJumpSpeed, BasePerception, BaseRunSpeed, BaseToHit, MaxFlySpeed, MaxJumpSpeed, MaxRunSpeed; `Boolean` EnableInherentSlotting; `Int32`
× 6: HealthSlots, HealthSlot1Level, HealthSlot2Level, StaminaSlots, StaminaSlot1Level, StaminaSlot2Level. (Note: Homecoming sets `EnableInherentSlotting=false` — relevant to the 67-slot rule
elsewhere.) **Port note: prefer parsing `SData.json`.**

##### A.4 `GlobalMods.mhd` — effect id strings (`DatabaseAPI.cs:1628-1668`)

`Int32` count; then `count` × `String`. (No header.) These strings back `Effect.EffectId`.

##### A.5 Text data files (tab-delimited, via `FileIO.IOGrab`)

`IOGrab` (`FileIO.cs:21-33`): reads one line, splits on `\t`, strips a leading `'` or space and a trailing space, and removes all `"` (char 34). `IOSeek`/`IOSeekReturn` scan lines until column 0
equals a marker. Ports must replicate the strip rules exactly.

- **`NLevels.mhd` / `RLevels.mhd`** (Normal/LevelUp vs Respec mode; `DatabaseAPI.cs:1705-1761`): skip lines until a row whose col0 == `"Level"`; then read exactly **50** rows. Each row → `LevelMap`
  (`LevelMap.cs:12-28`): `Powers = int(col1)` (0 if unparseable); `Slots = int(col2)` if present (0 otherwise). Then `Levels_MainPowers` = `[0] + [index for index where Levels[index].Powers > 0]`.
  This array drives power-slot placement and the fallback level assignment in build loading (§B).
- **`EClasses.mhd`** (`DatabaseAPI.cs:2238-2283`): seek `"Version:"`, then seek `"Index"`; read rows until col0 == `"End"`. Each row = `[ID:int, Name, ShortName, ClassID, Desc]`.
- **`Maths.mhd`** (`DatabaseAPI.cs:2412-2549`, init `:2356-2410`): seek `"Version:"`, seek `"EDRT"`. Defaults: all multipliers init to `1.0`. Then:
  - **ED (`MultED[4][3]`):** 3 rows; each row cols 1..4 → `MultED[col-1][row]` (i.e. `MultED[j][i]` for j=0..3, i=0..2).
  - seek `"EGE"`: 1 row × 4 floats → `MultTO[0][0..3]`; next row → `MultDO[0][0..3]`; next → `MultSO[0][0..3]`; next → `MultHO[0][0..3]`.
  - seek `"LBIOE"`: **53** rows × 4 floats → `MultIO[0..52][0..3]`.
  - Any parse failure defaults that cell to 1.0 and flags a pass error (ED=1, TO=2, DO=3, SO=4, HO=5, IO=6).

##### A.6 JSON data files

- **`TypeGrades.json`** (`DatabaseAPI.cs:2285-2344`): object with `"Name"` == `"Mids Reborn Types and Grades"`; arrays `SetTypes[]`, `EnhancementGrades[]`, `SpecialEnhancementTypes[]`, each element
  deserialized to `TypeGrade` with `Index` assigned by array position. Hardcoded (not from file): `EnhGradeStringLong = ["None","Training Origin","Dual Origin","Single Origin"]`,
  `EnhGradeStringShort = ["None","TO","DO","SO"]`.
- **`SData.json`**: preferred server-data source (see A.3).

##### A.7 `PowersReplTable.mhd` — static-index replacement table (`DatabaseAPI.cs:2099-2112`)

Loaded into `Database.ReplTable`. Provides `FetchAlternate(staticIndex, className) -> altStaticIndex` used when loading `.mxd` (§B.3 step 6). A Python `.mxd` reader needs this mapping to resolve
renamed/moved powers. Leaf format not transcribed; export alongside the DB.

---

#### B. Build files — `.mbd` (modern JSON) and `.mxd` (legacy binary)

There are two distinct on-disk forms and one shared-text form:

- **`.mbd` file on disk** = plain UTF-8 **JSON** (`Formatting.Indented`), no header, no compression. Written by `BuildManager.SaveToFile` (`BuildManager.cs:204-218`), read by
  `LoadFromFile`/`LoadFromFileLight` (`:37-202`).
- **`.mbd` shared chunk** = header line + Base64(Brotli(JSON)). Produced by `CharacterBuildData.GenerateChunkData` (`CharacterBuildData.cs:476-485`).
- **`.mxd` shared/data-link** = header line + Hex(zlib(binary buffer)). Legacy. Produced by `MxDBuildSaveString` (`MidsCharacterFileFormat.cs:189-268`).

##### B.1 Shared-text wrapper (both `.mbd` and `.mxd` paste blocks)

Classification (`DataClassifier.cs:36-53`) by first-line regex:

- `.mxd`: `^\|MxDz;[0-9]+;[0-9]+;[0-9]+;HEX;\|`
- `.mbd`: `^\|MBD;[0-9]+;[0-9]+;[0-9]+;BASE64;\|`
- else if `^[A-Za-z0-9+/]+={0,2}$` → `UnkBase64` (treated as a raw Base64 `.mbd` share payload).

Header/payload extraction (`BuildManager.cs:320-351`):

1. Split content on `\r`/`\n` (remove empty entries). Need ≥ 2 lines.
2. `header = lines[0].trim('|').trim()`. Split on `;` (remove empty) → **must be exactly 5 items**: `[magic, uncompressedSize, compressedSize, encodedSize, encoding]`.
    `uncompressedSize = int(items[1])`, `compressedSize = int(items[2])`. Any non-5 count or non-int size → reject.
3. `data = "".join(lines[1:]).replace("|","")` — concatenate all payload lines and strip the `|` bookends that `BreakString` added.

`BreakString(s, 67, bookend=true)` (`ModernZlib.cs:96-120`): the payload is emitted as 67-char chunks, each wrapped `|...|`, newline-separated. Readers must reverse this by removing `|` and joining.

##### B.2 `.mxd` payload decode (`BuildManager.cs:364-382`)

1. Validate `data` matches hex regex `\A\b[0-9A-F]+\b\Z` (case-insensitive).
2. `bytes = hex_decode(data)` (each 2 hex chars → 1 byte; `ModernZlib.cs:71-83`).
3. **Reject if `len(bytes) != compressedSize`.**
4. `buffer = zlib_decompress(bytes)` then truncate/resize to `uncompressedSize` (`ModernZlib.cs:25-42`). Compression on write is **zlib `Z_BEST_COMPRESSION`** (`ModernZlib.cs:13-23`) — standard zlib
    stream (has zlib header), NOT raw deflate, NOT gzip.
5. `MxDReadSaveData(buffer)` → §B.3.

##### B.3 `.mxd` binary buffer layout (`MxDReadSaveData`, `MidsCharacterFileFormat.cs:338-750`; write `MxDBuildSaveBuffer:83-152`)

Constants (`:32-49`): `MagicNumber = bytes [0x4D,0x78,0x44,0x0C]` = `'M','x','D',12`. `PriorVersion=3.10`, `ThisVersion=3.20`. Write defaults: `UseQualifiedNames=false`, `UseOldSubpowerFields=true`.

Read order:

1. **Magic scan:** read 4 bytes at offset 0; if not equal to `MagicNumber`, advance offset by 1 and retry (tolerant of leading garbage). If stream ends first → reject.
2. `Single` fVersion. Gate (`:406-422`): `> 3.20` → reject ("newer version"). `< 3.10` → format = **Legacy**. `[3.10, 3.20)` → **Prior**. `== 3.20` → **Current**.
3. `Boolean` qualifiedNames. `Boolean` hasSubPower.
4. `String` classUID → resolve to nID; if invalid (<0) → reject.
5. `String` originUID → origin nID (scoped to class).
6. If `fVersion > 1`: `Int32` Alignment (enum).
7. `String` character Name.
8. `Int32` powerSetCount (=`length-1`). Then read `(powerSetCount+1)` × `String` powerset FullName, applying **migrations** (`:459-467`): `"Pool.Leadership_beta"→"Pool.Leadership"`,
    `"Blaster_Support.Atomic_Manipulation"→"Blaster_Support.Radiation_Manipulation"`, `"Pool.Fitness"→"Pool.Invisibility"`. Load powersets by name.
9. `Int32` then `LastPower = value - 1` (**-1 offset**).
10. `Int32` powerCount (=`length-1`); loop `(powerCount+1)` power entries:
    - Power reference: if qualifiedNames → `String` powerUID; else → `Int32` staticIndex, then `staticIndex = ReplTable.FetchAlternate(staticIndex, className)` if that returns ≥0, then map
      staticIndex→nID (`NidFromStaticIndexPower`). Write side stores `Power[nID].StaticIndex` or `-1` for empty (`:108-114`).
    - If the reference is present (`staticIndex > -1` or non-empty UID):
      - `SByte` Level (**no +/-1 offset in `.mxd`** — stored directly).
      - Format-dependent fields (`:528-545`):
        - **Current:** `Boolean` StatInclude, `Boolean` ProcInclude, `Int32` VariableValue, `Int32` InherentSlotsUsed.
        - **Prior:** `Boolean` StatInclude, `Boolean` ProcInclude, `Int32` VariableValue.
        - **Legacy:** `Boolean` StatInclude, `Int32` VariableValue.
      - If hasSubPower (`:547-594`): `SByte` subCount (=`length-1`); then `(subCount+1)` × { power ref (UID or `Int32` staticIndex→nID); `Boolean` StatInclude }.
    - If nID<0 and this index < `Levels_MainPowers.Length`: set Level = `Levels_MainPowers[index]` (fallback).
    - `SByte` slotCount (=`length-1`); then `(slotCount+1)` × slot:
      - `SByte` Level (**no offset**).
      - `Boolean` IsInherent — **only read in Current format**; in Prior/Legacy it is `false` (not present on the wire) (`:608`).
      - `ReadSlotData` → primary enhancement (§B.5).
      - `Boolean` hasAlt; if true, `ReadSlotData` → flipped/alt enhancement.
    - Fitness migration (`:640-653`): if `Level==0` and power's FullSetName == `"Pool.Fitness"`, remap nID: `2553→1521, 2554→1523, 2555→1522, 2556→1524` (DB-specific nIDs).
    - Afterburner migration, **homecoming DB only** (`:655-674`): if FullName == `"Pool.Flight.Afterburner"` or (`"Inherent.Inherent.Afterburner"` and index<24) → remap to
      `"Pool.Flight.Evasive_Maneuvers"`.
11. After the loop, inherent powers are re-sorted into fixed grid order (`SortGridPowers`, `:270-336`): Class, then Inherent (Brawl,Sprint,Rest,Swift,Hurdle,Health,Stamina at fixed indices 0..6),
    Powerset, Power, Prestige, Incarnate, Accolade (Level forced to 49), Pet, Temp.

**Write side** mirrors read: `MagicNumber` bytes, `Single ThisVersion(3.20)`, `Bool UseQualifiedNames(false)`, `Bool UseOldSubpowerFields(true)`, class UID, origin, `Int32` Alignment, Name,
`Int32 (Powersets.Length-1)` + FullNames, `Int32 (LastPower+1)`, `Int32 (Powers.Count-1)` + power records (staticIndex or -1, `SByte` Level, flags, subpowers `SByte(len-1)`, slots `SByte(len-1)` each
with `SByte` Level, `Bool` IsInherent, slot data, `Bool includeAltEnh`, optional alt slot data).

##### B.4 `.mbd` JSON schema (`CharacterBuildData`, `CharacterBuildData.cs`)

Top-level object (`:37-71`):

- `BuiltWith`: `{ App: string, Version: "x.y.z", Database: string, DatabaseVersion: "x.y.z" }` (`MetaData`). On load, `Database` must equal the active DB name or load is refused/prompted
  (`BuildManager.cs:74-77, 114-134`); version mismatch is warn-only.
- `Level`: string (character level, e.g. `"50"`).
- `Class`: string (class UID, e.g. `"Class_Blaster"`).
- `Origin`: string.
- `Alignment`: string (enum name, e.g. `"Hero"`).
- `Name`: string. `Comment`: string?.
- `PowerSets`: `string[]` (each powerset FullName; `""` for an empty slot).
- `LastPower`: int = engine `CurrentBuild.LastPower + 1` (**+1 offset**; read: `LastPower - ...`— actually LoadBuild assigns `CurrentBuild.LastPower = LastPower` directly at `:284`, i.e. it does NOT
  subtract 1. See B.6 gotcha).
- `PowerEntries`: `PowerData[]`.

`PowerData` (`PowerData.cs`; write `:104-145`, read `:288-357`):

- `PowerName`: string FullName (empty entries are serialized as a default `PowerData` with `PowerName=""`, `Level=-1`, empty lists).
- `Level`: int = engine `Level + 1` (**+1**; read: engine `Level = Level - 1`, `:317`).
- `StatInclude`, `ProcInclude`: bool. `VariableValue`, `InherentSlotsUsed`: int.
- `SubPowerEntries`: `SubPowerData[]` = `{ PowerName: string, StatInclude: bool }`.
- `SlotEntries`: `SlotData[]`.

`SlotData` (`SlotData.cs`; write `:134-144`, read `:359-376`):

- `Level`: int = engine slot `Level + 1` (**+1**; read: engine `Level = Level - 1`, `:371`).
- `IsInherent`: bool.
- `Enhancement`: `EnhancementData?` (null if slot empty). `FlippedEnhancement`: `EnhancementData?`.

`EnhancementData` (`EnhancementData.cs` + `EnhancementDataConverter.cs`): **always these 5 keys in this order** (converter enforces): `Uid` (string), `Grade` (string enum `eEnhGrade`, default
`"None"`), `IoLevel` (int, default `1`), `RelativeLevel` (string enum `eEnhRelative`, default `"Even"`), `Obtained` (bool, default false). On read (`:36-64`) the converter also accepts a legacy
`"Enhancement"` key (resolves to `Uid` via `GetEnhancementUid`); a JSON `null` token → no enhancement. `Grade`/`RelativeLevel` are parsed only for Normal/SpecialO; `IoLevel`+`RelativeLevel` for
InventO/SetO (`CharacterBuildData.cs:164-173, 445-473`).

##### B.5 `ReadSlotData` / `WriteSlotData` (`.mxd` binary slot; `MidsCharacterFileFormat.cs:1381-1425` / `:1353-1379`)

Read:

1. Enhancement ref: if qualifiedNames → `String` UID; else → `Int32` staticIndex → nID (`NidFromStaticIndexEnh`). If nID ≤ -1 → empty slot, return.
2. Switch on the enhancement's `TypeID`:
    - **Normal / SpecialO:** `SByte` RelativeLevel(enum `eEnhRelative`); `SByte` Grade(enum `eEnhGrade`).
    - **InventO / SetO:** `SByte` IOLevel; **clamp `if IOLevel > 49: IOLevel = 49`**; then if `fVersion > 1.0`: `SByte` RelativeLevel.
    - **None:** nothing.

Write mirrors: `Int32 -1` if empty; else `Int32 Enhancements[Enh].StaticIndex`; if Normal/SpecialO → `SByte` RelativeLevel, `SByte` Grade; if InventO/SetO → `SByte` IOLevel, `SByte` RelativeLevel.

##### B.6 `.mbd` chunk/share compression (`CharacterBuildData.cs:476-494`, `Compression.cs`)

`GenerateChunkData`: JSON (`Formatting.None`, UTF-8) → **Brotli `CompressionLevel.SmallestSize`** → **Base64** → `BreakString(67, bookend=true)`. Header:
`|MBD;{uncompressed};{compressed};{encoded};BASE64;|\r\n{payload}` where `uncompressed`=source byte length, `compressed`=Brotli byte length, `encoded`=Base64 char length. Load path
(`BuildManager.cs:384-396`): Base64-decode, Brotli-decompress; **reject if decoded compressed length != header compressedSize**; deserialize JSON via `LoadShareData`. Note `.mbd` uses
**Brotli+Base64**; `.mxd` uses **zlib+Hex** — do not swap them.

##### B.7 RESOLUTION — legality: what the loaders accept vs reject

Definitive, from code:

**Reject (fatal):**

- Shared header not exactly 5 `;`-items, or sizes not integers (`BuildManager.cs:333-347`).
- `.mxd`: payload not pure hex, or `hex_decode` length ≠ header compressedSize (`:366-377`).
- `.mbd`: payload not valid Base64, or Brotli-decoded compressed length ≠ header compressedSize (`:386-396`).
- `.mxd` binary: empty buffer; magic `MxD\x0C` never found; `fVersion > 3.20`; invalid class UID (`MidsCharacterFileFormat.cs:343-437`).
- `.mbd` JSON: unparseable JSON; missing `BuiltWith`; `BuiltWith.Database` ≠ active DB **and** that DB not installed (`BuildManager.cs:56-122`).

**Accept with warning / non-fatal:**

- DB *version* older/newer than build's (warn, continue) (`BuildManager.cs:138-198`).
- Powerset-by-name load failures (message, continue) (`:472-476`).
- Individual power read exceptions are caught; loader returns false but keeps partial data (`:725-733`).
- `.mxd` versions between 1.0 and 3.20 load via the Prior/Legacy field subsets (not rejected).

**Out of scope for this section:** the exemplar `-3` set-bonus rule and slot/level *build-legality* validation (24 picks, 67 slots, etc.) are not enforced in the file readers — they belong to the
character/validation subsystem (`MidsContext.Character.Validate()` at `:736`), not to format parsing. Flag to the assembler that "exemplar -3 / accept-reject legality" for game rules is resolved
elsewhere; the format layer only enforces the structural/version/size/DB-name checks above.

---

#### Cross-format gotchas a naive port will miss

1. **`+1`/`-1` offsets differ by format.** `.mxd` stores power Level and slot Level **raw** (no offset); `.mbd` stores them **+1** and subtracts 1 on load. `LastPower` is `+1` on write in *both*
    formats, but `.mxd` read subtracts 1 (`MidsCharacterFileFormat.cs:478`) while `.mbd` `LoadBuild` assigns `LastPower` **without** subtracting (`CharacterBuildData.cs:284`) — replicate each exactly.
2. **Array counts are `length-1`** in all binary files; add 1 when reading. JSON arrays are natural length.
3. **`.mxd` references powers/enh by `StaticIndex`** (with `ReplTable.FetchAlternate` remap first); `.mbd` references by **UID string**. Migrations (Leadership_beta, Fitness→Invisibility, Fitness nID
    remap, Afterburner→Evasive_Maneuvers) are applied **only in the `.mxd` reader**, not in `.mbd` `LoadBuild`.
4. **IOLevel clamp to ≤ 49** happens only on `.mxd` binary read for InventO/SetO. `.mbd` stores raw `IoLevel` (default 1).
5. **Compression/encoding are format-specific:** `.mbd`=Brotli+Base64; `.mxd`=zlib(Z_BEST_COMPRESSION, standard zlib stream)+uppercase Hex. Both wrap payload in 67-char `|...|` bookended lines.
6. **.NET length-prefixed UTF-8 strings** (7-bit length) everywhere in binary — not null-terminated.
7. **`.mbd` on disk = indented plain JSON**; the `|MBD;...|` header form only appears in copy/paste share chunks. A file-loader and a paste-loader are two different entry points.

**Open items / gaps:**

- Full field-by-field transcription of Power (Power.cs:213-374), Effect (Effect.cs:87-172), Enhancement (Enhancement.cs:86-142), EnhancementSet (EnhancementSet.cs:61-107), Requirement
  (Requirement.cs:67-90) NOT reproduced field-by-field here — cited by exact line range; direct binary port must transcribe them in file order (enums as Int32). Recommendation is JSON export to
  sidestep this.
- Database.LoadEntities / StoreEntities (summon tables after BEGIN:SUMMONS) not read — leaf; format undocumented here.
- PowersReplTable.mhd and CrypticPowerNames.mhd on-disk formats not decoded (loaded via PowersReplTable.Initialize / CrypticReplTable.Initialize). FetchAlternate(staticIndex, className) mapping must
  be exported for .mxd loading.
- NidFromStaticIndexPower / NidFromStaticIndexEnh / NidFromUidClass / NidFromUidOrigin / GetEnhancementByUIDName lookup builders not traced — they map wire IDs to in-memory indices; Python needs
  equivalent lookup tables from the exported DB.
- SData.json and AttribMod.json / TypeGrade object schemas only partially covered (top-level keys); per-object TypeGrade fields not enumerated.
- Recipe.mhd, Salvage.mhd, Origins.mhd binary record layouts not detailed (not required for build parsing).
- eType / eEnhGrade / eEnhRelative / eGridType and the ~dozen enums read as Int32 in Power/Effect: ordinal->name maps not transcribed; must be lifted from Enums.cs for a direct binary port.
- The .mxd DateTime year>0 branch (I12 header) is legacy/unused by the current writer (writes -1 + Int64); confirm no shipped DB uses the year>0 form if parsing old .mhd files.
- Confirm whether .mbd LoadBuild intentionally does NOT subtract 1 from LastPower (CharacterBuildData.cs:284 assigns raw) while Update adds +1 (line 98) — appears asymmetric vs the .mxd path; flag as
  possible upstream bug or intended semantics to verify against a round-trip sample.

---

## at-modifier-tables

**Port risk:** medium — The lookup algorithm and constants are fully transcribed and low-ambiguity, but exact number-matching depends on two data payloads not fully enumerated here: the 102-table
AttribMod.json (available verbatim in-repo) and the per-AT Column+caps records inside binary I12.mhd (not decoded this pass). The two highest-risk gotchas — fixed level index 49 and the Column
indirection — are documented, but a port cannot match numbers without the class-record export.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (2609-2647) — GetModifier(IEffect) public entry + private GetModifier(iClass,iTable,iLevel) table lookup with bounds checks
- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (98-119) — NidFromUidAttribMod: table-name string to table index
- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (122-133) — NidFromUidClass: class-name string to class index
- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (3001-3010) — MatchModifierIDs: resolves every effect.ModifierTable string to nModifierTable int at load
- `<repos>/MidsReborn/MidsReborn/Core/Modifiers.cs` (124-184) — ModifierTable class: Table=List<List\<float>> [level]\[classColumn], BaseIndex, ID; JSON + binary loaders
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Effect.cs` (390-403) — Effect.Mag: (Damage?-1:1)*Scale*nMagnitude*GetModifier(this); the caller that turns a table cell into a number
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Effect.cs` (409-422) — Effect.Duration: Duration attribType uses Scale*GetModifier(this)
- `<repos>/MidsReborn/MidsReborn/Core/Base/Master_Classes/MidsContext.cs` (21-26) — MathLevelBase=49 constant; static Archetype used as default class
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Archetype.cs` (89-115) — Archetype binary read order incl. Column int and all caps (HP/Res/Recharge/Damage/Recovery/Regen)
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Archetype.cs` (30-58) — Archetype default constructor cap fallbacks (HPCap 5000, ResCap 90, DamageCap 4, RechargeCap 5, RecoveryCap 5, RegenCap
  20, PerceptionCap 1153)
- `<repos>/MidsReborn/MidsReborn/Core/ServerData.cs` (26-110) — ServerData constants: BaseToHit 0.75, MaxSlots 67, EnableInherentSlotting false, Health/Stamina slots, speed caps
- `<repos>/MidsReborn/MidsReborn/Core/Build.cs` (85-95) — TotalSlotsAvailable resolves 67 (EnableInherentSlotting false path)
- `<repos>/MidsReborn/MidsReborn/Core/Expressions.cs` (540-568) — Alternate lookup ModifierCaster: row 49 (maxPlayerLevel-1) x column = index within PLAYABLE-filtered class list
- `<repos>/MidsReborn/MidsReborn/Core/Expressions.cs` (627-634) — Alternate lookup GetModifier(expr): row=Character.Level (not 49), col=Archetype.Column
- `<repos>/MidsReborn/MidsReborn/Databases/Homecoming/AttribMod.json` (1-346889) — The 102 modifier tables themselves (data payload to port verbatim)
- `<repos>/MidsReborn/MidsReborn/UI/Forms/OptionsMenuItems/DbEditor/frmPowerBrowser.cs` (279-279) — Commented-out `Classes[index].Column = index` confirming Column normally equals class index

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| MathLevelBase | Core/Base/Master_Classes/MidsContext.cs:21 | 49 (const int; = character level 50; the fixed row index for all base magnitude lookups) |
| GetModifier out-of-bounds return | Core/DatabaseAPI.cs:2635-2646 | 0.0 for any of: iClass<0, iTable<0, iLevel<0, iClass>Classes.Length-1, column<0, iTable>tables-1, iLevel>rows-1, column>row width-1 |
| Effect.Mag formula | Core/Base/Data_Classes/Effect.cs:394-402 | (Damage?-1:1) \* Scale \* nMagnitude \* GetModifier(effect) |
| Effect.Duration formula (Duration attribType) | Core/Base/Data_Classes/Effect.cs:418 | Scale * GetModifier(effect) when \|Math_Duration\|<=0.01 |
| Default effect ModifierTable | Core/Base/Data_Classes/Effect.cs:43 | "Melee_Ones" (all-1.0 table) |
| Absorbed_Class_nID default | Core/Base/Data_Classes/Effect.cs:48 | -1 (means: use selected Archetype.Idx) |
| Modifier table shape | Core/Modifiers.cs:124-153 | Table=List<List\<float>> [levelIndex]\[classColumn]; fields ID, BaseIndex(unused), Table |
| Homecoming AttribMod table count | Databases/Homecoming/AttribMod.json (parsed) | 102 tables; 60 columns; most 55 rows, tables 89-93 have 50 rows |
| Melee_Damage row49 cols0-9 | Databases/Homecoming/AttribMod.json (table ID Melee_Damage) | [-55.6102,-30.5856,-30.5856,-62.5615,-52.8297,-47.2687,-47.2687,-61.1712,-41.7077,-55.6102] |
| Archetype.Column | Core/Base/Data_Classes/Archetype.cs:102,153; frmPowerBrowser.cs:279 | per-class stored int (class->column indirection); normally == class index |
| BaseToHit | Core/ServerData.cs:29 | 0.75f |
| MaxSlots | Core/ServerData.cs:42; resolved in Build.cs:85-95 | 67 |
| EnableInherentSlotting | Core/ServerData.cs:43; Build.cs:89-93 | false (=> TotalSlotsAvailable is a flat 67) |
| HealthSlots / StaminaSlots | Core/ServerData.cs:44-49 | 2 / 2 (slot levels Health 8,16; Stamina 12,22) — NOT added because EnableInherentSlotting=false |
| AT cap default fallbacks | Core/Base/Data_Classes/Archetype.cs:32-45 | HPCap 5000, Hitpoints 5000, ResCap 90, DamageCap 4, RechargeCap 5, RecoveryCap 5, RegenCap 20, PerceptionCap 1153 |
| AT record field read order (caps) | Core/Base/Data_Classes/Archetype.cs:89-115 | Hitpoints, HPCap, ...ResCap..., ClassName, ClassType, Column, ...Playable, RechargeCap, DamageCap, RecoveryCap, RegenCap, BaseRecovery, BaseRegen, BaseThreat, PerceptionCap |
| Speed bases/caps | Core/ServerData.cs:30-41 | BaseRun 21, BaseJumpSpeed 21, BaseJumpHeight 4, BaseFly 31.5, BasePerception 500, MaxRun 135.67, MaxFly 86, MaxJumpSpeed 114.40, MaxJumpHeight 200 |

### Section: AT Modifier / Attribute Base Tables

#### 1. Purpose

Resolves a power effect's abstract "scale" into a concrete magnitude for a given
archetype (AT) and level, by looking up a value in a named modifier table. This
is the mechanism behind every damage/heal/mez/buff base number MidsReborn shows.

The final magnitude formula (from `Effect.Mag`, Effect.cs:394-402) is:

```text
Mag = signFlag * Scale * nMagnitude * GetModifier(effect)
```

where `signFlag = -1 if EffectType == Damage else +1` (damage table cells are
stored NEGATIVE; the sign flip makes the displayed number positive).

`GetModifier(effect)` returns one cell from a 2-D table:
`table[ID].Table[levelIndex][classColumn]`.

#### 2. The lookup path (authoritative — DatabaseAPI.GetModifier)

##### 2a. Public entry `GetModifier(IEffect iEffect)` — DatabaseAPI.cs:2609-2630

Stepwise:

1. `iClass = 0` (default fallback), `iLevel = MidsContext.MathLevelBase = 49`
    (a compile-time constant, see §4 — NOT the character's displayed level).
2. `effPower = iEffect.GetPower()` (the power that owns this effect).
3. If `effPower == null`:
    - If the effect has no attached Enhancement -> return `1.0`.
    - Else return `GetModifier(iClass=0, iEffect.nModifierTable, 49)`.
4. Otherwise choose the class column source in this priority order:
    - If `effPower.ForcedClass` is a non-empty string:
      `iClass = NidFromUidClass(effPower.ForcedClass)` (e.g. pet powers force
      the pet's class).
    - Else if `iEffect.Absorbed_Class_nID > -1`: `iClass = Absorbed_Class_nID`
      (effect absorbed/granted from another class; default field value is -1).
    - Else `iClass = MidsContext.Archetype.Idx` (the build's currently selected
      archetype — the normal case).
5. Return `GetModifier(iClass, iEffect.nModifierTable, 49)`.

##### 2b. Private `GetModifier(int iClass, int iTable, int iLevel)` — DatabaseAPI.cs:2632-2647

```text
if iClass  < 0: return 0
if iTable  < 0: return 0
if iLevel  < 0: return 0
if iClass  > Classes.Length - 1: return 0
iClassColumn = Classes[iClass].Column          # per-AT stored int, see §3
if iClassColumn < 0: return 0
if iTable  > AttribMods.Modifier.Count - 1: return 0
if iLevel  > AttribMods.Modifier[iTable].Table.Count - 1: return 0
if iClassColumn > AttribMods.Modifier[iTable].Table[iLevel].Count - 1: return 0
return AttribMods.Modifier[iTable].Table[iLevel][iClassColumn]
```

Note the ordering nuance: the column is `Classes[iClass].Column`, an indirection
— NOT `iClass` itself (see §3). Every out-of-range condition returns `0.0`, and
`iTable == 0` is explicitly called out in-source as dangerous (a real table sits
at index 0, so a stray 0 silently returns Melee_Damage values). A port must
carry the `nModifierTable == -1 -> returns 0` behavior: an unresolved table name
yields 0, not table 0.

#### 3. Class -> column mapping

`Archetype.Column` (Archetype.cs:153) is a per-class **stored integer** read from
the class record (binary order at Archetype.cs:102: it is the field read
immediately after `ClassType`). It is NOT recomputed at load. In practice it
equals the class's own index in `Database.Classes` (confirmed by the historical
setup line `Classes[index].Column = index`, frmPowerBrowser.cs:279, now
commented, and by the DB editor which writes table cells at
`[cbArchetype.SelectedIndex]` = raw class index). The field exists so special
classes (pets, NPC variants) can point their `Column` at another class's column
to share a modifier profile. A port MUST read and honor each class's `Column`
value rather than assume `column == class index`.

The Homecoming `AttribMod.json` tables are **60 columns wide** => there are 60
class columns. Row width is uniform within a table.

`NidFromUidClass(name)` (DatabaseAPI.cs:122-133) is a case-insensitive lookup of
`ClassName` -> index in `Database.Classes` (cached in a dict); returns -1 if not
found.

#### 4. Level index — CRITICAL, fixed at 49

`MidsContext.MathLevelBase` is `public const int = 49` (MidsContext.cs:21). The
authoritative `GetModifier(IEffect)` path **always** passes `iLevel = 49`,
regardless of the character's displayed/exemplar level. The in-code comment says
"Currently expects a zero-based level," so row index 49 corresponds to character
level 50. **Every base power magnitude in MidsReborn is computed at level 50.**
A naive port that indexes the table by the character's current level will not
match MidsReborn's numbers. (Exemplaring affects set-bonus/enhancement
availability elsewhere; it does NOT re-index these base tables. There is no
"exemplar -3" adjustment inside this lookup — that concept belongs to
enhancement-set-bonus persistence, a different subsystem.)

Two OTHER, secondary lookups exist inside the expression evaluator and index
differently — do not confuse them with the base path:

- `Expressions.ModifierCaster` (Expressions.cs:540-568): row `maxPlayerLevel-1 = 49`;
  column = position within the **Playable-filtered** class list matched by
  `DisplayName` (not `.Column`). Used only when an effect's magnitude is an
  expression referencing `modifier>current`.
- `Expressions.GetModifier` (Expressions.cs:627-634): row = `Character.Level`
  (the live level, can be < 49); column = `Archetype.Column`. Used by expression
  functions that explicitly want the current-level value.

For the core port (resolving effect base magnitudes), implement only §2 with
level 49.

#### 5. Table data structure (Modifiers / ModifierTable)

`Modifiers.Modifier` is `List<ModifierTable>` (Modifiers.cs:12). Each
`ModifierTable` (Modifiers.cs:124-153) has:

- `ID` (string): table name, e.g. `"Melee_Damage"`. Lookup key via
  `NidFromUidAttribMod` (case-insensitive string -> list index, cached).
- `BaseIndex` (int): a legacy/decorative field; the source comment
  (frmEditAttribMod.cs:83) states "this field is not used anywhere." Do NOT use
  it for indexing.
- `Table` (`List<List<float>>`): outer index = level (0-based), inner index =
  class column. `Table[level][column]`.

Homecoming `AttribMod.json` shape (verified by parsing the file):

- 102 tables (indices 0-101).
- Most tables have 55 rows (level indices 0-54); a few (indices 89-93:
  Melee_PVPResist, Melee_WalkSpeed, Melee_Uniqueness, Melee_SSDamage,
  Ranged_SSDamage) have 50 rows (0-49) — row 49 is still valid, which is why 50
  rows suffice.
- Every table is 60 columns wide.
- Table `ID`s follow a `Melee_*` / `Ranged_*` naming with `BaseIndex` values
  0,100,200,... in steps of 100 (0->Melee_Damage, 100->Melee_TempDamage, ...,
  10100->Ranged_Debuff_Res_Dmg). Full ordered ID list is in the JSON; see
  open_items for the full enumeration.

##### Concrete anchor values (Homecoming AttribMod.json, for port verification)

- `Melee_Damage`, row 0, columns 0-9:
  `[-10.0, -9.1, -9.1, -10.25, -9.9, -9.7, -9.7, -10.2, -9.5, -10.0]`
- `Melee_Damage`, row 49 (the level used), columns 0-9:
  `[-55.6102, -30.5856, -30.5856, -62.5615, -52.8297, -47.2687, -47.2687, -61.1712, -41.7077, -55.6102]`
- `Melee_Ones`, row 49, columns 0-4: `[1.0, 1.0, 1.0, 1.0, 1.0]`
  (the all-ones table used as a neutral modifier; the default Effect
  `ModifierTable` is `"Melee_Ones"`, Effect.cs:43).

Damage cells are negative; `Effect.Mag` flips sign for `EffectType==Damage`.

#### 6. How a value becomes a number (caller)

- `Effect.Mag` (Effect.cs:390-403), for `AttribType == Magnitude`:
  `Mag = signFlag * Scale * nMagnitude * GetModifier(this)`.
  `Scale` and `nMagnitude` are per-effect floats; `nMagnitude` default is 1.
- `Effect.Duration` (Effect.cs:409-422), for `AttribType == Duration`:
  if `|Math_Duration| <= 0.01` then `Duration = Scale * GetModifier(this)`,
  else the precomputed `Math_Duration`.
- For `AttribType == Expression`, magnitude bypasses table lookup and is parsed
  (`Scale * nMagnitude`, or an expression string) — outside this section.

#### 7. TypeGrades.json — NOT part of this lookup

`TypeGrades.json` defines enhancement **SetTypes** (Melee Damage, Ranged Damage,
Defense Sets, ...) and enhancement **grades** (TO/DO/SO tiers). It is loaded by
`DatabaseAPI.LoadTypeGrades` into `Database.SetTypes` / `Database.EnhancementGrades`.
It does NOT participate in AT base-modifier resolution and can be ignored for
this section (listed here because it was named in the hints).

#### 8. Base caps per AT (HP / damage / recharge / resist / recovery / regen)

These are NOT in the modifier tables. They are per-class scalar fields stored on
the `Archetype` record (read order, Archetype.cs:89-115):
`Hitpoints (int base HP), HPCap, ResCap, RechargeCap, DamageCap, RecoveryCap,
RegenCap, BaseRecovery, BaseRegen, BaseThreat, PerceptionCap`. Default-constructor
fallbacks (used only for a brand-new/blank AT, Archetype.cs:30-45): `HPCap=5000`,
`Hitpoints=5000`, `ResCap=90`, `DamageCap=4`, `RechargeCap=5`, `RecoveryCap=5`,
`RegenCap=20`, `PerceptionCap=1153`. Real per-AT values come from the class
records in `I12.mhd` and must be sourced from the DB export (see open_items).
Caps are stored as multipliers/percent as noted (e.g. ResCap 90 = 90%,
RechargeCap 5 = +500%, DamageCap 4 = +400%).

#### 9. ServerData constants (ServerData.cs:26-110)

Global (non-AT) constants applied on top of the tables:

- `BaseToHit = 0.75f` — base to-hit; e.g. accuracy display multiplies by this
  (Effect.cs:1535). `ConfigData.ScalingToHit` defaults to this.
- `MaxSlots = 67`; `EnableInherentSlotting = false`; `HealthSlots = 2`,
  `StaminaSlots = 2` (with slot levels 8/16 and 12/22). Because
  `EnableInherentSlotting == false`, `Build.TotalSlotsAvailable` (Build.cs:85-95)
  resolves to exactly `67` — the Health/Stamina bonus slots are NOT added.
- Speed caps/bases: `BaseRunSpeed 21`, `BaseJumpSpeed 21`, `BaseJumpHeight 4`,
  `BaseFlySpeed 31.5`, `BasePerception 500`, `MaxRunSpeed 135.67`,
  `MaxFlySpeed 86`, `MaxJumpSpeed 114.40`, `MaxJumpHeight 200 (=50*4)`,
  `MaxMaxRunSpeed 8.398*21`, `MaxMaxFlySpeed 8.19*31.5`, `MaxMaxJumpSpeed 7.917*21`.

These are defaults baked into the ServerData singleton; they may also be loaded
from SData.mhd/JSON (`DatabaseAPI.cs:1213-1227` reads MaxSlots, BaseToHit,
EnableInherentSlotting, HealthSlots, StaminaSlots from the binary), so a port
should treat the code defaults as the source of truth unless a DB file overrides.

#### 10. Reimplementation pseudocode (core path)

```python
MATH_LEVEL_BASE = 49  # fixed; = character level 50

def get_modifier(effect, classes, attrib_mods, selected_at_idx):
    i_level = MATH_LEVEL_BASE
    power = effect.get_power()
    if power is None:
        return 1.0 if effect.enhancement is None \
                else _lookup(0, effect.n_modifier_table, i_level, classes, attrib_mods)
    if power.forced_class:
        i_class = nid_from_uid_class(power.forced_class, classes)   # may be -1
    elif effect.absorbed_class_nid > -1:
        i_class = effect.absorbed_class_nid
    else:
        i_class = selected_at_idx
    return _lookup(i_class, effect.n_modifier_table, i_level, classes, attrib_mods)

def _lookup(i_class, i_table, i_level, classes, attrib_mods):
    if i_class < 0 or i_table < 0 or i_level < 0:      return 0.0
    if i_class > len(classes) - 1:                     return 0.0
    col = classes[i_class].column
    if col < 0:                                        return 0.0
    if i_table > len(attrib_mods) - 1:                 return 0.0
    tbl = attrib_mods[i_table].table
    if i_level > len(tbl) - 1:                          return 0.0
    if col > len(tbl[i_level]) - 1:                     return 0.0
    return tbl[i_level][col]

# magnitude:
def effect_mag(effect):
    sign = -1.0 if effect.effect_type == 'Damage' else 1.0
    return sign * effect.scale * effect.n_magnitude * get_modifier(effect, ...)
```

#### 11. Edge cases a naive port gets wrong

1. Level is fixed at 49, not the character level. (§4)
2. Column is an indirection through `Archetype.Column`, not the raw class index. (§3)
3. Damage table cells are negative; sign is flipped only for `EffectType==Damage`. (§1,§5)
4. Unresolved table name (`nModifierTable == -1`) returns 0.0, NOT table index 0. (§2b)
5. `BaseIndex` is decorative — never index by it. (§5)
6. Default effect table is `"Melee_Ones"` (all 1.0), so an effect with no explicit
    table still returns 1.0 \* Scale \* nMagnitude. (§5)
7. `MaxSlots` is a flat 67 because `EnableInherentSlotting==false`; do not add
    Health/Stamina slots. (§9)

**Open items / gaps:**

- Full ordered enumeration of all 102 table IDs and their BaseIndex was captured in analysis (0=Melee_Damage ... 101=Ranged_Debuff_Res_Dmg, BaseIndex in steps of 100) but not transcribed cell-by-cell
  here; the port must consume Databases/Homecoming/AttribMod.json verbatim (346,889 lines). Only sample anchor rows are given.
- Per-AT Column values and per-AT cap values (HP/HPCap/ResCap/RechargeCap/DamageCap/RecoveryCap/RegenCap/BaseRecovery/BaseRegen) live in the binary I12.mhd class records and were NOT decoded in this
  pass. A port needs a class-list export (ClassName, Column, Playable, all caps). The companion project's data/canonical/archetypes/*.json is a different (City-of-Data) export and does not carry
  MidsReborn's Column field.
- Confirmed row index is fixed at 49 for the base path; did not exhaustively audit every alternate caller, but the two expression-evaluator variants (ModifierCaster row 49 by playable-list position;
  GetModifier(expr) row=Character.Level by Archetype.Column) are documented as the only divergent indexers found via grep.
- Did not read the SData.mhd binary loader field-by-field beyond confirming DatabaseAPI.cs:1213-1227 reads MaxSlots/BaseToHit/EnableInherentSlotting/Health/Stamina slots; assumed ServerData.cs code
  defaults are authoritative for Homecoming.
- Exemplar '-3' question: RESOLVED as out-of-scope for this subsystem. The base-modifier lookup uses a hardcoded level index of 49 and has no exemplar adjustment; exemplar/-3 logic belongs to
  enhancement set-bonus persistence, not AT base tables.
- nMagnitude default assumed 1 (dummy=1 at Effect.cs:189); exact per-effect values come from power data, out of this section's scope.

---

## enhancement-value-pipeline

**Port risk:** low — All tables, breakpoints, slopes, enum values, and pass ordering are transcribed from source with exact locations, and both worked assertions (99.08%, 83.32%) reproduce exactly.
Residual risk is the 0-based IOLevel convention and ForceLevel exemplar gate, which are inferred from surrounding code rather than every construction callsite.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/Core/I9Slot.cs` (38-153) — Per-slot value: GetScheduleMult (schedule tables + relative-level + Superior), GetRelativeLevelMultiplier, GetEnhancementEffect (per-effect
  aspect sum).
- `<repos>/MidsReborn/MidsReborn/Core/Enhancement.cs` (424-476) — ApplyED (breakpoints + slopes 1.0/0.9/0.7/0.15) and GetSchedule (aspect->schedule mapping).
- `<repos>/MidsReborn/MidsReborn/clsToonX.cs` (1732-1919) — Pipeline orchestration: Pass1 aggregate-per-aspect-across-slots (ForceLevel exemplar gate), Pass2 ApplyED-once, pass ordering in
  GenerateBuffedPowerArray.
- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (2356-2549) — InitializeMaths defaults + LoadMaths parser confirming MultED[sched]\[bp], MultTO/DO/SO[0]\[sched], MultIO[level-1]\[sched] and
  53-row / 4-column layout.
- `<repos>/MidsReborn/MidsReborn/Databases/Homecoming/Maths.mhd` (9-76) — Actual numeric tables: ED thresholds (11-13), grade effectiveness (17-20), 53-row level IO effectiveness (24-76).
- `<repos>/MidsReborn/MidsReborn/Core/Enums.cs` (645-677) — Enum integer values: eEnhGrade, eEnhRelative, eSchedule (None=-1,A=0..D=3,Multiple=4), eType.

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| MultED A (ED thresholds) | Maths.mhd:11-13 col A; MultED[0]\[0..2] | [0.700, 0.900, 1.000] |
| MultED B | Maths.mhd:11-13 col B; MultED[1] | [0.400, 0.500, 0.600] |
| MultED C | Maths.mhd:11-13 col C; MultED[2] | [0.800, 1.000, 1.200] |
| MultED D | Maths.mhd:11-13 col D; MultED[3] | [1.200, 1.500, 1.800] |
| ED slopes | Core/Enhancement.cs:467-473 | 1.0(<=thr1), 0.9(thr1->thr2), 0.7(thr2->thr3), 0.15(>thr3) |
| MultTO[0] (A,B,C,D) | Maths.mhd:17 | [0.083, 0.050, 0.100, 0.150] |
| MultDO[0] | Maths.mhd:18 | [0.167, 0.100, 0.200, 0.300] |
| MultSO[0] (also SpecialO) | Maths.mhd:19 | [0.333, 0.200, 0.400, 0.600] |
| MultHO[0] (loaded, unused in value path) | Maths.mhd:20 | [0.333, 0.200, 0.400, 0.600] |
| MultIO (idx0=lvl1..idx52=lvl53; A,B,C,D) | Maths.mhd:24-76 | lvl1-9=0.000; lvl10=[.117,.070,.140,.210]; lvl50(idx49)=[0.424,0.255,0.509,0.764]; lvl53=[.435,.261,.523,.784] |
| MultIO length / IOLevel clamp | DatabaseAPI.cs:2401; I9Slot.cs:94-97 | 53 rows (idx 0..52); IOLevel clamped to <=52 |
| Relative-level multiplier | I9Slot.cs:142-152 | offset=(int)RelativeLevel-4; >=0->1+offset*0.05; <0->1+offset*0.10; None->0.0 |
| Superior multiplier | I9Slot.cs:132 | 1.25 |
| Per-effect Multiplier gate | I9Slot.cs:55-58 | apply sEffect.Multiplier only if abs(Multiplier) > 0.01 |
| GetSchedule aspect->schedule | Enhancement.cs:424-448 | Defense=B, Interrupt=C, Range=B, Resistance=B, ToHit=B, Mez=D if sub in{4,5} else A, default=A |
| eSchedule values | Enums.cs:942-950 | None=-1, A=0, B=1, C=2, D=3, Multiple=4 |
| eEnhGrade values | Enums.cs:645-651 | None=0, TrainingO=1, DualO=2, SingleO=3 |
| eEnhRelative values | Enums.cs:665-677 | None=0, MinusThree=1..Even=4..PlusFive=9 |
| eType values | Enums.cs:1159-1166 | None=0, Normal=1, InventO=2, SpecialO=3, SetO=4 |
| Slot exemplar gate | clsToonX.cs:1751 | include slot only if Slots[i].Level < MidsContext.Config.ForceLevel |

### Enhancement-value pipeline (slotted enhancement to post-ED contribution)

This subsystem converts each slotted enhancement into a per-aspect numeric magnitude, aggregates those magnitudes across all slots of a power, then applies Enhancement Diversification (ED) **once** on
the aggregate per aspect. Match these numbers exactly.

#### Data model per slot (`I9Slot`)

A slotted enhancement carries (`Core/I9Slot.cs:11-15`):

- `Enh` — index into `Database.Enhancements`; `-1` means empty (contributes 0).
- `Grade` — `eEnhGrade` {None=0, TrainingO=1, DualO=2, SingleO=3}.
- `IOLevel` — **0-based** IO level (character level − 1). A level-50 IO stores `IOLevel = 49`.
- `RelativeLevel` — `eEnhRelative` {None=0, MinusThree=1, MinusTwo=2, MinusOne=3, Even=4, PlusOne=5, PlusTwo=6, PlusThree=7, PlusFour=8, PlusFive=9}.

The enhancement (`Database.Enhancements[Enh]`) carries `TypeID` (`eType` {None=0, Normal=1, InventO=2, SpecialO=3, SetO=4}), a bool `Superior`, and an array of `Effect` entries. Each effect has:
`Mode` (only `Enhancement` mode contributes to numeric aspects), `BuffMode` (Any/BuffOnly/DeBuffOnly), `Enhance.ID` (the aspect, an `eEnhance` value), `Enhance.SubID` (Mez sub-type), `Schedule`
(`eSchedule` {None=-1, A=0, B=1, C=2, D=3, Multiple=4}), and `Multiplier` (a per-effect scalar, often 1.0).

#### Step 1 — per-effect base schedule multiplier (`GetScheduleMult`, `I9Slot.cs:66-136`)

Clamp inputs first: `Grade`→[None,SingleO], `RelativeLevel`→[None,PlusFive], `IOLevel`→[0, len(MultIO)-1 = 52] (`I9Slot.cs:68-97`).

If `Schedule == None(-1)` or `Multiple(4)` → base `num1 = 0`. Otherwise select by `TypeID`, indexing the table column by `(int)Schedule` (A=0..D=3):

```text
Normal + Grade None      -> 0
Normal + Grade TrainingO -> MultTO[0][sched]
Normal + Grade DualO     -> MultDO[0][sched]
Normal + Grade SingleO   -> MultSO[0][sched]
InventO                  -> MultIO[IOLevel][sched]
SpecialO                 -> MultSO[0][sched]
SetO                     -> MultIO[IOLevel][sched]
(TypeID None)            -> 0 (no switch case, stays 0)
```

Then apply relative-level and Superior (`I9Slot.cs:129-133`):

```text
num2 = num1 * GetRelativeLevelMultiplier()
if enhancement.Superior: num2 *= 1.25
return num2
```

`GetRelativeLevelMultiplier()` (`I9Slot.cs:138-153`):

```text
if RelativeLevel == None: return 0.0
offset = (int)RelativeLevel - 4        # Even(4) -> 0
if offset >= 0: return 1.0 + offset*0.05    # +5% per level above even
else:           return 1.0 + offset*0.10    # -10% per level below even
```

Even→1.0, +1→1.05, +5→1.25, −1→0.90, −3→0.70, None→0.0 (kills the whole contribution). Source uses double literals `0.0500000007450581` and `0.100000001490116` for bit-exact parity.

#### Step 2 — per-effect aspect value (`GetEnhancementEffect`, `I9Slot.cs:38-64`)

```text
total = 0
for each sEffect in enhancement.Effect:
    skip unless sEffect.Mode == Enhancement
    skip if BuffMode==DeBuffOnly and not(mag<=0)
    skip if BuffMode==BuffOnly  and not(mag>=0)
    skip if sEffect.Schedule == None
    skip if (eEnhance)sEffect.Enhance.ID != iEffect
    skip if subEnh>=0 and subEnh != sEffect.Enhance.SubID
    m = GetScheduleMult(enhancement.TypeID, sEffect.Schedule)
    if abs(sEffect.Multiplier) > 0.01: m *= sEffect.Multiplier
    total += m
return total     # 0 if Enh < 0
```

`mag` is only a buff/debuff sign gate; it does not scale the return.

#### Step 3 — aggregate across all slots of the power (Pass 1, `clsToonX.cs:GBPA_Pass1_EnhancePreED`, ~1732-1849)

Per power, per aspect, sum Step-2 results over every slot. A slot is included only if `Slots[i].Enhancement.Enh > -1` **and** `Slots[i].Level < MidsContext.Config.ForceLevel` (`clsToonX.cs:1751`) —
the exemplar/level gate: enhancements placed at/above the current effective level do not contribute.

Scalar aspects summed directly: `Accuracy`, `EndCost`, `InterruptTime`, `Range`, `RechargeTime` (each gated by the power's `IgnoreEnhancement` flag). Effect-borne aspects (Damage, Defense, Resistance,
Heal, Mez…) accumulate into `effect.Math_Mag`/`Math_Duration`. Plain additive sum of raw pre-ED multipliers — **no ED yet**.

#### Step 4 — apply ED once on the aggregate (Pass 2, `clsToonX.cs:GBPA_Pass2_ApplyED`, 1870-1919)

Call `Enhancement.ApplyED(schedule, aggregate)` exactly once per aspect. Schedule from `Enhancement.GetSchedule(aspect[, mezSub])` (`Enhancement.cs:424-448`):

```text
Defense->B ; Interrupt->C ; Range->B ; Resistance->B ; ToHit->B
Mez->D if sub in {4,5} else A
everything else (Accuracy, RechargeTime, EnduranceDiscount, Damage, Heal, ...) -> A
```

`ApplyED(iSched, iVal)` (`Enhancement.cs:450-476`):

```text
if iSched in {None, Multiple}: return 0
ed[0..2] = MultED[iSched][0..2]
if iVal <= ed[0]: return iVal              # slope 1.0, no reduction
edm[0] = ed[0]
edm[1] = ed[0] + (ed[1]-ed[0])*0.9
edm[2] = ed[0] + (ed[1]-ed[0])*0.9 + (ed[2]-ed[1])*0.7
if iVal > ed[2]: return edm[2] + (iVal-ed[2])*0.15
if iVal > ed[1]: return edm[1] + (iVal-ed[1])*0.7
                  return edm[0] + (iVal-ed[0])*0.9
```

Piecewise-linear, slopes **1.0 / 0.9 / 0.7 / 0.15**. Passes 3–5 (`clsToonX.cs:2218-2221`) add unenhanceable base, apply caps, multiply — outside this section.

#### Pipeline order (do not reorder)

`schedule table lookup → × relative-level mult → × 1.25 if Superior → × per-effect Multiplier → sum across a slot's effects → sum across all slots per aspect → ApplyED once`. ED is never applied
per-slot.

#### Tables (from `Databases/Homecoming/Maths.mhd`; columns A,B,C,D = 0,1,2,3)

ED thresholds `MultED[sched][0..2]` (lines 11-13):

```text
      A      B      C      D
thr1  0.700  0.400  0.800  1.200
thr2  0.900  0.500  1.000  1.500
thr3  1.000  0.600  1.200  1.800
```

Grade effectiveness `MultXX[0][A..D]` (lines 17-20):

```text
TO: 0.083 0.050 0.100 0.150
DO: 0.167 0.100 0.200 0.300
SO: 0.333 0.200 0.400 0.600
HO: 0.333 0.200 0.400 0.600   (MultHO, unused by GetScheduleMult)
```

Level IO effectiveness `MultIO[level-1][A..D]` (lines 24-76, labels 1..53 → indices 0..52). Levels 1–9 all 0.000. Selected (label → A,B,C,D):

```text
10 -> 0.117 0.070 0.140 0.210
13 -> 0.167 0.100 0.200 0.300
20 -> 0.256 0.154 0.308 0.462
26 -> 0.333 0.200 0.400 0.600
30 -> 0.348 0.209 0.418 0.627
40 -> 0.386 0.232 0.464 0.695
48 -> 0.416 0.250 0.500 0.750
50 -> 0.424 0.255 0.509 0.764   (array index 49)
52 -> 0.431 0.259 0.518 0.777
53 -> 0.435 0.261 0.523 0.784
```

Transcribe all 53 rows verbatim for a complete port.

#### Worked assertions

**Hasten, 3× level-50 recharge IOs (99.08%).** RechargeTime→A. Level-50 IO → `IOLevel=49`, `MultIO[49][A]=0.424`, Even ×1.0, not Superior → 0.424 each. Sum = 3×0.424 = **1.272** pre-ED.

```text
ED A ed=[0.70,0.90,1.00]; 1.272 > ed[2]:
edm[2] = 0.70 + 0.20*0.9 + 0.10*0.7 = 0.95
result = 0.95 + (1.272-1.00)*0.15 = 0.9908  -> 99.08%
```

**Hasten, 2× level-50 recharge IOs (83.32%).** Sum = 0.848.

```text
0.70 < 0.848 <= 0.90:
result = 0.70 + (0.848-0.70)*0.9 = 0.8332  -> 83.32%
```

#### Edge cases a naive port gets wrong

- **ED applied once on the total**, not per slot (per-slot ED then sum over-values).
- **IOLevel is 0-based** (level−1); `MultIO[49]` for a level-50 IO.
- **RelativeLevel None → 0.0 multiplier** (zeros the enhancement; distinct from Even=1.0).
- **Superior ×1.25 is inside the per-effect multiplier**, before summation and before ED.
- **Schedule None or Multiple → contribution 0**; ED returns 0.
- **`Multiplier` applies only when `abs(Multiplier) > 0.01`**; a stored 0 means "no multiplier", not "zero out".
- **Slot exemplar gate:** slots with `Level >= ForceLevel` are excluded entirely.
- **SpecialO uses the SO table; SetO uses the IO table** — not their own.
- ED slope literals in source: `0.899999976158142`, `0.699999988079071`, `0.150000005960464`.

#### RESOLVED

- **Exemplar in this pipeline** is a hard slot include/exclude by `Slots[i].Level < ForceLevel` (`clsToonX.cs:1751`), not a value taper. The IO set-bonus "effective level − 3" persistence rule is a
  separate subsystem, not part of this ED/value math.
- **Grade/RelativeLevel/IOLevel legality:** the pipeline does not accept/reject; it silently **clamps** (`I9Slot.cs:68-97`) before lookup.

**Open items / gaps:**

- ForceLevel value assignment (exemplar vs build level) is set in MidsContext.Config elsewhere; the gate Slots[i].Level < ForceLevel is confirmed but the assigning source was not read.
- IgnoreEnhancement(aspect) per-power flags (gate Acc/Rech/End summation) referenced at clsToonX.cs:1743-1745 but flag-resolution logic not read.
- Passes 3-5 (post-ED base add, archetype caps, multiply) and GBPA_ApplyIncarnateEnhancements are out of scope and not transcribed.
- IO set-bonus 'effective level - 3' persistence is a separate subsystem, not this ED/value math; its code path was not located here.
- 0-based IOLevel is inferred from CheckAndFixIOLevel/GranularLevelZb returning level-1 and from the 99.08% worked math; not every I9Slot construction callsite was audited to confirm it is always
  stored level-1.

---

## effect-model

**Port risk:** medium — Core magnitude formula (Scale*nMagnitude*GetModifier), the summation filtering, ShortFX rollup, and PvP DR are fully pinned with exact constants and ordering. Residual risk is
in data-side pieces (AttribMod tables + class-column mapping must be extracted exactly from the .mhd files) and two leaf predicates (ValidateConditional, full CanInclude SpecialCase tail) plus the
Expressions engine, all of which affect which effects are included/excluded for conditional and expression-driven effects. The fixed level-49 lookup and the negative-damage sign are easy to get wrong
but are now documented.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Effect.cs` (87-172, 390-407, 1856-2000, 2610-2616) — Effect record: field set, BinaryReader field order (87-172), Mag getter (390-403),
  BuffedMag/MagPercent (405-407), CanInclude/SpecialCase (1856+), PvXInclude (2610-2616)
- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (2609-2647) — GetModifier public+private: AttribMod table lookup, MathLevelBase level, class-column indirection, bounds->0
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Power.cs` (1433-1626) — GetEnhancementMagSum (1433-1462), GetEffectMagSum x2 (1464-1596), GetDamageMagSum toggle /10 rule (1598-1626)
- `<repos>/MidsReborn/MidsReborn/Core/Enums.cs` (36-50, 935-940, 1081-1085, 1151-1157, 1760-1815) — ShortFX struct Add/Sum/Max/Present (1760-1815); enum orderings eAspect(36) eAttribType(45) ePvX(935)
  eStacking(1081) eToWho(1151)
- `<repos>/MidsReborn/MidsReborn/clsToonX.cs` (48-59, 335-338, 980) — ApplyPvpDr guard+Def loop (48-59), CalculatePvpDr atan formula (335-338), call site (980)
- `<repos>/MidsReborn/MidsReborn/Core/Base/Master_Classes/MidsContext.cs` (21) — MathLevelBase constant = 49

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| MathLevelBase | MidsReborn/Core/Base/Master_Classes/MidsContext.cs:21 | 49 (const int; zero-based level index = character level 50; the fixed level row used by GetModifier) |
| Effect.Mag damage sign | Effect.cs:394 | -1 when EffectType==Damage, else +1 |
| BuffedMag epsilon fallback | Effect.cs:405 | abs(Math_Mag) > float.Epsilon ? Math_Mag : Mag |
| Default ModifierTable | Effect.cs:43-44 | "Melee_Ones" (nModifierTable via NidFromUidAttribMod) |
| GetModifier out-of-bounds return | DatabaseAPI.cs:2619-2646 | 0.0 for any failed bound; 1.0 only when power==null && enhancement==null |
| AttribMod table (data) | DatabaseAPI.cs:2640-2646 | Database.AttribMods.Modifier[table].Table[level]\[classColumn]; large table loaded from AttribMod.mhd — transcribe from data file, not source |
| class column indirection | DatabaseAPI.cs:2640 | Database.Classes[iClass].Column |
| DelayedTime exclusion threshold | Power.cs:1498, 1560, 1603 | DelayedTime > 5 excluded unless includeDelayed |
| Ticks/Stacking multiplier | Power.cs:1513-1516, 1582-1585 | if Ticks>1 && Stacking==Yes: mag *= Ticks |
| Toggle enhancement damage divisor | Power.cs:1617-1620 | mag /= 10 when PowerType==Toggle && isEnhancementEffect (GetDamageMagSum only) |
| PvP DR params | clsToonX.cs:57, 335-337 | a=1.2, b=1.0; formula val*(1 - \|atan(a*val)\|*(2/pi)*b); applied to Totals.Def only, only when Config.Inc.DisablePvE |
| MinProcChance / MaxProcChance | Effect.cs:369-370 | Min = PPM>0 ? PPM*0.015+0.05 : 0.05 ; Max = 0.90 |
| PPM area factor | Effect.cs:330-340 | areaFactor = AoEModifier*0.75 + 0.25; Click prob = PPM*(recharge+castTimeReal)/(60*areaFactor); non-Click = PPM*10/(60*areaFactor) |
| eAttribType | Enums.cs:45-50 | Magnitude=0, Duration=1, Expression=2 |
| eToWho | Enums.cs:1151-1157 | Unspecified=0, Target=1, Self=2, All=3 |
| ePvX | Enums.cs:935-940 | Any=0, PvE=1, PvP=2 |
| eAspect | Enums.cs:36-43 | Res=0, Max=1, Abs=2, Str=3, Cur=4 |
| eStacking | Enums.cs:1081-1085 | No=0, Yes=1 |

### Effect model & per-effect magnitude summation

This section specifies (1) the `Effect` record and its computed magnitude, (2) the attribute-modifier table lookup `GetModifier`, (3) the per-attribute summation used by the engine (`GetEffectMagSum`
/ `GetEnhancementMagSum` / `GetDamageMagSum`) including the `ToWho` / probability / suppression / conditional filtering, and (4) PvP diminishing returns (`ApplyPvpDr`). All line refs are
`MidsReborn/...`.

#### 1. The Effect record

An `Effect` is one line-item of a power's behavior. Fields that drive magnitude (declared `Core/Base/Data_Classes/Effect.cs`, serialized in ctor `Effect(BinaryReader)` lines 87-172, so this is the
authoritative on-disk field order):

- `Scale` (float) — base scale coefficient.
- `nMagnitude` (float) — secondary magnitude multiplier.
- `nDuration` (float) — base duration.
- `AttribType` (enum `eAttribType`: `Magnitude=0, Duration=1, Expression=2`, Enums.cs:45).
- `Aspect` (enum `eAspect`: `Res=0, Max=1, Abs=2, Str=3, Cur=4`, Enums.cs:36).
- `ModifierTable` (string) → `nModifierTable` (int) resolved via `DatabaseAPI.NidFromUidAttribMod(ModifierTable)` (default table `"Melee_Ones"`).
- `EffectType` (enum `eEffectType`), `ETModifies` (for `Enhancement` effects, the buffed attribute), `DamageType` (`eDamage`), `MezType` (`eMez`).
- `EffectClass` (`eEffectClass`; values used as filters: `Ignored`, `Special`, `Primary`).
- `ToWho` (`eToWho`: `Unspecified=0, Target=1, Self=2, All=3`, Enums.cs:1151).
- `PvMode` (`ePvX`: `Any=0, PvE=1, PvP=2`, Enums.cs:935).
- `Stacking` (`eStacking`: `No=0, Yes=1`, Enums.cs:1081).
- `Suppression` (`eSuppress` bit-flags), `Buffable`, `Resistible`, `IgnoreED`, `IgnoreScaling`.
- `BaseProbability` (default 1.0), `ProcsPerMinute`, `Ticks`, `DelayedTime`.
- `SpecialCase` (`eSpecialCase`), `ActiveConditionals` (list of key/value).
- `nIDClassName` (int, -1 = any class; else restricts effect to that archetype index).
- `Absorbed_Effect` (bool), `Absorbed_PowerType` (`ePowerType`; `GlobalBoost` filtered out of enhancement sums), `Absorbed_Class_nID`.
- `Math_Mag`, `Math_Duration` (floats) — the **post-buff** magnitude/duration written by the enhancement/buff pass. Zero until buffing runs.
- `Expressions.Magnitude / .Duration / .Probability` (strings) — used only when `AttribType == Expression`.

#### 2. Raw per-effect magnitude — `Effect.Mag` (Effect.cs:390-403)

```text
sign = (EffectType == eEffectType.Damage) ? -1 : 1
Mag  = sign * switch (AttribType):
    Magnitude   -> Scale * nMagnitude * GetModifier(this)
    Duration    -> nMagnitude
    Expression  (Expressions.Magnitude non-blank) -> Parse(this, Magnitude)   // expression engine, out of scope
    Expression  (blank) -> Scale * nMagnitude
    default     -> 0
```

Note the **Damage sign flip**: damage effects report a *negative* raw Mag. This is intentional (damage is stored as a negative buff). Naive reimplementations that forget the sign will get
damage/summation signs wrong.

#### 3. Buffed magnitude used by summation

- `BuffedMag` (Effect.cs:405) = `abs(Math_Mag) > epsilon ? Math_Mag : Mag`. i.e. if the buff pass has written an enhanced value into `Math_Mag`, use it; otherwise fall back to raw `Mag`. **All
  summation functions below sum `BuffedMag`, not `Mag`.**
- `MagPercent` (Effect.cs:407) = `DisplayPercentage ? BuffedMag*100 : BuffedMag`. `DisplayPercentage` is a display-only concern (Effect.cs:424-465); it does NOT change the summed numeric value.

#### 4. `GetModifier` — attribute-modifier table lookup (DatabaseAPI.cs:2609-2647)

Public entry (2609):

```text
iClass = 0
iLevel = MidsContext.MathLevelBase          // == 49  (constant, MidsContext.cs:21)
effPower = effect.GetPower()
if effPower == null:
    return effect.Enhancement == null ? 1.0
                                      : GetModifier(0, effect.nModifierTable, 49)
iClass = ForcedClass set?  NidFromUidClass(ForcedClass)
        : effect.Absorbed_Class_nID > -1 ? Absorbed_Class_nID
        : MidsContext.Archetype.Idx
return GetModifier(iClass, effect.nModifierTable, 49)
```

Private core (2632-2647) — a 3-D table `Database.AttribMods.Modifier[table].Table[level][classColumn]`:

```text
if iClass  < 0: return 0
if iTable  < 0: return 0
if iLevel  < 0: return 0
if iClass  > Classes.Length-1: return 0
classColumn = Classes[iClass].Column
if classColumn < 0: return 0
if iTable  > Modifier.Count-1: return 0
if iLevel  > Modifier[iTable].Table.Count-1: return 0
if classColumn > Modifier[iTable].Table[iLevel].Count-1: return 0
return Modifier[iTable].Table[iLevel][classColumn]
```

Key facts a porter must replicate exactly:

- **`iLevel` is hard-fixed to 49** (`MathLevelBase`), a *zero-based* index = character level 50. It is NOT the character's current level; all displayed magnitudes are computed at the level-50 row of
  the AttribMod table.
- The **class column** is `Classes[iClass].Column` (an indirection — each archetype maps to a column index into every AttribMod table), not the class index itself.
- Any out-of-bounds condition returns **0** (not 1, not an error). Only the "no power AND no enhancement" case returns 1.
- The AttribMod tables themselves (`Database.AttribMods.Modifier[*]`) are large data loaded from `AttribMod.mhd`; transcribe from that file, not by hand.

#### 5. Per-attribute summation — `Power.GetEffectMagSum` (Power.cs:1464-1522)

Rolls all of a power's effects that match one `eEffectType` into a `ShortFX` accumulator. Signature: `GetEffectMagSum(iEffect, includeDelayed=false, onlySelf=false, onlyTarget=false, maxMode=false)`.

Per effect `fx` in `Effects[]`:

1. **ToWho gate** → `flag`:
    - `Target` and NOT `onlySelf` → include
    - `Self` and NOT `onlyTarget` → include
    - `All` → include
    - else (`Unspecified`) → exclude
2. **Movement-Max special-case**: if `iEffect` is `SpeedFlying`/`SpeedRunning`/`SpeedJumping`, NOT `maxMode`, and `fx.Aspect == Max` → `flag=false` (max-speed effects excluded from the non-max speed
    sum).
3. **Suppression**: if `(MidsContext.Config.Suppression & fx.Suppression) != None` → `flag=false`.
4. **Skip (continue) if any**: `!flag`, `fx.Probability <= 0`, (`maxMode && fx.Aspect != Max`), `fx.EffectType != iEffect`, `fx.EffectClass ∈ {Ignored, Special}`,
    (`fx.DelayedTime > 5 && !includeDelayed`), `!fx.CanInclude()`, `!fx.PvXInclude()`.
5. **Conditionals**: if `fx.ActiveConditionals.Count > 0` and `!fx.ValidateConditional()` → skip.
6. **Value**: `mag = fx.BuffedMag`; if `fx.Ticks > 1 && fx.Stacking == Yes` → `mag *= fx.Ticks`.
7. `shortFx.Add(index, mag)`.

Overload `GetEffectMagSum(iEffect, etModifies, damageType, mezType, ...)` (Power.cs:1524-1596) is identical except the initial hard match is on all four of `EffectType/ETModifies/DamageType/MezType`,
and it additionally **skips effects whose `|mag| < epsilon`** (line 1587).

`GetDamageMagSum(iEffect, iSub, includeDelayed)` (Power.cs:1598-1626): matches `EffectType==iEffect && EffectClass != Ignored && DamageType==iSub`, filtered by
`CanInclude/PvXInclude/DelayedTime<=5|includeDelayed` and conditionals. **Special rule**: if the power is a `Toggle` and `fx.isEnhancementEffect`, `mag /= 10` (line 1617-1620).

#### 6. Enhancement summation — `Power.GetEnhancementMagSum` (Power.cs:1433-1462)

Sums the "+X% to attribute" enhancement effects a power grants. For each `fx`, **skip unless all hold**: `fx.PvXInclude()`, `fx.Probability > 0`, `fx.ETModifies == iEffect`, `fx.CanInclude()`,
`fx.EffectType ∈ {Enhancement, DamageBuff}`, and NOT (`fx.Absorbed_Effect && fx.Absorbed_PowerType == GlobalBoost`).

Then:

- If `iEffect == Mez` and `fx.ToWho != Target`: add `fx.BuffedMag` when `(eMez)subType == fx.MezType` OR `subType < 0`.
- Else if `fx.ToWho != Target`: add `fx.BuffedMag`.

(So enhancement sums exclude Target-directed effects — they represent self-enhancement of the power.)

#### 7. `ShortFX` accumulator (Enums.cs:1760-1815)

```text
struct ShortFX { int[] Index; float[] Value; float Sum; }
Add(i, v): append i to Index, v to Value, Sum += v
Present  = Index has >=1 element and Index[0] != -1
Multiple = Index.Length > 1
Max      = index (into Value) of the largest Value; -1 if not Present
            (strict '>' scan from 0; ties keep the first/lowest index)
```

`Sum` is the rolled-up per-attribute total the rest of the engine consumes. `Value[k]` retains each contributing effect's magnitude and `Index[k]` its position in `Power.Effects`.

#### 8. `PvXInclude` (Effect.cs:2610-2616)

```text
return Archetype == null
    || ( (PvMode != PvP && !Config.Inc.DisablePvE)
        || (PvMode != PvE &&  Config.Inc.DisablePvE) )
        && (nIDClassName == -1 || nIDClassName == Archetype.Idx)
```

`Config.Inc.DisablePvE == true` is the flag that means "PvP mode is active" (PvE disabled). So in PvE mode, PvP-only effects are dropped; in PvP mode, PvE-only effects are dropped. Effects tagged to a
specific class (`nIDClassName >= 0`) only apply to that archetype.

#### 9. `CanInclude` (Effect.cs:1856+)

Returns true immediately if `Character == null` OR `ActiveConditionals == null` OR (`ActiveConditionals.Count == 0 && SpecialCase == None`). Otherwise it evaluates `SpecialCase` against character
state — a large switch (Effect.cs:1865-2000+): e.g. `Hidden` true only for Stalker/Arachnos, `Domination` true only when `Character.Domination`, `CriticalHit` true for Stalker or when
`Character.CriticalHits`, combo/perfection levels matched against `Character.ActiveComboLevel` / `PerfectionOfBodyLevel` etc. If the matched case's condition holds it returns true (effect counts);
otherwise the effect is excluded. Conditional (`ActiveConditionals`) evaluation continues past the SpecialCase block via `ValidateConditional()` (leaf, not fully transcribed here — see open_items).

#### 10. PvP diminishing returns — `ApplyPvpDr` (clsToonX.cs:48-59, 335-338)

Applied once after totals are built (called at clsToonX.cs:980), and **only when `Config.Inc.DisablePvE` (PvP mode) is true**; otherwise it returns immediately. It rescales **only the Defense
totals**:

```text
for index in Totals.Def:
    Totals.Def[index] = CalculatePvpDr(Totals.Def[index], a=1.2, b=1.0)

CalculatePvpDr(val, a, b) = val * (1 - |atan(a*val)| * (2/pi) * b)
                          = val * (1 - |atan(1.2*val)| * (2/pi))
```

This is the standard CoH PvP defense DR curve. No other attribute is passed through DR here (resistance/etc. DR, if any, is handled elsewhere and not in this code path).

#### Ordering / edge cases a naive port gets wrong

1. **Level is fixed at 49 (zero-based) for `GetModifier`.** Magnitudes are always the level-50 value regardless of the build's actual level. Do not use current level.
2. **`GetModifier` uses `Classes[iClass].Column`**, an archetype→column indirection, not `iClass` directly.
3. **Damage Mag is negative** (`sign = -1` for `eEffectType.Damage`).
4. **Summation uses `BuffedMag`** (falls back to raw `Mag` only when `Math_Mag ≈ 0`), so buffing/enhancement must run before summing to get final numbers.
5. **`Ticks>1 && Stacking==Yes` multiplies mag by Ticks** in `GetEffectMagSum` (DoT stacking) — but the toggle `/10` rule only exists in `GetDamageMagSum`.
6. **`Probability > 0` is a hard filter** — zero-probability effects never sum. Proc effects with `ProcsPerMinute>0` derive probability dynamically (Effect.cs:320-370) and can be included/excluded on
    that basis.
7. **`EffectClass ∈ {Ignored, Special}` are excluded** from generic mag sums.
8. **DelayedTime > 5s effects are excluded** unless `includeDelayed`.
9. **`Unspecified` ToWho is never summed** — only Target/Self/All.
10. **Enhancement sums drop `ToWho==Target`** effects and `Absorbed GlobalBoost` effects.
11. **PvP DR only touches Defense and only in PvP mode.**

**Open items / gaps:**

- ValidateConditional() (no-arg and its overloads) not fully transcribed — it evaluates ActiveConditionals (Stacks/Team/Config/power-active predicates) against character/config state; needed to
  reproduce conditional effect inclusion exactly. Location: Effect.cs (search ValidateConditional).
- CanInclude SpecialCase switch read only through ~line 1975; remaining SpecialCase branches (PerfectionOfMind2/3, and any after) and the post-SpecialCase conditional-evaluation tail were not fully
  read.
- Expressions engine (Parse / ExpressionType.Magnitude|Duration|Probability, `static Mids_Reborn.Core.Expressions`) is out of scope here but is required for AttribType==Expression effects; not
  transcribed.
- Math_Mag / Math_Duration population: these post-buff values are written by the enhancement/buff pass (GenerateBuffedPowerArray and related in clsToonX.cs) — that is the buffing subsystem, not read
  here. Summation depends on it having run.
- AttribMod table contents (Database.AttribMods.Modifier[*].Table) are data, not code; must be extracted from the AttribMod.mhd database file. Classes[*].Column mapping likewise comes from the class
  DB.
- eEffectClass full enum ordering (only Ignored/Special/Primary referenced) not transcribed.
- eSuppress bit-flag values and how MidsContext.Config.Suppression is populated (which suppression flags are active) not transcribed — needed to reproduce the suppression filter in GetEffectMagSum.
- Whether any attribute other than Defense receives PvP DR elsewhere in the pipeline was not verified (only the Def-only path in ApplyPvpDr was read).

---

## expressions-language

**Port risk:** medium — The expression evaluator itself is small and fully specified: a substitution pass (26 constants + 16 regex functions) followed by an infix math engine with 9 well-defined
custom functions. That is straightforwardly portable. Risk is medium not low because (1) exact numeric match depends on several large external subsystems (attrib-mod tables, BuffToHit, HPMax,
VariableValue, combat-context config) that must be ported first; (2) Jace4fc 1.0.2 precedence/grammar could not be verified against source; and (3) faithful behavior requires replicating C# quirks —
dictionary insertion-order string replacement with substring-collision/residual-token failures that legitimately yield 0, plus the hardcoded RPN Magnitude special case. Missing any of these produces
silently wrong (often 0) magnitudes/durations/probabilities.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/Core/Expressions.cs` (1-843) — PRIMARY. Entire expression subsystem: command/constant table (CommandsList UI metadata + CommandsDict runtime), function table
  (FunctionsDict), custom Jace functions, Parse/InternalParsing evaluator, Validate.
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Effect.cs` (15,100-149,372-422,940-975,1220,1273,1591-1607) — Call sites. Effect.Probability (378-388), Effect.Mag (390-403), Effect.Duration
  (409-422) getters invoke Parse when AttribType==Expression; clamping/sign rules live here. Rand field (15). Expressions deserialized (128-133).
- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (2609-2630) — DatabaseAPI.GetModifier(IEffect) — what modifier>current resolves to (class x level attrib-mod table lookup). Separate subsystem,
  referenced not transcribed.
- `<repos>/MidsReborn/MidsReborn/Core/BooleanExprPreprocessor.cs` (1-570) — SIBLING subsystem (out of strict scope): the Requires/conditional expression evaluator. Also Jace-based; converts infix
  &&/|| to prefix AND()/OR() functions. Confirms Jace operator handling limitations.
- `<repos>/MidsReborn/MidsReborn/MidsReborn.csproj` (721) — Dependency pin: Jace4fc 1.0.2 is the underlying infix math engine.

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| Custom function set (eq,ne,gt,gte,lt,lte,minmax,and,or) | Expressions.cs:704-714 | eq=\|a-b\|<double.Epsilon; ne=\|a-b\|>double.Epsilon; gt/gte/lt/lte=comparisons; minmax(a,b,c)=Min(max(b,c),Max(min(b,c),a)); and=both!=0; or=either!=0 |
| CommandsDict runtime substitution table (26 entries) | Expressions.cs:461-489 | full transcription in spec; keys like @StdResult, effect>scale, power.base>rechargetime, cur.kToHit, modifier>current, rand(), source>cur.kMeter[Abs], cfg>player/target>hp/end |
| FunctionsDict regex substitution table (16 entries) | Expressions.cs:496-514 | regex patterns + evaluators; e.g. source.ownPower?(), NAME>variableVal, NAME>mag(IDX), modifier>ID, powerIs(), powerVectorsContains(), source.owner>archIn(), GCMActive/GCMScale(), powerActive() |
| RPN Magnitude special-case literal + formula | Expressions.cs:662-671 | literal '.8 rechargetime power.base> 1 30 minmax \* 1.8 + 2 \* @StdResult \* 10 / areafactor power.base> /' → (clamp(RechargeTime,0,30)*0.800000011920929+1.79999995231628)/5.0 / AoEModifier \* Scale |
| ModifierCaster level index | Expressions.cs:542,567 | maxPlayerLevel=50; table row index = 50-1 = 49; column = playable-archetype index |
| source>Base.kHitPoints null fallback | Expressions.cs:481 | 1000 when Character.Archetype == null |
| rand() implementation | Effect.cs:15 | public double Rand => new Random().NextDouble(); nondeterministic per access |
| @StdResult / @Strength / effect>scale | Expressions.cs:469-471,588-596 | all resolve to sourceFx.Scale (GetStrength returns Scale, not buffed strength) |
| Probability clamp | Effect.cs:380 | Math.Max(0, Math.Min(1, retValue)); error→0 |
| Jace4fc engine version | MidsReborn.csproj:721 | Jace4fc 1.0.2 — CalculationEngine.New\<double>() |

### Expressions language (AttribType.Expression → Mag / Duration / Probability)

#### RESOLVED: Does it exist, and is it used?

**Yes, definitively.** An effect whose `AttribType == Enums.eAttribType.Expression` carries an `Expressions` object with three independent string fields: `Duration`, `Magnitude`, `Probability`
(Effect.cs:128-133, deserialized as three consecutive `reader.ReadString()`). Each is evaluated on demand by the effect's property getters:

- **Probability** (Effect.cs:378-380): `AttribType==Expression && Probability!=""` → `Parse(this, Probability)`, then clamp to `[0,1]` (`Math.Max(0, Math.Min(1, v))`); on parse error returns 0.
- **Magnitude / Mag** (Effect.cs:398-399): `Expression && Magnitude!=""` → `Parse(this, Magnitude)`; `Expression` with empty Magnitude → falls back to `Scale * nMagnitude`. The whole Mag is multiplied
  by `-1` when `EffectType==Damage`.
- **Duration** (Effect.cs:416-417): `Expression && Duration!=""` → `Parse(this, Duration)`; `Expression` with empty Duration → `Math_Duration` (if >0.01) else `nDuration`.

So a Python port that must reproduce Expression-type effect values **needs an expression evaluator**. Real example (data/canonical snipe):
`magnitude_expression = "@StdResult * (0.211 * minmax(4.54 * source>cur.kToHit - 3.41, -1, 1) + 1)"`.

#### Storage format: INFIX (not RPN)

The stored strings are standard **infix arithmetic** with function-call syntax, e.g. `@StdResult * (0.211 * minmax(...) + 1)`. There is exactly ONE postfix/RPN string in the wild, hardcoded and
intercepted (see "RPN special case" below); everything else is infix fed to Jace.

#### Evaluation pipeline (Expressions.cs `Parse` 647-687 → `InternalParsing` 689-756)

`Parse(sourceFx, exprType)`:

1. If `exprType==Magnitude` **and** the Magnitude string contains the literal RPN token block (see below) → compute the hardcoded formula and return immediately.
2. If `exprType==Magnitude` and the string is blank → return 0. Otherwise **clone** the effect (`sourceFx.Clone()`) and evaluate the clone. (Duration and Probability evaluate `sourceFx` directly, no
    clone.)
3. Return `error.Found ? 0 : retValue`.

`InternalParsing(sourceFx, exprType)` — the actual evaluator, in strict order:

1. Select the raw expression string for the requested type.
2. Create a Jace engine: `CalculationEngine.New<double>()`.
3. Register 9 **custom functions** (definitions below).
4. **Constant substitution:** iterate `CommandsDict(sourceFx)` (26 entries) and do a plain `string.Replace(key, value)` for each, in dictionary insertion order. Each value is the current numeric value
    formatted with default `ToString()` (invariant-ish; see edge cases).
5. **Function substitution:** iterate `FunctionsDict(sourceFx, pickedPowerNames)` (16 regex→evaluator pairs) and apply `Regex.Replace` for each, replacing every match with the computed numeric string.
6. `mathEngine.Calculate(expr)` → `double`, cast to `float`.
7. On `ParseException`, `VariableNotDefinedException`, or `InvalidOperationException`: set `error.Found=true`, return 0.

After steps 4-5 the string must be **pure math** (numbers, `+ - * / % ^`, parentheses, commas, and the registered function names). Any residual identifier (an unsubstituted token) makes Jace throw
`VariableNotDefinedException` → result 0.

#### Custom functions registered on the engine (Expressions.cs:704-714)

| Name | Arity | Definition |
| --- | --- | --- |
| `eq(a,b)` | 2 | `abs(a-b) < double.Epsilon ? 1 : 0` (double.Epsilon ≈ 4.94e-324 → effectively exact equality) |
| `ne(a,b)` | 2 | `abs(a-b) > double.Epsilon ? 1 : 0` |
| `gt(a,b)` | 2 | `a > b ? 1 : 0` |
| `gte(a,b)` | 2 | `a >= b ? 1 : 0` |
| `lt(a,b)` | 2 | `a < b ? 1 : 0` |
| `lte(a,b)` | 2 | `a <= b ? 1 : 0` |
| `minmax(a,b,c)` | 3 | clamp a into the interval bounded by b and c regardless of order: `Min(max(b,c), Max(min(b,c), a))` |
| `and(a,b)` | 2 | `(a!=0 && b!=0) ? 1 : 0` |
| `or(a,b)` | 2 | `(a!=0 \|\| b!=0) ? 1 : 0` |

#### Constant/keyword table — runtime authoritative (`CommandsDict`, Expressions.cs:461-489)

Plain-string replacements, applied in this order. `fxPower = sourceFx.GetPower()`; when null, the `power.base>*` entries yield `"0"`.

1. `power.base>activateperiod` → `fxPower.ActivatePeriod`
2. `power.base>activatetime` → `fxPower.CastTime`
3. `power.base>areafactor` → `fxPower.AoEModifier`
4. `power.base>rechargetime` → `fxPower.BaseRechargeTime`
5. `power.base>endcost` → `fxPower.EndCost`
6. `power.base>range` → `fxPower.Range`
7. `effect>scale` → `sourceFx.Scale`
8. `@StdResult` → `sourceFx.Scale`
9. `@Strength` → `GetStrength(sourceFx)` = `sourceFx.Scale` (NOT buffed strength — just Scale)
10. `ifPvE` → `PvMode==PvE ? "1" : "0"`
11. `ifPvP` → `PvMode==PvP ? "1" : "0"`
12. `caster>modifier>current` → `ModifierCaster(sourceFx.ModifierTable)` (see below)
13. `modifier>current` → `DatabaseAPI.GetModifier(sourceFx)` (class×level attrib-mod value)
14. `maxEndurance` → `Character.DisplayStats.EnduranceMaxEnd`
15. `rand()` → `sourceFx.Rand` = `new Random().NextDouble()` — **nondeterministic, fresh per call**
16. `cur.kToHit` → `Character.DisplayStats.BuffToHit`
17. `base.kToHit` → `MidsContext.Config.ScalingToHit`
18. `source>Max.kHitPoints` → `Character.Totals.HPMax`
19. `source>Base.kHitPoints` → `Archetype.Hitpoints` (1000 if archetype null)
20. `source>cur.kMeter` → `GetVariableValue(fxPower.FullName, absolute=false)` = `VariableValue * Power.VariableMax / 100.0`
21. `source>cur.kMeterAbs` → `GetVariableValue(fxPower.FullName, absolute=true)` = `VariableValue`
22. `cfg>player>hp` → `Config.CombatContextSettings.PlayerSettings.HpPercent`
23. `cfg>player>end` → `PlayerSettings.EndPercent`
24. `cfg>player>isAlive` → `PlayerSettings.IsAlive ? "1" : "0"`
25. `cfg>target>hp` → `TargetSettings.HpPercent`
26. `cfg>target>end` → `TargetSettings.EndPercent`

NOTE: `CommandsList` (Expressions.cs:69-455) is a SEPARATE list used only for the DB-editor autocomplete UI; it is NOT the runtime substitution set and its keys differ (e.g. it lists `source>kMeter`,
`>variableVal`, `powerActive(`, comparison funcs). **Do not port `CommandsList` as the evaluator's vocabulary — port `CommandsDict` + `FunctionsDict`.**

#### Function table — regex substitutions (`FunctionsDict`, Expressions.cs:496-514)

Applied after constants, each a `Regex.Replace`:

1. `source.ownPower?(NAME)` — regex `source\.ownPower\?\(([\w\-.]+)\)` → `1` if NAME ∈ picked power FullNames else `0`.
2. `source.ownPowerNum?(NAME)` → `1` if NAME in current build else `0` (`OwnPowerNumCheck`).
3. `NAME>variableVal` — regex `([a-zA-Z\-_.]+)>variableVal` → `GetVariableValue(NAME)` (absolute VariableValue; `0` if power not in build).
4. `NAME>mag(IDX)` — regex `([a-zA-Z\-_.]+)>mag\(([0-9]+)\)` → `GetPowerMag`: BuffedMag of the enhanced power NAME's effect index IDX (`0` on any miss/out-of-range).
5. `modifier>ID` — regex `modifier\>([\w\-]+)` → `GetModifier(ID)` = `modTable[ID].Table[Character.Level][Archetype.Column]` (`0` if not found).
6. `powerGroupIn(PREFIX)` → `1` if `fxPower.FullName.StartsWith(PREFIX)` else `0`.
7. `powerGroupNotIn(PREFIX)` → inverse of 6.
8. `powerIs(NAME)` → `1` if `fxPower.FullName == NAME` (case-insensitive) else `0`.
9. `powerIsNot(NAME)` → inverse of 8.
10. `powerVectorsContains(VECTOR)` → parse VECTOR as `Enums.eVector`; `1` if `power.AttackTypes` has that flag else `0`.
11. `source.owner>arch(NAME)` → `1` if `Archetype.DisplayName == NAME` (case-insensitive) else `0`.
12. `source.owner>archIn(N1,N2,...)` — regex arg `[a-zA-Z\s,]+`, split on comma, trimmed, lowercased → `1` if character archetype in list.
13. `caster>modifier(ID)` → `ModifierCaster(ID)`.
14. `GCMActive(GCM)` → `1` if `Character.ModifyEffects` contains key GCM else `0`.
15. `GCMScale(GCM)` → the scale value from `Character.ModifyEffects[GCM]` else `0`.
16. `powerActive(NAME)` → `1` if power NAME is picked and `Power.Active` else `0`.

`ModifierCaster(modifierId)` (540-568): looks up `AttribMods.Modifier` by ID (case-insensitive); returns `Table[49][archetypeColumn]` where 49 = maxPlayerLevel(50)−1 and archetypeColumn is the
playable-class index of the current archetype; `0` on any miss.

#### RPN special case (Magnitude only, Expressions.cs:662-671)

If `Magnitude` contains the literal substring:
`".8 rechargetime power.base> 1 30 minmax * 1.8 + 2 * @StdResult * 10 / areafactor power.base> /"`
then bypass Jace entirely and compute:

```text
ret = (float)((Max(Min(power.RechargeTime, 30), 0) * 0.800000011920929 + 1.79999995231628) / 5.0)
      / power.AoEModifier * effect.Scale
```

Then if the Magnitude string is longer than `literal.Length + 2`, multiply `ret` by `float.Parse(magnitude[literal.Length+1 ..][..2])` (the two chars after the literal+1 offset). Return directly. This
is a hand-patched interpretation of a City-of-Heroes postfix expression Jace cannot parse. Note the float constants `0.800000011920929` and `1.79999995231628` are the float32 representations of 0.8
and 1.8.

#### Jace math semantics (the residual pure-math evaluator)

Jace4fc 1.0.2 (fork of "Jace — Just Another Calculation Engine"). After substitution the port must evaluate standard infix math:

- Binary operators `+ - * / % ^`, unary minus, parentheses, comma-separated function arguments, decimal and scientific-notation number literals.
- `^` is exponentiation and is right-associative; `* / %` bind tighter than `+ -`; unary minus binds tighter than the binary ops. (Standard Jace precedence — verify against Jace4fc source; see open
  items.)
- Function names are matched literally against registered functions; the 9 custom functions above are the only ones CoH Mag/Dur/Prob expressions use in practice. Jace also ships stock functions (sin,
  cos, sqrt, log, max, min, round, etc.) and constants (`e`, `pi`) which could theoretically appear.
- MidsReborn deliberately AVOIDS native `&& || < > <= >= == !=` in Mag/Dur/Prob expressions, expressing logic through the `and/or/eq/ne/gt/gte/lt/lte` function forms instead. (The sibling Requires
  evaluator in BooleanExprPreprocessor.cs pre-rewrites infix `&&`/`||` into prefix `AND(a,b)`/`OR(a,b)` "due to limitations of the math engine" — evidence the port should mirror the function-form
  approach rather than rely on native boolean operators.)

#### Caller-side post-processing (must replicate for exact numbers)

- Probability result: `clamp(v, 0, 1)`; error → 0.
- Mag result: value as-is, then Mag getter multiplies by `-1` if `EffectType==Damage`.
- Duration result: value as-is; error → 0.
- Display code (Effect.cs:964-975) shows Probability as `round(clamp(v*100, 0, 100))% chance`.

#### Stepwise pseudocode for the port

```text
def parse(fx, kind):            # kind in {duration, magnitude, probability}
    if kind == MAGNITUDE:
        if RPN_LITERAL in fx.expr.magnitude: return rpn_special(fx)
        if blank(fx.expr.magnitude): return 0.0
        fx = fx.clone()
    s = fx.expr[kind]
    for key, value in COMMANDS_DICT(fx):        # insertion order, plain replace
        s = s.replace(key, format_num(value))
    for regex, evaluator in FUNCTIONS_DICT(fx): # regex replace-all
        s = regex.sub(lambda m: evaluator(m), s)
    try:
        return float(jace_calculate(s))         # custom funcs eq/ne/gt/.../minmax/and/or registered
    except (ParseError, UndefinedVar, InvalidOp):
        return 0.0
```

**Open items / gaps:**

- Jace4fc 1.0.2 source not present on disk (not in nuget cache, no DLL in repo). Exact operator precedence, associativity of ^, whitespace/number-literal grammar, function-name case-sensitivity, and
  stock function/constant set (e, pi, sqrt, etc.) are stated from general Jace knowledge, NOT verified against the 4fc fork. A porter should obtain Jace4fc 1.0.2 (AstBuilder/Tokenizer) to confirm
  precedence table and whether native &&/||/comparison operators are enabled.
- Substring-collision ordering risk in CommandsDict plain-string replaces: e.g. 'source>cur.kMeter' (entry 20) is a prefix of 'source>cur.kMeterAbs' (entry 21); replacing the shorter key first would
  corrupt the longer token to '\<value>Abs'. Also observed data uses 'source>cur.kToHit' but only 'cur.kToHit' is a key, leaving a residual 'source>' that makes Jace throw → result 0. Port must
  replicate C# Dictionary insertion-order iteration exactly and reproduce these residual-token failures (return 0) to match numbers. Whether this is intended or a latent MidsReborn bug is unresolved.
- Number formatting of substituted values: C# uses `$"{value}"` (current-culture ToString) for most CommandsDict entries; ModifierCaster explicitly uses InvariantCulture. Exact string form (decimal
  separator, precision, exponent) fed to Jace could affect parse; assumed invariant/period decimal. Not exhaustively verified.
- External stat dependencies are large separate subsystems not transcribed here: DatabaseAPI.GetModifier (class×level attrib-mod tables), Character.DisplayStats.BuffToHit, Character.Totals.HPMax,
  PowerEntry.VariableValue / Power.VariableMax, Config.CombatContextSettings, Config.ScalingToHit, Character.ModifyEffects (GCM). Exact-match of expression outputs requires those sections to be ported
  first.
- Effect.Clone() semantics for the Magnitude path (Parse line 678) not fully traced — assumed a deep-ish copy that leaves GetPower()/Scale intact. Confirm the clone does not reset ModifierTable/Scale.
- 'requires_expression' (Requires/conditional) system in BooleanExprPreprocessor.cs is a related but distinct evaluator (also Jace-based, rewrites infix &&/|| to prefix AND/OR, supports tokens like
  target>enttype eq 'critter', target>kHitPoints%). It is out of the Mag/Dur/Prob scope but shares vocabulary; full grammar of the Requires language was not exhaustively extracted here and may warrant
  its own section.
- rand() nondeterminism: expressions containing rand() produce different values each evaluation. A deterministic port must decide on seeding/caching to get reproducible build numbers; MidsReborn
  re-rolls every getter access.

---

## gbpa-pass-pipeline

**Port risk:** high — The pipeline's correctness depends on exact pass ordering, in-place array mutation quirks (Pass5 RemoveAt, buffed.Accuracy overwrite before AccuracyMult), enum-ordinal array
indexing, and many external leaf functions (ED tables, GetEnhancementMagSum, AbsorbEffects) not fully resolved here. A naive reimplementation will diverge on Accuracy/ToHit routing, Defiance
exclusion, generic-vs-typed defense folding, and the BuffDam min/avg/max heuristic without transcribing these details precisely.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/clsToonX.cs` (380-1002, 1004-2236) — Sole source. Contains GenerateBuffedPowerArray orchestrator (2194-2236) and every GBPA_* pass plus
  GenerateBuffData/CalculateAndApplyEffects/GBD_Totals bucket routing.

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| ForceLevel (slot legality gate) | clsToonX.cs:1751 (MidsContext.Config.ForceLevel) | A slot contributes only if Slots[i].Level < Config.ForceLevel; this is the build-level / exemplar slot cutoff applied in Pass1 |
| ScalingToHit | clsToonX.cs:2150 (MidsContext.Config.ScalingToHit) | base to-hit scalar multiplied into buffed.Accuracy in Pass6 (config value, e.g. 0.75) |
| Pass4 additive +1 | clsToonX.cs:2091-2098 | ++ on EndCost,InterruptTime,Range,RechargeTime and every effect Math_Mag/Math_Duration (Accuracy NOT incremented) |
| BuffDam heuristic range | clsToonX.cs:961-963 | _selfBuffs.Damage[1..8] -> C# range = indices 1 through 7 inclusive (excludes index 0) |
| Generic defense fold index | clsToonX.cs:867-872 | _selfBuffs.Defense[0] is generic/all-vector; folded additively into Defense[1..] |
| MezRes/DebuffRes scale | clsToonX.cs:885,890 | StatusResistance and DebuffResistance multiplied by 100 into Totals |
| Speed floor / caps | clsToonX.cs:907-920 | Spd=(1+max(eff,-0.9))*Base; clamp to ServerData.MaxMax{Fly=~257.985,Run=~166.257,Jump=~176.358}Speed (comments) |
| Archetype caps | clsToonX.cs:1277-1287,982-1001 | Archetype.RechargeCap, DamageCap, RegenCap, RecoveryCap, ResCap, HPCap, PerceptionCap (per-AT JSON, external) |
| TotalsCapped -1 offsets | clsToonX.cs:982-985 | BuffDam,BuffHaste,HPRegen,EndRec capped at (Cap - 1) |
| IsClickPower | clsToonX.cs:834-837 | PowerType==Click && ClickBuff==false |
| IsGlobalAccuracySource | clsToonX.cs:829 | ReferenceEquals(src, CurrentBuild.SetBonusVirtualPower) \|\| src.PowerType==GlobalBoost |
| Defiance predicate | clsToonX.cs:666-669 | (isEnhancementEffect && EffectClass==Tertiary) \|\| ValidateConditional("Active","Defiance") \|\| SpecialCase==Defiance |
| AddEnhFX ModifierTable regex | clsToonX.cs:1205 | ^(Melee\|Ranged)*Boosts* — excludes these enhancement-mode ATO effects |
| Legality intersection basis | clsToonX.cs:1458 | powerMath.Enhancements.Intersect(power1.Enhancements).Any() — uses Power.Enhancements, explicitly NOT BoostsAllowed |
| GetAllowedEffectsFromEnhance table | clsToonX.cs:1605-1693 | eEnhance -> allowed (EffectType,MezType,ETModifies) tuples; Defense also allows ResEffect->Defense; Heal allows Absorb/Regeneration/HitPoints/ResEffect->Regeneration; Slow maps to SpeedRunning/Jumping/Flying |

### GBPA buffed-power generation pipeline (clsToonX)

Two power arrays are produced per recompute, sized `CurrentBuild.Powers.Count`:

- `_mathPowers[]` — a per-power **multiplier/accumulator** power. Its `Accuracy/EndCost/InterruptTime/Range/RechargeTime` scalars and every effect's `Math_Mag`/`Math_Duration` hold *enhancement
  multipliers* (start at 0, get +1 added at Pass4 so they become "1 + sum").
- `_buffedPowers[]` — the **displayed** power. Base values multiplied by the corresponding `_mathPowers` multiplier.

Also two build-wide accumulators of type `Enums.BuffsX`:

- `_selfEnhance` — enhancement-sourced buffs (built in the first `GenerateBuffData(..., true)` pass).
- `_selfBuffs` — power/effect-sourced buffs (built in the second `GenerateBuffData(..., false)` pass).

#### Top-level order — `GenerateBuffedPowerArray()` (2194-2236)

Execute exactly in this order:

1. `CurrentBuild.GenerateSetBonusData()` — builds `SetBonusVirtualPower` (external; set-bonus + exemplar −3 handling lives there, NOT here).
2. `_selfBuffs.Reset()`, `_selfEnhance.Reset()`, `ModifyEffects = new Dictionary<string,float>()`.
3. Reallocate `_buffedPowers` and `_mathPowers` to `Count`.
4. **Pass 0** `GBPA_Pass0_InitializePowerArray()` (assemble base powers, apply global-boost incarnate enh, variable scaling, snapshot buffed).
5. `GenerateModifyEffectsArray()` — harvest `GlobalChanceMod` rewards → `ModifyEffects[reward]` (sum of `Scale`) from every included buffed power and from `SetBonusVirtualPower` (2285-2359).
6. `GenerateBuffData(ref _selfEnhance, true)` — **enhancement pass**: for every included, non-GlobalBoost buffed power and the set-bonus virtual power (and a PvP resist temp power when
    `Inc.DisablePvE`), call `CalculateAndApplyEffects(..., enhancementPass:true)` (2155-2192).
7. `Parallel.For` over all `_mathPowers` (order within a power fixed; powers independent) (2209-2222):
    1. `GBPA_Pass1_EnhancePreED(math,hIDX)`
    2. `GBPA_Pass2_ApplyED(math)`
    3. `GBPA_Pass3_EnhancePostED(math,hIDX)`
    4. `GBPA_Pass4_Add(math)`
    5. `GBPA_ApplyArchetypeCaps(math)`
    6. `GBPA_Pass5_MultiplyPreBuff(math, buffed)`
8. `GenerateBuffData(ref _selfBuffs, false)` — **buff pass** (same routine, `enhancementPass:false`).
9. `Parallel.For` → `GBPA_Pass6_MultiplyPostBuff(math, buffed)` for every non-null math power (2226-2232).
10. `ApplyGlobalEnhancements()` (380-505).
11. `GBD_Totals()` (839-1002).

> Ordering trap: `_selfBuffs` is empty during Pass1–Pass6 step 6 (built at step 8), but `Pass6` (step 9) reads `_selfBuffs` for ToHit/BuffAcc — Pass6 MUST run after the buff pass. `_selfEnhance` is
> built at step 6, before Pass3 which reads it.

#### Pass 0 — `GBPA_Pass0_InitializePowerArray` (1004-1065)

For each build slot `hIDX` with `NIDPower > -1`:

- Resync: if `Power.Stacks < VariableValue` set `Power.Stacks = VariableValue` (1021-1024).
- `_mathPowers[hIDX] = GBPA_SubPass0_AssemblePowerEntry(NIDPower, hIDX, stackingOverride=1)`; then set `_mathPowers[hIDX].Stacks = VariableValue`.

`GBPA_SubPass0_AssemblePowerEntry(nIDPower,hIDX,stackingOverride)` (1067-1095), in order:

1. Clone DB power `new Power(Database.Power[nIDPower])`.
2. Set `Stacks` = stackingOverride if >-1 else the entry's `VariableValue`.
3. `GBPA_ApplyPowerOverride` — if power has a `PowerRedirect` effect with `nOverride>-1 && Probability≈1 && CanInclude()`, replace the power with `Database.Power[nOverride]` keeping `Level`
    (1097-1120).
4. `GBPA_AddEnhFX(power,hIDX)` — append enhancement-granted proc/aura effects (see below).
5. `power.AbsorbPetEffects(hIDX, stackingOverride)` (external).
6. `power.ApplyGrantPowerEffects()` (external).
7. `GBPA_AddSubPowerEffects(power,hIDX)` — clone effects from each included subpower into the effect array (1239-1273).
8. `power.ApplyModifyEffects()` (external; consumes `ModifyEffects`).

Then two build-wide loops:

- Nested `index1×index2`: for every *other* included power `index2` (`StatInclude && NIDPower>-1`), call
  `GBPA_ApplyIncarnateEnhancements(_mathPowers[index1], hIDX=-1, _mathPowers[index2], ignoreED=false, effectType=GrantPower)` (1033-1050). This folds global-boost GrantPower incarnate/GlobalBoost
  effects into every power at assembly time.
- For each `hIDX`: `GBPA_MultiplyVariable(_mathPowers[hIDX],hIDX)` then `_buffedPowers[hIDX] = new Power(_mathPowers[hIDX]); SetMathMag()` (snapshot base into buffed) (1052-1062).

`GBPA_MultiplyVariable` (1570-1596): if `VariableEnabled`, for each effect with `VariableModified && !IgnoreScaling`, `fx.Scale *= VariableValue`.

`GBPA_AddEnhFX` (1122-1225): skipped entirely if `Config.I9.IgnoreEnhFX`. For each filled slot whose enhancement resolves to a power (`GetPower()`): skip if `ProcInclude && enhancement.IsProc`;
require an enhancement set. For each effect on the enhancement's power decide `shouldAddEffect`:

- Pets branch: if `enhEffect.AffectsPetsOnly() && power.IsSummonPower`, resolve the summoned entity's powerset and include the effect only if some entity power has a matching `EffectType`.
- Normal branch (1197-1205): find enhancement index in set; include only when `SpecialBonus[idx].Index.Length<=0` AND (enhancement has no `Enhancement`-mode effect OR the effect's `ModifierTable` does
  NOT match regex `^(Melee|Ranged)_Boosts_`).
- Included effects are cloned via `AddClonedEffectToList` (1227-1237): `isEnhancementEffect=true`, `IgnoreScaling=isProc`, `Absorbed_Effect=true`, `Buffable=false`. GrantPower effects set
  `HasGrantPowerEffect=true`.

#### Enhancement multiplier passes (per power, in Parallel.For)

**Pass1 `GBPA_Pass1_EnhancePreED` (1720-1868)** — sum enhancement magnitudes *before* ED:

- Zero the 5 scalars (`Accuracy,EndCost,InterruptTime,Range,RechargeTime`) and every effect's `Math_Mag`/`Math_Duration`.
- `isAcc/isRech/isEnd = Database.Power[...].IgnoreEnhancement(Accuracy/RechargeTime/EnduranceDiscount)` — these are inverted flags: a `true` means the power DOES accept that enhancement (guard added
  only for those three scalars).
- For each slot `index` in `0..SlotCount` that has `Enhancement.Enh>-1` AND **`Slots[index].Level < Config.ForceLevel`** (this is the exemplar/build-level gate: slots placed at a level ≥ ForceLevel
  contribute nothing):
  - Add `GetEnhancementEffect(Accuracy/EnduranceDiscount/Interrupt/Range/RechargeTime,-1,1)` to the matching scalar (Acc/End/Rech gated by their ignore-flags; Interrupt and Range always).
  - For each **Buffable** effect on the power, match its `EffectType` to an `eEnhance` bucket. Two special remaps (1795-1804): `Enhancement` with `ETModifies==Accuracy` → treat as Accuracy enhance
    (flag7); `ResEffect` with `ETModifies==Defense` → allowed. Otherwise consult `GetAllowedEffectsFromEnhance(enhancement)` (see constants) and skip if not allowed.
  - `fxMag = GetEnhancementEffect(iEffect, mezTypeOr-1, _buffedPowers[hIDX].Effects[effIdx].Math_Mag)`; for Mez pass the mez index; for `ResEffect→Defense` use `Defense` enhance, for
    `ResEffect→Regeneration` use `Heal` enhance (1825-1833).
  - Special-case: `Damage` of `DamageType==Special` → `fxMag=0`; `Mez` with `AttribType==Duration` → move `fxMag` into `fxDuration`, `fxMag=0` (1835-1843).
  - Accumulate into `Math_Mag`/`Math_Duration`.
- Finally, for every included power call `GBPA_ApplyIncarnateEnhancements(math,hIDX,_mathPowers[index],ignoreED=false,effectType=Enhancement)` (1851-1865).

**Pass2 `GBPA_Pass2_ApplyED` (1870-1922)** — apply Enhancement Diversification to the accumulated pre-ED sums:

- Scalars: `X = Enhancement.ApplyED(Enhancement.GetSchedule(<enh>), X)` for Accuracy, EnduranceDiscount, Interrupt, Range, RechargeTime.
- For each effect where `!isEnhancementEffect` (proc/absorbed effects skip ED): map `EffectType`→`eEnhance` with the same two special remaps; for `Mez` apply ED to both `Math_Mag` and `Math_Duration`
  using `GetSchedule(enh, mezType)`; for `ResEffect→Defense` use the `Defense` schedule; otherwise `GetSchedule(enh)`. (`Enhancement.ApplyED`/`GetSchedule` are external ED-table functions.)

**Pass3 `GBPA_Pass3_EnhancePostED` (1924-2087)** — add *global* enhancement buffs (`_selfEnhance`) that bypass ED:

- Iterate `index1` over `_selfEnhance.Effect[]` as an `eEffectType`. Accuracy/EnduranceDiscount/Interrupt/Range/RechargeTime add `_selfEnhance.Effect[index1]` to the scalar (Acc/End/Rech gated by the
  power's ignore flags).
- Default branch, per Buffable effect matching `eEffectType`: `Damage`→`+=_selfEnhance.Damage[DamageType]`; `Defense`→`+=_selfEnhance.Defense[DamageType]`;
  `Resistance`→`+=_selfEnhance.Resistance[DamageType]`; `Mez`→ duration or mag `+=_selfEnhance.Mez[MezType]` by AttribType; speed-scalar enhancement/effect → `_selfEnhance.Effect[type]` when buffed
  mag>0 else `_selfEnhance.EffectAux[type]`; fallback `+=_selfEnhance.Effect[index1]`.
- Then for every included power call `GBPA_ApplyIncarnateEnhancements(math,hIDX,_mathPowers[index],ignoreED=true,effectType=Enhancement)` (2077-2084).

**Pass4 `GBPA_Pass4_Add` (2089-2101)** — convert accumulated sums to multipliers: `++EndCost; ++InterruptTime; ++Range; ++RechargeTime;` and `++Math_Mag; ++Math_Duration` on every effect. (Note:
`Accuracy` is NOT incremented here; it is combined multiplicatively in Pass6.)

**`GBPA_ApplyArchetypeCaps` (1275-1289)** — clamp `math.RechargeTime` to `Archetype.RechargeCap`; clamp every `Damage` effect's `Math_Mag` to `Archetype.DamageCap`.

**Pass5 `GBPA_Pass5_MultiplyPreBuff` (2121-2141)** — apply the multipliers to the buffed power:

- `buffed.EndCost /= math.EndCost`, `buffed.InterruptTime /= math.InterruptTime`, `buffed.Range *= math.Range`, `buffed.RechargeTime /= math.RechargeTime` (End/Interrupt/Rech are *divisions* — the
  multiplier is a reduction denominator; Range multiplies).
- If `math.Effects.Length > buffed.Effects.Length` call `GBPA_Pass5_ResyncEffects` (2103-2119): walk both effect lists index-aligned; where the identity tuple
  `(EffectType,DamageType,MezType,ETModifies,Summon)` differs, remove that entry from the math list so lengths realign. (Naive-reimpl trap: this in-place `RemoveAt(i)` while iterating up to
  `min-length` can skip a comparison after a removal — reproduce the exact loop, do not "fix" it.)
- For each math effect index: `buffed.Math_Mag = buffed.Mag * math.Math_Mag`; `buffed.Math_Duration = buffed.Duration * math.Math_Duration`.

**Pass6 `GBPA_Pass6_MultiplyPostBuff` (2143-2153)** — accuracy/to-hit finalization, using the completed `_selfBuffs`:

- `nToHit = IgnoreBuff(ToHit) ? _selfBuffs.Effect[ToHit] : 0` (note `IgnoreBuff` semantics: `!IgnoreBuff(...) ? 0 : selfBuffs`; a `true` return means the power *accepts* the buff).
- `nAcc = IgnoreBuff(Accuracy) ? _selfBuffs.Effect[BuffAcc] : 0`.
- `buffed.Accuracy = buffed.Accuracy * (1 + math.Accuracy + nAcc) * (Config.ScalingToHit + nToHit)`.
- `buffed.AccuracyMult = buffed.Accuracy_afterAccPart` (i.e. `original_buffed.Accuracy * (1 + math.Accuracy + nAcc)`, computed BEFORE the ToHit multiply — read line 2150 then 2151; 2151 recomputes
  from the just-written `buffed.Accuracy` so it equals `buffed.Accuracy` line value again — see open_items note).

#### `GBPA_ApplyIncarnateEnhancements` (1393-1568) — global-boost/incarnate folding

Called with an `effectType` selector (`GrantPower` in Pass0, `Enhancement` in Pass1/3) and `ignoreED`. Only runs if `powerMath.Slottable`. For each effect on the source `power`:

- Disqualify if `EffectClass==Ignored`; if `effectType==Enhancement` and effect is not `Enhancement`/`DamageBuff`; if `effectType==GrantPower` and effect IS `Enhancement`/`DamageBuff`; if
  `IgnoreED != ignoreED`; if source is not a `GlobalBoost` power and the effect isn't an absorbed GlobalBoost effect; if effect is an absorbed GrantPower (1436-1441).
- Legality gate (**RESOLVED — accept/reject rule**, 1456-1463): `isAllowed = powerMath.Enhancements.Intersect(power1.Enhancements).Any()`. The check uses **`Power.Enhancements`, NOT
  `Power.BoostsAllowed`** (explicit code comment). If the target power and the source share no allowed enhancement type, the effect is rejected. `power1` is the absorbed source power when
  `Absorbed_Effect && Absorbed_Power_nID>-1`, else `power`.
- For `Enhancement`/`DamageBuff` effects, route by `ETModifies` (1470-1495): `Accuracy`(if power accepts)→`+=BuffedMag` to `Accuracy`; `EnduranceDiscount`→`EndCost`; `InterruptTime`→`InterruptTime`;
  `Range`→`Range`; `RechargeTime`(if accepts)→`RechargeTime`; default→`HandleDefaultIncarnateEnh` (1291-1363) which folds Damage/Defense/Mez into matching Buffable effects' `Math_Mag`/`Math_Duration`
  by damage/mez type.
- `GrantPower` effects → `HandleGrantPowerIncarnate` (declared but note the dispatch at 1497-1500 calls it; the standalone at 1366-1391 is dead). Else `AbsorbEffects(...)` appends the effect to both
  math and buffed, marking `Absorbed_Effect`, `ToWho=Target`.
- Then 3 GCM sub-passes (1522-1567): gather `EffectId` flags → compute `ModifyEffects[flag]=Σ Scale` for source's matching `Reward` effects → set each appended effect's
  `EffectiveProbability = Probability * (grantProbability ?? 1)`.

#### `CalculateAndApplyEffects` (507-832) — bucket routing into `BuffsX`

Called per included buffed power (and set-bonus virtual power). Skips `GlobalBoost` powers. Iterates `effIndex` over `nBuffs.Effect.Length` treating it as an `eEffectType`; **`Damage` type is always
skipped** (accumulated only via `DamageBuff`). Gathering:

- Enhancement pass (`enhancementPass && iEffect!=DamageBuff`): `shortFx = tPwr.GetEnhancementMagSum(iEffect,-1)`.
- Else `shortFx = tPwr.GetEffectMagSum(iEffect)`, with Max{Run,Jump,Fly}Speed special-cased to pull the corresponding SpeedX sum plus a self-only Max cap into `nBuffs.Effect[MaxXSpeed]`.

Per contributing sub-effect (only if `ToWho ∈ {Self,All}`):

- **Enhancement effect** (`EffectType==Enhancement`): `ETModifies==Mez`→`Boosts Mez[MezType]`; other non-null ETModifies→`Boosts[ETModifies]`. `Range` enhance also adds to `Effect[Range]` on the
  non-enh pass; `Heal` enhance redirects to `Effect[Heal]`.
- **Skip effects absorbed from a GlobalBoost power** (`Absorbed_PowerType==GlobalBoost`) — they were already folded in Pass0 (603-606).
- Non-enh pass status/resist buckets: `Mez`→`StatusProtection[MezType]`; `MezResist`→`StatusResistance[MezType]`; `ResEffect`→`DebuffResistance[ETModifies]` (611-625).
- Enh pass + (DamageBuff or Enhancement) routing (630-704):
  - `ETModifies==Mez`→`Mez[MezType]`.
  - `ETModifies==Defense`: **typed vs generic fold** — `DamageType!=None`→`Defense[DamageType]`, else generic→`Effect[effIndex]` (index 0). Same pattern for
    `Resistance`→`Resistance[DamageType]`/generic (638-660).
  - DamageBuff aggregation: **Defiance exclusion** (664-676) — a DamageBuff is skipped (NOT added to `Damage[DamageType]`) when `isDefiance` is true, where
    `isDefiance = (isEnhancementEffect && EffectClass==Tertiary) || ValidateConditional("Active","Defiance") || SpecialCase==Defiance`.
  - Skip pure `Accuracy`/`Heal` ETModifies here; speed scalars split buff→`Effect[ETModifies]` vs debuff→`EffectAux[ETModifies]`; fallback→`Effect[effIndex]`.
- Non-enh remaining (705-816): skip Heal; MaxEnd for `Endurance/Aspect.Max`; Mez/MezRes when modeled as ETModifies; typed Defense/Resistance/Elusivity by DamageType.
  - **Accuracy-as-ToHit vs BuffAcc by source** (754-768): a non-ResEffect effect with `ETModifies==Accuracy` goes to `Effect[BuffAcc]` **only if `IsGlobalAccuracySource(tPwr)`** (source is the
    `SetBonusVirtualPower` reference or a `GlobalBoost` power); otherwise it goes to `Effect[ToHit]` (all ordinary accuracy buffs behave as ToHit in CoH).
  - Max{Run,Fly,Jump}Speed caps from scalar `Aspect.Max` effects.
  - ToHit: added to `Effect[ToHit]` unless `(isEnhancementEffect && EffectClass==Tertiary)`.
  - Generic fallback (799-816): enhancement pass does nothing more. **Absorb %→HP normalization**: if `effIndex==Absorb && fx.DisplayPercentage`, `value *= Character.Totals.HPMax`. Add `value` to
    `Effect[effIndex]`. **Click-power mirror subtraction** (812-815): if `IsClickPower(fx.GetPower()) && !BuildEffectString().Contains("From Enh")`, subtract `fx.Mag` from `Effect[effIndex]` (cancels
    the base magnitude of a click that emits a mirrored removal entry). `IsClickPower = PowerType==Click && ClickBuff==false`.

#### `GBD_Totals` (839-1002) — finalize Totals/TotalsCapped

- Toggle end use: sum `ToggleCost` of included Toggle buffed powers; track `canFly`.
- **Generic defense fold at index 0** (867-873): if `|_selfBuffs.Defense[0]| > epsilon`, add `Defense[0]` (the generic/all-vector defense) into every typed `Defense[1..]`. Then copy Def/Res/Elusivity
  vectors into `Totals`.
- Status: `Mez=StatusProtection`, `MezRes=StatusResistance*100`, `DebuffRes=DebuffResistance*100`.
- Scalars pulled from `_selfBuffs.Effect[...]`: BuffAcc = `_selfEnhance.Effect[BuffAcc] + _selfBuffs.Effect[BuffAcc]`; BuffHaste = enh+buff Haste; BuffToHit, Perception
  (`BasePerception*(1+Perception)`), Stealth, HPRegen, EndRec, Absorb, BuffRange, etc.
- Speeds: `Spd = (1+max(effect,-0.9))*Base`; `MaxSpd = MaxBase + effect*BaseSpeed`; then clamp each `Spd` to `ServerData.MaxMax{Fly,Run,Jump}Speed`. Fly zeroed if `!canFly`.
- `HPMax = _selfBuffs.Effect[HPMax] + Archetype.Hitpoints`.
- **min/avg/max BuffDam heuristic** (953-976): if all `_selfBuffs.Damage` are ~0, seed them from `_selfEnhance.Damage`. Compute over `Damage[1..8]` (C# range = indices 1‑7): `min`, `max`, `avg`. Then:
  if `max-avg < avg-min` → `BuffDam=max`; else if `max-avg > avg-min && min>0` → `BuffDam=min`; else → `BuffDam=max`. (Chooses the tail closest to the mean; the "min only if all positive" branch
  avoids reporting a debuffed vector as the headline number.)
- `ApplyPvpDr()`; `TotalsCapped.Assign(Totals)` then clamp: `BuffDam ≤ DamageCap-1`, `BuffHaste ≤ RechargeCap-1`, `HPRegen ≤ RegenCap-1`, `EndRec ≤ RecoveryCap-1`, each `Res[i] ≤ ResCap`,
  `HPMax ≤ HPCap` and `Absorb ≤ HPMax` (when HPCap>0), speeds ≤ their Max, JumpHeight ≤ `ServerData.MaxJumpHeight`, Perception ≤ `PerceptionCap`.

#### `ApplyGlobalEnhancements` (380-505)

Runs after Pass6. Inventories `GrantPower` effects on main-set (Primary/Secondary/Pool/Ancillary) math powers that summon a `GlobalBoost` power; for each unique target boost, if the boost has
`SkipMax` it strips the source GrantPower effect (and marks the build power's effect `Ignored`, Probability/Scale=0). Then clones the boost power's effects into every eligible other main power that
shares at least one effect-identity `(EffectType,MezType,ETModifies,Summon)` and does not already grant it.

**Open items / gaps:**

- ED tables: Enhancement.ApplyED and Enhancement.GetSchedule(eEnhance[,mezType]) are external (Enhancement class, not clsToonX). The exact ED breakpoints/schedules must be extracted from that file in
  a separate section.
- External leaf functions not read here (in Power.cs / Effect.cs): GetEnhancementMagSum, GetEffectMagSum(with its bool-flag overloads), GetEnhancementEffect, AbsorbEffects, AbsorbPetEffects,
  ApplyGrantPowerEffects, ApplyModifyEffects, IgnoreEnhancement, IgnoreBuff, SetMathMag, BuildEffectString, ValidateConditional. Their exact semantics affect numbers and need their own sections.
- Enums.BuffsX field layout (Effect[], Boosts[], BoostsMez[], Damage[], Defense[], Resistance[], Elusivity[], Mez[], MezRes[], StatusProtection[], StatusResistance[], DebuffResistance[], MaxEnd) and
  eStatType index mapping (BuffAcc, ToHit, Absorb, MaxRunSpeed, etc.) are referenced but their index constants live in Enums.cs — needed to reproduce array indexing exactly.
- eDamage / eMez / eEffectType enum ordinal values are used as array indices (e.g. Damage[1..8]); the precise enum ordering must be transcribed from Enums.cs to know which damage types index 1-7
  cover.
- Pass6 line 2151: buffed.AccuracyMult = buffed.Accuracy \* (1 + math.Accuracy + nAcc) is computed AFTER line 2150 already overwrote buffed.Accuracy with the ToHit-multiplied value; this makes
  AccuracyMult = Accuracy*(1+...) using the post-multiply Accuracy. Confirm whether this is intended or a latent bug — behavior is as-written but semantically surprising.
- HandleGrantPowerIncarnate: two versions exist (1291 HandleDefaultIncarnateEnh is used; 1366 HandleGrantPowerIncarnate is commented 'never called' yet 1499 dispatches to a HandleGrantPowerIncarnate).
  Verify which overload binds at 1499.
- GBPA_Pass5_ResyncEffects uses RemoveAt(i) inside a for loop bounded by the original min length, which can skip the element shifted into position i. Reproduce verbatim; do not correct.
- MidsContext.Config.I9.IgnoreEnhFX default value not confirmed (gates whether enhancement proc/aura FX are added in Pass0).

---

## gbd-totals-and-caps

**Port risk:** medium — Formulas and constants fully transcribed with exact float literals. Residual risk: (1) upstream _selfBuffs/_selfEnhance aggregation (GBPA passes, ED, Boosts/BoostsMez
population) is out of scope but feeds every field here; a port must match those exactly. (2) ResCap/HPCap/BaseRegen etc. real values come from the .mhd DB per-AT, not the C# constructor defaults
shown; the port must read them from data. (3) The cap-minus-1 convention (BuffDam/BuffHaste/HPRegen/EndRec) and the fraction-vs-percent unit split (Def/Res fractional, MezRes/DebuffRes pre-scaled
*100) are easy to invert.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/clsToonX.cs` (48-59, 335-338, 839-1002) — GBD_Totals (839-1002): builds Totals from aggregated self-buffs/self-enhance and produces TotalsCapped by applying AT/server
  caps. ApplyPvpDr (48-59) + CalculatePvpDr (335-338).
- `<repos>/MidsReborn/MidsReborn/Core/Statistics.cs` (1-254) — Derived stat accessors (endurance recovery/regen per sec, defense, resist, HP, movement, buff haste/damage) computed from
  Totals/TotalsCapped and Archetype base values.
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Character.cs` (1857-1970) — TotalStatistics class: field list (Def/Res/Mez arrays, HPRegen, HPMax, EndRec, movement, buffs), Init and Assign.
- `<repos>/MidsReborn/MidsReborn/Core/ServerData.cs` (26-110) — Server-wide base/max movement/perception constants, BaseToHit 0.75, MaxSlots 67.
- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Archetype.cs` (23-45, 92-140) — AT cap fields (DamageCap/RechargeCap/RegenCap/RecoveryCap/ResCap/HPCap/PerceptionCap) and base values
  (BaseRegen/BaseRecovery/BaseThreat/Hitpoints).
- `<repos>/MidsReborn/MidsReborn/Core/Enums.cs` (1222-1254) — eStatType enum indices into \_selfBuffs.Effect / \_selfEnhance.Effect arrays.

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| BaseToHit | ServerData.cs:29 | 0.75 (used by accuracy/to-hit display, not by Totals/Statistics) |
| BaseFlySpeed | ServerData.cs:30 | 31.5 ft/s |
| BaseJumpSpeed | ServerData.cs:31 | 21 ft/s |
| BaseJumpHeight | ServerData.cs:32 | 4 ft |
| BasePerception | ServerData.cs:33 / Statistics.cs:19 | 500 ft |
| BaseRunSpeed | ServerData.cs:34 | 21 ft/s |
| MaxFlySpeed | ServerData.cs:35 | 86 (base personal fly cap) |
| MaxJumpSpeed | ServerData.cs:36 | 114.40 |
| MaxJumpHeight | ServerData.cs:37 | 50 * BaseJumpHeight = 200 |
| MaxRunSpeed | ServerData.cs:38 | 135.67 |
| MaxMaxFlySpeed | ServerData.cs:39 | 8.19 * 31.5 = 257.985 (absolute fly clamp) |
| MaxMaxJumpSpeed | ServerData.cs:40 | 7.917 * 21 = 166.257 (absolute jump clamp) |
| MaxMaxRunSpeed | ServerData.cs:41 | 8.398 * 21 = 176.358 (absolute run clamp) |
| MaxSlots | ServerData.cs:42 | 67 |
| BaseMagic | Statistics.cs:18 | 1.666667 (recovery multiplier); float32 form 1.66666662693024 used in regen) |
| MaxDefenseDebuffRes | Statistics.cs:20 | 95 |
| MaxGenericDebuffRes | Statistics.cs:21 | 100 |
| MaxHaste | Statistics.cs:22 | 400 (hard clamp on capped BuffHaste display) |
| Speed unit factors | Statistics.cs:137-188 | ft/s→m/s 0.3048; ft/s→mph 0.6818182; ft/s→km/h 1.09728 (+full pairwise matrix) |
| PvP DR params | clsToonX.cs:57,335-338 | a=1.2, b=1.0; f(v)=v*(1-\|atan(1.2*v)\|*(2/PI)) |
| Archetype.BaseThreat (default) | Archetype.cs:23 | 1.0 (per-AT from DB) |
| Archetype.BaseRegen (default) | Archetype.cs:24 | 1.0 (per-AT from DB) |
| Archetype.BaseRecovery (default) | Archetype.cs:25 | 1.67 (per-AT from DB) |
| Archetype.PerceptionCap (default) | Archetype.cs:32 | 1153 |
| Archetype.RecoveryCap (default) | Archetype.cs:37 | 5 (multiplier; applied as cap-1 to EndRec delta) |
| Archetype.RegenCap (default) | Archetype.cs:38 | 20 (applied as cap-1 to HPRegen delta) |
| Archetype.DamageCap (default) | Archetype.cs:39 | 4 (applied as cap-1 to BuffDam delta) |
| Archetype.RechargeCap (default) | Archetype.cs:40 | 5 (applied as cap-1 to BuffHaste delta) |
| Archetype.ResCap (default) | Archetype.cs:41 | 90 placeholder; real DB value is a FRACTION ~0.75 (0.90 Tank/Brute) compared directly to fractional Res |
| Archetype.HPCap (default) | Archetype.cs:44 | 5000 (only applied when >0) |
| Archetype.Hitpoints (default) | Archetype.cs:45,123 | 5000 (base HP; per-AT from DB) |
| eStatType indices | Enums.cs:1222-1254 | BuffAcc=1, BuffEndRdx=8, FlySpeed=11, Heal=13, HPMax=14, JumpHeight=16, JumpSpeed=17, Perception=23, Range=24, Haste=25, EndRec=26, HPRegen=27, RunSpeed=32, StealthPvE=36, StealthPvP=37, ThreatLevel=39, ToHit=40, MaxRunSpeed=49, MaxJumpSpeed=50, MaxFlySpeed=51, Absorb=66 |
| Damage buff slice / clamp floor | clsToonX.cs:908-910,961-963 | buff-speed delta floored at -0.9; damage buff picked from Damage[1..8) (indices 1-7) |

### Section: GBD_Totals per-attribute formulas and cap application; Statistics derived stats

#### 0. Scope and data flow

`GBD_Totals()` (`clsToonX.cs:839-1002`) is the final aggregation stage. Its **inputs** are already-summed per-character buff/enhancement accumulators, produced by earlier GBPA passes (out of scope
here):

- `_selfBuffs` — a `TotalStatistics`-like accumulator holding the summed *buff* contributions across all active powers. Fields used: `Defense[]`, `Resistance[]`, `Elusivity[]`, `StatusProtection[]`,
  `StatusResistance[]`, `DebuffResistance[]`, `MaxEnd`, `Damage[]`, and a generic `Effect[]` array indexed by `eStatType`.
- `_selfEnhance` — the summed *enhancement* contributions. Fields used: `Effect[]` (indexed by `eStatType`) and `Damage[]`.
- `_buffedPowers[]` — the per-slot buffed power objects (used only to sum toggle end cost and detect Fly).

Its **outputs** are two objects on the Character:

- `Totals` (uncapped `TotalStatistics`)
- `TotalsCapped` (AT/server-capped copy)

`Statistics.cs` then derives human-facing numbers from these two.

**Array unit convention (critical):** `Def[]`, `Res[]`, `Elusivity[]` are stored as **fractions** (0.75 = 75%). `Statistics` multiplies by 100 for display. `MezRes[]` and `DebuffRes[]` are stored
already `* 100` (see §2). Buff scalars (`BuffAcc`, `BuffHaste`, `BuffDam`, `BuffToHit`, `BuffHeal`, `HPRegen`, `EndRec`) are stored as **fractional deltas** (e.g. `BuffHaste = 0.70` means +70%);
Statistics adds 1 and/or ×100 to display.

#### 1. Preamble loop (clsToonX.cs:843-865)

```text
canFly = false
for each power index i in CurrentBuild.Powers:
    if power[i] == null: continue
    # StatInclude is the per-power "include in totals" checkbox; must be true AND buffed power exists
    if not (power[i].StatInclude AND (_buffedPowers[i] != null)): continue
    if _buffedPowers[i] == null: continue
    if _buffedPowers[i].PowerType == Toggle:
        Totals.EndUse += _buffedPowers[i].ToggleCost      # sum of toggle end/sec
    for each effect in _buffedPowers[i].Effects:
        if effect.EffectType == Fly and effect.Mag > 0:
            canFly = true
```

`Totals.EndUse` therefore = sum of ToggleCost over all included toggle powers. (Click/auto powers do not contribute here.)

#### 2. Defense / Resistance / Elusivity / Mez / Debuff (clsToonX.cs:867-891)

**Positional/typed defense fold-in first** (index 0 is the "all/positional base" slot):

```text
if abs(_selfBuffs.Defense[0]) > epsilon:
    for index = 1 .. Defense.Length-1:
        _selfBuffs.Defense[index] += _selfBuffs.Defense[0]
```

i.e. a nonzero base-defense (`[0]`) is added into every typed defense channel before copying. **This mutates `_selfBuffs.Defense` in place** — order matters if GBD_Totals is called twice on the same
accumulator (it is re-init'd upstream each GBPA pass, so normally safe).

Then copy per damage-type index:

```text
for index in 0 .. Def.Length-1:
    Totals.Def[index]       = _selfBuffs.Defense[index]
    Totals.Res[index]       = _selfBuffs.Resistance[index]
    Totals.Elusivity[index] = _selfBuffs.Elusivity[index]
```

Mez/status (indexed by `eMez`):

```text
for index in 0 .. StatusProtection.Length-1:
    Totals.Mez[index]    = _selfBuffs.StatusProtection[index]
    Totals.MezRes[index] = _selfBuffs.StatusResistance[index] * 100      # scaled to percent
```

Debuff resistance (indexed by `eEffectType`):

```text
for index in 0 .. DebuffResistance.Length-1:
    Totals.DebuffRes[index] = _selfBuffs.DebuffResistance[index] * 100   # scaled to percent
```

#### 3. Scalar buff/effect copies (clsToonX.cs:893-905)

`_selfBuffs.Effect[]` and `_selfEnhance.Effect[]` are indexed by the integer value of `eStatType` (see constants). Assignments:

```text
Totals.EndMax     = _selfBuffs.MaxEnd
Totals.BuffAcc    = _selfEnhance.Effect[BuffAcc(1)]  + _selfBuffs.Effect[BuffAcc(1)]
Totals.BuffEndRdx = _selfEnhance.Effect[BuffEndRdx(8)]
Totals.BuffHaste  = _selfEnhance.Effect[Haste(25)]   + _selfBuffs.Effect[Haste(25)]
Totals.BuffToHit  = _selfBuffs.Effect[ToHit(40)]
Totals.Perception = BasePerception(500) * (1 + _selfBuffs.Effect[Perception(23)])
Totals.StealthPvE = _selfBuffs.Effect[StealthPvE(36)]
Totals.StealthPvP = _selfBuffs.Effect[StealthPvP(37)]
Totals.ThreatLevel= _selfBuffs.Effect[ThreatLevel(39)]
Totals.HPRegen    = _selfBuffs.Effect[HPRegen(27)]
Totals.EndRec     = _selfBuffs.Effect[EndRec(26)]
Totals.Absorb     = _selfBuffs.Effect[Absorb(66)]
Totals.BuffRange  = _selfBuffs.Effect[Range(24)]
```

Note asymmetry: `BuffAcc` and `BuffHaste` combine BOTH enhance+buff sources; `BuffEndRdx` uses enhance only; the rest use buff-only.

#### 4. Movement (clsToonX.cs:907-945) — use the ACTIVE code (907-920); block 922-945 is commented-out and must be ignored

Current speeds (floor the buff delta at -0.9 = -90%):

```text
Totals.FlySpd    = (1 + max(_selfBuffs.Effect[FlySpeed(11)],  -0.9)) * BaseFlySpeed(31.5)
Totals.RunSpd    = (1 + max(_selfBuffs.Effect[RunSpeed(32)],  -0.9)) * BaseRunSpeed(21)
Totals.JumpSpd   = (1 + max(_selfBuffs.Effect[JumpSpeed(17)], -0.9)) * BaseJumpSpeed(21)
Totals.JumpHeight= (1 + max(_selfBuffs.Effect[JumpHeight(16)],-0.9)) * BaseJumpHeight(4)
```

Per-character max speed caps (these are the character's *personal* speed ceiling, which buffs to MaxRunSpeed etc. raise):

```text
Totals.MaxFlySpd = MaxFlySpeed(86)     + _selfBuffs.Effect[MaxFlySpeed(51)]  * BaseFlySpeed(31.5)
Totals.MaxRunSpd = MaxRunSpeed(135.67) + _selfBuffs.Effect[MaxRunSpeed(49)]  * BaseRunSpeed(21)
Totals.MaxJumpSpd= MaxJumpSpeed(114.40)+ _selfBuffs.Effect[MaxJumpSpeed(50)] * BaseJumpSpeed(21)
# No MaxJumpHeight equivalent
```

Server absolute ("MaxMax") clamp on current speed:

```text
Totals.FlySpd  = min(Totals.FlySpd,  MaxMaxFlySpeed  = 8.19  * 31.5 = 257.985)
Totals.RunSpd  = min(Totals.RunSpd,  MaxMaxRunSpeed  = 8.398 * 21   = 176.358)
Totals.JumpSpd = min(Totals.JumpSpd, MaxMaxJumpSpeed = 7.917 * 21   = 166.257)
```

(No MaxMax clamp on JumpHeight here.)

#### 5. HP, Fly gating, Damage buff, Heal (clsToonX.cs:947-978)

```text
Totals.HPMax = _selfBuffs.Effect[HPMax(14)] + (Archetype?.Hitpoints ?? 0)   # absolute HP: base AT HP + flat HP buffs

if not canFly:
    Totals.FlySpd = 0     # zero out fly speed if no Fly-granting power is active
```

**Damage buff** — `_selfBuffs.Damage[]` is per-damage-type (index 0 unused; 1..7 are the seven damage types). If all buff-damage entries are ~0, fold in enhancement damage:

```text
if all(abs(e) < epsilon for e in _selfBuffs.Damage):
    for i in 0 .. Damage.Length-1:
        _selfBuffs.Damage[i] += _selfEnhance.Damage[i]
```

Then pick a single representative buff from the slice `Damage[1..8)` (indices 1..7 inclusive):

```text
minDmg = min(Damage[1..7]);  maxDmg = max(Damage[1..7]);  avgDmg = average(Damage[1..7])
if (maxDmg - avgDmg) < (avgDmg - minDmg):        Totals.BuffDam = maxDmg
elif (maxDmg - avgDmg) > (avgDmg - minDmg) and minDmg > 0:  Totals.BuffDam = minDmg
else:                                             Totals.BuffDam = maxDmg
```

(Effectively: if the distribution is skewed toward the low end and the min is positive, report the min; otherwise report the max. Ties → max.)

```text
Totals.BuffHeal = _selfBuffs.Effect[Heal(13)]
```

#### 6. PvP diminishing returns on defense (clsToonX.cs:980, 48-59, 335-338)

`ApplyPvpDr()` runs on `Totals.Def[]` **before** cloning to TotalsCapped. It is a **no-op unless PvP mode is active** (`MidsContext.Config.Inc.DisablePvE == true`). When active:

```text
for index in 0 .. Def.Length-1:
    Totals.Def[index] = CalculatePvpDr(Totals.Def[index], a=1.2, b=1.0)

CalculatePvpDr(val, a, b) = val * (1.0 - abs(atan(a*val)) * (2.0/PI) * b)
```

For PvE builds (the normal case) skip this entirely.

#### 7. Producing TotalsCapped (clsToonX.cs:981-1001)

`TotalsCapped = Assign(Totals)` deep-clones all arrays (Def/Res/Mez/MezRes/DebuffRes/Boosts/BoostsMez are `.Clone()`d; `Elusivity` is a reference copy, scalars copied by value —
`Character.cs:1937-1969`). Then apply caps. **Caps subtract 1 from the AT cap for the "+1 baseline" buff stats**, because these buff deltas are stored as (multiplier − 1):

```text
TotalsCapped.BuffDam   = min(TotalsCapped.BuffDam,   Archetype.DamageCap   - 1)
TotalsCapped.BuffHaste = min(TotalsCapped.BuffHaste, Archetype.RechargeCap - 1)
TotalsCapped.HPRegen   = min(TotalsCapped.HPRegen,   Archetype.RegenCap    - 1)
TotalsCapped.EndRec    = min(TotalsCapped.EndRec,    Archetype.RecoveryCap - 1)

for index in 0 .. Res.Length-1:
    TotalsCapped.Res[index] = min(TotalsCapped.Res[index], Archetype.ResCap)   # ResCap is a FRACTION (e.g. 0.75 / 0.90)

if Archetype.HPCap > 0:
    TotalsCapped.HPMax  = min(TotalsCapped.HPMax, Archetype.HPCap)
    TotalsCapped.Absorb = min(TotalsCapped.Absorb, TotalsCapped.HPMax)   # absorb can't exceed capped max HP

TotalsCapped.RunSpd     = min(TotalsCapped.RunSpd,   Totals.MaxRunSpd)    # note: capped against UNcapped Totals.MaxRunSpd
TotalsCapped.JumpSpd    = min(TotalsCapped.JumpSpd,  Totals.MaxJumpSpd)
TotalsCapped.FlySpd     = min(TotalsCapped.FlySpd,   Totals.MaxFlySpd)
TotalsCapped.JumpHeight = min(TotalsCapped.JumpHeight, ServerData.MaxJumpHeight = 200)   # 50 * BaseJumpHeight(4)
TotalsCapped.Perception = min(TotalsCapped.Perception, Archetype.PerceptionCap)
```

**Ordering gotchas a naive port gets wrong:**

1. Cap stats are `ATcap - 1` for BuffDam/BuffHaste/HPRegen/EndRec (not the raw cap).
2. Def is NOT capped in TotalsCapped (only Res is). Defense soft-cap is a display concept, not enforced here.
3. Speed caps use `Totals.MaxRunSpd` (uncapped object), not `TotalsCapped.MaxRunSpd`.
4. ResCap semantics: compared directly against fractional Res, so the DB stores it as a fraction. The `Archetype` constructor default `ResCap = 90f` is a placeholder overwritten by DB load; real
    per-AT values are ~0.75 (0.90 Tanker/Brute).
5. Absorb clamp only happens inside the `HPCap > 0` branch.

#### 8. Statistics.cs derived stats (Core/Statistics.cs)

All accessors read `_character.Totals` (uncapped) or `_character.TotalsCapped` (capped) plus `_character.Archetype`. Constants at top: `BaseMagic = 1.666667`, `MaxDefenseDebuffRes = 95`,
`MaxGenericDebuffRes = 100`, `MaxHaste = 400`, `BasePerception = 500` (from ServerData).

**Endurance (31-47, 80-88):**

```text
EnduranceMaxEnd = Totals.EndMax + 100                                  # total blue bar
EnduranceRecovery(uncapped) = (uncapped? Totals.EndRec : TotalsCapped.EndRec) + 1
EnduranceRecoveryPercentage(u) = EnduranceRecovery(u) * 100
EnduranceRecoveryNumeric = EnduranceRecovery(false)
    * (Archetype.BaseRecovery * BaseMagic)
    * (TotalsCapped.EndMax/100 + 1)                                    # end/sec, capped
EnduranceRecoveryNumericUncapped = EnduranceRecovery(true)
    * (Archetype.BaseRecovery * BaseMagic)
    * (Totals.EndMax/100 + 1)
EnduranceUsage           = Totals.EndUse                               # end/sec from toggles
EnduranceRecoveryNet     = EnduranceRecoveryNumeric - EnduranceUsage
EnduranceTimeToFull      = EnduranceMaxEnd / EnduranceRecoveryNumeric
EnduranceTimeToFullNet   = EnduranceMaxEnd / (EnduranceRecoveryNumeric - EnduranceUsage)
EnduranceTimeToZero      = EnduranceMaxEnd / -(EnduranceRecoveryNumeric - EnduranceUsage)
```

Note `Archetype.BaseRecovery` default 1.67; `BaseRecovery * BaseMagic` ≈ 1.67 × 1.666667 ≈ 2.783 end/sec at 100% recovery, 100 max end.

**Health / regen (49-55, 90-103):**

```text
HealthRegen(uncapped) = (uncapped? Totals.HPRegen : TotalsCapped.HPRegen) + 1
HealthRegenPercent(u) = HealthRegen(u) * 100
HealthRegenHealthPerSec = HealthRegen(false) * Archetype.BaseRegen * 1.66666662693024      # HP/sec absolute regen rate factor
HealthRegenHPPerSec     = HealthRegen(false) * Archetype.BaseRegen * 1.66666662693024
                          * HealthHitpointsNumeric(false)/100                                # HP/sec at current max HP
HealthRegenTimeToFull   = HealthHitpointsNumeric(false) / HealthRegenHPPerSec
HealthHitpointsNumeric(uncapped) = uncapped? Totals.HPMax : TotalsCapped.HPMax
HealthHitpointsPercentage = TotalsCapped.HPMax / Archetype.Hitpoints * 100
```

(The literal `1.66666662693024` is the float32 expansion of BaseMagic; use it verbatim for exact matching.)

**Defense / Resistance (114-135):**

```text
Defense(dType)          = Totals.Def[dType] * 100      # NOTE: uses uncapped Totals (Def is never capped)
DefenseMin/Max/Avg      = Totals.Def.{Min,Max,Average} * 100
DamageResistance(dType, uncapped) = (uncapped? Totals.Res[dType] : TotalsCapped.Res[dType]) * 100
DamageResistanceMin/Max/Avg = Totals.Res.{Min,Max,Average} * 100     # min/max/avg use uncapped Totals
```

**Perception / misc buffs (57-65, 108-126):**

```text
Perception(uncapped) = uncapped? Totals.Perception : TotalsCapped.Perception
BuffToHit    = Totals.BuffToHit * 100
BuffAccuracy = Totals.BuffAcc  * 100
BuffEndRdx   = Totals.BuffEndRdx * 100
BuffHeal     = Totals.BuffHeal * 100
ThreatLevel  = (Totals.ThreatLevel + Archetype.BaseThreat) * 100
Absorb       = Totals.Absorb                     # flat value, no scaling
Range        = Totals.BuffRange ; RangePercent = Totals.BuffRange * 100
```

**Haste / Damage buff with caps (240-252):**

```text
BuffHaste(uncapped):
    capped   -> min(MaxHaste=400, (TotalsCapped.BuffHaste + 1) * 100)
    uncapped -> (Totals.BuffHaste + 1) * 100
BuffDamage(uncapped):
    capped   -> min(Archetype.DamageCap * 100 (or +inf if AT null), (TotalsCapped.BuffDam + 1) * 100)
    uncapped -> (Totals.BuffDam + 1) * 100
```

Note the `+1` restores the multiplier form; and `MaxHaste=400` is an additional hard clamp beyond `RechargeCap-1`.

**Movement display (202-238):** `MovementRunSpeed/FlySpeed/JumpSpeed` return `Totals.RunSpd/FlySpd/JumpSpd` (uncapped) unless `!uncapped` and the speed exceeds the corresponding
`Totals.MaxRunSpd/MaxFlySpd/MaxJumpSpd`, in which case they return the Max value; then unit-convert via `Speed()`. `MovementJumpHeight` uses `TotalsCapped.JumpHeight`, converting `* 0.3048` for metric
units (feet→meters).

**Unit conversion factors** (`Speed()` 137-147): FeetPerSec = raw; MetersPerSec = ×0.3048; MilesPerHour = ×0.6818182; KilometersPerHour = ×1.09728. (Full pairwise matrix at 149-188.)

#### 9. RESOLVED: BaseToHit

`ServerData.BaseToHit = 0.75f` (`ServerData.cs:29`). It is NOT consumed by GBD_Totals or Statistics derived stats. It is used (a) for accuracy→to-hit% display in `Effect.cs:1535`
(`AtrModAccuracy * BaseToHit * 100`), and (b) as the default for `ConfigData.ScalingToHit`. Loaded from DB at `DatabaseAPI.cs:1219`. Port it as a constant 0.75 available to the to-hit/accuracy
subsystem, not this one.

#### 10. RESOLVED: exemplar -3 / legality

No exemplar level-shift, `-3` set-bonus cutoff, or build-legality accept/reject logic exists in GBD_Totals, Statistics, ServerData, or the TotalStatistics class. Those live in other subsystems (build
validation / effect eligibility). This section performs no legality checks and no exemplar handling.

**Open items / gaps:**

- \_selfBuffs and \_selfEnhance are inputs from earlier GBPA passes (not read here): exact summation of Effect[], Damage[], Defense[], Resistance[], StatusProtection/Resistance, DebuffResistance,
  MaxEnd, and Boosts/BoostsMez population is out of this section's scope.
- Real per-AT cap and base values (ResCap, HPCap, Hitpoints, BaseRegen, BaseRecovery, BaseThreat, PerceptionCap) are loaded from the binary DB (Archetype.cs:92-114 reader); only C# constructor
  placeholder defaults were transcribed. Confirm actual per-AT numbers from data/canonical archetypes.
- ResCap unit: inferred to be a fraction (compared directly to fractional Res). The constructor default 90f contradicts this and appears to be a dead placeholder; not 100% confirmed against a live DB
  value.
- Boosts[]/BoostsMez[] arrays (Totals.Boosts, Statistics.Boosts/BoostsMez 67-78) are exposed but their population happens upstream (not in GBD_Totals) — the dictionary filter (abs(value)>epsilon) is
  the only logic here.
- eDamage/eMez enum lengths (array sizes for Def/Res/Mez) not transcribed; needed to size the arrays — read Enums.eDamage and Enums.eMez.
- Whether MidsContext.Config.Inc.DisablePvE is the sole PvP switch (ApplyPvpDr guard) — assumed yes from the single early-return.

---

## set-bonuses-ruleof5

**Port risk:** medium — Core accumulation, tier gating, Rule-of-Five, and the exemplar gate are fully transcribed with exact predicates and the -3 question is resolved from source. Residual risk: (1)
the Rule-of-Five counter's index space assumes Bonus.Index[] values fit within the set_bonus powerset id range (guarded by a throw at Build.cs:1198); a port must replicate whatever id mapping the DB
uses. (2) CalculateAndApplyEffects (the actual number-folding) is out of this section's scope and must be specified elsewhere. (3) FindMatchingEffectIndex/IsEffectMatch folding in
GetCumulativeSetBonuses is display-only and not on the apply path, but if a port mistakes it for the apply path it will produce wrong totals.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/Core/Build.cs` (1151-1226,1236-1313) — Set-bonus accumulation entry points: GenerateSetBonusData (exemplar gate + PvMode selection), GetSetBonusVirtualPower (Rule of
  Five + MyPet skip), GetCumulativeSetBonuses (effect folding for display).
- `<repos>/MidsReborn/MidsReborn/Core/I9SetData.cs` (31-128) — Per-power set accumulator: Add (SetO filter, per-set slotted-count tally), BuildEffects (tier activation by Slotted<=count and PvMode
  match; special/unique bonus activation at count>=1).
- `<repos>/MidsReborn/MidsReborn/Core/EnhancementSet.cs` (12-107,363-371) — EnhancementSet + BonusItem struct definitions: Bonus[] (tiered set bonuses), SpecialBonus[] (per-enhancement
  globals/uniques), fields Slotted, PvMode, Index[].
- `<repos>/MidsReborn/MidsReborn/Core/Enums.cs` (935-940) — ePvX enum: Any=0, PvE=1, PvP=2.
- `<repos>/MidsReborn/MidsReborn/Core/ConfigData.cs` (121,582) — ForceLevel default 50 (exemplar level control); Inc.DisablePvE bool (PvE/PvP toggle).
- `<repos>/MidsReborn/MidsReborn/clsToonX.cs` (2177-2178,2194-2196) — Consumption: GenerateBuffedPowerArray calls GenerateSetBonusData, then CalculateAndApplyEffects on the SetBonusVirtualPower folds
  the bonus effects into character totals.
- `<repos>/MidsReborn/MidsReborn/Core/DatabaseAPI.cs` (242-261) — NidPowers("set_bonus") returns global Power-array indexes of the set_bonus virtual powerset; sizes the Rule-of-Five counter array.

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| ForceLevel (exemplar/display level) | ConfigData.cs:121 | default 50; the single exemplar gate value compared to each power's pick Level in Build.cs:1160 |
| Rule of Five threshold | Build.cs:1213 | if (setCount[power] < 6) — folds instances 1..5, drops 6th+; counter incremented at Build.cs:1203 before the test |
| Tier bonus minimum pieces | I9SetData.cs:77 | SlottedCount > 1 (>=2 pieces) required for any Bonus[] tier |
| Special/unique bonus minimum pieces | I9SetData.cs:100 | SlottedCount > 0 (>=1 piece) required for SpecialBonus[] global uniques |
| Tier activation predicate | I9SetData.cs:82-88 | Bonus.Slotted <= SlottedCount AND (Bonus.PvMode == pvMode OR Bonus.PvMode == ePvX.Any) |
| PvMode selection | Build.cs:1168 | (!Config.Inc.DisablePvE) ? ePvX.PvE : ePvX.PvP |
| ePvX enum | Enums.cs:935-940 | Any=0, PvE=1, PvP=2 |
| Set-IO type filter | I9SetData.cs:33 | Enhancements[enh].TypeID must == eType.SetO; else the piece contributes no set data |
| MyPet skip predicate (ShouldSkipEffects) | Build.cs:1229-1234 | power.Target.HasFlag(MyPet) AND power.EntitiesAffected.HasFlag(MyPet) -> skip effects (still counts toward Rule of Five) |
| IgnoreSetBonusFX toggle | Build.cs:1181-1184 | Config.I9.IgnoreSetBonusFX -> return empty virtual power (all set FX off) |
| set_bonus virtual powerset lookup | Build.cs:1186 / DatabaseAPI.cs:242-261 | DatabaseAPI.NidPowers("set_bonus") returns global Power-array ids; sizes the Rule-of-Five counter array; Bonus.Index[] values are ids into this space |
| BonusItem struct fields | EnhancementSet.cs:363-371 | Special:int, Name:string[], Index:int[], AltString:string, PvMode:ePvX, Slotted:int |
| EnhancementSet bonus arrays | EnhancementSet.cs:14,25 | Bonus = BonusItem[5] (tiers), SpecialBonus = BonusItem[6] (per-enhancement globals); Enhancements:int[] pairs 1:1 with SpecialBonus by index |

### Set-bonus accumulation, tier activation, Rule of Five, exemplar gating

This subsystem converts a build's slotted set-IO enhancements into a single synthetic "set bonus virtual power" whose effects are then folded into the character's totals exactly like any other buff
power. The pipeline has four stages, executed in this order every time the buffed-power array is regenerated (`clsToonX.GenerateBuffedPowerArray`, Build.cs entry at clsToonX.cs:2196):

1. `Build.GenerateSetBonusData()` — build one `I9SetData` accumulator per power (Build.cs:1151-1176).
2. `I9SetData.BuildEffects(pvMode)` — resolve which tier bonuses + specials activate (I9SetData.cs:73-128), called inside stage 1.
3. `Build.GetSetBonusVirtualPower()` — apply Rule of Five, MyPet skip, and collect the surviving bonus effects into one synthetic `Power` (Build.cs:1178-1226).
4. `CalculateAndApplyEffects(setBonusPower)` — fold into totals (clsToonX.cs:2177-2178). (Effect application math is out of scope for this section; here the bonuses simply become effects on a normal
    power.)

`Build.GetCumulativeSetBonuses()` (Build.cs:1236-1262) is a *separate* read-only aggregation used only for the totals/set-viewer UI (it sums like effects for display); it is NOT part of the
number-affecting apply path and can be reimplemented independently or skipped.

#### Stage 1 — GenerateSetBonusData (exemplar gate + PvMode selection)

Pseudocode (Build.cs:1151-1176):

```text
SetBonuses = []                      # list<I9SetData>, one per power slot index
for index1 in 0 .. Powers.Count-1:
    sd = I9SetData(PowerIndex = index1)
    power = Powers[index1]
    if power != null AND power.Level <= Config.ForceLevel:     # <-- THE EXEMPLAR GATE
        for index2 in 0 .. power.SlotCount-1:
            sd.Add(power.Slots[index2].Enhancement)             # ref I9Slot
    pvMode = (Config.Inc.DisablePvE == false) ? PvE : PvP
    sd.BuildEffects(pvMode)
    if not sd.Empty:                                            # Empty == (SetInfo.Length < 1)
        SetBonuses.append(sd)
SetBonusVirtualPower cache invalidated (_setBonusVirtualPower = null)
```

Key points a naive port gets wrong:

- **`power.Level` is the character level at which the POWER was picked**, not the enhancement/set level and not the slot placement level. `Config.ForceLevel` is the exemplar/display level (default 50,
  ConfigData.cs:121; lowered when the user exemplars).
- **There is NO per-slot level check.** If the power's pick-level passes the gate, ALL of its slots are added regardless of the level at which each slot was placed. A set fully slotted in a level-2
  power contributes its bonuses even when the individual slots were placed at level 50 and the character is exemplared to level 5 (power Level 2 <= ForceLevel 5).
- When a power's pick Level > ForceLevel the entire inner slot loop is skipped, so that power contributes zero set data (an empty `I9SetData`, dropped by the `not Empty` guard).

#### Stage 1b — I9SetData.Add: which enhancements count (I9SetData.cs:31-52)

```text
Add(enh I9Slot):
    if enh.Enh < 0: return
    if Database.Enhancements[enh.Enh].TypeID != eType.SetO: return   # ONLY set IOs count
    nIdSet = Database.Enhancements[enh.Enh].nIDSet
    idx = Lookup(nIdSet)          # find existing sSetInfo with same SetIDX, else -1
    if idx >= 0:
        SetInfo[idx].SlottedCount += 1
        append enh.Enh to SetInfo[idx].EnhIndexes
    else:
        append new sSetInfo{ SetIDX=nIdSet, SlottedCount=1, Powers=[], EnhIndexes=[enh.Enh] }
```

So per power, each distinct enhancement set gets one `sSetInfo` with a running `SlottedCount` and the list of the specific enhancement DB indexes slotted from that set. Non-set IOs (generic IOs,
HamiOs, SOs/DOs/TOs) never contribute — `TypeID != SetO` bails. Duplicate-piece prevention is enforced elsewhere at slot time; this counter naively increments per matching piece.

#### Stage 2 — BuildEffects: tier activation + PvMode + specials (I9SetData.cs:73-128)

For each `sSetInfo` (a set slotted in one power):

Tier (count-threshold) bonuses — only if `SlottedCount > 1`:

```text
set = Database.EnhancementSets[SetIDX]
for b in set.Bonus:                       # BonusItem[]
    activate = (b.Slotted <= SlottedCount)
                AND (b.PvMode == pvMode OR b.PvMode == ePvX.Any)
    if not activate: continue
    for idx in b.Index:                    # each Index is a global set_bonus Power id
        append idx to SetInfo.Powers
```

Special / per-enhancement bonuses (global uniques like Luck of the Gambler +Recharge, Steadfast, Gladiator's Armor, Shield Wall, Reactive Defenses procs) — gated on `SlottedCount > 0` (i.e. >= 1, a
single slotted piece):

```text
if SlottedCount > 0:
    for index2 in 0 .. set.Enhancements.Length-1:
        # set.SpecialBonus[index2] pairs with set.Enhancements[index2]
        if set.SpecialBonus[index2].Index.Length <= -1: continue   # (never true; length>=0)
        for each slotted enh e in SetInfo.EnhIndexes:
            if e == set.Enhancements[index2]:
                for idx in set.SpecialBonus[index2].Index:
                    append idx to SetInfo.Powers
```

Notes / gotchas:

- **Tier bonuses require at least 2 pieces** (`SlottedCount > 1`); `b.Slotted` is the 1-based piece threshold (2,3,4,5,6). The 2-piece bonus has `Slotted == 2`. Activation is cumulative: every bonus
  whose threshold `<= SlottedCount` fires, so a 6-piece set fires all of its 2..6 tier bonuses.
- **Global uniques activate at 1 piece** via the SpecialBonus branch, matched by the *specific enhancement DB index* being present. This is the fold path for LotG/Steadfast/Gladiator/Shield
  Wall/Reactive. They are not `Bonus[]` tiers.
- **PvMode filter applies only to tier `Bonus[]`**, not to SpecialBonus. `pvMode` = PvE unless `Config.Inc.DisablePvE` is set (then PvP). `ePvX.Any` bonuses always match. Enum values: Any=0, PvE=1,
  PvP=2 (Enums.cs:935-940).
- The C# uses bitwise `&`/`|` on bools but semantics equal logical AND/OR here.
- `SetInfo.Powers` ends up as a flat list of global `set_bonus` Power ids, with duplicates preserved (the same bonus id can appear once per activating source). Duplicates matter for Rule of Five
  below.

#### Stage 3 — GetSetBonusVirtualPower: Rule of Five + MyPet skip (Build.cs:1178-1226)

```text
if Config.I9.IgnoreSetBonusFX: return empty Power     # user toggle disables all set FX
nidPowers = DatabaseAPI.NidPowers("set_bonus")        # global Power ids of set_bonus powerset
setCount = int[nidPowers.Length]  (all zero)
effectList = []
for sd in SetBonuses:
    for si in sd.SetInfo:
        for power in si.Powers where power > -1:       # global set_bonus Power id
            if power >= setCount.Length: throw
            setCount[power] += 1                        # count BEFORE the test
            pInfo = Database.Power[power]
            if pInfo != null AND ShouldSkipEffects(pInfo): continue   # MyPet-only bonus skip
            if setCount[power] < 6:                     # <-- RULE OF FIVE (max 5 instances)
                effectList.addRange(pInfo.Effects.map(clone))
virtualPower.Effects = effectList
return virtualPower
```

Rule of Five, precisely:

- The counter is keyed on the **global `set_bonus` virtual-power id** — i.e. the *identity of the exact bonus value*. Two different sets that grant the identical bonus (e.g. two sources of "+5%
  Recharge") reference the *same* set_bonus Power id and therefore share one counter; distinct bonus magnitudes/types map to distinct ids and count independently. This is how real CoH Rule-of-Five (5
  identical bonuses max) is modeled.
- `setCount[power] += 1` happens *before* the `< 6` check, so instances 1..5 (counts 1,2,3,4,5) fold their effects; the 6th and beyond (`setCount >= 6`) are counted but their effects are dropped. Net:
  **at most 5 instances** of any given bonus value contribute.
- `ShouldSkipEffects` (Build.cs:1229-1234) returns true when the bonus power's `Target` has flag `MyPet` AND `EntitiesAffected` has flag `MyPet` — pet-only set bonuses are excluded from the
  character's own totals. Note the counter is still incremented before the skip, so a MyPet bonus consumes a Rule-of-Five slot for that id.
- `Config.I9.IgnoreSetBonusFX` short-circuits the whole thing to an empty power (debug/what-if toggle).

#### Stage 4 — Application

`clsToonX.cs:2177-2178`: `setBonusPower = CurrentBuild.SetBonusVirtualPower; CalculateAndApplyEffects(setBonusPower, ...)`. The virtual power carries the surviving cloned effects and is run through
the normal effect-application path, folding set bonuses into character totals. The `SetBonusVirtualPower` property (Build.cs:49) lazily calls `GetSetBonusVirtualPower()` and caches until
`GenerateSetBonusData` clears it.

### RESOLVED: exemplar -3 question

**Definitive: MidsReborn does NOT implement the real-CoH "(set min level − 3)" set-bonus retention rule. There is no −3 anywhere in the set-bonus path.**

The one and only gate that removes set bonuses under exemplar is Build.cs:1160:

```csharp
if (Powers[index1] != null && Powers[index1].Level <= MidsContext.Config.ForceLevel)
```

This drops a power's slot contributions entirely when the **power's pick level** exceeds `ForceLevel` (the exemplar level). It does not consult the enhancement set's `LevelMin`/`LevelMax`, and it
applies no −3 offset. A repo-wide search for `- 3` / `LevelMin` in the set-bonus code found `LevelMin` used only for IO level-range display and IO relative-level clamping (Enhancement.cs,
EnhancementSet.cs, Character.cs UI strings, Build.cs:314-332 IO level fitting) — never for gating bonus activation.

Consequence for a faithful port: a set slotted in a power picked at level ≤ exemplar level retains **all** its set bonuses at any exemplar depth (Mids does not fade them by set min level). Do not add
a −3 rule; doing so would make the port diverge from MidsReborn numbers. (This is a known simplification versus live CoH, which does apply set-min-−3.)

### Ordering pitfalls for reimplementation

- Compute PvMode once from `DisablePvE` and pass it into tier activation; do not re-derive per bonus.
- Increment the Rule-of-Five counter *before* the `< 6` test and *before* the MyPet skip (both consume a slot).
- Preserve duplicate bonus ids in `SetInfo.Powers`; each duplicate independently advances the Rule-of-Five counter.
- Tier bonuses need ≥2 pieces; specials/uniques need ≥1. Do not merge these thresholds.
- Gate on power *pick* level vs ForceLevel; never on slot placement level or set min level.

**Open items / gaps:**

- CalculateAndApplyEffects (clsToonX.cs, called at 2178) — not read here; the actual math that turns the virtual power's cloned effects into stat totals is a separate section.
- Exact index space of Bonus.Index[] / SpecialBonus.Index[]: confirmed they are global set_bonus Power ids consumed by Database.Power[power] and counted in setCount sized by
  NidPowers("set_bonus").Length, with a bounds throw, but the DB build-time guarantee that these ids fall within that range was not independently verified.
- GetCumulativeSetBonuses effect-matching helpers (IsEffectMatch/IsSpecialMatch/IsFallbackMatch, Build.cs:1265-1313) are transcribed by location only; they drive UI aggregation, not the apply path, so
  were not deep-specified.
- Power.Effects cloning semantics (t.Clone()) assumed to be deep copies of effect records; not independently verified against Effect.Clone().
- How ForceLevel is set when the user changes the exemplar/level slider (writer side) not traced; only its read-side use as the gate is specified.

---

## legality-predicates

**Port risk:** medium — Core predicates are fully transcribed and both flagged cases are resolved with code+data evidence. Residual risk is the EnhancementClass name->ID mapping (in the binary .mhd,
not read) and PEnhancementsList construction, which are needed to reproduce the class check and mutex results byte-for-byte. The subtle accept/reject trap (EnhancementTest does not class-check
standard enhancements; legality for standard enh comes from IsEnhancementValid/picker population + load-time strip) is documented so a naive single-gate port won't silently accept illegal standard
IOs.

**Source files:**

- `<repos>/MidsReborn/MidsReborn/Core/Base/Data_Classes/Power.cs` (2104-2127, 2348-2354, 492, 506) — Standard-enh validity (GetValidEnhancements non-SetO branch), SetO validity
  (GetValidEnhancementsFromSets), IsEnhancementValid, and Power.Enhancements/Power.SetTypes field decls
- `<repos>/MidsReborn/MidsReborn/Core/Build.cs` (1026-1149, 608-654, 211-241) — EnhancementTest (SetO set-type gate + unique/superior/mutex/dup build-wide rules), CheckAndFixAllEnhancements (load-time
  strip via IsEnhancementValid), ValidateEnhancements
- `<repos>/MidsReborn/MidsReborn/UI/Forms/MainWindow2.cs` (2737-2799, 2762, 2546-2618) — Actual placement path: I9Picker_EnhancementPicked gates placement through EnhancementTest;
  GetRepeatEnhancement/GetFirstValidSetEnh repeat-placement paths
- `<repos>/MidsReborn/MidsReborn/UI/Controls/I9Picker.cs` (1645-1719) — Picker population: GetValidSetTypes=Power.SetTypes (which set tabs shown), GetValidEnhancements(type)=Power.GetValidEnhancements
  (which standard enh shown) — the presentation-layer filter
- `<repos>/MidsReborn/MidsReborn/Core/Enums.cs` (1159-1166, 952-1004, 653-663) — eType (None/Normal/InventO/SpecialO/SetO), eSetType (Knockback=23 etc.), eEnhMutex (None/Stealth/ArchetypeA..F)
- `<repos>/homecoming-build-companion/data/canonical/powers/scrapper_melee/dark_melee/soul_drain.json` (boosts_allowed, allowed_boostset_cats) — Empirical data for the Soul Drain flagged case
- `<repos>/homecoming-build-companion/data/canonical/powers/scrapper_defense/shield_defense/one_with_the_shield.json` (boosts_allowed, allowed_boostset_cats) — Empirical data for the One with the
  Shield flagged case

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| eType enum | Core/Enums.cs:1159-1166 | None=0, Normal=1, InventO=2, SpecialO=3, SetO=4 |
| eSetType enum (Knockback=23; ordinal list Untyped=0..TeleportNoSprint) | Core/Enums.cs:952-1004 | Untyped,MeleeST,RangedST,RangedAoE,MeleeAoE,Snipe,Pets,Defense,Resistance,Heal,Hold,Stun,Immob,Slow,Sleep,Fear,Confuse,Flight,Jump,Run,Teleport,DefDebuff,EndMod,Knockback(23),Threat,ToHit,ToHitDeb,PetRech,Travel,AccHeal,AccDefDeb,AccToHitDeb,Arachnos,Blaster,Brute,Controller,Corruptor,Defender,Dominator,Kheldian,Mastermind,Scrapper,Stalker,Tanker,UniversalDamage,Sentinel,RunNoSprint,JumpNoSprint,FlightNoSprint,TeleportNoSprint |
| eEnhMutex enum | Core/Enums.cs:653-663 | None=0, Stealth=1, ArchetypeA=2, ArchetypeB=3, ArchetypeC=4, ArchetypeD=5, ArchetypeE=6, ArchetypeF=7 |
| Power.Enhancements | Core/Base/Data_Classes/Power.cs:492 | int[] of allowed EnhancementClass.ID values (canonical field boosts_allowed) |
| Power.SetTypes | Core/Base/Data_Classes/Power.cs:506 | List\<int> of allowed eSetType category values (canonical field allowed_boostset_cats) |
| Mutex name-normalization regex | Core/Build.cs:1077, 1092, 1095 | Regex.Replace(enh.UID, "(Attuned_\|Superior_)", "") ; collision patterns "Superior_Attuned_{stem}" and "Superior_Attuned_Superior_{stem}" |
| Soul Drain (Scrapper Dark Melee) allowed set categories | data/canonical/powers/scrapper_melee/dark_melee/soul_drain.json | allowed_boostset_cats=[Melee AoE Damage, Scrapper Archetype Sets, To Hit Buff, Universal Damage Sets]; boosts_allowed=[Reduce Endurance Cost, Enhance Recharge Speed, Enhance Damage, Enhance ToHit Buffs, Enhance Accuracy] |
| One with the Shield (Shield Defense) allowed enhancements | data/canonical/powers/{scrapper,tanker}_defense/shield_defense/one_with_the_shield.json | boosts_allowed=[Enhance Damage Resistance, Enhance Endurance Modification, Reduce Endurance Cost, Enhance Heal]; allowed_boostset_cats=[Endurance Modification, Healing, Resist Damage] |
| Force Feedback set category | data/canonical/boost_sets/force_feedback.json | group_name=Knockback -> SetType=eSetType.Knockback |
| IsEnhancementValid bounds check | Core/Base/Data_Classes/Power.cs:2121-2124 | iEnh<0 or iEnh>len(Enhancements)-1 => false |

### Enhancement placement legality — port spec

Two independent data structures on each power drive legality; they are keyed by the enhancement's `TypeID`:

- `Power.Enhancements` — `int[]` of allowed **enhancement-class IDs** (source: canonical `boosts_allowed`). Gates *standard* enhancements: `Normal` (SO/DO/TO), `InventO` (common/generic IOs),
  `SpecialO` (HamiO/special). (Power.cs:492)
- `Power.SetTypes` — `List<int>` of allowed **IO set categories** (`eSetType` values; source: canonical `allowed_boostset_cats`). Gates *set* enhancements: `SetO`. (Power.cs:506)

`eType` enum: `None=0, Normal=1, InventO=2, SpecialO=3, SetO=4` (Enums.cs:1159).

#### Predicate 1 — standard-enhancement validity (`IsEnhancementValid` / `GetValidEnhancements` non-SetO branch)

`Power.GetValidEnhancements(iType, iSubType=0)` (Power.cs:2104):

```text
if iType == SetO:  return GetValidEnhancementsFromSets()   # see Predicate 2
else:
    return [ index for (enh, index) in enumerate(Database.Enhancements)
              if enh.TypeID == iType
              and any( Database.EnhancementClasses[classId].ID in Power.Enhancements
                      for classId in enh.ClassID )
              and (enh.SubTypeID == 0 or iSubType == 0 or enh.SubTypeID == iSubType) ]
```

Key subtleties a naive port gets wrong:

- `enh.ClassID` is an **array** of class-index values; each indexes `Database.EnhancementClasses`, and it is that class's `.ID` field (not the array index) that must be a member of
  `Power.Enhancements`. Enhancement is valid if **any** of its classes is allowed.
- SubType filter: passes when the enhancement has no subtype (`SubTypeID==0`), OR the caller passed no subtype (`iSubType==0`), OR they match exactly. Used only for `SpecialO` (e.g. HamiO exposure
  subtypes).

`Power.IsEnhancementValid(iEnh)` (Power.cs:2119): bounds-check `0 <= iEnh <= len(Enhancements)-1` (else false), then `iEnh in GetValidEnhancements(Enhancements[iEnh].TypeID)`.

#### Predicate 2 — set-IO validity (`GetValidEnhancementsFromSets`)

(Power.cs:2348):

```text
return [ e for set in Database.EnhancementSets
            if any(set.SetType == f for f in Power.SetTypes)
          for e in set.Enhancements ]
```

I.e. a `SetO` enhancement is type-valid for a power iff its parent set's `SetType` is in `Power.SetTypes`. (No per-enhancement class check for set pieces — set membership alone.)

#### Predicate 3 — build-wide placement test (`Build.EnhancementTest`)

`EnhancementTest(iSlotID, hIdx, iEnh, silent=false) -> bool` (Build.cs:1026). This is the gate the UI actually calls at placement time. MessageBox side-effects removed; port returns bool only.

```text
if iEnh < 0 or iSlotID < 0: return false
enh = Database.Enhancements[iEnh]

# (a) SET-TYPE gate — ONLY for SetO enhancements
if enh.TypeID == SetO and enh.nIDSet > -1 and hIdx > -1 and Powers[hIdx].Power != null:
    setType = Database.EnhancementSets[enh.nIDSet].SetType
    if not any(t == setType for t in Powers[hIdx].Power.SetTypes):
        return false            # rejected: set category not allowed in this power

foundMutex=false; foundInPower=false; foundEnh=""; mutexType=-1
for each existing slot (powerIdx, slotIndex) across ALL powers:
    skip the target slot itself (slotIndex==iSlotID and powerIdx==hIdx); skip empty slots (Enh<=-1)

    # (b) UNIQUE: one copy of this exact enhancement in the whole build
    if enh.Unique and slot.Enh == iEnh:
        return false

    # (c) MUTUALLY-EXCLUSIVE families (checked against MidsContext.Character.PEnhancementsList,
    #     the build-wide list of placed enhancement UIDs):
    if enh.Superior and enh.MutExID != None:
        nVersion = regex_replace(enh.UID, "(Attuned_|Superior_)", "")   # strip prefixes
        for item in PEnhancementsList:
            if nVersion in item: foundMutex=true; mutexType=0; foundEnh=<that enh LongName>
    elif (not enh.Superior) and enh.MutExID != None and enh.MutExID != Stealth:
        nVersion = regex_replace(enh.UID, "(Attuned_|Superior_)", "")
        for item in PEnhancementsList:
            if ("Superior_Attuned_"+nVersion) in item or ("Superior_Attuned_Superior_"+nVersion) in item:
                foundMutex=true; mutexType=0; foundEnh=<that enh LongName>
    elif enh.MutExID == Stealth:
        for item in PEnhancementsList:
            if Database.Enhancements[GetEnhancementByUIDName(item)].MutExID == Stealth:
                foundMutex=true; mutexType=1; foundEnh=<that enh LongName>

    # (d) DUPLICATE-IN-POWER: same set piece already in THIS power
    if enh.nIDSet <= -1 or powerIdx != hIdx or slot.Enh != iEnh: continue
    foundInPower = true; break

if foundMutex: return false
if not foundInPower: return true
return false                     # (d) same set enhancement already slotted in this power
```

`eEnhMutex`: `None=0, Stealth=1, ArchetypeA..F=2..7` (Enums.cs:653). Superior/Attuned families collide by their name stem (UID with `Attuned_`/`Superior_` stripped); Stealth procs are mutually
exclusive with each other build-wide.

**Critical gap in EnhancementTest a naive port must preserve:** the set-type gate (a) fires **only** when `enh.TypeID == SetO`. For `Normal`/`InventO`/`SpecialO` enhancements, EnhancementTest performs
**no `Power.Enhancements` class check at all** — it only checks unique/mutex/duplicate. So EnhancementTest is *not* a complete legality check for standard enhancements.

#### Where each predicate is enforced (placement paths)

1. **Picker population** (I9Picker.cs:1645-1719): builds the choices actually offered.
    - `Ui.No  = Power.GetValidEnhancements(Normal)`
    - `Ui.Io  = Power.GetValidEnhancements(InventO)`
    - `Ui.SpecialO = Power.GetValidEnhancements(SpecialO, subType)`
    - `Ui.SetTypes = Power.SetTypes` (only these set-category tabs appear).
    So standard enhancements failing Predicate 1, and set categories not in `Power.SetTypes`, are **never presented**.
2. **Pick confirm** (MainWindow2.cs:2762, `I9Picker_EnhancementPicked`): places iff `EnhancementTest(slot,power,e.Enh) | e.Enh<0` (bitwise, non-short-circuit OR). Predicate 3 is the gate here.
3. **Repeat last enhancement** (MainWindow2.cs:2583 `GetRepeatEnhancement`): for non-`SetO`, returns the enh **only if `Power.IsEnhancementValid`** (Predicate 1) — this is where a repeated standard
    enhancement is class-checked (2600-2606); for `SetO` it defers to `GetFirstValidSetEnh` which loops `EnhancementTest`.
4. **Load / recalc** (Build.cs:608 `CheckAndFixAllEnhancements`): for every slotted enhancement, if `!Power.IsEnhancementValid(Enh)` set `Enh = -1` (strip); else fix IO level. Runs Predicate 1 (which
    covers Predicate 2 for SetO) on both the primary and flipped enhancement. This is the catch-all that removes illegal enhancements arriving via file import/paste.

Net model for a faithful port: a placement is legal iff **IsEnhancementValid (Predicate 1, which routes SetO to Predicate 2) AND EnhancementTest (Predicate 3) both pass.** IsEnhancementValid supplies
the standard-class / set-category check; EnhancementTest supplies the unique/mutex/duplicate build-wide rules (plus a redundant set-category check). Do not rely on EnhancementTest alone.

---

### RESOLVED flagged cases (current Mids behavior)

#### Force Feedback (Knockback set, incl. the +Recharge proc) in Soul Drain → **REJECTED**

Evidence chain:

- Soul Drain `allowed_boostset_cats` = `[Melee AoE Damage, Scrapper Archetype Sets, To Hit Buff, Universal Damage Sets]` → `Power.SetTypes` = `{MeleeAoE, Scrapper, ToHit, UniversalDamage}`. **No
  `Knockback`.**
- Force Feedback set `group_name`/category = `Knockback` → set `SetType = eSetType.Knockback` (=23).
- Predicate 3 (a), Build.cs:1035-1044: `setType=Knockback` not in `Power.SetTypes` ⇒ `allowedSet=false` ⇒ `return false`. Placement rejected.
- Reinforced upstream: picker never renders a Knockback set-type tab for Soul Drain (I9Picker `Ui.SetTypes = Power.SetTypes`), and `GetValidEnhancementsFromSets` (Predicate 2) excludes the Force
  Feedback set, so `IsEnhancementValid` is false and load-time `CheckAndFixAllEnhancements` strips it. Rejected on every path.

#### A (common) Recharge IO in One with the Shield → **REJECTED**, but *not* by EnhancementTest

Evidence chain:

- One with the Shield `boosts_allowed` = `[Enhance Damage Resistance, Enhance Endurance Modification, Reduce Endurance Cost, Enhance Heal]` → `Power.Enhancements` has **no Recharge class**.
  (`allowed_boostset_cats` = `[Endurance Modification, Healing, Resist Damage]`.)
- A common Recharge Reduction IO is `TypeID = InventO`, `ClassID = {Recharge}` — a **standard** enhancement, `nIDSet = -1`, `TypeID != SetO`.
- Predicate 1 (`GetValidEnhancements(InventO)`): Recharge class ∉ `Power.Enhancements` ⇒ `IsEnhancementValid` = **false**.
  - Picker: `Ui.Io = GetValidEnhancements(InventO)` omits it → **not offered**.
  - Repeat path: `GetRepeatEnhancement` returns empty because `IsEnhancementValid` is false (MainWindow2.cs:2600-2606).
  - Load: `CheckAndFixAllEnhancements` sets `Enh=-1` → **stripped**.
- **Trap:** Predicate 3 (`EnhancementTest`) does **not** reject it. Its set-type gate only runs for `SetO`; for this `InventO` enhancement it finds no unique/mutex/duplicate conflict and returns
  **true**. A reimplementation that routes all placement solely through `EnhancementTest` (as MainWindow2:2762 literally does) would wrongly **accept** a Recharge IO in One with the Shield. In real
  Mids the enhancement can't reach that call because the picker population (Predicate 1) never offers it; and any that sneak in via import are removed by `CheckAndFixAllEnhancements` (Predicate 1).
  Net observable behavior: **rejected**.

Correct port rule: gate standard (`Normal`/`InventO`/`SpecialO`) enhancements with Predicate 1 (`ClassID ∈ Power.Enhancements`); gate set (`SetO`) enhancements with Predicate 2
(`set.SetType ∈ Power.SetTypes`) **plus** the unique/mutex/duplicate rules of Predicate 3.

**Open items / gaps:**

- EnhancementClasses table not read in full: GetValidEnhancements compares Database.EnhancementClasses[classId].ID against Power.Enhancements. The exact numeric ID<->name mapping (how canonical
  boosts_allowed strings become the int IDs in Power.Enhancements) lives in the .mhd database, not read here. Port needs that class-name->ID table.
- Enhancement.Unique, .Superior, .MutExID, .UID, .LongName, .nIDSet, .ClassID[], .SubTypeID field semantics inferred from usage; the Enhancement class definition and how these are populated from the
  DB were not opened.
- PEnhancementsList (MidsContext.Character.PEnhancementsList) — the build-wide list of placed enhancement UIDs consumed by the mutex/superior checks — its construction/refresh point was not traced;
  needed to reproduce mutex results exactly.
- GetEnhancementByUIDName and CheckAndFixIOLevel (leaf DB helpers used in the mutex loop and load-fix) not read.
- proc_allowed field (='All' on both flagged powers) was observed but its enforcement path was not traced; may impose a separate proc/global legality rule beyond the class/set-type gates covered here.
- eEnhGrade (Normal SO grade) clamping in ValidateEnhancements (Build.cs:211) noted but full eEnhGrade/eEnhRelative ranges not enumerated; peripheral to accept/reject legality.
- Whether a drag-drop-from-set path exists separate from I9Picker/repeat (e.g. dragging an enhancement onto a slot) was not exhaustively searched; the confirmed placement gate is EnhancementTest via
  I9Picker_EnhancementPicked plus the repeat path, backed by load-time CheckAndFixAllEnhancements.

---

## updater-protocol

**Port risk:** medium — The wire protocol (fixed URLs, System.Version compare, SHA256 sidecar) is simple and exactly transcribable. The medium risk is the custom binary container: it is a zlib-wrapped
stream (SharpZipLib Deflater/Inflater = zlib header + Adler32, NOT gzip and NOT raw deflate) whose payload uses .NET BinaryWriter framing — 7-bit-encoded (LEB128) length-prefixed UTF-8 strings and
little-endian 4-byte ints. A naive Python reimplementation that assumes gzip, raw-deflate (wbits=-15), or fixed-length string prefixes will silently fail. All constants are resolved; nothing requires
runtime data. CP8 typically only needs the READ path (fetch manifest, download, inflate, parse, SHA256-verify, drop into Databases/Homecoming/), not the C#/WinForms install mechanics (MoveFileEx,
backup, relaunch).

**Source files:**

- `MidsReborn/UI/Forms/UpdateSystem/UpdateUtils.cs` (19-201) — Manifest fetch + version comparison (client side). CheckForUpdatesAsync, FetchAllRelevantManifestEntriesAsync,
  CompareAgainstCurrentVersions, FetchManifest, GetBaseUriFromFileUrl.
- `MidsReborn/UI/Forms/UpdateSystem/UpdateCoordinator.cs` (19-153) — Orchestration: check, build temp manifest DTO, write temp JSON, launch MRBBootstrap.exe with --manifest.
- `MidsReborn/UI/Forms/UpdateSystem/Models/Manifest.cs` (1-11) — Manifest DTO: ManifestVersion, Updates[], LastUpdated.
- `MidsReborn/UI/Forms/UpdateSystem/Models/ManifestEntry.cs` (1-34) — ManifestEntry (Type/Name/Version/File + [JsonIgnore] SourceUri) and ManifestEntryDto (SourceUri serialized).
- `MidsReborn/Core/Utils/PatchCompressor.cs` (241-295) — AUTHORITATIVE writer for the custom container + zlib compression (Deflater level 9). Reverse of what the reimplementation must READ.
- `MidsReborn/Core/Utils/FileHash.cs` (21-30) — SHA256 hash format: BitConverter hex, lowercase, no dashes.
- `MidsReborn/Core/Utils/PatchManifestBuilder.cs` (14-79) — Server-side manifest URL resolution + entry upsert (authoring side).
- `MidsReborn/Core/Utils/Helpers.cs` (33-37) — IsVersionNewer: System.Version.CompareTo(...) > 0.
- `MidsReborn/Core/Utils/StructAndEnums.cs` (43-48) — PatchType enum {Application, Database, Bootstrapper}.
- `MidsReborn/Core/DatabaseAPI.cs` (1141-1143) — DatabaseName = directory name of Config.DataPath (e.g. 'Homecoming').
- `MidsReborn/Core/ServerData.cs` (28-66) — Default ManifestUri = <https://updates.midsreborn.com/update_manifest.json.>
- `MRB_Boostrap/Program.cs` (14-251) — Bootstrapper entry: arg parse, load manifest, DI host, per-entry patch loop, restart.
- `MRB_Boostrap/Models/BootstrapArguments.cs` (10-52) — CLI arg parsing: --manifest, --rollback, --help; mode selection.
- `MRB_Boostrap/Utilities/UpdateManifestLoader.cs` (9-50) — Reads temp manifest JSON (Newtonsoft) into List\<UpdateEntry>.
- `MRB_Boostrap/Models/UpdateEntry.cs` (3-10) — UpdateEntry: Type/Name/Version/File/SourceUri (all strings).
- `MRB_Boostrap/Services/PatchFlowManager.cs` (39-223) — THE 11-step install pipeline: size, download patch, download hash, decompress, stage, validate, close app, backup, apply, cleanup; rollback on
  failure.
- `MRB_Boostrap/Services/FileDownloader.cs` (49-169) — HEAD size probe + GET download with 3 retries, 15s timeout, AutomaticDecompression.All.
- `MRB_Boostrap/Services/FileDecompressor.cs` (16-156) — AUTHORITATIVE reader: zlib Inflate then parse custom container; ReadDotNetString / Read7BitEncodedInt.
- `MRB_Boostrap/Services/HashValidator.cs` (21-96) — SHA256 sidecar validation against staged files.
- `MRB_Boostrap/Services/FileStager.cs` (20-142) — Write staged files; ApplyStagedFilesAsync uses MoveFileEx(ReplaceExisting), Rebirth first-chunk strip, skips Logs.
- `MRB_Boostrap/Services/BackupManager.cs` (19-211) — Pre-install backup (.mrbak container) + restore path.
- `MRB_Boostrap/Utilities/ProcessUtils.cs` (8-70) — Kill MidsReborn before install; StartMidsReborn after (triggers DB reload).
- `MRB_Boostrap/App.cs` (25-41) — Per-entry loop; on success restart MidsReborn; abort remaining on failure.

**Key constants / tables:**

| Name | Location | Value / pointer |
| --- | --- | --- |
| Primary manifest URL | UpdateUtils.cs:36 / PatchManifestBuilder.cs:14 / ServerData.cs:28 | <https://updates.midsreborn.com/update_manifest.json> |
| Manifest fetch UA (Cloudflare bypass) | UpdateUtils.cs:104 | Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.127 Safari/537.36 |
| Manifest HTTP timeout | UpdateUtils.cs:105 | 5 seconds |
| Manifest fetch sequence | UpdateUtils.cs:113-141 | .xml=>legacy abort; HEAD (Accept: application/json); GET; tag SourceUri |
| Homecoming short-circuit | UpdateUtils.cs:39-42 | DatabaseName=='Homecoming' (OrdinalIgnoreCase) => primary manifest only |
| IsVersionNewer | Helpers.cs:33-37 | System.Version candidate.CompareTo(current) > 0 (strict) |
| App version test | UpdateUtils.cs:65 | newAppVersion > MidsContext.AppFileVersion (System.Version >) |
| AssemblyFileVersion (example current app ver) | MidsContext.cs:14 | 3.8.0.0 |
| PatchType enum | StructAndEnums.cs:43-48 | Application=0, Database=1, Bootstrapper=2 (serialized as name string) |
| Temp manifest path | UpdateCoordinator.cs:93 | %TEMP%/mids_patch_<Guid:N>.json |
| Temp manifest wrapper | UpdateCoordinator.cs:105-110 | {ManifestVersion:'3.0', Updates:[dto], LastUpdated:'MM/dd/yyyy HH:mm:ss'} |
| Bootstrap launch command | UpdateCoordinator.cs:120-133 | MRBBootstrap.exe --manifest "\<tempfile>" (UseShellExecute) |
| Update-check delay gate | UpdateCoordinator.cs:146-152 | (UtcNow.Date - LastChecked.Date).TotalDays >= AutomaticUpdates.Delay |
| Container magic (patch) | PatchCompressor.cs:249 / FileDecompressor.cs:74 | 'Mids Reborn Patch Data' |
| Container magic (backup) | FileDecompressor.cs:74 | 'Mids Reborn Backup Data' |
| zlib compression level | PatchCompressor.cs:276 | SharpZipLib Deflater(9); Inflater() default => RFC1950 zlib (header+Adler32) |
| Container record order | PatchCompressor.cs:251-260 / FileDecompressor.cs:80-99 | magic(str); count(int32 LE); per file: dataLength(int32 LE), fileName(str), directory(str), data[dataLength] |
| .NET string encoding | FileDecompressor.cs:130-156 | 7-bit LEB128 length prefix (max 5 bytes) + UTF-8 bytes; length guard 0..65536 |
| Directory normalization | FileDecompressor.cs:127-128 | replace '/'->OS sep; TrimStart leading sep |
| SHA256 hash format | FileHash.cs:21-26 / HashValidator.cs:90-96 | BitConverter.ToString(SHA256(bytes)).Replace('-','').ToLowerInvariant() (lowercase hex, no dashes) |
| Hash sidecar JSON shape | HashValidator.cs:13 / PatchCompressor.cs:202-209 | array of {Directory, FileName, Hash} |
| Hash file extension | PatchFlowManager.cs:54 | patch path with extension changed to .hash |
| DB install path | PatchFlowManager.cs:49-51 | AppContext.BaseDirectory/Databases/<entry.Name>  (e.g. Databases/Homecoming) |
| App install path | PatchFlowManager.cs:49-50 | AppContext.BaseDirectory |
| Staging / Backup paths | PatchFlowManager.cs:56-57 | BaseDir/Staging/\<Name>, BaseDir/Backup/\<Name> |
| Backup file extension | BackupManager.cs:77 | .mrbak (same container format as .mru) |
| Download retries / timeout / buffer | FileDownloader.cs:82,21,81 | maxRetries=3, timeout=15s, buffer=8192, backoff=attempt*1000ms |
| Transport decompression | FileDownloader.cs:17 | HttpClientHandler.AutomaticDecompression = DecompressionMethods.All (transport only; independent of payload zlib) |
| Patch filename template | PatchCompressor.cs:118-119 | '\<Name>-\<Version>-cumulative.mru'.ToLower() ; hash: '...-cumulative.hash' |
| Apply move API | FileStager.cs:114 | Win32 MoveFileEx(source, target, ReplaceExisting) |
| Apply skip / dir-strip rule | FileStager.cs:83-101 | skip Directory=='Logs'; if installPathSubDir not in ('',''Images') strip first dir chunk |
| Post-install relaunch | App.cs:36-39 / ProcessUtils.cs:47-70 | wait 1s, Process.Start MidsReborn.exe (reload via restart, no hot reload) |
| DatabaseName derivation | DatabaseAPI.cs:1141-1143 | leaf directory name of Config.DataPath |

### Updater Protocol — Homecoming DB/Ruleset Pull (CP8 data-refresh)

Two cooperating processes. `MidsReborn.exe` decides *whether* an update exists and writes a small temp manifest; `MRBBootstrap.exe` (separate EXE, `MRB_Boostrap/`) does the *download + inflate +
verify + install + relaunch*. For a Python data-refresh you almost always reimplement only the **client check + the download/inflate/verify/install READ path**; the WinForms install mechanics
(MoveFileEx, backup, process kill) are optional.

#### Part A — Manifest source & URL scheme

`UpdateUtils.FetchAllRelevantManifestEntriesAsync` (UpdateUtils.cs:32-55):

1. Always fetch the **primary** manifest from the hardcoded URL
    `https://updates.midsreborn.com/update_manifest.json` (UpdateUtils.cs:36).
2. If `DatabaseAPI.DatabaseName == "Homecoming"` (case-insensitive) → **stop; return only primary list** (UpdateUtils.cs:39-42). Homecoming's DB updates live in the primary manifest.
3. Otherwise, additionally fetch an **external** manifest from `DatabaseAPI.ServerData.ManifestUri` (skipped if null/whitespace) and append its entries (UpdateUtils.cs:44-53).

`DatabaseName` = the leaf directory name of `Config.DataPath` (DatabaseAPI.cs:1141-1143), e.g. `Databases/Homecoming` → `"Homecoming"`. Default `ServerData.ManifestUri` is the same midsreborn URL
(ServerData.cs:28).

##### FetchManifest (UpdateUtils.cs:92-157) — RestSharp, System.Text.Json, case-insensitive, `JsonStringEnumConverter`, UA spoofed to a Chrome string (Cloudflare), 5s timeout

- **Step 0**: if URL ends `.xml` (case-insensitive) → legacy XML, show warning, return empty `Manifest` (UpdateUtils.cs:113-117).
- **Step 1**: HTTP **HEAD** with `Accept: application/json`. If not successful or 404 → warn, return empty (UpdateUtils.cs:119-128).
- **Step 2**: HTTP **GET**, deserialize to `Manifest`. Null → empty (UpdateUtils.cs:130-136).
- **Step 3**: for each `entry` in `result.Updates`, set `entry.SourceUri = GetBaseUriFromFileUrl(manifestUrl)` — i.e. the manifest URL with its **last path segment (filename) removed**, keeping the
  trailing `/` (UpdateUtils.cs:138-141, 189-201). Example: `https://updates.midsreborn.com/update_manifest.json` → `https://updates.midsreborn.com/`.

##### Manifest JSON shape (Manifest.cs, ManifestEntry.cs)

```json
{
  "ManifestVersion": "3.0",
  "Updates": [
    { "Type": "Database", "Name": "Homecoming", "Version": "2025.1.1.0", "File": "homecoming-2025.1.1.0-cumulative.mru" }
  ],
  "LastUpdated": "07/02/2026 12:00:00"
}
```

`Type` is an enum string: `"Application"` | `"Database"` (| `"Bootstrapper"`, unused here) — StructAndEnums.cs:43-48. `SourceUri` is **not** in the on-wire manifest (`[JsonIgnore]`,
ManifestEntry.cs:20-21); it is injected client-side per Step 3.

#### Part B — Version comparison (RESOLVED)

`CompareAgainstCurrentVersions` (UpdateUtils.cs:57-90):

- **App entry**: match `Type==Application && Name==MidsContext.AppName` (case-insensitive). Update available iff `Version.TryParse(entry.Version)` succeeds AND
  `newAppVersion > MidsContext.AppFileVersion` (UpdateUtils.cs:65). Direct `System.Version` `>` operator.
- **DB entry**: match `Type==Database && Name==DatabaseName` (case-insensitive). Update available iff `Version.TryParse` succeeds AND
  `Helpers.IsVersionNewer(newDbVersion, DatabaseAPI.Database.Version)` (UpdateUtils.cs:74-82).

`Helpers.IsVersionNewer(candidate, current)` = `candidate.CompareTo(current) > 0` (Helpers.cs:33-37). This is **`System.Version` semantics**: compare Major, then Minor, then Build, then Revision, each
as an integer; a component absent from one side is treated as **-1** (so `1.2` < `1.2.0`). `Version.TryParse` requires at least `major.minor`. Reimplement in Python with a 4-tuple where missing = -1,
strictly-greater test.

#### Part C — Client hands off to bootstrapper (UpdateCoordinator.cs)

1. `CheckAndHandleUpdatesAsync` optionally honors a delay gate: update check is "due" only if `(UtcNow.Date - LastChecked.Date).TotalDays >= AutomaticUpdates.Delay` (UpdateCoordinator.cs:146-152).
2. Build `ManifestEntryDto` list from the check result — App and/or DB, each carrying `Type, Name, Version, File, SourceUri` (UpdateCoordinator.cs:60-89). **Here `SourceUri` IS serialized**
    (ManifestEntry.cs:24-33).
3. Write a temp file `%TEMP%/mids_patch_<guid:N>.json` wrapping entries as a Manifest-shaped object (UpdateCoordinator.cs:91-114):

```json
{ "ManifestVersion":"3.0", "Updates":[<dtos>], "LastUpdated":"MM/dd/yyyy HH:mm:ss" }
```

  (indented, `JsonStringEnumConverter`). Note: must be an object, not a bare array — the bootstrapper deserializes `Manifest`.
4. Launch `MRBBootstrap.exe --manifest "<tempfile>"` from `AppContext.BaseDirectory`, `UseShellExecute=true` (UpdateCoordinator.cs:116-144). Then the client process exits/continues; the bootstrapper
    takes over.

#### Part D — Bootstrapper (MRBBootstrap.exe)

##### Arg parse (BootstrapArguments.cs:10-52)

`--manifest <file>` → Patch mode; `--rollback <name>` → Rollback; `--help/-h` → help; empty args → help. Precedence: help > rollback > manifest.

##### Manifest load (UpdateManifestLoader.cs) — Newtonsoft, `NullValueHandling.Ignore`, into `List<UpdateEntry>` (all string fields). Empty list → throw

##### Per-entry install pipeline — `PatchFlowManager.ExecutePatchFlowAsync` (PatchFlowManager.cs:39-223). Path math (lines 46-57), with `baseDir = AppContext.BaseDirectory`

- `baseUrl` = `entry.SourceUri` **with a trailing `/` ensured** (line 47).
- `installPath` = if `Type=="Application"` → `baseDir`; else → `baseDir/Databases/<entry.Name>` → **e.g. `Databases/Homecoming/`** (line 49-51).
- `patchPath` = `baseDir/<entry.File>` (line 53).
- `hashPath` = `patchPath` with extension changed to `.hash` (line 54).
- `stagingPath` = `baseDir/Staging/<entry.Name>`; `backupPath` = `baseDir/Backup/<entry.Name>` (lines 56-57).

**Steps (each throws on failure → rollback if past install start):**

1. **Size probe**: local file size if patch already on disk, else HTTP HEAD `GetRemoteFileSizeAsync(baseUrl + entry.File)` (lines 64-77).
2. **Download patch** (only if `patchPath` absent): GET `baseUrl + entry.File` → `patchPath` (lines 79-96).
3. **Download hash** (only if `hashPath` absent): GET **`entry.SourceUri + Path.GetFileName(hashPath)`** → `hashPath` (lines 98-114). ⚠ Note the asymmetry: patch URL uses `baseUrl`
    (trailing-slash-normalized), hash URL uses raw `entry.SourceUri` (which already ends `/` from `GetBaseUriFromFileUrl`). Reimplementation: base both on the same trailing-slash base; the `.hash`
    filename = patch filename with `.mru`→`.hash`.
4. **Decompress** patch → `List<FileEntry>` (see Part E). Empty → fail (lines 116-122).
5. **Stage**: write every FileEntry to `stagingPath/<Directory>/<FileName>` (FileStager.cs:20-66).
6. **Validate** staged files against the `.hash` sidecar (Part F). Mismatch/missing → fail (lines 133-139).
7. **Close app**: if `MidsReborn` process running, CloseMainWindow then Kill; abort (return -1) if it won't close (lines 141-155).
8. **Backup** current `installPath` into `Backup/<Name>.mrbak` (BackupManager.cs).
9. (no-op cleanup placeholder.)
10. **Apply**: move each staged file into place (Part G).
11. **Cleanup**: delete `patchPath`, `hashPath`, `stagingPath` (recursive); also in `finally` (lines 183-221).

On any post-install exception with a backup made → `RollbackAsync` restores from `.mrbak` (lines 207-244).

##### FileDownloader (FileDownloader.cs) — `HttpClient` with `AutomaticDecompression=All` (transport-level gzip/deflate/br only), **15s** timeout

`DownloadFileAsync`: up to **3** attempts, `HttpCompletionOption.ResponseHeadersRead`, 8192-byte buffer, streams to `FileStream(Create)`; on failure deletes partial file and backs off `attempt*1000`
ms (lines 73-169). Transport decompression is independent of the payload's own zlib wrapper (Part E)

##### After success — `App.RunAsync` (App.cs:25-41)

For each entry, run the flow; if it returns 0, wait 1s and `ProcessUtils.StartMidsReborn()` (relaunch `MidsReborn.exe`, ProcessUtils.cs:47-70). The relaunched MidsReborn reloads the DB from
`Databases/<Name>/` on startup via its normal `Load*` path — there is **no in-process hot reload**; the reload IS the restart. If a patch fails, remaining entries are skipped (App.cs:31-34)

#### Part E — Patch container format (READ path — critical)

Producer: `PatchCompressor` (writer, PatchCompressor.cs:241-295). Consumer: `FileDecompressor.DecompressAsync` (reader, FileDecompressor.cs:16-156). The `.mru` file is:

**Outer layer — zlib.** Written with SharpZipLib `DeflaterOutputStream(new Deflater(9))` (PatchCompressor.cs:276) and read with SharpZipLib `Inflater()` default ctor (FileDecompressor.cs:39).
SharpZipLib default = **zlib format (2-byte header + DEFLATE + 4-byte Adler-32 trailer)** — i.e. RFC 1950, NOT gzip, NOT raw DEFLATE. In Python: `zlib.decompress(data)` (default `wbits=15`) or
`zlib.decompressobj()`; do **not** use `wbits=-15` or `gzip`.

**Inner layer — .NET `BinaryWriter` stream** (little-endian). Byte layout, in order:

| Field | Encoding | Notes |
| --- | --- | --- |
| magic | .NET string | `"Mids Reborn Patch Data"` (patch) or `"Mids Reborn Backup Data"` (backup). Reader rejects anything else. |
| fileCount | 4-byte LE int | written as Int32 (`hashedFiles.Count`), read as UInt32. |
| repeat fileCount times: | | |
| dataLength | 4-byte LE int | written Int32 (`file.Data.Length`), read UInt32. |
| fileName | .NET string | leaf filename. |
| directory | .NET string | relative dir (may be empty). |
| data | `dataLength` raw bytes | file contents. |

**.NET string encoding** (`ReadDotNetString`, FileDecompressor.cs:130-138): a **7-bit-encoded (LEB128) length prefix** followed by that many **UTF-8** bytes. `Read7BitEncodedInt` (lines 140-156): read
bytes low-group-first, 7 data bits each, high bit = continuation, **max 5 bytes**; malformed → -1. Reader guards `length < 0 || length > 65536` → `InvalidDataException`.

Python read pseudocode:

```python
def read_7bit_int(buf, pos):
    result = shift = 0
    for _ in range(5):
        b = buf[pos]; pos += 1
        result |= (b & 0x7F) << shift
        if not (b & 0x80):
            return result, pos
        shift += 7
    raise ValueError("bad 7-bit int")

def read_dotnet_string(buf, pos):
    n, pos = read_7bit_int(buf, pos)
    if n < 0 or n > 65536: raise ValueError
    s = buf[pos:pos+n].decode("utf-8"); return s, pos + n

raw = zlib.decompress(open(mru,'rb').read())
pos = 0
magic, pos = read_dotnet_string(raw, pos)
assert magic in ("Mids Reborn Patch Data", "Mids Reborn Backup Data")
count = int.from_bytes(raw[pos:pos+4], "little"); pos += 4
files = []
for _ in range(count):
    dlen = int.from_bytes(raw[pos:pos+4], "little"); pos += 4
    fname, pos = read_dotnet_string(raw, pos)
    ddir,  pos = read_dotnet_string(raw, pos)
    data = raw[pos:pos+dlen]; pos += dlen
    files.append((fname, ddir.replace("/", os.sep).lstrip(os.sep), data))
```

`NormalizeDirectory` (FileDecompressor.cs:127-128): replace `/`→OS separator, `TrimStart` leading separator.

#### Part F — SHA256 sidecar (`.hash`) validation

`.hash` is JSON: an **array** of `{ "Directory": str, "FileName": str, "Hash": str }` (HashValidator.cs:13, 30-51; produced by PatchCompressor.CompileList → `FileHash`, PatchCompressor.cs:202-209).
`Hash` = **SHA-256 of the raw file bytes, hex, lowercase, no separators** — `BitConverter.ToString(sha256).Replace("-","").ToLowerInvariant()` (FileHash.cs:21-26, mirrored HashValidator.cs:90-96).
Validation (HashValidator.cs:21-88): for each entry, `fullPath = stagingPath/Directory/FileName`; missing file → fail; recompute SHA256; compare case-insensitive; any mismatch → whole patch fails. In
Python: `hashlib.sha256(data).hexdigest()` == entry hash (lowercased).

#### Part G — Install (apply) semantics — `ApplyStagedFilesAsync` (FileStager.cs:68-142)

Per file: skip if `Directory == "Logs"` (case-insensitive). Compute `fDir`:

- `installPathSubDir = leaf name of installPath`.
- If `installPathSubDir` is not `""` and not `"Images"` (case-insensitive) → **strip the first path chunk** of `file.Directory` (handles DB patches whose container dirs are prefixed with the DB name,
  which would otherwise double-nest, lines 88-101).
- Move `stagingPath/file.Directory/FileName` → `installPath/fDir/FileName` via **Win32 `MoveFileEx(..., ReplaceExisting)`** (line 114); Win32 error → fail → rollback.

For a Python data-refresh you can ignore the MoveFileEx nuance and simply write each parsed FileEntry into `Databases/Homecoming/<normalized-dir-with-first-chunk-stripped>/<FileName>`, replacing
existing files.

#### Part H — On-disk data-dir layout the engine reads after install

Target `Databases/Homecoming/` contains the `.mhd`/`.json` DB files the engine loads on startup (verified listing): `AttribMod.json`, `bbcode.mhd`, `Compare.json`, `Compare.mhd`,
`CrypticPowerNames.mhd`, `EClasses.mhd`, `EnhDB.mhd`, `GlobalMods.mhd`, `I12.mhd`, `I9.mhd`, `Images/`, `Maths.mhd`, `NLevels.mhd`, `Origins.mhd`, `PowersReplTable.mhd`, `Recipe.mhd`, `RLevels.mhd`,
`Salvage.mhd`, `SData.mhd`, `TypeGrades.json`. The cumulative `.mru` patch overwrites/adds these files; the relaunched app parses them.

#### Ordering / gotchas a naive port gets wrong

1. **zlib, not gzip/raw-deflate.** SharpZipLib default Inflater expects the 2-byte zlib header + Adler-32. Use `wbits=15`.
2. **7-bit LEB128 string length, not a fixed 1/2/4-byte prefix.** Single-byte only holds len ≤ 127; longer names use continuation bytes.
3. **Field order inside each record is dataLength, fileName, directory, data** — length precedes the two strings even though data comes last.
4. Ints are 4-byte little-endian (BinaryWriter). `count`/`dataLength` are semantically unsigned on read.
5. **Homecoming short-circuits** external manifest fetch; only the primary midsreborn manifest matters.
6. Version test is **strictly greater** using System.Version tuple compare (missing component = -1); equal versions do NOT update.
7. Hash sidecar URL derivation differs subtly from patch URL (Part D step 3) — normalize to one trailing-slash base.
8. `Logs/` entries are skipped on apply; DB patches strip the leading dir chunk (Part G).
9. Magic string may also be `"Mids Reborn Backup Data"` (rollback path); accept both if you reuse the reader.
10. The "reload" is a full process restart of MidsReborn.exe, not an in-place reload.

**Open items / gaps:**

- MidsContext.AppFileVersion / AppName exact values and how AppMajor/Minor/Build/Revision are seeded were not fully read (only AssemblyFileVersion const 3.8.0.0 at MidsContext.cs:14 and AppFileVersion
  property at :16); the app-side comparison target version source is approximate. DB-side is the material path for CP8 and is fully resolved.
- DatabaseAPI.Database.Version (the current on-disk DB version used as the compare baseline) was not traced to its load/parse site (SData.mhd). The comparison semantics (System.Version) are resolved;
  the exact field origin is not.
- The client-side check path (UpdateUtils) and the bootstrapper both exist; I did not confirm whether any code path ALSO downloads/inflates DB data in-process without the external MRBBootstrap.exe.
  All evidence indicates install is exclusively via the separate bootstrapper EXE.
- FileCompressor.cs (backup writer) was not read; assumed symmetric to PatchCompressor. Only relevant to rollback/backup, not to the DB pull.
- The engine's actual Load*startup sequence that parses Databases/Homecoming/*.mhd after restart was not traced (out of this section's scope: covered by the DB-load/serialization section). Only the
  file-set landed on disk is documented here.
- RestSharp GetAsync\<Manifest> JSON binding details (property-name matching for ManifestVersion/Updates/LastUpdated) assumed standard case-insensitive System.Text.Json; confirmed options at
  UpdateUtils.cs:94-98 but not the enum member-name casing accepted for Type (relies on default JsonStringEnumConverter, i.e. exact 'Application'/'Database').
