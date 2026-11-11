# Chat clients

`CohEngine.Ai` builds an `IChatClient` (Microsoft.Extensions.AI) that speaks the OpenAI chat
completions protocol. `ChatClientFactory.Create(ChatProviderOptions)` returns it. Nothing in the
engine calls it except the `ask` command (see the end of this page). The Anthropic Messages API has
its own client; see [ai-anthropic-client.md](ai-anthropic-client.md).

## Presets

| Preset    | Base URL                    | Key variable      |
| --------- | --------------------------- | ----------------- |
| `openai`  | `https://api.openai.com/v1` | `OPENAI_API_KEY`  |
| `mistral` | `https://api.mistral.ai/v1` | `MISTRAL_API_KEY` |
| `local`   | `http://localhost:11434/v1` | none              |

Each request is one `POST` to `<base URL>/chat/completions`.

## Options

- `BaseUrl` replaces the preset's base URL. It must be `https`, unless the host is loopback
  (`localhost`, `127.0.0.0/8`, `::1`) or the preset is `local`; otherwise `Create` fails with E132,
  because the key would cross the network unencrypted. This applies to the Anthropic client too.
- `ApiKeyEnvironmentVariable` replaces the preset's key variable. Naming one on the `local` preset
  makes it required.
- `Model` is sent as the model name on every request.
- `Handler` replaces the network layer. Tests pass a fake `HttpMessageHandler`; production leaves it
  null.

## API keys

The key is read from the environment variable and nowhere else. It is never written to a file,
or logged. If the variable is empty or unset, `Create` throws `InvalidOperationException` with the
message `E130: <variable> is not set; export the API key in that variable or pass --api-key-env`.

A malformed `BaseUrl` fails with
`E132: base URL '<value>' is not an absolute http(s) URL; pass --base-url https://host/v1`.

When the provider answers with an HTTP error, such as 401 for a rejected key, the call throws
`ChatProviderException` with `Status` set and the message
`E131: the provider returned HTTP <status>; check the key in <variable> and the base URL`. The
response body is left out because a server may echo the key into it, and the SDK's own exception is
not attached for the same reason.

When no response comes back at all (connection refused, server down) the call throws
`ChatProviderException` with `Status` 0 and
`E133: the provider could not be reached; check the base URL and that the server is running`.

Error codes E130 to E139 are reserved for the chat clients (`AiErrorCodes`).

## Pointing at a local server

Use the `local` preset and set `BaseUrl` to the server's OpenAI-compatible root. No key is needed.

| Server    | Default `BaseUrl`           |
| --------- | --------------------------- |
| Ollama    | `http://localhost:11434/v1` |
| vLLM      | `http://localhost:8000/v1`  |
| llama.cpp | `http://localhost:8080/v1`  |

`Model` must be a name the server knows, for example the tag shown by `ollama list`.

## The ask command

```text
coh-engine ask --build <dump-dir|file.mbd> --model NAME
  [--provider anthropic|openai|mistral|local] [--base-url URL] [--api-key-env NAME]
  [--] "<question>"
```

Put `--` before a question that starts with a dash. Option values may not be empty, and
`--api-key-env` must be a variable name (letters, digits and underscores, not starting with a
digit), so a key typed there by mistake is refused without being printed.

The command sends the question to the model with the engine's tools attached, runs each tool the
model calls in the local engine, sends the result back, and prints the model's final text. The
provider defaults to `anthropic`. The key comes from the provider's variable
(`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `MISTRAL_API_KEY`) or from the variable named by
`--api-key-env`; there is no option that takes a key.

The tools are the same functions the MCP server exposes (`rules`, `check_legality`,
`check_hard_limits`, `score`, `totals`, `set_bonus_lookup`, `power_lookup`), listed in
`EngineToolCatalog`. The model never passes the build path. A `.mbd` build gets `rules` and
`validate`, because the other tools need a Mids dump directory. `chain` is not offered. A tool
failure goes back to the model as text starting `error:`.

The build is read before the first model call, so a directory that is not a Mids dump fails at once.
A question takes at most 8 model replies, each limited to 4096 output tokens. Exit codes: 0 with the
answer on standard output, 1 on a failed run with one `error:` line on standard error, 2 on a bad
command line. A caller's cancellation exits 1 without a message.

Error codes E170 to E179 are reserved for `ask` (`AskErrorCodes`): E170 the build cannot be read,
E171 the model was still calling tools at the round limit, E172 the provider did not answer in time,
sent an unreadable reply or returned no text, E173 the reply stopped at the output limit (the
partial text is still printed). Provider failures keep their own codes (E130 to E133, E140 to E142).
