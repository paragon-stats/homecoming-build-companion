# Contract: power assembly, split into four seams

Issue #42. Goal: every one of the 12 sample builds computes totals that match Mids' own `totals.json`,
exactly, on the fields the parity tests compare, with the engine assembling each power itself.

## Why there is an assembly step

Before Mids computes any totals it builds each power's effect list (`GBPA_SubPass0_AssemblePowerEntry`
and the cross-power loop in `GBPA_Pass0_InitializePowerArray`). The engine today reads the raw database
power and skips this. Giving the engine Mids' assembled lists makes 7 of the 12 builds match at once; the
other five also need extra expression keywords (Arachnos Soldier, HAZMAT Brute, NUKE Corruptor, QUANTUM
Tanker) or conditional effects (Arachnos Widow and the Mastermind, through Cross Punch).

The seams meet only at the names, types and files in this document. A seam that needs any of them changed
stops and reports the change it needs instead of improvising one.

## Ownership

Each file has one owner. A seam edits only its own files.

| Seam | Owns |
| --- | --- |
| 1 Harness export | `tools/mids-dump/**`; `src/coh_engine/assembly_data.py` and `tests/test_assembly_data.py`; in `src/coh_engine/effect.py`, the `Effect` and `Power` dataclass fields, `_parse_effect` and `load_powers_effects`; in `src/coh_engine/enhancement.py`, `EnhancementRecord` and `load_enhancement_effects`; `tests/conftest.py`; every fixture file named under Seam 1 |
| 2 Assembly | `src/coh_engine/assembly.py`, `tests/test_assembly.py`, `tests/test_assembly_parity.py` |
| 3 Expressions and conditions | `src/coh_engine/expressions.py`, `tests/test_expressions.py`; in `src/coh_engine/effect.py`, `effect_mag` and the expression helpers below it (`_parse_magnitude`, `_evaluate`, `_float_text`, `_unported` and their tables), which it may move into `expressions.py`; the expression tests in `tests/test_effect.py` |
| 4 Integration | `src/coh_engine/base_totals.py`, `src/coh_engine/incarnate.py`, `tests/test_sample_builds.py`, `tests/test_base_totals*.py`, `tests/test_incarnate*.py`, `docs/engine/**` |

`effect.py` is the one shared file. Seam 1 adds dataclass fields and their loading first; seam 3 changes only
the magnitude code. Neither touches the other's region.

Seams 2 and 3 do not edit `conftest.py`. They build test records with `make_effect` / `make_power` from it,
or with a helper local to their own test module, in exactly the dataclass shapes below.

## Error codes

Codes from E85 up are reserved, one block per seam. E08 stays the refusal for an unported expression.

| Seam | Codes |
| --- | --- |
| 1 | E85–E87 (harness and loaders) |
| 2 | E88–E91 |
| 3 | E92–E94 |
| 4 | E95–E97 |

Every diagnostic follows `.claude/rules/error-output.md`: code, what failed, expected and found, where, and
the smallest fix.

## Seam 1: harness export

Seam 1 runs the harness against the patched fork (the patch stays applied), with database version
2026.1.1242, and copies back only the files listed here. All paths are under
`src/coh_engine/tests/fixtures/mids/`. Floats are .NET float32 written shortest-round-trip; enum values are
member names.

### Effect record, extended

Every effect in the new files is the record `powers_effects.json` already uses (all its keys, same meaning)
plus these keys:

| Key | Type | Meaning |
| --- | --- | --- |
| `Summon` | string | The granted or executed power's full name (`""` if none) |
| `nSummon` | int | The granted power's database index for a GrantPower effect (`-1` if none) |
| `Duration` | float | `Effect.Duration`, the duration Mids hands to the absorbed effects |
| `EffectId` | string | `Effect.EffectId` |
| `Reward` | string | `Effect.Reward` |
| `ActiveConditionals` | list of `{"Key": string, "Value": string}` | The conditions, in order, as Mids stores them: `Key` is `"<type>:<power full name>"`, possibly prefixed `"OR "` or `"AND "` (`"Active:Inherent.Inherent.Critical_Hit"`, `"Taken:Pool.Fighting.Kick"`, `"Stacks:Pool.Teleportation.Recall_Friend"`); `Value` is `"True"`/`"False"`, or `"= 1"` style for `Stacks` |
| `IsEnhancementEffect` | bool | `Effect.isEnhancementEffect` |
| `Absorbed_Power_nID` | int | `Effect.Absorbed_Power_nID` |

