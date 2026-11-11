# MCP server and `validate --expect`

`coh-engine mcp` serves the engine's numbers over the Model Context Protocol on standard input and output.
An agent that asks the server for a number gets the engine's answer instead of working it out from raw data.
`coh-engine validate --expect metric=value` checks a stated number against the engine and fails the command when it is wrong.
Both sit on the same code: the server's tools are thin calls into `CohEngine.Core`.

## Connect a client

The server speaks MCP over stdio.
Point the client at the CLI with the single argument `mcp`.
For a client that reads a `.mcp.json` file:

```json
{
  "mcpServers": {
    "coh-engine": {
      "command": "coh-engine",
      "args": ["mcp"]
    }
  }
}
```

Nothing but protocol messages is written to standard output.
The server ends when the client closes the connection.
`coh-engine mcp` accepts no other arguments.

The published build is a native AOT binary.
Tools are registered by type with source-generated JSON metadata, and the server never scans assemblies or reflects over types.

## What every result carries

Each tool result has a `data_version` field: `sha256:` and 64 lowercase hex digits, or the text `unknown`.
It identifies the data files the answer was computed from, so two answers with the same `data_version` read identical files, and a claim can be re-checked later against them.

The hash covers these files, in this order, and nothing else:

- A directory counts as its own files (not its subdirectories), in ordinal name order, each as its name and its bytes.
- The shared dump directory (`midsDir`, or the build directory's grandparent), then the build directory.
  For a `.mbd` the file stands where the build directory would.
- For `score`, `totals` and `set_bonus_lookup`: also `AttribMod.json` (and for `score` and `totals`, `Maths.mhd`), at the path given or the default.
- For `chain`: also `strategy/breakpoints.json` and `data/canonical/manifest.json` of the repository, and `AttribMod.json` in `midsDb` when given.
  `chain` is the only tool that reads `data/canonical`; the manifest's own SHA-256 per file stands for that tree.
  The repository is found from the binary's directory and then the working directory.
- `power_lookup` reads only the build directory.
- `rules` reads no data file, so its `data_version` is `unknown`.

Each directory or file is hashed once per process and cached, so a file changed while a server runs is not noticed until the next server starts.
`validate --format json` and `chain --format json` print the same bytes as the Python engine and carry no `data_version`; the MCP results carry it.

## Paths

Tools that take a build take `buildDir`: a Mids dump directory, as `validate` takes.
`check_legality` and `check_hard_limits` also take a `.mbd` file there, read through the same `MbdBuildSource` `validate` uses.
A `.mbd` has no neighbouring shared dumps, so `midsDir` is then required (`E164` without it).
The other tools compute from power effects, which a `.mbd` does not hold, so they refuse a `.mbd` with `E180`.
`midsDir` is the shared dump directory.
When it is left out, the engine uses the build directory's grandparent, as `validate` does.
A build directory at or next to the filesystem root has no grandparent, so `midsDir` is required there (`E164`).

`score`, `totals` and `set_bonus_lookup` also read `AttribMod.json`, and `score` and `totals` read `Maths.mhd`.
By default these sit in the parent of the shared dump directory, at `attribmod/AttribMod.json` and `Maths.mhd` (the `tests/fixtures` layout).
`attribModPath` and `mathsPath` override those two locations.
When the shared dump directory is a filesystem root, give both explicitly.

A path that does not exist is rejected with `E164`, naming the parameter and the path.
The tools read only fixed file names inside `buildDir` and `midsDir`, plus the two files above.
No file name comes from the client.

A directory that exists but lacks a dump file is `E72`.
The error names the missing file by name and echoes the client's own `buildDir` and `midsDir`.
It never carries a path the client did not supply.
A dump file of the wrong shape is also `E72`, without the file's path.
`E168` is a data-shape error: the shared `enums.json` has no name for an index in the totals.

## Tools

| Tool | Inputs | Result |
| --- | --- | --- |
| `rules` | none | `legality`, `hard_limits`, `enhancement_aspects` and `metrics`, each a list of `rule_id` and `summary`, in the order `coh-engine rules` prints them. |
| `check_legality` | `buildDir`, optional `midsDir` | `exit_code` and `diagnostics`: the enhancement legality rules' findings, in the JSON `validate --format json` prints. |
| `check_hard_limits` | `buildDir`, optional `midsDir` | The same shape for the hard-limit rules (slots, picks, pools, levels). |
| `score` | `buildDir`, optional `midsDir`, `attribModPath`, `mathsPath` | `class_name` and `metrics`: each registered metric with a `number` or a `flag`. |
| `totals` | `buildDir`, optional `midsDir`, `attribModPath`, `mathsPath` | `totals` and `totals_capped`. Defense and resistance are percentages per damage type; mez, mez resistance, debuff resistance and elusivity are named by type; `scalars` holds the single-value totals. |
| `set_bonus_lookup` | `setName`, `buildDir`, optional `midsDir`, `attribModPath` | The set's `tiers` by piece count and its `special` per-piece bonuses. Each effect has its resolved `magnitude` and, where the game shows a percentage, `percent`. |
| `power_lookup` | `fullName`, `buildDir` | The power's record: `available_level` and `pick_level` as game levels (1-based), base recharge, cast time, endurance cost, `stat_include` and the effect types it carries. |
| `chain` | `buildDir`, optional `midsDir`, `stance`, `form`, `target`, `move`, `standoff`, `midsDb` | `exit_code` and `stances`: the plans `coh-engine chain <buildDir> --format json` prints, one per stance. |

`check_legality` plus `check_hard_limits` give exactly the diagnostics `validate <build> --format json` prints, hard limits first.
Their `exit_code` follows the same rule as the CLI: 1 on any error, 2 on warnings only, otherwise 0.

`totals` and `score` run the same phases the parity tests run: assembly when the dump carries `power_assembly.json`, then slots, set bonuses and incarnates.
They need `totals.json` in the build directory for the class name.

`chain` runs the same command as the CLI, so a plan is the one the CLI computes, with the same exit code rule (1 on an error finding, 2 on warnings only).
`stance` is `melee`, `ranged`, `hybrid` or `all` (the default); `target` is `single` or `spawn:N`; `move` is `run`, `jump` or `fly`; `standoff` is feet.
`midsDb` names the directory holding `AttribMod.json`, which prices pets; without it pets are left out.
The CLI's `--assume` and `--clicksoff-dir` are not offered.
A bad option or an unreadable input is a tool error carrying the command's own message.
`coh-engine ask` offers `chain` too, with `stance`, `form`, `target`, `move` and `standoff`.

`set_bonus_lookup` matches `setName` against the set UID, ignoring case.
`buildDir` supplies the class that picks the modifier column.

## Unknown results

A lookup that finds nothing answers with an explicit unknown, not an empty success and not a tool error.
The result has an `unknown` field holding the `E<NN>` message and omits the value:

```json
{"data_version":"sha256:...","unknown":"E167: no power named 'No.Such.Power' in ...; check the full name against the dump's powers_effects.json"}
```

`set_bonus_lookup` with a set the data does not hold gives `unknown` with `E166`, and `power_lookup` with a power the dump does not hold gives `unknown` with `E167`; `set` and `power` are absent.
A found result has no `unknown` field.
A caller must not fill the gap: no value was available.

A tool failure comes back as an MCP tool error.
Its text starts with `An error occurred invoking '<tool>':` and continues with the engine's own `E<NN>: <what>; <fix>` message.

Tool arguments use camelCase names; result fields use snake_case.

## `validate --expect`

```text
coh-engine validate <build_dir> --expect defense.ranged=34.96
```

`--expect` is repeatable.
After the normal run, the engine computes the build and compares each claim with its number.
A claim names a registered scoring metric, or a total as `defense.<type>` or `resistance.<type>`.
`<type>` is a damage type or position (`Melee`, `Ranged`, `AoE`, `Smashing`, `Fire` and the rest), in any case.
A total is the uncapped percentage, the same number the `totals` tool gives in `defense_percent` and `resistance_percent`.
A numeric claim matches when the number, rounded once, half away from zero, to the claim's own number of decimal places, equals the claim.
A total is rounded from its unrounded value, with no intermediate rounding, the same way the four metric names have always been compared.
A total of `12.34499` matches the claim `12.34` and not `12.35`; a total of `12.3451` matches `12.35`; and `12.3` matches the first, because a claim of one decimal place rounds it to 12.3.
A flag metric (`perma_hasten`, `perma_dom`) takes `true` or `false`.

Each claim that does not match adds one error diagnostic to the run's output, and the command exits 1.
The diagnostic has rule `P-EXPECT-001` and message code `E160`.
It uses the text format and the `--format json` fields `validate` already uses.
The `expected` and `actual` fields carry both values.
The text form reads like this, where the message ends with a fix:

```text
build:1:1: error: P-EXPECT-001: E160: defense.ranged is 34.959024..., expected 40.1; correct the claim, ...
```

The metric names are the ones `coh-engine rules` lists under scoring metrics (`defense_positional_min`, `resist_min`, `perma_hasten`, `perma_dom`); they stay as they were, so no earlier claim breaks.
The dotted names are added beside them.
`--expect` needs a Mids dump directory; a `.mbd` file is `E163`, because a `.mbd` holds no power effects to compute totals from.

The regression test for a ranged-defense claim runs against the committed reference build `shield_scrapper_set_bonuses`, whose Mids dump holds a ranged defense of 34.959024 percent.
`defense.ranged=34.96` holds and `defense.ranged=40.1` fails with that actual value.

## Error codes

| Code | Meaning |
| --- | --- |
| `E160` | An `--expect` claim does not match the computed number. |
| `E161` | An `--expect` names neither a registered metric nor a `defense.<type>` or `resistance.<type>` total. Input error, exit 1. |
| `E162` | An `--expect` value does not fit the number it names (not a number, or not `true` or `false` for a flag). Input error, exit 1. |
| `E163` | `--expect` was given a `.mbd` file. Input error, exit 1. |
| `E164` | A path given to an MCP tool does not exist. |
| `E166` | `set_bonus_lookup` found no set with that name; the result's `unknown` field carries it. |
| `E167` | `power_lookup` found no power with that full name in the dump; the result's `unknown` field carries it. |
| `E168` | The shared `enums.json` does not cover an index in the build's totals. |
| `E180` | A tool that computes from power effects was given a `.mbd` file. Tool error. |

`E165` (the `chain` tool was not available) is retired: the tool works.

A claim that is not of the form `name=value` is a usage error (exit 2), like any other bad argument.
