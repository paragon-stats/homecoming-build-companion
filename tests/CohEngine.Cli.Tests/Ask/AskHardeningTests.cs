using System.Net;

using CohEngine.Cli.Ask;
using CohEngine.Core.BuildFile;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests.Ask;

/// <summary>The input checks, transport failures and reply handling of <c>ask</c> that the happy-path tests do not reach.</summary>
public sealed class AskHardeningTests : IDisposable
{
    private const string Question = "Which class is this build?";
    private const string TestKey = "sk-test-secret-value";

    private static readonly FixtureStore Store = FixtureStore.Locate(AppContext.BaseDirectory);
    private static readonly string Dump = Path.Join(Store.MidsDirectory, "builds", "shield_scrapper_set_bonuses");
    private static readonly string Bane = CliCapture.MbdPath("Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training)");

    private readonly string scratch = Directory.CreateTempSubdirectory("coh-ask-hard").FullName;

    public void Dispose()
    {
        Directory.Delete(scratch, recursive: true);
    }

    private static string Fixture(string name)
    {
        return File.ReadAllText(Path.Join(Store.Root, "ai", "ask", name));
    }

    private static async Task<(int Exit, string Output, string Error)> RunAsync(HttpMessageHandler handler, CancellationToken token, params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await AskCommand.RunAsync(args, output, error, new MbdBuildSource(Store.MidsDirectory), handler, token).ConfigureAwait(false);
        return (exit, output.ToString(), error.ToString());
    }

    private static Task<(int Exit, string Output, string Error)> RunAsync(HttpMessageHandler handler, params string[] args)
    {
        return RunAsync(handler, TestContext.Current.CancellationToken, args);
    }

    private static string[] Args(string provider, string keyVariable, string? baseUrl = null, string build = "")
    {
        string[] args = [Question, "--provider", provider, "--build", build.Length == 0 ? Dump : build, "--api-key-env", keyVariable, "--model", "test-model"];
        return baseUrl is null ? args : [.. args, "--base-url", baseUrl];
    }

    private static void WithKey(string variable)
    {
        Environment.SetEnvironmentVariable(variable, TestKey);
    }