### `enhancement_effects.json` (re-dumped)

Each record keeps every existing key and adds:

| Key | Type | Meaning |
| --- | --- | --- |
| `IsProc` | bool | `Enhancement.IsProc` |
| `PowerNid` | int | Index of the enhancement's own power (`Enhancement.GetPower()`), `-1` if it has none |

```json
{"Nid": 640, "StaticIndex": 641, "TypeId": 4, "TypeName": "SetO", "Superior": false, "nIDSet": 106,
  "IsProc": true, "PowerNid": 9213, "Effects": []}
```

### `assembly_powers.json` (new, database level)

One record per power that is the own power of any enhancement, or is reachable, recursively, through a
GrantPower (`nSummon`) or ExecutePower (`Summon`) effect from an enhancement's power or from any power in
`powers.json`. Powers in `powers.json` are not listed unless reached. About 1,700 powers.

| Key | Type | Meaning |
| --- | --- | --- |
| `Nid` | int | Database index (the key the other records point at) |
| `FullName` | string | Power full name |
| `PowerType` | string | `ePowerType` name |
| `PowerSetType` | string | `ePowerSetType` name of the power's powerset (`"Pet"` drives the pet duration rule) |
| `EntitiesAffected` | list of string | `eEntity` flag names set on `Power.EntitiesAffected` |
| `EntitiesAutoHit` | list of string | `eEntity` flag names set on `Power.EntitiesAutoHit` |
| `ActivatePeriod`, `RechargeTime`, `BaseRechargeTime`, `CastTime`, `AoEModifier` | float | The power's scalars |
| `Enhancements` | list of int | Enhancement class IDs the power accepts |
| `HasGrantPowerEffect`, `HasPowerOverrideEffect`, `AbsorbSummonEffects`, `AbsorbSummonAttributes` | bool | The power's flags of those names |
| `IgnoreEnh` | list of string | `eEnhance` names the power ignores (pet absorption copies it onto the absorbing power) |
| `Effects` | list of extended effect records | The database effects, in order |

The file also holds every power of every powerset of a summoned entity that an absorbing power (one with
`AbsorbSummonEffects` or `AbsorbSummonAttributes`) creates through EntCreate, and of the entities those powers
create in turn. It is written unindented, about 10 MB and 1,800 powers.

```json
{"Nid": 9213, "FullName": "Boosts.Crafted_Force_Feedback_F.Crafted_Force_Feedback_F", "PowerType": "Auto_",
  "PowerSetType": "Boost", "EntitiesAffected": ["Caster"], "EntitiesAutoHit": ["Caster"],
  "ActivatePeriod": 0, "RechargeTime": 0, "BaseRechargeTime": 0, "CastTime": 0, "AoEModifier": 1,
  "Enhancements": [], "HasGrantPowerEffect": true, "HasPowerOverrideEffect": false,
  "AbsorbSummonEffects": false, "AbsorbSummonAttributes": false,
  "Effects": [{"Index": 0, "EffectType": "GrantPower", "ToWho": "Self", "Probability": 0.33333334,
    "Summon": "Set_Bonus.Set_Bonus.Force_Feedback", "nSummon": 8120, "ActiveConditionals": [], "...": "..."}]}
```

The numbers in the examples show the shape; the dump supplies the real ones.

### `assembly_entities.json` (new, database level)

The summoned entities pet absorption reads, and their powersets.

| Key | Type | Meaning |
| --- | --- | --- |
| `Entities` | list of `{"Nid": int, "UID": string, "ClassNid": int, "PowersetNids": [int]}` | Each entity an absorbing power creates (its EntCreate `nSummon` is the `Nid`), with `GetNClassId()` and `GetNPowerset()` in order |
| `Powersets` | list of `{"Nid": int, "FullName": string, "SetType": string, "PowerNids": [int]}` | Each powerset named above, its powers' indexes in order (`-1` for an empty slot); every power is in `assembly_powers.json` |

