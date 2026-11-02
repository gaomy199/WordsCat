using System.Net;
using System.Text;
using System.Text.Json;

namespace ReadDock;

internal static class ModelServiceSelfTests
{
    static void Require(bool condition, string name) { if (!condition) throw new Exception("Model service regression: " + name); }
    static async Task ExpectAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T ex)
        {
            Require(!ex.ToString().Contains(SecuritySelfTests.FakeCredential) && !ex.ToString().Contains("Authorization", StringComparison.OrdinalIgnoreCase), "safe exception boundary");
            return;
        }
        throw new Exception("Model service regression: expected safe rejection");
    }
    public static async Task RunAsync(string directory)
    {
        var defaults = JsonSerializer.Deserialize<AppConfig>("{}", AppConfig.Json)!;
        Require(defaults.BaseUrl == "https://api.deepseek.com" && defaults.Model == "deepseek-flash" && defaults.VisionModel == "deepseek-flash", "unchanged default preset");
        Require(defaults.RequestTimeoutSeconds == 100 && defaults.MaxRetries == 0 && defaults.ScreenshotEngine == "vision", "legacy timeout, no retries and vision default");
        var target = typeof(CredentialStore).GetField("Target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Require((string)target.GetRawConstantValue()! == "ReadDock.DeepSeek.ApiKey.v1", "stored credential compatibility target without reading it");

        string previous = AppPaths.DataDirectory;
        try
        {
            AppPaths.DataDirectory = Path.Combine(directory, "model-compat"); Directory.CreateDirectory(AppPaths.DataDirectory);
            string notebook = Path.Combine(AppPaths.DataDirectory, "legacy.md"), template = Path.Combine(AppPaths.DataDirectory, "custom.json");
            File.WriteAllText(notebook, "Existing notebook stays untouched.");
            File.WriteAllText(template, File.ReadAllText(AppPaths.DefaultTemplate) + "\n");
            byte[] templateBytes = File.ReadAllBytes(template);
            var legacy = new { schema_version = 2, base_url = "https://legacy.example.invalid/v1", model = "legacy-text", vision_model = "legacy-image", screenshot_engine = "local", target_file = notebook, template_file = template };
            File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(legacy));
            var loaded = AppConfig.Load();
            Require(loaded.BaseUrl == legacy.base_url && loaded.Model == legacy.model && loaded.VisionModel == legacy.vision_model && loaded.ScreenshotEngine == "local", "legacy provider and local OCR settings preserved");
            Require(loaded.RequestTimeoutSeconds == 100 && loaded.MaxRetries == 0, "missing new fields default safely");
            loaded.RequestTimeoutSeconds = 42; loaded.MaxRetries = 2; loaded.Save();
            var roundTrip = AppConfig.Load();
            Require(roundTrip.RequestTimeoutSeconds == 42 && roundTrip.MaxRetries == 2 && roundTrip.Model == "legacy-text", "new options round trip");
            Require(File.ReadAllBytes(template).SequenceEqual(templateBytes) && File.ReadAllText(notebook) == "Existing notebook stays untouched.", "custom template and notebook preserved");
            using var stored = JsonDocument.Parse(File.ReadAllText(AppPaths.ConfigFile));
            Require(!stored.RootElement.TryGetProperty("api_key", out _), "no stored credential field");
        }
        finally { AppPaths.DataDirectory = previous; }

        using var handler = new Handler();
        using IModelService service = new OpenAiCompatibleClient(handler);
        ITextModelClient text = service; IImageModelClient image = service;
        var options = new ModelRequestOptions("https://models.example.invalid/v1", "text-model", TimeSpan.FromSeconds(2));
        ChatMessage[] messages = [new("user", "Synthetic selection")];
        Task<ApiResult> Send(ModelRequestOptions? setting = null, CancellationToken token = default) => text.CompleteAsync(setting ?? options, SecuritySelfTests.FakeCredential, messages, false, token);
        var result = await Send();
        Require(result.Text == "synthetic response" && result.InputTokens == 7 && result.OutputTokens == 3, "normal response and usage");
        Require(handler.Endpoint == "https://models.example.invalid/v1/chat/completions" && handler.Last.GetProperty("model").GetString() == "text-model", "generic endpoint and model");
        Require(!handler.Last.TryGetProperty("thinking", out _) && !handler.Last.GetProperty("stream").GetBoolean() && handler.Last.GetProperty("max_tokens").GetInt32() == 4096, "standard request excludes provider extension");
        handler.Reset("401"); await ExpectAsync<InvalidOperationException>(() => Send(options with { MaxRetries = 2 })); Require(handler.Calls == 1, "401 never retried");
        handler.Reset("429"); await ExpectAsync<InvalidOperationException>(() => Send()); Require(handler.Calls == 1, "default 429 not retried");
        handler.Reset("429-once"); await Send(options with { MaxRetries = 1 }); Require(handler.Calls == 2, "explicit bounded retry");
        handler.Reset("429"); await ExpectAsync<InvalidOperationException>(() => Send(options with { MaxRetries = 2 })); Require(handler.Calls == 3, "retry limit");
        handler.Reset("network-once"); await Send(options with { MaxRetries = 1 }); Require(handler.Calls == 2, "safe transient transport retry");
        handler.Reset("html"); await ExpectAsync<InvalidDataException>(() => Send(options with { MaxRetries = 2 })); Require(handler.Calls == 1, "non-JSON body never exposed or retried");
        handler.Reset("timeout"); await ExpectAsync<TimeoutException>(() => Send(options with { Timeout = TimeSpan.FromMilliseconds(40) }));
        handler.Reset("429"); await ExpectAsync<TimeoutException>(() => Send(options with { Timeout = TimeSpan.FromMilliseconds(40), MaxRetries = 3 })); Require(handler.Calls == 1, "single total deadline covers backoff");
        handler.Reset("timeout"); using (var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(30)))
            await ExpectAsync<OperationCanceledException>(() => Send(token: cancelled.Token));
        handler.Reset("normal");
        var imageMessages = new[] { new ChatMessage("user", "Synthetic image", "data:image/png;base64,AA==") };
        await image.CompleteImageAsync(options with { Model = "vision-model" }, SecuritySelfTests.FakeCredential, imageMessages, CancellationToken.None);
        Require(handler.Last.GetProperty("model").GetString() == "vision-model" && handler.Last.GetProperty("response_format").GetProperty("type").GetString() == "json_object", "separate image capability and model");
        Require(handler.Last.GetProperty("messages")[0].GetProperty("content")[1].GetProperty("image_url").GetProperty("detail").GetString() == "auto", "portable image detail");
        await ExpectAsync<InvalidDataException>(() => text.CompleteAsync(options, SecuritySelfTests.FakeCredential, imageMessages, false, CancellationToken.None));
        await ExpectAsync<InvalidDataException>(() => image.CompleteImageAsync(options, SecuritySelfTests.FakeCredential, messages, CancellationToken.None));
    }

    sealed class Handler : HttpMessageHandler
    {
        string mode = "normal";
        public int Calls { get; private set; }
        public string Endpoint { get; private set; } = "";
        public JsonElement Last { get; private set; }
        public void Reset(string value) { mode = value; Calls = 0; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Require(request.Headers.Authorization?.Parameter == SecuritySelfTests.FakeCredential, "fixed fake credential only");
            Endpoint = request.RequestUri!.AbsoluteUri;
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)); Last = doc.RootElement.Clone();
            if (mode == "timeout") await Task.Delay(Timeout.Infinite, token);
            if (mode == "network-once" && Calls == 1) throw new HttpRequestException("Authorization" + ": Bearer " + SecuritySelfTests.FakeCredential);
            var status = mode == "401" ? HttpStatusCode.Unauthorized : mode == "429" || mode == "429-once" && Calls == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK;
            string safeBody = JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = "synthetic response" } } }, usage = new { prompt_tokens = 7, completion_tokens = 3 } });
            string body = mode == "html" || status != HttpStatusCode.OK ? "<html>" + "Authorization" + ": Bearer " + SecuritySelfTests.FakeCredential + "</html>" : safeBody;
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mode == "html" ? "text/html" : "application/json") };
        }
    }
}
