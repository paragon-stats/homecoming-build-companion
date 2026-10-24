# Chain planner: how the engine picks an attack chain a build can afford

The chain planner answers one question about a build: what is the best repeatable attack chain this character can actually afford?
Raw damage alone doesn't settle that. Three more things constrain a chain:

- **The endurance budget.** A chain that drains faster than recovery eventually empties the blue bar.
- **Where the character stands.** A ranged character must close the distance before a melee attack or a point-blank AoE.
- **Damage that runs beside the chain.** Auras, patches, pets, click buffs, target debuffs and AT inherents all add to, or scale, what the chain deals.

The planner is the engine's own model, not a port. MidsReborn's DPS calculator (`frmDPSCalc.cs`) fires one greedy chain.
It reports damage and endurance per second, but never reads recovery, toggle drain, max endurance or position.
Run it with `python -m coh_engine chain <build_dump>`. Tracked in #67, #68 and #69.

## The endurance budget

The budget is a pool, an income and a committed drain:

- **Pool:** 100 plus `Totals.EndMax` (`Statistics.cs:31`), clamped at the AT's hard cap from canonical `attrib_max_max` (365 endurance for every playable AT).
- **Income:** recovery per second (`Statistics.cs:33`). It scales with max endurance, so a permanent pool increase raises the tick too.
- **Committed drain:** the running toggles (`Totals.EndUse`).

A chain adds its own drain: its endurance cost over its cycle time.
When net income is negative, the budget reports a time to empty: pool ÷ deficit.
Health is reported beside it: max HP, AT-capped, and HP regenerated per second (`Statistics.cs:51`), which scales with max HP in the same way.

## Attack types and stance

Each damaging power is classified once from canonical data into a delivery, a shape and a role.

| Field | Values | Decided by |
| --- | --- | --- |
| Delivery | melee, ranged | `range` at or under the melee threshold (`melee_range_ft_max`, 20 ft) |
| Shape | single, cone, targeted AoE, point-blank AoE, location | `effect_area` and `range` |
| Role | chain, aura, patch, pet summon, buff click | entity creation, power type, foe damage, self damage buff |

The power's set categories cross-check the verdict. A melee verdict on a power whose sets are all ranged is reported, not silently accepted.

The planner evaluates each chain under a declared stance:

- **Melee:** the character stands in melee. Every attack is usable, and nothing costs movement.
- **Ranged:** the character stands off at range, so melee attacks and point-blank AoEs drop out.
- **Hybrid:** the character opens at range, closes in for the melee group, and steps back out.

A hybrid chain that mixes positions pays two switches per cycle.
Each switch takes the standoff distance (`ranged_standoff_ft`, 40 ft) divided by the build's own speed for the declared travel mode.

## Choosing the chain

Each attack fires once per cycle. The planner tries every subset of the chain attacks:

```text
cycle = max(Σ arcanatime + switches × switch_time, max(arcanatime_i + recharge_i))
```

Movement time also covers recharge, and it costs no endurance. A hybrid chain is therefore slower than a melee one but cheaper to run.

A chain is **affordable** when it never drains, or when it runs `chain_min_cycles` (4) full cycles before the pool empties.
Most play is on a team, where recovery buffs and endurance refills cover the rest.
So the test is whether the chain survives a few rotations, not whether it runs forever solo.
Override it with `--assume chain.min_cycles=3`.

Chains are ranked in this order:

1. Affordable first.
1. Then higher DPS, a smaller gap, and fewer attacks.

Ranking "never drains" first would pick a near-idle chain whenever toggles already take most of recovery.
The planner keeps the best affordable, best sustainable and best overall chains, so the trade between damage and endurance stays visible.
P-END-002 warns when no chain is affordable.

## Damage beside the chain

| Layer | Kind | Uses cast time | Notes |
| --- | --- | --- | --- |
| Chain | source | yes | Runs last, on the timeline the other layers leave |
| Auras | source | no | Count only while in melee; the toggle cost is already in the drain |
| Patches | source | cast ÷ cycle | Placeable at range; fired on cooldown |
| Pets | source | cast ÷ recharge (timed), none (permanent) | Each pet's own chain is planned with the same planner |
| Buff clicks | source and modifier | cast ÷ cycle | Additive damage buff, uptime weighted; self only, so pets are unaffected |
| Target debuffs | modifier | from the host attack | Multiplies every layer, pets included |
| AT inherents | modifier | no | One rule per playable AT; neutral ones are stated explicitly |

Damage buffs are additive in the game: they join the enhancement and global buffs already inside a power's damage.
So an additive modifier scales damage by `1 + bonus × uptime ÷ baseline`, where the baseline is 1 plus the damage strength already applied.
True multipliers such as Containment, critical hits and resistance debuffs scale damage directly.

## Inputs

- **Damage per activation** is MidsReborn's own figure, read from the dump.
    It stands in for the faithful `FXGetDamageValue` port (#31).
    It comes from the dump's clicks-off variant, because Mids counts every included click buff as permanently on.
- **Attack geometry** comes from canonical power data.
- **Caps** come from canonical archetype data.

## Limits

These are the model's known simplifications, not defects. Each one is labelled in the planner's output wherever it applies:

- A chain fires each attack once per cycle, so a chain that repeats a fast filler (A B A C) is undercounted.
- Movement is a straight line at full speed. Gap-closing attacks and knockback chasing are not modelled.
- Buffs and debuffs are assumed fired on cooldown, even when the chosen chain fires them less often.
- Aura damage assumes the Mids figure for a toggle is per tick. A damage-aura fixture has not yet confirmed this.
- Area attacks hit min(target cap, foes in the scenario). Cone and sphere geometry is not modelled.
- Pet AI, accuracy and endurance are not modelled.
    Mastermind henchman damage stays unverified, because the Mids henchman damage table reads roughly six times the normal pet value.
- Several AT inherents carry no magnitude in canonical data. Their defaults are labelled "approximate; verify in MidsReborn" and can be overridden with `--assume`.
- Misses are not weighted yet. An attack's endurance is paid whether it hits or not.