```json
{"Entities": [{"Nid": 2, "UID": "Pets_TarPatch", "ClassNid": 18, "PowersetNids": [3299]}],
  "Powersets": [{"Nid": 3299, "FullName": "...", "SetType": "Pet", "PowerNids": [11021]}]}
```

### `builds/<name>/power_assembly.json` (new, per build)

What assembly needs about the build's own powers that `powers_effects.json` does not carry. One record per
build power that `powers_effects.json` lists, matched by `BuildIndex`.

| Key | Type | Meaning |
| --- | --- | --- |
| `BuildIndex` | int | Same key as `powers_effects.json` |
| `EntitiesAffected`, `EntitiesAutoHit`, `PowerSetType`, `BaseRechargeTime`, `AoEModifier`, the four flags, `IgnoreEnh` | as above | The build power's values |
| `SubPowers` | list of `{"NIDPower": int, "StatInclude": bool}` | The entry's sub-powers (`PowerEntry.SubPowers`) |
| `Effects` | list of `{"Index", "Summon", "nSummon", "Duration", "EffectId", "Reward", "ActiveConditionals", "IsEnhancementEffect", "Absorbed_Power_nID"}` | The extended keys for each effect of `powers_effects.json`, matched by `Index` |

### `builds/<name>/slots.json` (re-dumped)

Each per-power entry gains `"ProcInclude": bool` (`PowerEntry.ProcInclude`). Nothing else changes.

### `builds/<name>/assembled_powers.json` (new, per build, the reference for seam 2)

