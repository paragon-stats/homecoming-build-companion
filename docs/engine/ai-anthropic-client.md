# Anthropic chat client

`CohEngine.Ai.Anthropic.AnthropicChatClient` is an `IChatClient` (Microsoft.Extensions.AI) for the
Anthropic Messages API. Nothing in the engine calls it yet; the `ask` command (#112) will.

## Why it is hand-written

The official `Anthropic` NuGet package is not Native AOT compatible: its constructors use
reflection-based `System.Text.Json` (upstream issue #79, no fix date). The release binary is a
Native AOT executable, so the client is written over `HttpClient` instead:

- request and response bodies are records with a source-generated `AnthropicJsonContext`;
- the Server-Sent Events stream is parsed by hand (`SseParser`), with no `Regex`;
- no `dynamic`, and the client's own bodies are serialized only through the generated context.

Caller values have two limits under Native AOT:

- tool-call arguments are serialized with the Microsoft.Extensions.AI serializer options, so each
  argument value must be a type those options know: a `JsonElement`, a string, a number or a
  boolean;
- tool results are not serialized at all. A result must be a string, a `JsonElement` or null; any
  other type throws `ArgumentException` with
  `E142: tool result must be a string or JsonElement under AOT; serialise it before returning it`.

The project sets `IsAotCompatible`, so a trim or AOT warning (IL2026, IL3050) fails the build.

## Construction

```csharp
new AnthropicChatClient(model, baseUrl: null, apiKeyEnvironmentVariable: null, handler: null)
```

- `model` is sent when `ChatOptions.ModelId` is unset.
- `baseUrl` replaces `https://api.anthropic.com`, for a proxy or gateway. Requests go to
  `<baseUrl>/v1/messages`.
- `apiKeyEnvironmentVariable` replaces `ANTHROPIC_API_KEY`.
- `handler` replaces the network layer. Tests pass a fake `HttpMessageHandler`; production leaves it
  null and shares one `HttpClient` across clients. That client recycles pooled connections every
  5 minutes and times a request out after 10 minutes, so a long generation is not cut off.

The parameters follow the order of `ChatProviderOptions`, so the factory can build the client with
one `case`.

Each request carries the headers `x-api-key`, `anthropic-version: 2023-06-01` and
`content-type: application/json`.

## API keys

The key is read from the environment variable at construction and nowhere else. It is never logged
or put in an exception; an API error type, message or request id that echoes it is redacted. If the
variable is empty or unset, the constructor throws `InvalidOperationException` with the message
`E140: <variable> is not set; export the API key in that variable or pass --api-key-env`.

Error codes E140 to E149 are reserved for this client (`AnthropicErrorCodes`).

## What is supported

| `IChatClient` input           | Messages API field                                       |
| ----------------------------- | -------------------------------------------------------- |
| system messages, instructions | top-level `system`, joined with blank lines              |
| user and assistant text       | `text` blocks                                            |
| `FunctionCallContent`         | `tool_use` block; the arguments become `input`           |
| `FunctionResultContent`       | `tool_result` block in a user turn; see E142 above       |
| `ChatOptions.ModelId`         | `model`                                                  |
| `ChatOptions.MaxOutputTokens` | `max_tokens`; 1024 when unset, because the API needs one |
| `ChatOptions.Temperature`     | `temperature`                                            |
| function declarations         | `tools`, with the JSON schema as `input_schema`          |
| `ChatOptions.ToolMode`        | `tool_choice`: `auto`, `none`, `any` or `tool`           |

Responses map `text` blocks to `TextContent` and `tool_use` blocks to `FunctionCallContent`, whose
arguments are the API's JSON values, unchanged. `stop_reason` maps to the finish reason
(`end_turn` and `stop_sequence` to stop, `max_tokens` to length, `tool_use` to tool calls, `refusal`
to content filter) and `usage` to the token counts; a count the API sends as null stays null. Other
block types, such as server tool results and thinking, are skipped.

Streaming (`GetStreamingResponseAsync`) yields one update per text delta. Tool-call argument
fragments are buffered per content block and the call is yielded whole when its block stops. A
fragment for a block that no `tool_use` start opened throws `JsonException`. The finish reason and
usage arrive with the `message_delta` event, and `ping` is skipped. A stream that ends before
`message_stop`, or with a tool call still open, throws the `AnthropicApiException` described under
Errors, with type `incomplete_stream` and the message
`E141: the response stream ended before message_stop; retry the request`.

Content the API cannot take, such as images or hosted tools, throws `NotSupportedException` rather
than being dropped.

## Errors

A non-2xx response and an `error` event inside a stream both throw one `AnthropicApiException`
with:

- `Status`: the HTTP status. An `error` event reports the stream's status, 200, because the API
  sends no other;
- `ErrorType`: the API's error type, such as `authentication_error` or `overloaded_error`, or
  `unknown_error` when the body is not the API's error JSON;
- `RequestId`: the `request-id` response header, for a support request.

## What is not supported

- Retries. An `overloaded_error` (HTTP 529) or a rate limit (HTTP 429) reaches the caller as it
  came; a caller that wants retries wraps the client.
- The Message Batches API.
- The Files API, images, documents and PDFs in messages.
- Extended thinking: `thinking` blocks and deltas are skipped, and no thinking budget is sent.
- Prompt caching (`cache_control`), stop sequences, `top_p` and `top_k`.
