# Source material manifest

`manifest.json` lists every forum capture the guide index reads: one row per capture with `url`, `title`, `date_posted`,
`archetype`, `content_hash`, `path` and `era`. It holds no post text. `path` is relative to the source root.

The captures live in the private `homecoming-source-material` repository, checked out beside this one.
The tools find it through `--source-root`, else `COH_SOURCE_ROOT`, else `../homecoming-source-material`.
A missing directory exits with `E04`.

Regenerate the manifest after the captures change. The output is sorted, so an unchanged source root writes the same bytes:

```bash
python -m scripts.forum_scrape.manifest --source-root ../homecoming-source-material
```
