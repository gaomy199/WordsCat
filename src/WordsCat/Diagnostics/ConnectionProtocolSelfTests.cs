using System.Net;
using System.Text;
using System.Text.Json;

namespace ReadDock;

// No production vault, real endpoint or sample content is used by this regression.
internal static class ConnectionProtocolSelfTests
{
    static void Require(bool value, string name) { if (!value) throw new Exception("Connection protocol regression: " + name); }
    internal static async Task RunAsync()
    {
        var config = new AppConfig { BaseUrl = ModelPresets.GiteeBaseUrl, Model = "Qwen3-32B", RequestTimeoutSeconds = 100, MaxRetries = 5 };
        var recording = new RecordingService();
        var result = await new ConnectionTester(() => recording).TestAsync(config, new ServiceSetupSelfTests.FakeVault(true), CancellationToken.None);
        Require(result.Kind == ConnectionTestKind.Success && recording.Options!.Timeout == TimeSpan.FromSeconds(100), "configured timeout is not cut to 15 seconds");
        Require(recording.Options!.MaxRetries == 0 && recording.Options.MaxOutputTokens == 128 && recording.Prompt == "Reply OK. /no_think", "bounded Qwen non-thinking probe contains no user data");
        foreach (var test in new[]
        {
            ("ok", ConnectionTestKind.Success), ("length", ConnectionTestKind.OutputLimit),
            ("reasoning", ConnectionTestKind.OutputLimit), ("bad-shape", ConnectionTestKind.InvalidResponse),
            ("dns", ConnectionTestKind.NameResolutionFailure), ("tls", ConnectionTestKind.SecureConnectionFailure),
            ("proxy", ConnectionTestKind.ProxyFailure), ("network", ConnectionTestKind.NetworkOrTimeout),
            ("timeout", ConnectionTestKind.Timeout), ("unauthorized", ConnectionTestKind.InvalidKey)
        })
        {
            var handler = new Handler(test.Item1);
            result = await new ConnectionTester(() => new OpenAiCompatibleClient(handler)).TestAsync(config, new ServiceSetupSelfTests.FakeVault(true), CancellationToken.None);
            Require(result.Kind == test.Item2 && handler.Calls == 1, "correct category and single request: " + test.Item1);
            Require(!result.Message.Contains(SecuritySelfTests.FakeCredential) && !result.Message.Contains("Authorization"), "safe message: " + test.Item1);
            if (test.Item1 == "timeout") Require(result.TimeoutSeconds == 100 && result.Message.Contains("100"), "timeout includes configured duration");
        }
        using var truncated = new OpenAiCompatibleClient(new Handler("length"));
        try
        {
            await truncated.CompleteAsync(ModelRequestOptions.From(config), SecuritySelfTests.FakeCredential, [new ChatMessage("user", "Synthetic selection")], false, CancellationToken.None);
            throw new Exception("Truncated translation must not be accepted as complete.");
        }
        catch (ModelOutputLimitException) { }
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        result = await new ConnectionTester(() => new OpenAiCompatibleClient(new Handler("ok"))).TestAsync(config, new ServiceSetupSelfTests.FakeVault(true), cancel.Token);
        Require(result.Kind == ConnectionTestKind.Cancelled, "user cancellation remains distinct from timeout");
        string language = UiText.Language;
        try
        {
            UiText.Language = "en";
            Require(new ConnectionTestResult(ConnectionTestKind.Timeout, 100).Message.Contains("100 seconds"), "localized timeout is formatted");
        }
        finally { UiText.Language = language; }
    }
    sealed class RecordingService : IModelService
    {
        internal ModelRequestOptions? Options;
        internal string Prompt = "";
        public Task<ApiResult> CompleteAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, bool json, CancellationToken token)
        {
            Require(key == SecuritySelfTests.FakeCredential, "fixed fake key only"); Options = options; Prompt = messages.Single().Content;
            return Task.FromResult(new ApiResult("OK", 1, 1));
        }
        public Task<ApiResult> CompleteImageAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, CancellationToken token) => throw new Exception("No image requests expected.");
        public void Dispose() { }
    }
    sealed class Handler(string mode) : HttpMessageHandler
    {
        internal int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Require(request.Headers.Authorization?.Parameter == SecuritySelfTests.FakeCredential, "fixed fake key only");
            Require(request.RequestUri?.AbsoluteUri == ModelPresets.GiteeBaseUrl + "/chat/completions", "official Gitee chat route");
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Require(!payload.RootElement.TryGetProperty("response_format", out _), "connection probe does not require JSON mode");
            if (mode == "timeout") throw new TaskCanceledException();
            var error = mode switch { "dns" => HttpRequestError.NameResolutionError, "tls" => HttpRequestError.SecureConnectionError, "proxy" => HttpRequestError.ProxyTunnelError, _ => HttpRequestError.ConnectionError };
            if (mode is "dns" or "tls" or "proxy" or "network") throw new HttpRequestException(error, "Synthetic transport failure");
            string body = mode == "bad-shape" ? "{}" : JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = mode is "length" or "reasoning" ? "length" : "stop", message = new { content = mode == "reasoning" ? null : "OK", reasoning_content = mode == "reasoning" ? "Synthetic reasoning" : "" } } } });
            return new HttpResponseMessage(mode == "unauthorized" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
