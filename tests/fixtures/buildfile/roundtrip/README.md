# Round-trip reference files

Each file here is the Python writer's output for one `.mbd` under `samples/builds/` or `tests/fixtures/buildfile/builds/`:
`json.dumps(dump_mbd(load_mbd(path)), indent=2) + "\n"`, encoded as UTF-8.
A fixture build keeps its file name. A sample build is named by its path under `samples/builds/` with each `/` written
`__`, so `characters/account1/laser-nuke/main.mbd` is `characters__account1__laser-nuke__main.mbd`.

They were generated once with the repository venv's Python at commit `5812ba12`
(`feat/104-dotnet-numeric-core`), by a script that imports `coh_engine.buildfile.mbd`, loops over the sources in
ordinal path order and writes each result here. The C# suite (`RoundTripTests`) compares `Mbd.Serialize` against these
bytes. Do not regenerate them from the C# writer.

Five sources are byte-identical to their reference: `arsenal_dominator_incarnate`, `arsenal_dominator_incarnate_slotted`,
`arsenal_dominator_noslots`, `shield_scrapper_noslots` and `wpss_tanker_leadership`. Every other source differs only in
lacking the final newline the writer appends, except `community__dominator__arsenal-arsenal`, which
stores each enhancement under the legacy `Enhancement` key and is written back under `Uid`. The model round trip is
asserted for all of them and the bytes are asserted against these references.
