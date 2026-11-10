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

Each tool result has a `data_version` field.
Core does not expose the hash of `data/canonical/manifest.json`, so the field holds the engine build string from `EngineInfo.Version` (the MinVer version with the commit).
The same engine build gives the same answer for the same input, so the string is enough to tell whether two answers can differ.
A manifest hash replaces it once Core exposes one.

## Paths

Tools that take a build take `buildDir`: a Mids dump directory, as `validate` takes.
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
| `power_lookup` | `fullName`, `buildDir` | The power's record: `available_level` and `pick_level` as game levels, base recharge, cast time, endurance cost, `stat_include` and the effect types it carries. |
| `chain` | none | Always an `E165` error: the chain planner is not part of this build. |

`check_legality` plus `check_hard_limits` give exactly the diagnostics `validate <build> --format json` prints, hard limits first.
Their `exit_code` follows the same rule as the CLI: 1 on any error, 2 on warnings only, otherwise 0.

`totals` and `score` run the same phases the parity tests run: assembly when the dump carries `power_assembly.json`, then slots, set bonuses and incarnates.
They need `totals.json` in the build directory for the class name.

`set_bonus_lookup` matches `setName` against the set UID, ignoring case.
`buildDir` supplies the class that picks the modifier column.
An unknown set is `E166`.
An unknown power in `power_lookup` is `E167`.

A tool failure comes back as an MCP tool error.
Its text starts with `An error occurred invoking '<tool>':` and continues with the engine's own `E<NN>: <what>; <fix>` message.

Tool arguments use camelCase names; result fields use snake_case.

## `validate --expect`

```text
coh-engine validate <build_dir> --expect defense_positional_min=48.58
```

`--expect` is repeatable.
After the normal run, the engine computes the registered scoring metrics and compares each claim with its metric.
A numeric claim matches when the metric, rounded to the claim's own number of decimal places, equals the claim.
`48.58` matches `48.5799…`; `48.6` matches too, because the claim stated one decimal place.
A flag metric (`perma_hasten`, `perma_dom`) takes `true` or `false`.

Each claim that does not match adds one error diagnostic to the run's output, and the command exits 1.
The diagnostic has rule `P-EXPECT-001` and message code `E160`.
It uses the text format and the `--format json` fields `validate` already uses.
The `expected` and `actual` fields carry both values.
The text form reads like this, where the message ends with a fix:

```text
build:1:1: error: P-EXPECT-001: E160: defense_positional_min is 45.2, expected 48.58; correct the claim, ...
```

The metric names are the ones `coh-engine rules` lists under scoring metrics.
`--expect` needs a Mids dump directory; a `.mbd` file is `E163`.

## Error codes

| Code | Meaning |
| --- | --- |
| `E160` | An `--expect` claim does not match the computed metric. |
| `E161` | An `--expect` names a metric that is not registered. Input error, exit 1. |
| `E162` | An `--expect` value does not fit its metric (not a number, or not `true` or `false`). Input error, exit 1. |
| `E163` | `--expect` was given a `.mbd` file. Input error, exit 1. |
| `E164` | A path given to an MCP tool does not exist. |
| `E165` | The `chain` tool is not available in this build. |
| `E166` | `set_bonus_lookup` found no set with that name. |
| `E167` | `power_lookup` found no power with that full name in the dump. |
| `E168` | The shared `enums.json` does not cover an index in the build's totals. |

A claim that is not of the form `metric=value` is a usage error (exit 2), like any other bad argument.
