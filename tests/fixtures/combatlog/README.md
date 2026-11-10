# Combat-log fixture

`sample-trimmed.log` is 185 lines of Homecoming combat log, trimmed to cover every event class the classifier knows, the ignored classes and two unknown lines.
The tests read it through `CombatLogReader` and `CombatLogClassifier` (`tests/CohEngine.Core.Tests/CombatLog/`).

## Where the lines come from

Most lines are copied from the client's own `/chatlog` output for 2026-11-05 and 2026-11-07, one line each, in file order.
Every character name and global handle is replaced before the line reaches this directory: the log owner is `Player`, other players are `Ally` and `Ally Two`.
Foe names (`Big Mob Minion`, `Big Mob Boss`) and pet names come from the game and stay.

A few lines are rebuilt in the shape of a real line from other days, because the trimmed day held no example:

- two `You are no longer held.` and `You are no longer afraid.` status-end lines;
- the `level 41` line;
- two `granting them` endurance lines (`Ally`, `Ally Two`);
- the `Player HITS you!` and `HIT Player!` Hasten pair, the self-targeted case;
- the two unknown lines, `You increase your regeneration rate and gain an endurance discount.` and `Your Energy Drain increases your defense!`, which are real lines no rule claims;
- the last two lines: a chat line written for this fixture, and a continuation line with no timestamp.

## What the file must hold

The test `SampleLogTests` fails if any of these drift:

- every `CombatEvent` subtype appears at least once;
- exactly two lines are `Unknown`, the two above;
- the `Your Tactics increases your team's chance to hit and Perception.` line classifies as `Ignored`.