    [Theory]
    [InlineData("--model=")]
    [InlineData("--api-key-env=")]
    [InlineData("--build=")]
    public async Task An_empty_option_value_is_a_usage_error(string option)
    {
        string[] args = [Question, "--build", Dump, "--model", "m", option];

        (int exit, string output, string error) = await RunAsync(new ThrowingHandler(), args);

        Assert.Equal((2, string.Empty), (exit, output));
        Assert.Contains($"argument {option[..^1]}: expected a non-empty value", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_question_is_a_usage_error()
    {
        (int exit, _, string error) = await RunAsync(new ThrowingHandler(), string.Empty, "--build", Dump, "--model", "m");

        Assert.Equal(2, exit);
        Assert.Contains("the following arguments are required: question", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--api-key-env", TestKey)]
    [InlineData("--api-key-env=" + TestKey, "")]
    [InlineData("--api-key-env", "1STARTS_WITH_DIGIT")]
    public async Task A_key_given_where_a_variable_name_belongs_is_refused_without_echoing_it(string first, string second)
    {
        string[] args = second.Length == 0 ? [Question, "--build", Dump, "--model", "m", first] : [Question, "--build", Dump, "--model", "m", first, second];

        (int exit, string output, string error) = await RunAsync(new ThrowingHandler(), args);

        Assert.Equal(2, exit);
        Assert.Contains("argument --api-key-env: takes the name of an environment variable, not a key", error, StringComparison.Ordinal);
        Assert.DoesNotContain(TestKey, output + error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_option_is_named_without_its_value()
    {
        (int exit, _, string error) = await RunAsync(new ThrowingHandler(), Question, "--build", Dump, "--model", "m", "--api-key=" + TestKey);

        Assert.Equal(2, exit);
        Assert.Contains("unrecognized arguments: --api-key", error, StringComparison.Ordinal);
        Assert.DoesNotContain(TestKey, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_question_that_starts_with_a_dash_is_taken_after_the_terminator()
    {
        WithKey("COH_ASK_HARD_KEY_1");
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-answer.json"));

        (int exit, _, _) = await RunAsync(handler, "--build", Dump, "--model", "m", "--api-key-env", "COH_ASK_HARD_KEY_1", "--", "-why is it so?");

        Assert.Equal(0, exit);
        Assert.Contains("-why is it so?", Assert.Single(handler.Sent), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("anthropic", "http://api.example.test")]
    [InlineData("openai", "http://api.example.test/v1")]
    [InlineData("mistral", "http://10.0.0.5:8000/v1")]
    public async Task A_remote_http_base_url_is_refused_before_any_request(string provider, string url)
    {
        WithKey("COH_ASK_HARD_KEY_2");
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("openai-answer.json"));

        (int exit, string output, string error) = await RunAsync(handler, Args(provider, "COH_ASK_HARD_KEY_2", url));

        Assert.Equal((1, string.Empty), (exit, output));
        Assert.StartsWith($"error: E132: base URL '{url}", error, StringComparison.Ordinal);
        Assert.Contains("use https, or a loopback address", error, StringComparison.Ordinal);
        Assert.Empty(handler.Sent);
    }

    [Theory]
    [InlineData("anthropic", "https://proxy.example.test", "anthropic-answer.json")]
    [InlineData("openai", "https://proxy.example.test/v1", "openai-answer.json")]
    [InlineData("anthropic", "http://localhost:8080", "anthropic-answer.json")]
    [InlineData("openai", "http://127.0.0.1:8000/v1", "openai-answer.json")]
    [InlineData("mistral", "http://[::1]:8000/v1", "openai-answer.json")]
    [InlineData("local", "http://llm.example.test:8000/v1", "openai-answer.json")]
    public async Task An_https_remote_a_loopback_host_or_the_local_preset_is_accepted(string provider, string url, string reply)
    {
        WithKey("COH_ASK_HARD_KEY_3");
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture(reply));

        (int exit, _, _) = await RunAsync(handler, Args(provider, "COH_ASK_HARD_KEY_3", url));

        Assert.Equal(0, exit);
        Assert.Single(handler.Sent);
    }

    [Fact]
    public async Task A_directory_that_is_not_a_dump_exits_with_the_build_code_before_any_request()
    {
        WithKey("COH_ASK_HARD_KEY_4");
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-answer.json"));

        (int exit, string output, string error) = await RunAsync(handler, Args("anthropic", "COH_ASK_HARD_KEY_4", build: scratch));

        Assert.Equal((1, string.Empty), (exit, output));
        Assert.StartsWith($"error: {AskErrorCodes.BuildUnreadable}: cannot read the build '", error, StringComparison.Ordinal);
        Assert.Empty(handler.Sent);
    }

    [Fact]
    public async Task The_mbd_extension_is_matched_in_any_case()
    {
        WithKey("COH_ASK_HARD_KEY_5");
        string upper = Path.Join(scratch, "BUILD.MBD");
        File.Copy(Bane, upper);
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-answer.json"));

        (int exit, _, _) = await RunAsync(handler, Args("anthropic", "COH_ASK_HARD_KEY_5", build: upper));

        Assert.Equal(0, exit);
        Assert.Contains("\"name\":\"validate\"", Assert.Single(handler.Sent), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_reply_with_no_text_is_a_provider_reply_error()
    {
        WithKey("COH_ASK_HARD_KEY_6");

        (int exit, string output, string error) = await RunAsync(new SequenceHandler(HttpStatusCode.OK, Fixture("anthropic-empty.json")), Args("anthropic", "COH_ASK_HARD_KEY_6"));

        Assert.Equal((1, string.Empty), (exit, output));
        Assert.StartsWith($"error: {AskErrorCodes.ProviderReply}: the model returned no text", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_reply_cut_off_at_the_output_limit_is_printed_and_exits_non_zero()
    {
        WithKey("COH_ASK_HARD_KEY_7");
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-truncated.json"));

        (int exit, string output, string error) = await RunAsync(handler, Args("anthropic", "COH_ASK_HARD_KEY_7"));

        Assert.Equal(1, exit);
        Assert.StartsWith("The build is a Scrapper and the first metrics are", output, StringComparison.Ordinal);
        Assert.StartsWith($"error: {AskErrorCodes.ReplyCutOff}: the reply stopped at the output limit of {AskCommand.MaxOutputTokens} tokens", error, StringComparison.Ordinal);
        Assert.Contains($"\"max_tokens\":{AskCommand.MaxOutputTokens}", Assert.Single(handler.Sent), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_timeout_is_a_provider_reply_error_and_a_caller_cancellation_stops_quietly()
    {
        WithKey("COH_ASK_HARD_KEY_8");
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        (int timeoutExit, _, string timeoutError) = await RunAsync(new TimeoutHandler(), Args("anthropic", "COH_ASK_HARD_KEY_8"));
        (int cancelExit, string cancelOutput, string cancelError) = await RunAsync(new SequenceHandler(HttpStatusCode.OK, Fixture("anthropic-answer.json")), cancelled.Token, Args("anthropic", "COH_ASK_HARD_KEY_8"));

        Assert.Equal((1, 1), (timeoutExit, cancelExit));
        Assert.StartsWith($"error: {AskErrorCodes.ProviderReply}: the provider did not answer in time", timeoutError, StringComparison.Ordinal);
        Assert.Equal((string.Empty, string.Empty), (cancelOutput, cancelError));
    }

    [Fact]
    public async Task An_unreachable_openai_compatible_server_is_not_reported_as_an_http_status()
    {
        WithKey("COH_ASK_HARD_KEY_9");

        (int exit, _, string error) = await RunAsync(new ThrowingHandler(), Args("openai", "COH_ASK_HARD_KEY_9"));

        Assert.Equal(1, exit);
        Assert.DoesNotContain("HTTP 0", error, StringComparison.Ordinal);
        Assert.StartsWith("error: E133: the provider could not be reached", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_error_body_that_echoes_the_key_never_reaches_the_output()
    {
        WithKey("COH_ASK_HARD_KEY_10");
        SequenceHandler anthropic = new(HttpStatusCode.Unauthorized, Fixture("anthropic-error-echo-key.json"));

        (int exit, string output, string error) = await RunAsync(anthropic, Args("anthropic", "COH_ASK_HARD_KEY_10"));

        Assert.Equal(1, exit);
        Assert.StartsWith("error: Anthropic API 401 authentication_error: invalid x-api-key", error, StringComparison.Ordinal);
        Assert.DoesNotContain(TestKey, output + error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_engine_tool_error_goes_back_to_the_model_as_text()
    {
        WithKey("COH_ASK_HARD_KEY_11");
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("anthropic-tool-use-bad-power.json"), Fixture("anthropic-answer.json"));

        (int exit, _, _) = await RunAsync(handler, Args("anthropic", "COH_ASK_HARD_KEY_11"));

        Assert.Equal(0, exit);
        Assert.Contains("error: E167: no power named", handler.Sent[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tool_call_whose_arguments_cannot_be_read_is_not_run()
    {
        WithKey("COH_ASK_HARD_KEY_12");
        SequenceHandler handler = new(HttpStatusCode.OK, Fixture("openai-tool-call-bad-arguments.json"), Fixture("openai-answer.json"));

        (int exit, _, _) = await RunAsync(handler, Args("openai", "COH_ASK_HARD_KEY_12"));

        Assert.Equal(0, exit);
        Assert.Contains("the tool was not run", handler.Sent[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Class_Scrapper", handler.Sent[1], StringComparison.Ordinal);
    }
}
