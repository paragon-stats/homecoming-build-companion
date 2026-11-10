# Python `chain` captures

These files are the Python port's own output for `chain`, committed so the C# `chain` command can be held to it byte for byte.
`ChainParityTests` runs the C# command over every capture and compares the exit code, standard output and, where a `.err` file exists, standard error.

## What each file is

| Name | Holds |
| --- | --- |
| `<build>.txt.out`, `<build>.json.out` | Standard output of `chain tests/fixtures/mids/builds/<build> --format text` and `--format json`, every stance and every form. |
| `<build>.pets.txt.out`, `<build>.pets.json.out` | The same with `--mids-db tests/fixtures/attribmod`, for the builds that summon pets. |
| `*.exit` | The exit code, which is 0, 1 for an input error or 2 for a warning finding. |
| `*.err` | Standard error, kept only where it holds no machine-specific path. |

No sample build has a `_clicksoff` sibling, so every capture counts click buffs as always on. No sample build has a power that needs a form, so the captures carry no per-form files.

## How they were made

The Python engine at commit `5812ba123ad06c0f22771902c71ece520d56e819`, Python 3.14, run from the repository root once per build and format:

```text
python -m coh_engine chain tests/fixtures/mids/builds/<build> --format <text|json> [--mids-db tests/fixtures/attribmod]
```

Windows line endings were changed to LF.
Standard output sits in `.out` files because a run that failed printed nothing, and an empty `.json` file fails the JSON check.
The diagnostic lines and the JSON `path` fields print the build path the way Python's `str(Path)` does, so a capture made on Windows holds backslashes.
The test rewrites only the `tests\fixtures\mids\builds\` prefix (and its JSON-escaped form) to forward slashes before it compares.

Do not regenerate these files to make a test pass.
A difference is a divergence between the two ports.
