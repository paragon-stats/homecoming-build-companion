# Rule: Slotting efficiency

**Status:** SHOULD. These rules find slots that earn little and slots that a better piece would earn more from.
`coh-engine validate <build> --slotting` and the `check_slotting` MCP tool report them. Default `validate` does not.
A finding is resolved by a fix or recorded as a trade-off in the build's "Slotting rules applied" section.

## Sets outrank diversification

Five or six pieces of a Very Rare, Winter or archetype set are the preferred use of a power's slots.
Their set bonuses are the more useful mechanic, and enhancement value lost to diversification inside such a set is not a fault.
The diversification rules apply to plain IOs only, and no rule here counts against a full set.

## Enhancement Diversification (P-ED-001, P-ED-002; warning)

Enhancement Diversification (ED) cuts the value of each aspect, per power, past three thresholds: 70%, 90% and 100% on schedule A.
Past the last threshold a slot counts at 15% of its value.
Hasten, Stamina and any power holding several IOs of one aspect are where it bites.

- **P-ED-001.** A plain IO is past the last threshold: the power's other slots already carry the aspect beyond 100% before it.
  Set pieces, Single Origin and Hamidon enhancements are never flagged, but their value counts toward the total.
- **P-ED-002.** Three copies of one plain IO that two copies boosted to +5 would match within five points.
  Remove the last copy and boost the others with Enhancement Boosters.

Hasten holds at most two Recharge IOs, boosted to +5.

| One power, level-50 IOs (Recharge in Hasten, or Endurance Modification in Stamina) | Before ED | After ED | Finding |
| --- | --- | --- | --- |
| 2 plain IOs | 84.80% | 83.32% | none |
| 2 plain IOs boosted +5 | 106.00% | 95.90% | none |
| 3 plain IOs | 127.20% | 99.08% | P-ED-002 on slot 3 (3.18 points over 2 boosted) |
| 4 plain IOs | 169.60% | 105.44% | P-ED-001 on slot 4 (adds 6.36) |
| 5 plain IOs | 212.00% | 111.80% | P-ED-001 on slots 4 and 5 |
| Stamina with the five Performance Shifter pieces and Endurance Modification | 116.60% | 97.49% | none (set pieces) |
| The same plus one plain Endurance Modification IO | 159.00% | about 103.85% | P-ED-001 on the IO (adds 6.36) |

The check does not weigh a set bonus against a slot; that comparison needs build totals (#21).

## Luck of the Gambler (P-SET-002; warning)

A lone Luck of the Gambler recharge piece earns only its global.
When the build holds slots that diversification wastes, a second piece earns the set's 2-piece Regeneration bonus and a third its Hit Points bonus.
Keep the piece alone only when slots are tight.
A cheap toggle is not an exception: its endurance cost is not a condition.

## Resistance uniques (P-IO-002; warning)

A resistance toggle that holds only generic IOs, while the build lacks Steadfast Protection or Gladiator's Armor, is the home of the missing unique.
Tough and the archetype's armor auto power both hold them.

Author guidance, no registered check: place each unique in the power whose level keeps it active where it matters.
A unique in a level 1 armor power works at every exemplar level; one in Tough is lost below Tough's pick level.

## Survivability uniques (P-IO-003; info)

Health takes Numina's Convalesence, Miracle, Panacea, Preventive Medicine and Regenerative Tissue, once each.
An always-on power fires a chance-based piece more often than a long-recharge click.
Fill Health with them before a luxury sixth slot elsewhere.
Take the slots from plain IOs or from a common set's last piece, not from a Very Rare, Winter or archetype set.

## Quality-of-life picks (P-POWER-001; warning)

A movement-and-recharge auto power (Mental Training, Quickness, Lightning Reflexes, Athletic Regulation, Particle Acceleration) is picked at the level it opens.
An exemplared task force then keeps it: a character exemplared to level N keeps powers picked at or below N + 5, and a later pick leaves the levels in between without it.
The message names that range of levels when it exists; for a power that opens at a low level and is picked a few levels late there is none, and the message says only that the pick is late.
See [exemplar-10.md](./exemplar-10.md).

## Rarer sets first (P-SET-003; info)

A set whose every piece is unique is rarer: the Very Rare, Winter and archetype sets and their Superior versions.
When a power holds two or more pieces of a lower-tier set while a rarer set of the same category is slotted nowhere in the build, the rule points at the candidate.
Two all-unique sets are not top tier and are left out: Overwhelming_Force and Attuned_Cupids_Crush.
The message lists the Superior sets first, then the rest, each in uid order, so the strongest candidates head the list.
The comparison itself needs build totals (#21), so the rule only names the set.
Archetype and Winter set advice beyond that is author guidance, with no registered check (#35).

## Permanent Hasten

Author guidance, no registered check: report Hasten's recharge against its duration as a fact.
Do not give up defense, resistance, hit points or survivability pieces to make it permanent.
When slots are short, spend them in this order:

1. Positional defense.
2. Resistance.
3. Hit points, regeneration and survivability pieces.
4. Hit chance.
5. Endurance.
6. Damage and recharge.
