# CLI parity captures

These files are the Python engine's CLI output, kept so the C# CLI is compared with it byte for byte.
The C# tests in `tests/CohEngine.Cli.Tests/` run `rules` and `validate` in process and compare stdout and the exit code.

## Layout

| Path | Holds |
| --- | --- |
| `rules/rules.txt`, `rules/rules.exit` | `python -m coh_engine rules`: stdout and exit code. |
| `validate/<build>.txt`, `validate/<build>.json` | `validate <build> --format text` and `--format json`: stdout. |
| `validate/<build>.exit` | The exit code, which is the same for both formats. |

`<build>` is each directory name under `tests/fixtures/mids/builds/`.
Every capture ended with nothing on stderr.

## How they were made

Run once from the repository root at the Python source of commit `5812ba12` (`src/coh_engine/` is unchanged since):

```text
PYTHONPATH=src PYTHONUTF8=1 python -m coh_engine rules
PYTHONPATH=src PYTHONUTF8=1 python -m coh_engine validate tests/fixtures/mids/builds/<build> --format text
PYTHONPATH=src PYTHONUTF8=1 python -m coh_engine validate tests/fixtures/mids/builds/<build> --format json
```

On Windows Python writes CRLF to a pipe, so each file was rewritten with LF line endings.
The default `--mids-dir` (two levels above the build directory) is `tests/fixtures/mids`.
Exit codes follow the severity contract: 1 for an error finding, 2 for warnings only, 0 for none.

## Refreshing

Do not edit these by hand.
If the Python output changes on purpose, regenerate every file the same way and review the diff.
