# Sample builds

Real builds saved by MidsReborn, kept as fixtures.
One build per archetype is curated; five builds, curated or not, also have Mids-saved `.mxd` copies the `.mxd` reader is checked against.

## Layout

| Path | Holds |
| --- | --- |
| `characters/<account>/<character>/<build>.mbd` | A player's own characters. `<account>` is a neutral label (`account1`, `account2`, ...), never an account name. A character folder holds at most three builds, one per in-game build slot. |
| `reference/<archetype>/<focus>/<build>.mbd` | Requested and test builds. |
| `community/<archetype>/<use-type>/<author>-<goal>.mbd` | Other players' builds, any number to a folder. |

`<focus>` is the choice that changes which powers a build takes:
a Kheldian's form mix (`human-form`, `tri-form`, `human-dwarf`, `human-nova`),
an Arachnos Soldier's branch and range (`bane-ranged`, `bane-melee`, `crab-ranged`, `crab-melee`, `huntsman`),
a Blaster's `ranged` or `blapper`.

`<use-type>` is what a community build is tuned for: `general` for a sound build not tuned to a goal,
or a tuned type, `hard-mode`, `exemplar` or `pylon`.
The list is open; a new type is one entry in `UseTypes` in `SampleLayoutTests`.
`<author>` is the poster's handle or archive codename, or `unknown` when the source does not say,
and `<goal>` is the play style or what the build is built to do, in a few words (`<author>-ranged-softcap`).

A build's file name says what the build is for, not who it is:
in a character folder the content mode or purpose (`pve.mbd`), or `main.mbd` when nothing sets one apart;
under `reference/` the powersets and any content mode (`4-star`, `exemplar`).
A build is one `.mbd` or `.mxd`; files beside it that share its name, such as a write-up, do not count towards a character's three.

## Adding a build

1. Pick the layout: your own character, a requested or test build, or another player's build.
2. Name every folder and the file in lower-case words joined by hyphens: no spaces, no capitals, and no version text such as `v2`, `r4` or `i25`.
   A revision replaces the file; git keeps the history.
3. Commit it. The tests read only the files git tracks, and `SampleLayoutTests` fails when a file breaks the layout or the names.

## Coverage

| Archetype | File | Origin |
| --- | --- | --- |
| Arachnos Soldier | `reference/arachnos-soldier/bane-mace/bane-leviathan.mbd` | user-supplied, local capture |
| Arachnos Widow | `community/arachnos-widow/general/unknown-melee.mbd` | Mids archive |
| Blaster | `characters/account1/laser-prime/main.mbd` | own character, local capture |
| Brute | `characters/account1/laser-hazmat/pve.mbd` | own character, local capture |
| Controller | `characters/account1/laser-alt-f4/main.mbd` | own character, local capture |
| Corruptor | `characters/account1/laser-nuke/main.mbd` | own character, local capture |
| Dominator | `characters/account2/hollowmind/main.mbd` | own character, local capture |
| Mastermind | `community/mastermind/general/unknown-petless.mbd` | Mids archive |
| Scrapper | `characters/account1/laser-accretion/main.mbd` | own character, local capture |
| Sentinel | `characters/account1/laser-fallout/main.mbd` | own character, local capture |
| Stalker | `community/stalker/general/unknown-fiery-invulnerability.mbd` | Mids archive |
| Tanker | `characters/account1/laser-quantum/main.mbd` | own character, local capture |

The curated builds are the twelve above, listed by path in `BuildFileFixtures` and `test_sample_builds.py`; each has a Mids dump.
Defender, Peacebringer and Warshade have no sample: the archive holds only `.mxd` files for them.

## What was left out, and why

- The Mastermind archive builds other than the Petless one carry pet powers the current MidsReborn database does not define, so they cannot resolve to powers.
- An earlier HAZMAT Brute placed slots the level schedule had not granted; its PvE variant replaced it.
- The remaining 3,133 `.mxd` files and the other archive `.mbd` files stay in the shared stash for the `.mxd` to `.mbd` conversion (issue 22).

## How they are exercised

- `test_mbd.py` parses every `.mbd` under this folder and resolves the Arachnos Soldier build to powers and slots.
- `test_mbd_writer.py` round-trips every build through `dump_mbd` and the format invariants.
- `test_sample_builds.py` runs, for each curated build, the round trip, both validator registries, and the unenhanced totals against the archetype caps.
- `python -m coh_engine validate "<file>.mbd"` validates one file against the committed dump tables.

Each build's reference files, CLI captures and Mids dump are named by its path here without `.mbd`, each `/` written `__`:
`characters/account1/laser-nuke/main.mbd` has its Mids dump under `tests/fixtures/mids/builds/characters__account1__laser-nuke__main/`.
