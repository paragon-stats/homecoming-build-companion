# Level-up harness: design

**Status:** proposal for review. Nothing here is built.

A build made by an AI client should be legal by construction and judged the same way every time.
This page designs a harness in the engine binary that makes a client build a character the way the game does: level by level, through moves the binary offers and checks.
The client never writes a build file.

## 1. The decision in one page

- **The binary holds the build.** A client starts a session, and from then on it can only make moves: pick a power, place a slot, choose an enhancement, choose an incarnate power.
- **The binary offers only what the game would offer** at that point, and refuses anything else with a rule ID and a reason.
- **The build has five phases:** start, level up, enhance, incarnate, finish. Each one closes before the next opens.
- **Finishing is the only way to get a build file.** The binary writes the file, runs every registered evaluation step and writes the result file. A client cannot skip a step.
- **The move list is the build's record.** Replaying it gives the same file, byte for byte.
- **Evals measure the harness and the client separately:** how often a client's first move is legal, how many builds pass the gate, and how the finished builds score.

It builds on what exists and what is planned: the `.mbd` writer (#44) and `validate` on a `.mbd` file (#45), both delivered;
the writer gate in [guides-to-engine.md](guides-to-engine.md) section 7.3; and the slotting optimizer (#21).

## 2. The problem

Today a client writes a whole build and has it checked afterwards.

- **A bad build can be written, and then patched in a loop.** A generated Crab Spider build put branch powers at level 1 that a new character cannot take.
  It loaded in MidsReborn and could not be followed in game.
- **The checks are calls the client chooses between.** In one week of build work a final build was reported with a required scoring step not run, twice.
- **Decisions that depend on the build's goal are left to prose.** Whether a piece is attuned, crafted at a level or boosted was decided per build by whoever wrote the brief,
  and a build tuned for level 50 was measured losing a third of its defense when exemplared.
- **The outcome is not repeatable.** Two runs of the same request produce different builds, judged by different steps.

## 3. What exists to build on

| Piece | Where | What it gives the harness |
| --- | --- | --- |
| The level schedule as rules | `RuleRegistry.HardLimitRules`: H-POWER-001 to 003, H-SLOT-001 to 005, H-POOL-001 and 002, prerequisites | What a level offers, already encoded as checks on a finished build |
| Enhancement legality | `RuleRegistry.LegalityRules`: placement, H-ENH-004 to 007 | Which enhancements a slot accepts |
| Slotting rules | `RuleRegistry.SlottingRules`: P-ED, P-SET, P-IO, P-POWER | Warnings a client sees as it slots |
| The build file writer | `src/CohEngine.Core/BuildFile/` | The only thing that writes a `.mbd` |
| The MCP server and `ask` | `src/CohEngine.Mcp/`, `docs/engine/mcp-server.md` | The surface a plug-in talks to |
| Game data per piece | `data/canonical/boost_sets/`: `boostable`, `attuned`, `bonuses_ignore_exemplar`, level range | The facts behind the attuned-or-boosted choice |
| The exemplar rules | [exemplar-10.md](../style-guides/build-rules/exemplar-10.md) | What survives at each level |
| Planning figures | [survivability-planning.md](../survivability-planning.md) | How finished builds are compared |

The rules above run on a whole build. The harness runs the same rules one move at a time, so nothing new decides legality.

### 3.1 A planner that already works this way

[Hate's CoH Planner](https://hateborne.github.io/HatesCoHPlanner/Homecoming/) is a browser planner whose stated aim is that it "looks and plays like the in-game level-up screens".
It is a tool for a person; this harness is the same idea for an AI client, with a gate on the end.
The first table is what its live page (v1.3.0) did when stepped through in a browser on 2026-11-24.
The builds were a Fire Blast/Fire Manipulation Blaster as a Leveling build (Hero, then Villain to level 36) and an Arachnos Soldier.
The second is from its repository (README, `docs/game-rules.md`, `docs/exemplar-rules.md` and the changelog), read on 2026-11-23.

Seen on the live page:

| What it does | What this design takes from it |
| --- | --- |
| The first screen sets game version (Issue 28 Page 4 or Page 3), origin, alignment, archetype, both power sets, and a build type of Leveling or Respec; Respec takes a "Respec to level" from 24 to 50 | `start` takes the same inputs, with build type as an explicit choice rather than a rule hidden in the archetype |
| A Leveling build is one screen per level-up event, in the game's order: level 1 primary pick (secondary greyed), level 1 secondary pick, level 2 power, two "Add an Enhancement Slot" screens at 3, a power at 4 with a pool choice, slots at 5, a power at 6, and on; each has Back | The per-level loop in section 4.3, including slots placed at the level they are granted; `undo` is its Back |
| Only legal powers are buttons; every other power is listed but inert | The `offer` move, and a refusal that names the rule |
| Fitness powers (Stamina, Health, Hurdle, Swift) and the inherents are granted without a pick and can take slots from level 3 | The automatic grants the harness adds itself, never as a pick |
| A slot screen shows every owned power with its slots, a "Slots Available" count, and a click to remove a slot just placed | Slot moves per level, with the count of slots left at that level |
| "Show totals at this level" is on every screen; the overview lists each power with the level it was taken and live totals (Max HP, endurance, regeneration, recovery, endurance balance, recharge), with a button to continue levelling | The per-level survival check in section 4.3 reads totals at the level being built, not only at the end |
| An Arachnos Soldier cannot be a Leveling build: choosing it opens a notice that Soldiers respec at 24 to pick a branch, so their builds are planned only as a Respec build | The build after the respec in section 4.3, which is the default for those archetypes |
| A Respec build is one pick per screen, all 24 powers before any slot screen, with "Powers Bought" and "Powers Remaining" counts | Not the order this harness uses: it builds in level-up order (#234), since the respec view hides the level each slot was gained |
| In the Soldier's respec the first level 1 pick is the secondary (Wolf Spider Armor), with the primary greyed until it is taken | A difference from the level-up order, where the primary comes first at level 1; the harness keeps primary first |
| Both branch sets are listed beside the base sets until the first branch power is picked; picking a Crab power removes the Bane sets from the screen | The branch is fixed by the first branch pick; `offer` drops the other branch from then on |
| A pool is chosen on its own screen in two regions, as in the game: "1. Choose one power pool set", then "2. Choose one power from set", with powers not yet open shown greyed | A pool pick is its own move with two parts, set and power, not one more entry in the power list |
| At level 35 the power screen offers "Choose from new pool power set" and "Choose from new Epic power set" as separate entries; the Epic screen lists the Ancillary and Patron sets in one list (Arsenal, Dark, Electricity, Fire, Force, Ice, Leviathan, Mace, Mu and Soul Mastery for a Villain Blaster) | `offer` at 35 and later lists epic sets apart from ordinary pools; whether Patron sets depend on alignment or the Patron arc is not shown and is read from data |
| After an epic pick the screen returns to that level's power screen showing the set and power; a second Finish moves on to the next level | Nothing to take; a harness move commits in one step |
| A power at six slots is marked "FULL!" and refuses another slot | The six-slot cap as a refusal that names the rule |

From its repository:

| What it does | What this design takes from it |
| --- | --- |
| A power is pickable when picks remain at the level, the power is available at that level, its requirement expression passes and it is not owned | The rules behind `offer` |
| Slots come from a schedule of 67; a slot placed at a level goes on a power owned at that level; six per power | The same hard-limit rules, applied per move |
| The enhancement picker filters to what a power accepts, tells crafted-at-a-level from attuned and superior pieces, and allows boosters only on pieces that can take them | The kind, level and boost of `set_enhancement` in section 4.4 |
| Rule violations are "flagged in red (never auto-fixed)", and an invalid imported build still opens | A difference: this harness refuses an illegal move, because its client is a program that would otherwise carry on |
| Its exemplar rules are a document "for sign-off before any code", with the five-level grace on powers marked unverified for the live game | The same open question as #64; neither project has measured it in game |
| It reads and writes `.mbd` files, Mids share codes and the game's `/build_save` file | `/build_save` import is a way to start a session from a live character; not designed here |

Two facts matter for reuse. Its game data comes from the live game client files, with MidsReborn's database "a reference only, never a source of truth".
Its licence is GPL-3.0, the same as this repository's, and its rule document covers pick rules, automatic grants, slot caps and piece eligibility.
Not checked on the live page: incarnate powers, Kheldian forms, the enhancement picker and an exemplar view.

## 4. The model

A session is a build state plus the list of moves that produced it.

### 4.1 Phases

| Phase | Opens when | The client decides | Closes when |
| --- | --- | --- | --- |
| Start | A session is created | Archetype, primary, secondary, build goal, content mode | The binary accepts the combination |
| Level up | Start is closed | At each level, the power to pick or where the granted slots go | Level 50 is complete |
| Enhance | Level up is closed | An enhancement, its kind and its level, for each slot | Every slot is filled or left empty on purpose |
| Incarnate | Enhance is closed, level 50 | One power for each incarnate slot, or none | Every incarnate slot has a choice or a stated "none" |
| Finish | The earlier phases are closed | Nothing | The build file and the result file are written |

### 4.2 Start

The start move fixes the things everything else depends on.

- **Build goal** names what the build is for. It selects the profile the finished build is scored against and the defaults in section 6.
- **Content mode** (casual, standard, hard mode) sets how severe each finding is, as [guides-to-engine.md](guides-to-engine.md) section 3.2 describes.
- **The build slot.** A character holds at most three builds. The session names which one it fills.

### 4.3 Level up

At each level the binary states what the game grants, which is one of two things:

- **A power pick.** The offer is every power the character can take now: its sets' powers at or under this level, pools within the four-pool limit, prerequisites met.
- **Slots.** The offer is every power already taken that has fewer than six slots.

The client answers with a move, and the binary applies the hard-limit rule that covers it. A refused move changes nothing.

**Epic archetypes: which build is being made.** An Arachnos Soldier or Widow follows the 1 to 24 path only once, on its first trip up the levels.
On that trip it picks from the base sets only, until the game forces a respec at 24 that opens the branch sets.
Every respec after that is free of the restriction, so branch powers can sit in the level 1 picks.
These are two different builds, and one session makes one of them. `start` asks which, and only for an archetype that has such a path:

- **The build after the respec.** This is the usual request, and the only one for a level 50 or exemplar build.
  The loop runs 1 to 50 with the branch sets offered from level 1. There is no respec step inside the loop.
- **The first-time levelling build.** The loop runs 1 to 24 with the base sets only and stops there. It is made only when the player asks for it.

The harness never joins the two into one plan, and it does not ask the question for other archetypes.
Whether Kheldians have an equivalent first-time restriction, and at what level, is to be read from the game data before `start` is built.
If they do, the same question applies to them; if not, a Kheldian's forms are ordinary picks.

**Each level is judged as it is made.** After every accepted move the binary works out the character at that level, so a weak level shows up while the build is being made:

1. The state at level N: powers usable, slots held, enhancements in them.
2. The set bonuses live at N, piece by piece: an attuned piece to the set minimum minus 3, a crafted piece to its own level minus 3,
    sets that ignore exemplaring always, procs only in usable powers, and the rule of five.
3. The expected survival at N against enemies of that level range in the session's content mode:
    chance to be hit times damage let through, against hit points; the hit chance of each attack; endurance against fight length.
4. The move and the level's figures go into the record.

Level by level, the loop looks like this:

```text
LEVEL N
  granted a power pick ---> offer the powers selectable now ---> pick_power
  granted slots ----------> offer owned powers with room ------> place_slots
        |
  legal? (pick level, prerequisites, pool limit, slot caps)
        | no: refused with the rule ID, nothing changes, offer again
        | yes
  state at N ---> set bonuses live at N ---> expected survival at N ---> record
        |
  next level, until 50 (or 24 for a first-time levelling build)
```

For a level 50 build the enhancements are chosen after the loop (section 4.4), so steps 2 and 3 run again over every level once they are placed.
That second pass is also what gives the character's state when exemplared to any level.

#### 4.3.1 The respec view

A player who asks for a respec build, or whose install preference turns off the leveling-first view, gets the same build through a different view.
The rules do not change: one file, legal under level-up rules, comes out of either view.

The two views were compared in Hate's planner on 2026-11-24 by making one Villain Fire Blast/Fire Manipulation Blaster both ways, with the same pick and slot priorities.

| | Leveling view | Respec view |
| --- | --- | --- |
| Power picks | One per screen, level by level, slots between | One per screen in level order, all 24 before any slot |
| Level 1 order | Primary, then secondary | Secondary, then primary |
| Picks and their levels | 24 powers at 1, 1, 2, 4, 6 … 47, 49 | The same 24 powers at the same levels |
| Slots | Placed at the level each is granted | All 67 on one screen, "Choose All Enhancement Slots", with no level shown |
| Slots per power at 50 | Fire Blast 6, Fire Ball 6, Stamina 4 … | The same counts |
| Slot levels in the saved `.mbd` | Where the player put them: Fire Ball 7, 9, 9, 11, 11 | Filled in by the planner in power order: Fire Ball 17, 17, 19, 19, 21 |

So the two views differ in one thing only: who decides the level of each slot.
In the respec view nobody does, and the planner fills the slot schedule in power order on export, which gives early powers their slots first whatever their worth.

The harness's respec view works like this:

- **Picks** run in level order through the same `offer` and the same refusals, one move each, as in the loop above.
- **Slots** are placed by count per power in one move after the last pick, refused when a power would pass six or the total would pass the schedule.
- **Slot levels** are still written to the file, because the file format and the leveling view need them.
  The binary assigns them by the client's slot priority: each slot, highest priority first, takes the first scheduled slot level at which its power is owned.
  This is what a player levelling with that priority would have done, and it keeps the file sensible if it is later opened in the leveling view.
- **Level 1** is written primary first, whatever order the client gave (#234).
- **Judging** runs at the target level only: level 50, or the exemplar levels of an exemplar build. The per-level survival check of the loop does not run.

What the player is shown:

- The picks as a list in level order: level, power, set.
- Slots and enhancements per power, without slot levels.
- The target-level figures and the gate's findings.
- One line saying the file holds a level for every slot, assigned from the slot priority, so the build opens in the leveling view too.

For an Arachnos Soldier or Widow the respec view is the natural one for the build after the respec, since that is how the game presents it.

#### 4.3.2 Levelling efficiency

A build made for levelling is played at every level it passes through, not only at 50.
Each pick and slot changes how the character plays while it earns the experience that brings the next pick or slot.
The leveling view in section 3.1 is that experience for a person: one screen per level-up, with totals at that level a click away.
What a person learns only by playing those levels, the harness states at each one.

For a session whose goal is levelling efficiency, the loop adds these to its per-level figures:

- **What the character can do at N:** the attacks it holds, the chain they make and the gap in it, damage per second against an even-level enemy, and how long a typical fight lasts.
- **Whether it can keep going:** endurance over that fight with the slots it holds, and the rest needed between fights.
- **What the move changed:** the same figures before and after the pick or slot, so its effect shows when it is made.
  Hasten at 6 instead of an attack leaves a gap in the chain until recharge is slotted; three Stamina slots at 13 instead of Fire Ball slots trade damage for fewer rests.
- **When it pays off:** a power whose worth needs slots it does not yet have, or a pick that opens a later one, is shown with the level at which it starts to pay.

Values at each level come from the game data's level tables, the same source as at 50.
The figures for every level go into the record.
A run reads back as the character's progress, and one request made with different choices can be compared level by level.
Enhancements for this goal are chosen per level, from what can be slotted at that level, rather than once after the loop.

### 4.4 Enhance

Enhancements are not chosen while levelling in practice: a level 50 build is slotted in one go. So this is its own phase, over the finished slot layout.

For a slot, the binary offers the enhancements the power accepts, less those a rule forbids (a unique already used, a mutex pair, a set piece already in this power).
The client's move names three things:

- **The piece.**
- **Its kind:** attuned, or crafted.
- **For a crafted piece, its level and its boost** (none to +5).

The binary refuses a kind the data does not allow: an attuned-only piece cannot be crafted, an attuned piece cannot be boosted, a crafted level must lie in the set's range.
Slotting warnings (P-ED, P-SET and the rest) come back with the accepted move, so a client sees them at the slot that caused them.

Section 6 gives the defaults by build goal.

### 4.5 Incarnate

Open only for a level 50 build. One move per slot: Alpha, Judgement, Interface, Lore, Destiny and Hybrid.

- **The offer** is the powers of that slot, each with its tier.
- **Rules the binary applies:**
  - Incarnate powers count only at level 45 and above, so a build whose goal is exemplared play must not depend on one.
    The binary scores such a build without them and says so.
  - Content rules that forbid a slot's use, such as Lore pets in four-star content, make "none" the only accepted choice for that content mode.
- **What stays a preference, not a rule:** which Destiny a team wants, which Alpha covers the build's largest gap.
  These come from the build goal's profile and the guides, and a client may choose against them.
- **An explicit "none" is a choice.** A slot left unanswered keeps the phase open.

### 4.6 Finish

The client asks to finish; it decides nothing.

1. The binary writes the `.mbd` from the state.
2. It runs every registered evaluation step in order (legality, hard limits, slotting, totals and metrics, attack chains; later the exemplar totals and the survivability figures).
3. It writes the result file: each step's output, the build file's hash, the data version, the engine version, and the move list.
4. It returns the result. A build with an error-severity finding is returned as failed, with the file kept for inspection and marked as not deliverable.

The separate `gate` command checks a build file against its result file later, for example in CI.

## 5. The moves

Each move is an MCP tool and a command, with the same result shape the server uses today.

| Move | Phase | Input | Returns |
| --- | --- | --- | --- |
| `start` | Start | Archetype, primary, secondary, goal, content mode, build slot; for an archetype with a first-time path, which of the two builds | A session and the first offer |
| `offer` | Any | Session | What the current step allows: powers, slots, enhancements or incarnate powers |
| `pick_power` | Level up | Session, power | The new state, or a refusal |
| `place_slots` | Level up | Session, powers to receive the granted slots | The new state, or a refusal |
| `respec` | Any closed build | A finished build's move list | A new session from level 1 for the same character, free of any first-time restriction |
| `set_enhancement` | Enhance | Session, power, slot, piece, kind, level, boost | The new state with any slotting warnings, or a refusal |
| `set_incarnate` | Incarnate | Session, slot, power or "none" | The new state, or a refusal |
| `undo` | Any open phase | Session | The state before the last move |
| `state` | Any | Session, optional exemplar level | The build so far, as it stands at that level |
| `finish` | Finish | Session | The build file path, the result file, pass or fail |
| `replay` | Start | A move list | The same state and files as the session that recorded it |

A refusal carries the rule ID of the check that refused it (for example H-POOL-001 for a fifth pool) and the text the rule already prints.

## 6. Attuned, crafted or boosted, by build goal

The choice of kind is a property of the goal, so the binary can supply it as a default and flag a departure.

**The facts it rests on** (sources in [exemplar-10.md](../style-guides/build-rules/exemplar-10.md) and [survivability-planning.md](../survivability-planning.md)):

- A boost raises a piece's own enhancement values by 5% for each booster, 25% at +5. It does not raise a global bonus, a set bonus or a proc.
- An attuned piece cannot be boosted. Superior archetype and Winter pieces exist only as attuned.
- A crafted piece keeps its set bonus and any global down to three levels below the piece's own level. An attuned piece keeps them down to three below the set's minimum level.
- Purple, PvP, archetype and Winter sets keep their bonuses at every level.
- A proc works at any piece level while its power is usable.
- Exemplared to 45 or below, each enhancement value is capped at 41.5% before it is scaled, so a +5 boost on a single-aspect piece gains nothing there.
- A boost's best use is to let fewer pieces hold a power's value. On top of a full set, Enhancement Diversification removes most of it.

**Defaults by goal:**

| Piece | Level 50 content only (hard mode, farming) | Exemplared play (task forces at 10 to 45) | General play at every level |
| --- | --- | --- | --- |
| Sets that ignore exemplaring | Crafted 50 and boosted where the data allows | First choice wherever they fit; crafted 50, boosted | First choice wherever they fit |
| Regular set pieces | Crafted 50, boosted where the boost survives Diversification | Attuned | Attuned |
| Global and unique pieces | Crafted 50 | Attuned, or crafted at the set's lowest level | Attuned, or crafted at the set's lowest level |
| Procs | Any level | The lowest level | The lowest level |
| Common IOs | Crafted 50, boosted | Crafted 50; boost only multi-aspect value | Crafted 50 |
| Boosting | Yes, where it frees a slot or raises a lightly slotted power | Little to gain below 46 | Where the character spends most of its time at 50 |

These defaults come from measurements on Arachnos Soldier builds in November 2026. The finish step's evaluation is what confirms them for any one build:
the exemplar totals show what a crafted piece costs below its level, and the level 50 totals show what a boost adds.

**How the binary uses the table:**

- `offer` in the enhance phase marks the default kind for each piece under the session's goal.
- `set_enhancement` accepts a departure and returns an information finding that names what it costs, so the choice is visible in the result file.
- A goal may be split: a character's three builds can hold one for level 50 content and one for exemplared play, each with its own defaults.

## 7. The record

- **The move list is written into the result file** and can be saved beside the build.
- **`replay` is the test of repeatability.** The same move list against the same data version yields the same build file and the same result file.
- **A revision is a new move list.** `undo` and `respec` edit the session; the finished build replaces the earlier file for that build slot. Git keeps the history.
- **A data refresh is visible.** A move list recorded against an older data version replays with a warning, and any move the new data refuses is reported at that move.

## 8. Evals

The harness makes three things measurable that are opinions today.

| Measure | What it shows | Target |
| --- | --- | --- |
| Share of moves accepted on the first attempt | Whether the client understands the game's rules | Above 95% |
| Share of sessions that finish with no error-severity finding | Whether the client can complete a legal build | Above 95% |
| Evaluation steps skipped | Whether the process was followed | Zero, by construction |
| Score of the finished build against a reference build for the same request | Whether the build is good | Reported per goal; no single target |
| Spread of scores across repeated runs of one request | Whether the outcome is repeatable | Reported; narrowing over time |
| Replay equality | Whether the record is complete | Always equal |

**The eval set** is a list of requests, one per archetype and focus in `samples/builds/reference/`.
Examples are a ranged Blaster and a blapper, each Kheldian form mix, and each Arachnos Soldier branch.
Each request has a reference build that passes the gate. A run plays every request through the harness and reports the table above.

**The level-up exercise** is one eval run the way a player levels: one screen at a time from level 1 to 50, with a stated reason for every power, pool, epic and slot choice, read against the build's goal.
A run that picks the first legal option on each screen passes the rule checks and still fails this eval, because its reasons are missing or do not match its choices.
For a levelling-efficiency goal the exercise is scored on the per-level figures of section 4.3.2 as well as the finished build.
A scripted first-in-list walk through Hate's planner on 2026-11-24 showed the failure: every slot went to the first power that could take one, and the walk stalled once that power was full.

**View equivalence** is a second check: one request made in the leveling view and in the respec view with the same choices.
Both files must pass the gate and match in powers, pick levels and slots per power; they may differ only in slot levels.

**What the evals cannot say:** whether a build is fun, or right for one player. Combat logs remain the check on whether a planning figure matches play.

## 9. How it fits the existing plan

| Existing item | Relation |
| --- | --- |
| Writer gate, [guides-to-engine.md](guides-to-engine.md) 7.3 | The finish phase is that gate. The session's goal and content mode are the "playstyle choice" it takes |
| The `.mbd` writer, #44 (delivered) | `finish` writes the build through it; the move list is the level-by-level record it emits |
| `validate` on a `.mbd` file, #45 (delivered) | Legality, hard limits and slotting already run on a build file alone |
| Totals from a build file alone (no issue yet; the move to game data is planned in `docs/engine/mids-4-alignment.md`) | Needed for `finish` to compute totals in the binary. Until then `finish` calls the Mids dump harness where one is available |
| Slotting optimizer, #21 | A proposer of moves. It plays through the same session, so it cannot bypass a rule |
| Intent checks, [guides-to-engine.md](guides-to-engine.md) 7.2 | Must-take and skip findings come back with `pick_power`, at the pick that caused them |
| Exemplar rule by measurement, #64 | Decides which exemplar rule the `state` move and the exemplar totals apply |
| Archetype-specific rule layer, #35 | What `start` checks: the declared archetype and its powersets |
| An evaluate command and a delivery gate, #231 | `finish` runs the first; CI runs the second |

## 10. Phased plan

1. **Evaluate and gate.** One command that runs every registered step and writes the result file; a gate that rejects a missing, stale or incomplete one. Small, and it stops skipped steps at once.
2. **Session and level-up moves.** The build state, `start`, `offer`, `pick_power`, `place_slots`, `undo`, `state`, with the hard-limit rules applied per move.
    Tested by replaying the committed sample builds.
3. **The epic archetypes' two builds.** The question at `start`, the base-sets-only path to 24, and the branch sets from level 1 for the build after the respec.
4. **Enhance moves.** `set_enhancement` with kind, level and boost; the legality rules per move; the goal defaults from section 6.
5. **Incarnate moves.**
6. **Finish and replay.** The binary writes the build and the result file; `replay` reproduces them.
7. **Evals.** The request list, the reference builds and the report.

Each phase is one or more issues with tests, and each leaves the binary usable.

## 11. Risks and open questions

- **Totals need a Mids dump today.** The tools that compute from power effects refuse a `.mbd` file (E180), so `finish` cannot score a build inside the binary alone.
  This is the largest dependency, and it has no issue yet.
- **Legal is not good.** The harness guarantees a build the game accepts. Quality still depends on the evaluation steps and the slotting rules, and those are only as good as their data.
- **The defaults in section 6 come from one archetype.** They need the same measurements on others before they are more than defaults.
- **Session state.** An MCP server is asked one call at a time. Where a session lives between calls (a file the client names, or state the server keeps) is undecided.
- **How much a client sees.** A long offer list at every level costs tokens. The offer may need filtering by the goal's profile, without hiding legal choices.
- **Kheldians.** Whether they have a first-time path like the Arachnos archetypes' is not yet read from the data.
- **Incarnate rules by content.** Which incarnate choices a content mode forbids is known for four-star Lore pets and not catalogued otherwise.
