using System.Net;
using System.Text;
using System.Text.Json;

namespace ReadDock;

internal static class SecuritySelfTests
{
    internal const string FakeCredential = "test-api-key-not-real";
    static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException("Security regression: " + name); }

    public static async Task RunAsync(string directory)
    {
        // Generated negative probes are synthetic and must never be logged.
        string prefixProbe = string.Concat('s', 'k', '-') + new string('x', 32);
        string headerProbe = "Authorization" + ": Bearer " + FakeCredential;
        string assignmentProbe = JsonSerializer.Serialize(new Dictionary<string, string> { ["api" + "_key"] = prefixProbe });
        Require(SecretRedactor.Find(prefixProbe).Any(f => f.Type == "key-prefix"), "prefix detection");
        Require(SecretRedactor.Find(assignmentProbe).Any(f => f.Type == "credential-literal"), "literal detection");
        string headerNegative = "Authorization" + ": Bearer " + prefixProbe;
        Require(SecretRedactor.Find(headerNegative).Any(f => f.Type == "authorization-header"), "header detection");
        Require(SecretRedactor.Find(FakeCredential).Count == 0, "explicit fake value accepted");
        Require(SecretRedactor.FindBinary("Authorization" + ": Bearer " + '\0').Count == 0, "empty native header template is not a credential");
        SecretRedactor.Register(FakeCredential);
        string cleaned = SecretRedactor.Redact(prefixProbe + "\n" + headerProbe + "\n" + assignmentProbe);
        Require(!cleaned.Contains(prefixProbe) && !cleaned.Contains(FakeCredential) && !cleaned.Contains("Authorization", StringComparison.OrdinalIgnoreCase), "redacted diagnostics");
        string nested = SecretRedactor.Redact(new Exception("outer", new Exception(headerProbe)).ToString());
        Require(!nested.Contains(FakeCredential) && !nested.Contains("Authorization"), "nested exceptions");

        string previousDirectory = AppPaths.DataDirectory;
        string sinkDirectory = Path.Combine(directory, "security-sinks");
        Directory.CreateDirectory(sinkDirectory);
        try
        {
            AppPaths.DataDirectory = sinkDirectory;
            new AppConfig { Glossary = FakeCredential, TranslationInstruction = headerProbe }.Save();
            string settings = File.ReadAllText(AppPaths.ConfigFile);
            using var parsed = JsonDocument.Parse(settings);
            Require(!settings.Contains(FakeCredential) && !settings.Contains("Authorization"), "settings JSON persistence");
            Require(!parsed.RootElement.TryGetProperty("api_key", out _), "settings have no credential property");
            var history = new ReadingHistory(Path.Combine(sinkDirectory, "history"));
            history.Add(FakeCredential, headerProbe, 10);
            var restored = new ReadingHistory(Path.Combine(sinkDirectory, "history")).Records.Single();
            Require(!restored.Original.Contains(FakeCredential) && !restored.Translation.Contains("Authorization"), "history plaintext before encryption");
            string legacyDirectory = Path.Combine(sinkDirectory, "legacy-history"); Directory.CreateDirectory(legacyDirectory);
            byte[] legacy = JsonSerializer.SerializeToUtf8Bytes(new[] { new ReadingRecord("synthetic", DateTimeOffset.UnixEpoch, prefixProbe, headerProbe) });
            try { File.WriteAllBytes(Path.Combine(legacyDirectory, "reading-history.dat"), LocalDataProtection.Protect(legacy)); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(legacy); }
            var loadedLegacy = new ReadingHistory(legacyDirectory).Records.Single();
            Require(!loadedLegacy.Original.Contains(prefixProbe) && !loadedLegacy.Translation.Contains("Authorization"), "legacy history sanitized in memory without modifying its file");
            string diagnosticPath = Path.Combine(sinkDirectory, "safe-diagnostic.txt");
            File.WriteAllText(diagnosticPath, cleaned + nested);
            Require(!File.ReadAllText(diagnosticPath).Contains(FakeCredential), "diagnostic file persistence");
        }
        finally { AppPaths.DataDirectory = previousDirectory; }

        using var handler = new SensitiveMockHandler();
        using var client = new OpenAiCompatibleClient(handler);
        var config = new AppConfig();
        var messages = new[] { new ChatMessage("user", "synthetic input") };
        var result = await client.CompleteAsync(config, FakeCredential, messages, false, CancellationToken.None);
        Require(!result.Text.Contains(FakeCredential) && !result.Text.Contains("Authorization"), "successful response echo");
        handler.FailStatus = true;
        try { await client.CompleteAsync(config, FakeCredential, messages, false, CancellationToken.None); throw new Exception("expected rejection"); }
        catch (InvalidOperationException ex)
        { Require(ex.Message.Contains("401") && !ex.ToString().Contains(FakeCredential) && !ex.ToString().Contains("Authorization"), "HTTP body and header not exposed"); }
        handler.FailStatus = false; handler.ThrowTransport = true;
        try { await client.CompleteAsync(config, FakeCredential, messages, false, CancellationToken.None); throw new Exception("expected rejection"); }
        catch (InvalidOperationException ex)
        { Require(ex.InnerException == null && !ex.ToString().Contains(FakeCredential) && !ex.ToString().Contains("Authorization"), "transport exception sanitized without sensitive inner exception"); }
    }

    sealed class SensitiveMockHandler : HttpMessageHandler
    {
        public bool FailStatus { get; set; }
        public bool ThrowTransport { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Require(request.Headers.Authorization?.Parameter == FakeCredential, "mock uses the fixed fake credential");
            string echo = "Authorization" + ": Bearer " + FakeCredential;
            if (ThrowTransport) throw new InvalidOperationException(echo);
            string body = FailStatus ? echo : JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = echo } } } });
            return Task.FromResult(new HttpResponseMessage(FailStatus ? HttpStatusCode.Unauthorized : HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
