# Combat log: how the engine reads the client's own record of a fight

The Homecoming client writes a text log of every fight to the player's own disk when the player runs `/chatlog`.
The engine reads that file as a third source of truth beside the Python port and the MidsReborn reference dumps.
The two existing sources are models. The log holds what the server computed.

Reading the log is the only sanctioned route to this data.
The Homecoming Terms of Service bar programs that read the game client's memory, so the engine never does.
Tracked in #62.

## Where it lives

The code is `src/CohEngine.Core/CombatLog/`. It depends on nothing else in `CohEngine.Core`, and nothing depends on it yet.

| Type | Job |
| --- | --- |
| `CombatLogReader` | Splits a file into `CombatLogLine` records: position, timestamp, message body, channel, raw text. |
| `CombatLogClassifier` | Turns one `CombatLogLine` into one `CombatEvent`. |
| `CombatEvent` | The result. Its `Outcome` is `Recognised`, `Ignored` or `Unknown`, and it always keeps the line it came from. |

The rules are literal matches on the game's English line shapes, written with span slicing. There are no regular expressions.
Numbers use the invariant culture: the game writes a dot for decimals and commas for thousands.

## Line format

Every line is `YYYY-MM-DD HH:MM:SS` and a space followed by the message body.
Timestamps have one-second resolution, so the order of events within a second is lost.
Chat lines carry a `[Channel]` tag. Combat lines do not, so a parser keyed on a channel tag would drop every combat event.
A chat message that wraps leaves continuation lines with no timestamp.

A line that starts like a timestamp but holds no real date and time raises `E151`.
A file that cannot be opened raises `E150`.
A line that is only a timestamp (19 characters or fewer) is kept as a continuation rather than raising `E151`.
One malformed timestamped line ends the whole enumeration with `E151`, by design: the file is corrupt, not the line.

## The taxonomy

Every recognised class is a sealed record under `RecognisedEvent`. Damage, heals and rolls keep the decimals the log prints.

| Event | Line shape | Fields beyond the line |
| --- | --- | --- |
| `AttackRoll` | `HIT <target>! Your <power> power had a X% chance to hit, you rolled a Y.` | Direction, counterpart, power, hit, chance, roll, `Autohit`, `ForcedByStreakbreaker`, `IsSelf`, origin |
| `DamageHit` | `You hit <target> with your <power> for N points of <Type> damage.` | Direction, counterpart, power, amount, type, `IsDot`, `IsProc`, modifier, origin |
| `Heal` | `You are healed by your <power> for N health points.` | Healer, recipient, power, amount, `IsOverTime` |
| `EnduranceChange` | `Your <power> grants you N points of endurance.` | Kind, counterpart, power, amount, `IsOverTime` |
| `BuffApplied` | `Your <power> has <effect>.` | Power, effect text |
| `DebuffApplied` | `You Stun <target> with your <power>.` | Status, target, power |
| `EffectExpired` | `You are no longer <status>.` | Status |
| `Defeat` | `You have defeated <foe>` | Defeater (null for the log owner), victim |
| `ExperienceGained` | `You gain N experience and N influence.` | Experience, debt worked off, currency amount, currency name |
| `LevelUp` | `Your combat improves to level N! Seek a trainer to further your abilities.` | Level |
| `PowerActivated` | `You activated the <power> power.` | Power |
| `PowerRecharged` | `<power> is recharged.` | Power |

The table shows one shape per class. The classifier also reads these variants:

- `AttackRoll`: the `MISSED <target>!! Your ...` form; `<foe> HITS you! <power> power had a X% chance to hit and rolled a Y.`; `<foe> MISSES! <power> power had a X% chance to hit, but rolled a Y.`.
  An autohit ends `power is autohit.` or `power was autohit.`, and a forced hit ends `power was forced to hit by streakbreaker.`.
- `DamageHit`: `<source> hits you with their <power> for ...`, an `over time` suffix, and a parenthesised modifier such as `(JUDGEMENT CRITICAL)`.
- `Heal`: `You heal <target> with <power> for ...` and `<healer> heals you with their <power> for ...`, each with an optional `over time`.
- `EnduranceChange`: `<giver> hits you with their <power> granting you N points of endurance` and `Your <power> drains N points of your endurance.`
  The two target forms are `You hit <target> with your <power> for N points of their endurance` and `... granting them N points of endurance`.
  Kind is `Gained`, `Spent`, `DrainedFromTarget` or `GrantedToTarget`.
- `DebuffApplied`: the verbs Taunt, Immobilize, Stun, Hold, Sleep, Confuse, Terrorize and Placate.
- `Defeat`: `<player> has defeated <foe>`.
- `ExperienceGained`: `, work off N debt` before the currency, and `infamy` in place of `influence` for villains.

