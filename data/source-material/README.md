# Source material manifest

`manifest.json` lists every forum capture the guide index reads: one row per capture with `url`, `title`, `date_posted`,
`archetype`, `content_hash`, `path` and `era`. It holds no post text. `path` is relative to the source root.

The captures live in the private `homecoming-source-material` repository, checked out beside this one.
The tools find it through `--source-root`, else `COH_SOURCE_ROOT`, else `../homecoming-source-material`.
A missing directory exits with `E04` from the Python tools and `E210` from `coh-engine source-manifest`.

Regenerate the manifest after the captures change. The output is sorted, so an unchanged source root writes the same bytes:

```bash
coh-engine source-manifest --source-root ../homecoming-source-material
```

From a source checkout, `dotnet run --project src/CohEngine.Cli -- source-manifest ...` runs the same command.
It writes the bytes `python -m scripts.forum_scrape.manifest` writes, and leaves out rows that name a person in `COH_BANNED_NAMES`.
With the variable unset it falls back to the git user name and its first word, as the banned-word check does.
No captures under the root is `E211`; a capture whose front matter has no title is skipped with `E212`.
A root whose every capture the name filter withholds is `E213`, and a manifest that cannot be written is `E214`.
