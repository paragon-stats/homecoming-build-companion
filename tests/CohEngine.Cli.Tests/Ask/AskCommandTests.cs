using System.Net;

using CohEngine.Cli.Ask;
using CohEngine.Core.BuildFile;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests.Ask;

/// <summary><c>ask</c> end to end against canned provider replies; no network and no live model.</summary>
public sealed class AskCommandTests : IDisposable
{
    private const string Answer = "The build is a Scrapper and every metric was computed.";
    private const string Question = "Which class is this build?";
    private const string TestKey = "sk-test-secret-value";

    private static readonly FixtureStore Store = FixtureStore.Locate(AppContext.BaseDirectory);
    private static readonly string Dump = Path.Join(Store.MidsDirectory, "builds", "shield_scrapper_set_bonuses");
    private static readonly string Bane = CliCapture.MbdPath("Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training)");

    private readonly string scratch = Directory.CreateTempSubdirectory("coh-ask").FullName;

    public void Dispose()
    {
        Directory.Delete(scratch, recursive: true);
    }

    private static string Fixture(string name)
    {
        return File.ReadAllText(Path.Join(Store.Root, "ai", "ask", name));
    }

    private static string AiFixture(string name)
    {
        return File.ReadAllText(Path.Join(Store.Root, "ai", "anthropic", name));
    }

