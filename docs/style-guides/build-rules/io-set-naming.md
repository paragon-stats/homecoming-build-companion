# Rule: Use full IO set names in prose

**Status:** MUST. Applies to every word emitted in prose, frontmatter, lessons documents, summaries, headings, conversation responses, and code comments.
Abbreviations are tolerated **only** inside ```text``` fenced blocks holding a verbatim Mids forum-export.

## Why

The Mids forum-export shorthand (`SprScrStr`, `Hct`, `Arm`, `AchHee`, `FuroftheG`, etc.) is an artifact of the export format, not a documentation standard.
Prose is for humans (and for future Claude sessions) who read without the export reader's mental dictionary loaded.
"Superior Scrapper's Strike: Recharge/+50% Crit" is unambiguous; "SprScrStr-Rchg/+Crit" requires the reader to know the lookup.
Discoverability, searchability, and future-readability all improve with the full name.

## How to apply

- **Inside ```text``` code fences holding a verbatim Mids forum-export block:** keep abbreviations exactly as the export emits them. That's faithful reproduction of the format.
- **Everywhere else** (prose, frontmatter values, lessons docs, build-creation rules, summaries, conversation responses): full set name.
- **For procs:** "Achilles' Heel: Chance for Resistance Debuff" or "Fury of the Gladiator: Chance for -Res", not "AchHee-ResDeb%".
- **For uniques:** "Steadfast Protection: Resistance/+Defense", "Gladiator's Armor: TP Protection +3% Defense (All)", "Luck of the Gambler: Defense/Increased Global Recharge Speed".
- **When transcribing a build's slotting into prose,** translate every abbreviation to the full name even if the source build text used the shorthand.
  The verbatim ```text``` block stays verbatim; the prose translates.

## Reference table for common abbreviations

| Mids abbreviation | Full set name |
| --- | --- |
| SprScrStr | Superior Scrapper's Strike |
| SprCrtStr | Superior Critical Strikes |
| SprBlsCol | Superior Blistering Cold |
| SprAvl | Superior Avalanche |
| SprWntBit | Superior Winter's Bite |
| SprVglAss | Superior Vigilant Assault |
| SprDfnBst | Superior Defender's Bastion |
| SprBlsWrth | Superior Blaster's Wrath |
| SprDfnBrr | Superior Defiant Barrage |
| SprEnt | Superior Entomb |
| Hct | Hecatomb |
| Arm | Armageddon |
| Apc | Apocalypse |
| Rgn | Ragnarok |
| Obl | Obliteration |
| Erd | Eradication |
| ScrDrv | Scirocco's Dervish |
| AchHee | Achilles' Heel |
| FuroftheG / FotG | Fury of the Gladiator |
| Ann | Annihilation |
| FrcFdb | Force Feedback |
| GssSynFr | Gaussian's Synchronized Fire-Control |
| StdPrt | Steadfast Protection |
| GldArm | Gladiator's Armor |
| GldJvl | Gladiator's Javelin |
| GldNet | Gladiator's Net |
| ShlWal | Shield Wall |
| Rct | Reactive Defenses |
| LucoftheG | Luck of the Gambler |
| UnbGrd | Unbreakable Guard |
| Ags | Aegis |
| RedFrt | Red Fortune |
| NmnCnv | Numina's Convalescence |
| Pnc | Panacea |
| Mrc | Miracle |
| RgnTss | Regenerative Tissue |
| PrfShf | Performance Shifter |
| PwrTrns | Power Transfer |
| Prv | Preventive Medicine |
| Hea | Healing |
| BslGaz | Basilisk's Gaze |
| UnbCns | Unbreakable Constraint |
| GhsWdwEmb | Ghost Widow's Embrace |
| NrnSht | Neuronic Shutdown |
| CldSns | Cloud Senses |
| AdjTrg | Adjusted Targeting |
| Ksm | Kismet |
| BlsoftheZ | Blessing of the Zephyr |
| WntGif | Winter's Gift |
| StnoftheM | Sting of the Manticore |
| Thn | Thunderstrike |
| Mako | Mako's Bite |
| CrcPrs | Coercive Persuasion |
| DmpSpr | Dampened Spirits |
| HO:Ribo | Ribosome Exposure (Hami-O) |
| HO:Centri | Centriole Exposure (Hami-O) |
| HO:Cyto | Cytoskeleton Exposure (Hami-O) |
| HO:Endo | Endoplasm Exposure (Hami-O) |
| HO:Membrane | Membrane Exposure (Hami-O) |
| HO:Enzyme | Enzyme Exposure (Hami-O) |
| HO:Lyso | Lysosome Exposure (Hami-O) |
| HO:Nucleo | Nucleolus Exposure (Hami-O) |
| HO:Peroxi | Peroxisome Exposure (Hami-O) |
| HO:Golgi | Golgi Exposure (Hami-O) |
| HO:Microfil | Microfilament Exposure (Hami-O) |

When an abbreviation appears in a Mids forum-export source and the full name isn't in this table, look it up in [`data/canonical/boost_sets/`](../../../data/canonical/boost_sets/).
The file name (snake-cased) and `display_name` field together resolve to the full set name.

## Reference

- [`error-output.md`](../error-output.md) — diagnostic messages also follow this rule (full names in messages).
- [`data/canonical/boost_sets/`](../../../data/canonical/boost_sets/) — authoritative IO set composition and display names.