Four points about the fields:

- **Autohit and streakbreaker.** `Autohit` and `ForcedByStreakbreaker` mark a hit that was not a roll.
  A consumer building to-hit samples excludes them rather than counting a hit with no chance behind it.
- **Self-targeted lines.** The log never says whose it is.
  Pass the character name to `CombatLogClassifier` and a roll whose counterpart is that name gets `IsSelf`.
  That separates `HIT <own character>! Your Hasten power is autohit.` from an attack.
- **Procs.** A proc names its power `<Full Set Name>: <Piece Name>`, a third naming scheme beside the internal and display names. `IsProc` is set from that shape.
- **Origin.** A pet or effect line starts `<Origin>:` and two spaces.
  The prefix is kept as `Origin` and the rest of the line is matched as usual.

The log holds no expiry line for a buff or a toggle. `EffectExpired` covers the status endings the game does print, such as `You are no longer held.`
The log also never prints current or maximum endurance, so endurance totals can only be summed from the change lines, never read.

## Three outcomes

Every line gets exactly one.

- **Recognised.** A taxonomy event, as in the table.
- **Ignored.** Known and deliberately unused. `IgnoredEvent.Reason` says why.
- **Unknown.** No rule claimed it. `UnknownEvent` keeps the raw text and the line number.

An explicit ignore class matters because without it "unmatched" and "safe to skip" look the same, and a coverage gap is invisible.
The unknown bucket is the coverage report: a run over a captured log lists its unknown lines, and a new game message shows up there first.

## Rule order

The first matching rule wins, in this order:

1. A line with no timestamp is ignored as a continuation.
2. A line with a channel tag is ignored as chat, even when its text looks like combat.
3. The `Origin:` prefix and its two spaces is split off.
4. The ignore list below.
5. Attack rolls, then damage and endurance drains, then self endurance, heals, buffs, debuffs, expiries, defeats, experience, level-ups, and activations and recharges.
6. Anything left is unknown.

The ignore list runs before the event rules so a line that reads like an event is never taken for one.
The case that forced this is `Your Tactics increases your team's chance to hit and Perception.`
It names a chance to hit but carries no roll, and it appears in real captures.
Read as a roll it would corrupt every to-hit sample.

## The ignore list

| Match | Reason |
| --- | --- |
| contains `increases your team's chance to hit` | Team to-hit notice. It names a chance to hit but carries no roll. |
| starts `You are no longer seeking a team` | Team-search status, not a combat effect. |
| starts `Readying` and a space | Power-queue echo. |
| starts `Shutting off` and a space | Toggle echo. |
| starts `Not enough endurance to use` and a space | Refusal echo; no power fired. |
| ends the text `missed!` after a space | Repeats the `MISSED` roll line that follows it. |
| starts `You received` and a space | Loot and rewards; no consumer needs them yet. |
| starts `You paid` and a space | Market fee; no consumer needs it yet. |
| starts `You reduce your targets damage resistance` | Resistance-debuff echo; carries no numbers. |
| starts `You knock` and a space or contains the text `knocks you off your feet` after a space | Knockdown echo; the roll line before it carries the data. |
| starts `You contaminate` and a space | Status echo; carries no numbers. |
| contains `Fury from` with a space on each side | Fury gauge change; not a combat result. |
| contains the text `points of absorption` after a space | Absorption shield; not in the taxonomy until a consumer needs it. |
| starts `You're too far away to use` and a space | Refusal echo; no power fired. |
| ends the text `is still recharging.` after a space | Refusal echo; no power fired. |
| starts `Joined channel` and a space | Chat-system notice. |
| starts `Using global chat handle` and a space | Chat-system notice. |
| starts `Your friend` and a space | Friend-list notice. |

Against about 293,000 lines of real captures from several characters, under 2 percent classify as unknown.
The largest remaining groups are buff messages with no power name, such as `Your Energy Drain increases your defense!`, and `You terrify ...` lines.
Those stay unknown until a consumer needs them.
Adding one means adding a rule and an exemplar line to the sample log.

## The sample log

`tests/fixtures/combatlog/sample-trimmed.log` is 185 lines trimmed from real captures, with every character name replaced.
Its README says which lines were rebuilt and why.
`SampleLogTests` holds it to the taxonomy: every event class appears at least twice, and the only unknown lines are the two planted ones.

## What comes next

Issue #63 replays captured combat logs against the engine to re-validate the delivered checkpoints.
It reads the events this module produces and reports the unknown count as coverage.
Joining parsed power names to `data/canonical/` waits for the internal-to-display resolver in #48, because the log holds display names only.
