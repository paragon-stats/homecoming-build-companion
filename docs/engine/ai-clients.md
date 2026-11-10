# Chat clients

`CohEngine.Ai` builds an `IChatClient` (Microsoft.Extensions.AI) that speaks the OpenAI chat
completions protocol. `ChatClientFactory.Create(ChatProviderOptions)` returns it. Nothing in the
engine calls it yet; the `ask` command (#112) will.

## Presets

| Preset    | Base URL                    | Key variable      |
| --------- | --------------------------- | ----------------- |
| `openai`  | `https://api.openai.com/v1` | `OPENAI_API_KEY`  |
| `mistral` | `https://api.mistral.ai/v1` | `MISTRAL_API_KEY` |
| `local`   | `http://localhost:11434/v1` | none              |

Each request is one `POST` to `<base URL>/chat/completions`.

## Options

- `BaseUrl` replaces the preset's base URL.
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

Error codes E130 to E139 are reserved for the chat clients (`AiErrorCodes`).

## Pointing at a local server

Use the `local` preset and set `BaseUrl` to the server's OpenAI-compatible root. No key is needed.

| Server    | Default `BaseUrl`           |
| --------- | --------------------------- |
| Ollama    | `http://localhost:11434/v1` |
| vLLM      | `http://localhost:8000/v1`  |
| llama.cpp | `http://localhost:8080/v1`  |

`Model` must be a name the server knows, for example the tag shown by `ollama list`.
