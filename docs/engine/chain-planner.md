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
| Role | chain, aura, patch, pet summon, buff click, support, persistent debuff | entity creation, power type, foe damage, self damage buff, target -Res |

The power's set categories cross-check the verdict. A melee verdict on a power whose sets are all ranged is reported, not silently accepted.

The classifier follows `Execute_Power` and `Create_Entity` links through canonical data, so a power that only triggers or summons another power is classified by what that power does.
Effect direction is decided by the effect's table name, not by the sign of its magnitude.
A -Res debuff on foes and a +Res buff on the caster are told apart by the table, because a negative number alone does not say who it lands on.

A power with no effects of its own, such as a Kheldian blast or a Stalker snipe, is classified through its `redirect`.
The planner takes the redirect that applies with no mode active: `Always`, no condition, or a negated mode.
A redirect gated on target state or distance cannot be read statically and is not followed.
A click that damages on activation is a chain attack even when it also spawns an entity, such as Chain Induction's jump.

Canonical effects are read for PvE.
A PvP-only group never counts, and a group gated only on the target being a critter always counts.
Phantom Army's decoys, for example, net their healing-back damage this way.
Templates under a costume condition are alternates, so a summon counts only the largest group of them.
A pet gated on the caster's level spawns only once the caster reaches that level.

The Mids dump does not carry a -Res debuff that lives on a summoned entity, such as Freezing Rain's or Acid Mortar's.
For a patch, support or persistent-debuff power whose dump value is 0, the planner reads it from canonical data for the build's archetype.
It takes the weakest of the eight core damage types, at level 50, held for the entity's lifetime.
Without `--mids-db` the power is reported in the skipped list rather than left at 0.

Two roles cover powers that deal no damage themselves:

- **Support** is a non-damaging click or location power that buffs the character's damage or puts -Res on foes, such as Melt Armor or Siphon Power. It is woven in on cooldown.
- **Persistent debuff** is a toggle or auto power that holds -Res on foes, such as Enervating Field. It runs beside the chain and multiplies every layer.

A persistent debuff that is a point-blank AoE toggle is gated by stance: melee counts it fully (1), ranged not at all (0), and hybrid uses the rotation's melee share.
Self and team damage-buff toggles get no role, because Mids already counts them in every attack's damage.

The planner evaluates each chain under a declared stance:

- **Melee:** the character stands in melee. Every attack is usable, and nothing costs movement.
- **Ranged:** the character stands off at range, so melee attacks and point-blank AoEs drop out.
- **Hybrid:** the character opens at range, closes in for the melee group, and steps back out.

A hybrid chain that mixes positions pays two switches per cycle.
Each switch takes the standoff distance (`ranged_standoff_ft`, 40 ft) divided by the build's own speed for the declared travel mode.

## Forms

Some archetypes change form, such as the Kheldian Nova and Dwarf forms.
A form power carries a required mode in canonical data, and only attacks whose mode matches are usable while in that form.
The planner runs each stance once per form: the base (human) form, then every mode that some attack requires.
Each run plans its own chain, endurance and diagnostics.

Text output labels a base block `== melee stance ==` and a form block `== melee stance, Nova form ==`.
JSON carries a `form` key, empty for the base form.
`--form NAME` restricts the run to one form, and `--form ''` selects the base form.
An unknown form is `E65` and names the forms the build has.
When every attack is form-locked, the base-form block reports `rotation: none`.

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
1. Then higher debuff-adjusted DPS, DPS × (1 + stacked −Res), a smaller gap, and fewer attacks.

Ranking "never drains" first would pick a near-idle chain whenever toggles already take most of recovery.
The planner keeps the best affordable, best sustainable and best overall chains, so the trade between damage and endurance stays visible.
P-END-002 warns when no chain is affordable.

## Damage beside the chain

| Layer | Kind | Uses cast time | Notes |
| --- | --- | --- | --- |
| Chain | source | yes | Runs last, on the timeline the other layers leave |
| Auras | source | no | Count only while in melee; the toggle cost is already in the drain; the hybrid share comes from the rotation |
| Patches | source | cast ÷ cycle | Placeable at range; fired on cooldown |
| Pets | source | arcana cast ÷ (arcana cast + recharge) (timed), none (permanent) | Each pet's own chain is planned with the same planner |
| Buff clicks | source and modifier | cast ÷ cycle | Additive damage buff, uptime weighted; self only, so pets are unaffected |
| Target debuffs | modifier | from the host attack | Multiplies every layer, pets included |
| AT inherents | modifier | no | One rule per playable AT; neutral ones are stated explicitly |

Damage buffs are additive in the game: they join the enhancement and global buffs already inside a power's damage.
So an additive modifier scales damage by `1 + bonus × uptime ÷ baseline`, where the baseline is 1 plus the damage strength already applied.
True multipliers such as Containment, critical hits and resistance debuffs scale damage directly.

Under the hybrid stance an aura works only while the character stands in melee, so its share comes from the chosen rotation:

- No melee-position attack in the rotation: the share is 0.
- Only melee-position attacks: the share is 1.
- A mix: the share is the time in melee over the cycle, the arcana cast times of the melee-position attacks plus half the move time, divided by the cycle time.
- `--assume hybrid.melee_share=N` overrides the derived share when it is given.

A patch that deals no damage, such as a decoy, takes no timeline share or endurance here; debuff patches are modelled separately.

## Inputs

- **Damage per activation** is MidsReborn's own figure, read from the dump.
    It stands in for the faithful `FXGetDamageValue` port (#31).
    It comes from the dump's clicks-off variant, because Mids counts every included click buff as permanently on.
- **Attack geometry** comes from canonical power data.
- **Caps** come from canonical archetype data.

A power the model leaves out is never dropped silently.
A power with no canonical file, or one the classifier rejects as not an attack, raises an info diagnostic `P-CHAIN-002` naming the power and the reason.
Virtual inherents and incarnate powers are the exception: they are expected to be absent.

## Limits

These are the model's known simplifications, not defects. Each one is labelled in the planner's output wherever it applies:

- A chain fires each attack once per cycle, so a chain that repeats a fast filler (A B A C) is undercounted.
- Movement is a straight line at full speed. Gap-closing attacks and knockback chasing are not modelled.
- Click buffs are assumed fired on cooldown.
    A chain attack's debuff counts only when the attack is in the chosen chain, once per cycle.
    Ranking scores debuff-adjusted DPS against a 0%-resistance target, so it undervalues debuffs against resistant ones.
- Aura damage assumes the Mids figure for a toggle is per tick. A damage-aura fixture has not yet confirmed this.
- Area attacks hit min(target cap, foes in the scenario). Cone and sphere geometry is not modelled.
- Pet AI, accuracy and endurance are not modelled.
    Mastermind henchman damage stays unverified, because the Mids henchman damage table reads roughly six times the normal pet value.
- Several AT inherents carry no magnitude in canonical data. Their defaults are labelled "approximate; verify in MidsReborn" and can be overridden with `--assume`.
- Misses are not weighted yet. An attack's endurance is paid whether it hits or not.
- Team buffs are not modelled.
- Foe -Defense and to-hit effects are not modelled, so they do not change hit chance.
- Pet debuffs are not modelled.
- Form-locked auras are not modelled.
- The cost of switching form is not modelled. A form's chain assumes the character stays in that form.