    private static async Task<(int Exit, string Output, string Error)> RunAsync(HttpMessageHandler handler, IMbdBuildSource? source, params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await AskCommand.RunAsync(args, output, error, source, handler, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (exit, output.ToString(), error.ToString());
    }

    private static string[] Args(string provider, string keyVariable, string build, string extra = "--model=test-model")
    {
        return [Question, "--provider", provider, "--build", build, "--api-key-env", keyVariable, extra];
    }

    private static void WithKey(string variable, string? value)
    {
        Environment.SetEnvironmentVariable(variable, value);
    }

    [Fact]
    public async Task The_model_asks_for_a_tool_gets_the_engines_result_and_answers_over_anthropic()
    {
        WithKey("COH_ASK_TEST_KEY_A", TestKey);
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-tool-use-score.json"), Fixture("anthropic-answer.json"));

        (int exit, string output, string error) = await RunAsync(handler, null, Args("anthropic", "COH_ASK_TEST_KEY_A", Dump));

        Assert.Equal((0, Answer + Environment.NewLine, string.Empty), (exit, output, error));
        Assert.Equal(2, handler.Sent.Count);
        Assert.Contains("\"name\":\"score\"", handler.Sent[0], StringComparison.Ordinal);
        Assert.Contains(Question, handler.Sent[0], StringComparison.Ordinal);
        Assert.Contains("toolu_ask_01", handler.Sent[1], StringComparison.Ordinal);
        Assert.Contains("tool_result", handler.Sent[1], StringComparison.Ordinal);
        Assert.Contains("Class_Scrapper", handler.Sent[1], StringComparison.Ordinal);
        Assert.DoesNotContain(TestKey, handler.Sent[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("mistral")]
    public async Task The_same_loop_runs_over_the_openai_compatible_providers(string provider)
    {
        string variable = "COH_ASK_TEST_KEY_" + provider.ToUpperInvariant();
        WithKey(variable, TestKey);
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("openai-tool-call-score.json"), Fixture("openai-answer.json"));

        (int exit, string output, string error) = await RunAsync(handler, null, Args(provider, variable, Dump));

        Assert.Equal((0, Answer + Environment.NewLine, string.Empty), (exit, output, error));
        Assert.Equal(2, handler.Sent.Count);
        Assert.Contains("call_ask_01", handler.Sent[1], StringComparison.Ordinal);
        Assert.Contains("Class_Scrapper", handler.Sent[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_local_server_needs_no_key_and_takes_a_base_url()
    {
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("openai-answer.json"));

        (int exit, string output, _) = await RunAsync(handler, null, Question, "--provider", "local", "--build", Dump, "--model", "llama3", "--base-url", "http://localhost:8080/v1");

        Assert.Equal((0, Answer + Environment.NewLine), (exit, output));
        Assert.Contains("\"model\":\"llama3\"", Assert.Single(handler.Sent), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bare_mbd_gets_the_rules_and_validate_tools()
    {
        WithKey("COH_ASK_TEST_KEY_MBD", TestKey);
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-tool-use-validate.json"), Fixture("anthropic-answer.json"));

        (int exit, string output, _) = await RunAsync(handler, new MbdBuildSource(Store.MidsDirectory), Args("anthropic", "COH_ASK_TEST_KEY_MBD", Bane));

        Assert.Equal((0, Answer + Environment.NewLine), (exit, output));
        Assert.Contains("\"name\":\"validate\"", handler.Sent[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"score\"", handler.Sent[0], StringComparison.Ordinal);
        Assert.Contains("toolu_ask_02", handler.Sent[1], StringComparison.Ordinal);
        Assert.Contains("tool_result", handler.Sent[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_tool_and_a_tool_missing_its_argument_go_back_to_the_model_as_error_text()
    {
        WithKey("COH_ASK_TEST_KEY_B", TestKey);
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-tool-use-unknown.json"), Fixture("anthropic-answer.json"));

        (int exit, _, _) = await RunAsync(handler, null, Args("anthropic", "COH_ASK_TEST_KEY_B", Dump));

        Assert.Equal(0, exit);
        Assert.Contains("error: no tool named no_such_tool", handler.Sent[1], StringComparison.Ordinal);
        Assert.Contains("error: the tool needs the argument fullName", handler.Sent[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_model_that_never_stops_calling_tools_ends_at_the_round_limit()
    {
        WithKey("COH_ASK_TEST_KEY_C", TestKey);
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-tool-use-score.json"));

        (int exit, string output, string error) = await RunAsync(handler, null, Args("anthropic", "COH_ASK_TEST_KEY_C", Dump));

        Assert.Equal((1, string.Empty), (exit, output));
        Assert.StartsWith($"error: {AskErrorCodes.RoundLimit}: the model was still calling tools after {AskCommand.MaxRounds} rounds", error, StringComparison.Ordinal);
        Assert.Equal(AskCommand.MaxRounds, handler.Sent.Count);
    }

    [Theory]
    [InlineData("anthropic", "E140")]
    [InlineData("openai", "E130")]
    public async Task A_missing_key_is_named_and_nothing_is_sent(string provider, string code)
    {
        WithKey("COH_ASK_TEST_KEY_UNSET", null);
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("openai-answer.json"));

        (int exit, string output, string error) = await RunAsync(handler, null, Args(provider, "COH_ASK_TEST_KEY_UNSET", Dump));

        Assert.Equal((1, string.Empty), (exit, output));
        Assert.StartsWith($"error: {code}: COH_ASK_TEST_KEY_UNSET is not set", error, StringComparison.Ordinal);
        Assert.Empty(handler.Sent);
    }

    [Fact]
    public async Task An_http_error_is_reported_with_its_code_and_never_echoes_the_key_or_the_body()
    {
        WithKey("COH_ASK_TEST_KEY_D", TestKey);
        SequenceHandler anthropic = new(HttpStatusCode.Unauthorized, AiFixture("error-401.json"));
        SequenceHandler openai = new(HttpStatusCode.InternalServerError, "{\"error\":\"" + TestKey + "\"}");

        (int anthropicExit, _, string anthropicError) = await RunAsync(anthropic, null, Args("anthropic", "COH_ASK_TEST_KEY_D", Dump));
        (int openaiExit, _, string openaiError) = await RunAsync(openai, null, Args("openai", "COH_ASK_TEST_KEY_D", Dump));

        Assert.Equal((1, 1), (anthropicExit, openaiExit));
        Assert.StartsWith("error: Anthropic API 401 authentication_error", anthropicError, StringComparison.Ordinal);
        Assert.StartsWith("error: E131: the provider returned HTTP 500", openaiError, StringComparison.Ordinal);
        Assert.DoesNotContain(TestKey, anthropicError + openaiError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_server_and_a_reply_that_is_not_json_are_a_provider_reply_error()
    {
        WithKey("COH_ASK_TEST_KEY_E", TestKey);

        (int downExit, _, string downError) = await RunAsync(new ThrowingHandler(), null, Args("anthropic", "COH_ASK_TEST_KEY_E", Dump));
        (int garbledExit, _, string garbledError) = await RunAsync(new SequenceHandler(HttpStatusCode.OK, "this is not JSON"), null, Args("anthropic", "COH_ASK_TEST_KEY_E", Dump));

        Assert.Equal((1, 1), (downExit, garbledExit));
        Assert.StartsWith($"error: {AskErrorCodes.ProviderReply}: the provider did not give a usable reply (HttpRequestException)", downError, StringComparison.Ordinal);
        Assert.StartsWith($"error: {AskErrorCodes.ProviderReply}: the provider did not give a usable reply (JsonException)", garbledError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bad_base_url_is_named()
    {
        WithKey("COH_ASK_TEST_KEY_F", TestKey);

        (int exit, _, string error) = await RunAsync(new ThrowingHandler(), null, Question, "--build", Dump, "--api-key-env", "COH_ASK_TEST_KEY_F", "--model", "m", "--base-url=ftp://example.test");

        Assert.Equal(1, exit);
        Assert.StartsWith("error: E132: base URL 'ftp://example.test'", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_build_that_cannot_be_read_exits_with_its_own_code()
    {
        WithKey("COH_ASK_TEST_KEY_G", TestKey);
        string garbled = Path.Join(scratch, "broken.mbd");
        await File.WriteAllTextAsync(garbled, "not a build", TestContext.Current.CancellationToken);
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-answer.json"));
        MbdBuildSource source = new(Store.MidsDirectory);

        (int missingExit, _, string missingError) = await RunAsync(handler, source, Args("anthropic", "COH_ASK_TEST_KEY_G", Path.Join(scratch, "no-such-build")));
        (int brokenExit, _, string brokenError) = await RunAsync(handler, source, Args("anthropic", "COH_ASK_TEST_KEY_G", garbled));
        (int noSourceExit, _, string noSourceError) = await RunAsync(handler, null, Args("anthropic", "COH_ASK_TEST_KEY_G", Bane));

        Assert.Equal((1, 1, 1), (missingExit, brokenExit, noSourceExit));
        Assert.StartsWith($"error: {AskErrorCodes.BuildUnreadable}: '", missingError, StringComparison.Ordinal);
        Assert.StartsWith($"error: {AskErrorCodes.BuildUnreadable}: cannot read the build file", brokenError, StringComparison.Ordinal);
        Assert.StartsWith($"error: {AskErrorCodes.BuildUnreadable}: cannot read the build file", noSourceError, StringComparison.Ordinal);
        Assert.Empty(handler.Sent);
    }

    [Theory]
    [InlineData("the following arguments are required: question", "--build", "x", "--model", "m")]
    [InlineData("the following arguments are required: --build", "q", "--model", "m")]
    [InlineData("the following arguments are required: --model", "q", "--build", "x")]
    [InlineData("argument --provider: invalid choice: 'gemini'", "q", "--build", "x", "--model", "m", "--provider", "gemini")]
    [InlineData("unrecognized arguments: --stream", "q", "--build", "x", "--model", "m", "--stream")]
    [InlineData("unrecognized arguments: second", "q", "second", "--build", "x", "--model", "m")]
    [InlineData("argument --api-key-env: expected one argument", "q", "--build", "x", "--model", "m", "--api-key-env")]
    public async Task A_bad_command_line_is_a_usage_error(string problem, params string[] args)
    {
        (int exit, string output, string error) = await RunAsync(new ThrowingHandler(), null, args);

        Assert.Equal((2, string.Empty), (exit, output));
        Assert.StartsWith("usage: coh-engine ask", error, StringComparison.Ordinal);
        Assert.Contains("coh-engine ask: error: " + problem, error, StringComparison.Ordinal);
    }
}