For each StatInclude build power, Mids' assembled effect list after Pass0: `{"BuildIndex": int, "FullName":
string, "Effects": [extended effect records, in Mids' order]}`. On a VariableEnabled power, `Scale` already
carries Mids' variable-value multiply, which the engine applies when it reads the magnitude instead; compare
`Scale` against the engine's assembled scale times that factor, or skip it for such effects.

### Python side, `src/coh_engine/assembly_data.py`

```python
@dataclass(frozen=True, slots=True)
class AssemblyPower:
    nid: int
    full_name: str
    power_type: str
    power_set_type: str
    entities_affected: frozenset[str]
    entities_auto_hit: frozenset[str]
    activate_period: float
    recharge_time: float
    base_recharge_time: float
    cast_time: float
    aoe_modifier: float
    enhancements: frozenset[int]
    has_grant_power_effect: bool
    has_power_override_effect: bool
    absorb_summon_effects: bool
    absorb_summon_attributes: bool
    ignore_enh: frozenset[str]
    effects: tuple[Effect, ...]

@dataclass(frozen=True, slots=True)
class AssemblyEntity:
    nid: int
    uid: str
    class_nid: int
    powerset_nids: tuple[int, ...]

@dataclass(frozen=True, slots=True)
class AssemblyPowerset:
    nid: int
    full_name: str
    set_type: str
    power_nids: tuple[int, ...]

@dataclass(frozen=True, slots=True)
class AssemblyEntities:
    entities: Mapping[int, AssemblyEntity]
    powersets: Mapping[int, AssemblyPowerset]

@dataclass(frozen=True, slots=True)
class BuildPowerAssembly:
    build_index: int
    proc_include: bool
    sub_powers: tuple[tuple[int, bool], ...]   # (nid, stat_include)
    power: AssemblyPower                       # the build power, effects enriched with the extended keys

def load_assembly_powers(path: Path | str) -> Mapping[int, AssemblyPower]: ...
def load_assembly_entities(path: Path | str) -> AssemblyEntities: ...
def load_build_power_assembly(
    power_assembly_path: Path | str, slots_path: Path | str, powers: Sequence[Power]
) -> Mapping[int, BuildPowerAssembly]: ...
def load_assembled_powers(path: Path | str) -> Mapping[int, tuple[Effect, ...]]: ...   # the reference
```

`Effect` gains, all defaulted so existing records load unchanged: `summon: str = ""`, `n_summon: int = -1`,
`duration: float = 0.0`, `effect_id: str = ""`, `reward: str = ""`,
`conditionals: tuple[tuple[str, str], ...] = ()`, `absorbed_power_nid: int = -1`. The existing
`is_enhancement_effect` reads `IsEnhancementEffect` where present. `active_conditionals_count` stays and
equals `len(conditionals)` wherever both are known.

`Power` gains `aoe_modifier: float = 1.0` and `base_recharge_time: float = 0.0`.

`EnhancementRecord` gains `is_proc: bool = False` and `power_nid: int = -1`.

Loaders refuse a record missing a required key with E85 (file, record, key); a pointer to a power the export
does not hold refuses with E86.

## Seam 2: assembly step

Module `src/coh_engine/assembly.py`.

```python
Conditions = Callable[[Effect], bool]   # seam 3's conditionals_met, bound to a context by seam 4

def assemble_power(
    build_power: BuildPowerAssembly,
    slots: Sequence[SlotRef],
    *,
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
    powers: Mapping[int, AssemblyPower],
    entities: AssemblyEntities,
    archetype_index: int,
    conditions: Conditions,
) -> tuple[Effect, ...]: ...

def assemble_build(
    build: Mapping[int, BuildPowerAssembly],
    slots: Mapping[int, Sequence[SlotRef]],
    *,
    stat_include: Mapping[int, bool],
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
    powers: Mapping[int, AssemblyPower],
    entities: AssemblyEntities,
    archetype_index: int,
    conditions: Conditions,
) -> dict[int, tuple[Effect, ...]]: ...   # build index -> assembled effects
```

Output: the assembled effects as plain `Effect` records, re-indexed so `Effect.index` is the position in the
returned tuple (the multiplier and incarnate maps key on it). No new type.

Order, per power, as Mids runs it:

1. Power override: a PowerRedirect effect with probability about 1 replaces the power (E88 if one is met and
    the target is not in `powers`).
2. The slotted enhancements' own effects: for each filled slot, the enhancement's power's effects, skipped
    when the entry's `proc_include` and the enhancement's `is_proc` are both true, kept only when the slot's
    set has no special bonus for that piece and (the enhancement has no enhancement-mode effect, or the
    effect's modifier table does not start `Melee_Boosts_` or `Ranged_Boosts_`). Each copy gets
    `is_enhancement_effect=True`, `ignore_scaling=is_proc`, `absorbed_effect=True`, `buffable=False`, its own
    `to_who` and `ticks`. The pets-only branch is not ported: an effect that affects pets only on a summon
    power refuses with E89.
3. Pet absorption (`Power.AbsorbPetEffects`): a power with `absorb_summon_effects` or
    `absorb_summon_attributes` and an EntCreate effect of probability about 1 takes its entity's powersets from
    `assembly_entities.json`: attributes from the first powerset's powers (including `entities_affected`,
    `entities_auto_hit` and `ignore_enh`), then, with `absorb_summon_effects`, every powerset power's effects as
    Mids absorbs them (the entity's `class_nid` as the absorbed class, the stack count, the EntCreate's duration
    and delay, the pet duration rule from a SilentKill on a `"Pet"` powerset) and the powers of entities those
    create. Four sample builds need this (Distortion Field, Spectral Terror, Fallout, Hell on Earth). An
    entity or powerset missing from the export refuses with E89. `assemble_power` and `assemble_build` take
    `entities: AssemblyEntities` as one more keyword argument.
4. GrantPower replacement, repeated until no grant is left (at most 100 rounds): each GrantPower effect that is
    not Ignored, has `n_summon > -1` and passes `CanGrantPower` (its special-case switch ported here; its
    conditionals through `conditions`) becomes `effect_class="Ignored"`, and the granted power's effects are
    appended with `absorbed_effect=True`, `absorbed_power_type` and `absorbed_power_nid` of the granted power,
    `absorbed_class_n_id=archetype_index`, and the target rules: Friend-not-Caster or MyPet-not-Caster
    auto-hit makes `to_who="Target"` (stacking `Yes` multiplies `scale` by the stack count, 1 here). Then, on
    the last appended effect only, as Mids does: `absorbed_power_type` from the grant when it had one,
    `to_who` from the grant unless that effect is itself a GrantPower, the All-to-Target rewrite from the
    power's `entities_affected`, and `is_enhancement_effect` from the grant.
5. ExecutePower expansion, recursive to depth 5: each ExecutePower effect is replaced in place by the
    executed power's effects (an empty or unknown `summon` drops it). Each inserted effect takes the caller's
    `n_duration` when that is above 0, adds its `delayed_time`, takes `min(probability)` unless the caller's
    probability is 0 or 1, takes its `procs_per_minute`, appends its conditionals, and takes its `ticks` when
    its own are 0. A cycle stops at depth 5, as in Mids.
6. Sub-power effects: each StatInclude sub-power's effects are appended with `absorbed_effect=True`,
    `absorbed_power_type` and `absorbed_power_nid` of the sub-power.

Then `assemble_build` runs the cross-power GrantPower pass Mids runs once every power is assembled (the
incarnate loop with effect type GrantPower, `hIDX = -1`), for every StatInclude power other than the target.

Probability: assembly keeps `probability` as Mids' rules above leave it; totals read only `probability > 0`,
so the reference test compares that sign, not the value.

Seam 3 is called at exactly one point: `conditions(effect)` inside `CanGrantPower` for a GrantPower effect
that carries conditionals.

Tests: unit tests on hand-made records for each step; `test_assembly_parity.py` assembles every StatInclude
power of the 12 builds and compares with `assembled_powers.json`, effect by effect, on `effect_type`,
`et_modifies`, `damage_type`, `mez_type`, `to_who`, `effect_class`, `buffable`, `absorbed_effect`,
`absorbed_power_type`, `absorbed_class_n_id`, `is_enhancement_effect`, `scale`, `n_magnitude`, `aspect`,
`modifier_table`, `delayed_time`, `pv_mode`, `ignore_scaling`, `conditionals`, and `probability > 0`.

## Seam 3: expressions and conditions

Module `src/coh_engine/expressions.py`.

```python
@dataclass(frozen=True, slots=True)
class ExpressionContext:
    prev_hp_max: float                       # Totals.HPMax of the previous recompute
    build_power_names: frozenset[str]        # full names of every power in the build
    active_power_names: frozenset[str]       # full names of the StatInclude powers
    variable_values: Mapping[str, int]       # full name -> the entry's VariableValue
    player_hp_percent: int = 100

def evaluate_magnitude(effect: Effect, power: Power, ctx: ExpressionContext | None, *, scale: float, modifier: float) -> float: ...
def conditionals_met(effect: Effect, ctx: ExpressionContext) -> bool: ...
def effect_included(effect: Effect, ctx: ExpressionContext | None) -> bool: ...
```

`effect_mag` gains a keyword `expr: ExpressionContext | None = None` and passes it on; with `None` it behaves
as it does today.

Keywords to add to the ones already ported (`@StdResult`, `modifier>current`, `cfg>player>hp`, the power's
own `>variableVal`): `source>Max.kHitPoints` (`ctx.prev_hp_max`), `power.base>rechargetime`
(`power.base_recharge_time`), `power.base>areafactor` (`power.aoe_modifier`), `power.base>activateperiod`
(`power.activate_period`), `powerGroupNotIn(X)` and `powerGroupIn(X)` (whether `power.full_name` starts with
`X`), and another power's `>variableVal` from `ctx.variable_values`. Text substitution follows Mids' order
(the constant table first, then the function patterns). The power is the one that owns the effect after
assembly. Any other keyword or function, or a missing context for one that needs it, is still refused with
E08.

`effect_included` is the engine's `CanInclude`: true for an effect with no conditionals and no special case;
`conditionals_met` for an effect with conditionals and no special case; false for a special case, as today.
`conditionals_met` evaluates the conditions in order with Mids' AND/OR chaining and supports the condition
types the sample builds carry (`Active`, `Taken` and `Stacks`; Cross Punch uses `Active` and `Taken`); any other condition type
refuses with E92.

## Seam 4: integration

Seam 4 lands after seams 2 and 3:

- Assemble each build power before `compute_base_totals` reads it, and pass `effect_included` and the
  expression context through `base_totals` (`_can_include`, `_mag`).
- Supply `prev_hp_max` from a first recompute, as Mids' repeated recompute does, if the five builds need it.
- Resolve the `incarnate.py` overlap: incarnate enhancement stays its own cross-power pass; the Alpha's
  absorbed effects now arrive through assembly. Keep every incarnate parity test passing.
- Widen `test_unslotted_sample_matches_mids_totals` to all 12 builds, exact as now.
