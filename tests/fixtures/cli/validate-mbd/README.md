# CLI parity captures for `.mbd` builds

The Python engine's `validate <file>.mbd` output, kept so the C# CLI is compared with it byte for byte.
The C# tests in `tests/CohEngine.Cli.Tests/` run `validate` on each build in process and compare stdout and the exit code.

## Layout

| Path | Holds |
| --- | --- |
| `<build>.txt`, `<build>.json` | `validate <build>.mbd --format text` and `--format json`: stdout. |
| `<build>.exit` | The exit code, which is the same for both formats. |

`<build>` names each build under `tests/fixtures/buildfile/builds/` and `samples/builds/`.
A fixture build is named by its file name without `.mbd`.
A sample build is named by its path under `samples/builds/` without `.mbd`, each `/` written `__`: `characters/account1/laser-nuke/main.mbd` is `characters__account1__laser-nuke__main`.
The tests list `samples/builds/` with `git ls-files`, so they ignore untracked builds there; a new sample build must be tracked before they see it.
One sample, `community/dominator/general/unknown-arsenal-arsenal.mbd`, has no capture.
The Python reports E78 on stderr for it and exits 1 before any finding, and the C# test checks that message instead.
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
The Python engine is gone, so the captures are frozen. After a deliberate change to the C# output, regenerate the files from the C# CLI
(`dotnet run --project src/CohEngine.Cli -- <the same arguments>`) and review the diff.
