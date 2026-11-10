# CLI parity captures for `.mbd` builds

The Python engine's `validate <file>.mbd` output, kept so the C# CLI is compared with it byte for byte.
The C# tests in `tests/CohEngine.Cli.Tests/` run `validate` on each build in process and compare stdout and the exit code.

## Layout

| Path | Holds |
| --- | --- |
| `<build>.txt`, `<build>.json` | `validate <build>.mbd --format text` and `--format json`: stdout. |
| `<build>.exit` | The exit code, which is the same for both formats. |

`<build>` is the file name without `.mbd` of each build under `tests/fixtures/buildfile/builds/` and `samples/builds/`.
One sample, `arsen-arsen_-_Dominator_Arsenal_Control_-_Arsenal_Assault.mbd`, has no capture: the Python reports E78 on stderr for it and exits 1 before any finding, and the C# test checks that message instead.
Every captured build ended with nothing on stderr.

## How they were made

Run once from the repository root at the Python source of commit `1adeb627` (`src/coh_engine/` is unchanged since):

```text
PYTHONPATH=src PYTHONUTF8=1 python -m coh_engine validate <build>.mbd --format text
PYTHONPATH=src PYTHONUTF8=1 python -m coh_engine validate <build>.mbd --format json
```

On Windows Python writes CRLF to a pipe, so each file was rewritten with LF line endings.
The default `--mids-dir` for a `.mbd` is `tests/fixtures/mids`.
Exit codes follow the severity contract: 1 for an error finding, 2 for warnings only, 0 for none.

## Refreshing

Do not edit these by hand.
If the Python output changes on purpose, regenerate every file the same way and review the diff.
