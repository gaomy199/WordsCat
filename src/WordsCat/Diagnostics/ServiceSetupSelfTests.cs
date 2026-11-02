using System.Net;
using System.Text;
using System.Text.Json;

namespace ReadDock;

internal static class ServiceSetupSelfTests
{
    internal sealed class FakeVault(bool saved = false, string? scopedUrl = null) : ICredentialVault
    {
        bool present = saved; string? scope = scopedUrl;
        internal int Reads, Writes;
        public bool HasKey(string url) => present && (scope == null || CredentialScope.Same(scope, url));
        public string Read(string url) { Reads++; return HasKey(url) ? SecuritySelfTests.FakeCredential : ""; }
        public void Save(string value, string url) { Require(value == SecuritySelfTests.FakeCredential, "only fixed fake key may be saved"); Writes++; present = true; scope = url; }
    }
    internal sealed class ProbeHandler : HttpMessageHandler
    {
        internal HttpStatusCode Status = HttpStatusCode.OK;
        internal string Mode = "normal";
        internal int Calls;
        internal JsonElement Last;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Require(request.Headers.Authorization?.Parameter == SecuritySelfTests.FakeCredential, "fake authorization only");
            using var parsed = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)); Last = parsed.RootElement.Clone();
            if (Mode == "timeout") throw new TaskCanceledException();
            if (Mode == "network") throw new HttpRequestException("synthetic network failure");
            string echo = "Authorization" + ": Bearer " + SecuritySelfTests.FakeCredential;
            string body = Mode == "html" ? "<html>" + echo + "</html>" : Mode == "shape" ? "[]" : JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = "OK" } } } });
            return new HttpResponseMessage(Status) { Content = new StringContent(Status == HttpStatusCode.OK ? body : echo, Encoding.UTF8, "application/json") };
        }
    }
    static void Require(bool value, string name) { if (!value) throw new Exception("Service setup regression: " + name); }
    public static async Task RunAsync(string directory)
    {
        var legacy = JsonSerializer.Deserialize<AppConfig>("{}", AppConfig.Json)!; legacy.NormalizeNotebooks();
        Require(legacy.ModelProvider == "deepseek" && !legacy.ServiceGuideSeen, "old defaults and guide state");
        var custom = JsonSerializer.Deserialize<AppConfig>("{\"base_url\":\"https://custom.example.invalid/v1\",\"model\":\"local-model\"}", AppConfig.Json)!; custom.NormalizeNotebooks();
        Require(custom.ModelProvider == "custom" && custom.Model == "local-model", "legacy custom URL and model preserved");
        Require(CredentialScope.Same("https://host.example.invalid/v1", "https://host.example.invalid/v2") && !CredentialScope.Same("https://host.example.invalid", "https://other.example.invalid"), "credential origins isolated");
        Require(CredentialScope.Matches(null, ModelPresets.BaseUrl) && CredentialScope.Matches("单词猫 DeepSeek API Key", ModelPresets.BaseUrl) && !CredentialScope.Matches(CredentialScope.Prefix + ModelPresets.BaseUrl, "https://other.example.invalid"), "legacy metadata accepted and new metadata bound to origin");
        Require(ServiceSetupGuide.ShouldShow(legacy, false) && !ServiceSetupGuide.ShouldShow(legacy, true), "guide only when missing key");
        legacy.ServiceGuideSeen = true; Require(!ServiceSetupGuide.ShouldShow(legacy, false), "dismissed guide not repeated");
        string previous = AppPaths.DataDirectory;
        try
        {
            AppPaths.DataDirectory = Path.Combine(directory, "setup-config"); Directory.CreateDirectory(AppPaths.DataDirectory);
            custom.ServiceGuideSeen = true; custom.TemplateFile = AppPaths.DefaultTemplate; custom.Save();
            var loaded = AppConfig.Load(); Require(loaded.ModelProvider == "custom" && loaded.ServiceGuideSeen, "preset and guide state persisted");
            using var json = JsonDocument.Parse(File.ReadAllText(AppPaths.ConfigFile)); Require(!json.RootElement.TryGetProperty("api_key", out _), "no key property in configuration");
        }
        finally { AppPaths.DataDirectory = previous; }
        var empty = new FakeVault(); int factories = 0;
        var missing = await new ConnectionTester(() => { factories++; throw new Exception("No transport expected"); }).TestAsync(new AppConfig(), empty, CancellationToken.None);
        Require(missing.Kind == ConnectionTestKind.MissingKey && empty.Reads == 0 && factories == 0, "missing key never read or sent");
        foreach (var test in new[] {
            (HttpStatusCode.OK, "normal", ConnectionTestKind.Success), (HttpStatusCode.Unauthorized, "normal", ConnectionTestKind.InvalidKey),
            (HttpStatusCode.PaymentRequired, "normal", ConnectionTestKind.QuotaOrRateLimit), (HttpStatusCode.TooManyRequests, "normal", ConnectionTestKind.QuotaOrRateLimit),
            (HttpStatusCode.OK, "network", ConnectionTestKind.NetworkOrTimeout), (HttpStatusCode.OK, "timeout", ConnectionTestKind.NetworkOrTimeout),
            (HttpStatusCode.OK, "html", ConnectionTestKind.InvalidResponse), (HttpStatusCode.OK, "shape", ConnectionTestKind.InvalidResponse) })
        {
            var handler = new ProbeHandler { Status = test.Item1, Mode = test.Item2 }; var vault = new FakeVault(true);
            var result = await new ConnectionTester(() => new OpenAiCompatibleClient(handler)).TestAsync(new AppConfig { MaxRetries = 5 }, vault, CancellationToken.None);
            Require(result.Kind == test.Item3 && handler.Calls == 1 && vault.Reads == 1, "category without retries");
            Require(handler.Last.GetProperty("max_tokens").GetInt32() == 8 && handler.Last.GetProperty("messages").GetArrayLength() == 1 && handler.Last.GetProperty("messages")[0].GetProperty("content").GetString() == "Reply OK.", "minimal probe has no user content");
            Require(!result.MessageKey.Contains(SecuritySelfTests.FakeCredential) && !result.MessageKey.Contains("Authorization"), "safe result without raw response");
        }
        string language = UiText.Language;
        try { UiText.Language = "en"; foreach (ConnectionTestKind kind in Enum.GetValues<ConnectionTestKind>()) { string key = new ConnectionTestResult(kind).MessageKey; Require(UiText.T(key) != key, "all categories localized"); } }
        finally { UiText.Language = language; }
    }
    internal static async Task RunUiAsync()
    {
        var vault = new FakeVault(true); var cfg = new AppConfig { TemplateFile = AppPaths.DefaultTemplate, ServiceGuideSeen = true }; var handler = new ProbeHandler();
        using (var settings = new SettingsDialog(cfg, useCredentials: false, credentialVault: vault, connectionTester: new ConnectionTester(() => new OpenAiCompatibleClient(handler))))
        {
            settings.Show(); await Task.Delay(100); var key = Find<TextBox>(settings, "modelApiKey");
            Require(key.UseSystemPasswordChar && key.Text.Length == 0 && vault.Reads == 0, "settings never reads or echoes saved key");
            await settings.TestAsync(); Require(vault.Reads == 1 && Find<Label>(settings, "connectionTestStatus").Text == UiText.T(new ConnectionTestResult(ConnectionTestKind.Success).MessageKey), "visible test uses saved fake key");
            key.Text = SecuritySelfTests.FakeCredential; await settings.TestAsync(); Require(vault.Writes == 0 && vault.Reads == 1 && handler.Calls == 1, "draft not sent or persisted");
            key.Clear(); Find<ComboBox>(settings, "modelProvider").SelectedIndex = 1;
            Find<TextBox>(settings, "modelBaseUrl").Text = "https://custom.example.invalid/v1"; Find<ComboBox>(settings, "textModel").Text = "custom-model";
            key.Text = SecuritySelfTests.FakeCredential; Find<ComboBox>(settings, "modelProvider").SelectedIndex = 0;
            Require(Find<TextBox>(settings, "modelBaseUrl").Text == ModelPresets.BaseUrl && Find<ComboBox>(settings, "textModel").Text == ModelPresets.Model && key.Text.Length == 0, "DeepSeek preset and cleared draft");
            Find<ComboBox>(settings, "modelProvider").SelectedIndex = 1; Require(Find<TextBox>(settings, "modelBaseUrl").Text == "https://custom.example.invalid/v1", "custom draft preserved");
            var languageChoice = Children(settings).OfType<ComboBox>().Single(c => c.AccessibleName == "uiLanguage");
            key.Text = SecuritySelfTests.FakeCredential;
            languageChoice.SelectedIndex = 1;
            Require(Find<ComboBox>(settings, "modelProvider").Text == "Custom OpenAI-compatible", "provider preset localizes immediately");
            languageChoice.SelectedIndex = 0;
            Require(Find<ComboBox>(settings, "modelProvider").Text == "自定义 OpenAI 兼容" && Find<TextBox>(settings, "modelBaseUrl").Text == "https://custom.example.invalid/v1" && Find<ComboBox>(settings, "textModel").Text == "custom-model" && key.Text == SecuritySelfTests.FakeCredential && vault.Writes == 0, "language refresh preserves selection and unsaved service/key drafts");
            key.Text = SecuritySelfTests.FakeCredential; settings.Close(); Require(vault.Writes == 0, "cancel never saves key");
        }
        using (var settings = new SettingsDialog(cfg, useCredentials: false, credentialVault: vault))
        { settings.Show(); Find<Button>(settings, null, "保存设置").PerformClick(); Require(settings.IsDisposed && vault.Writes == 0, "blank preserves saved key"); }
        using (var settings = new SettingsDialog(cfg, useCredentials: false, credentialVault: vault))
        {
            settings.Show(); Find<ComboBox>(settings, "modelProvider").SelectedIndex = 1; Find<TextBox>(settings, "modelBaseUrl").Text = "https://custom.example.invalid/v1";
            Find<ComboBox>(settings, "textModel").Text = "custom-model"; Find<TextBox>(settings, "modelApiKey").Text = SecuritySelfTests.FakeCredential;
            Find<Button>(settings, null, "保存设置").PerformClick(); Require(vault.Writes == 1 && cfg.ModelProvider == "custom" && cfg.VisionModel == "custom-model", "custom preset and replacement saved");
        }
        Require(!File.ReadAllText(AppPaths.ConfigFile).Contains(SecuritySelfTests.FakeCredential), "key never serialized");
        using (var settings = new SettingsDialog(cfg, useCredentials: false))
        { settings.Show(); await settings.TestAsync(); Require(Find<Label>(settings, "connectionTestStatus").Text == UiText.T(new ConnectionTestResult(ConnectionTestKind.Offline).MessageKey), "offline preview cannot request real service"); settings.Close(); }
        var guideConfig = new AppConfig { TemplateFile = AppPaths.DefaultTemplate, ShowFloating = false }; var noKey = new FakeVault();
        using (var main = new MainWindow(guideConfig, true, true, credentialVault: noKey))
        {
            main.Show(); await Task.Delay(150); var guide = Application.OpenForms.OfType<ServiceSetupGuide>().Single();
            Require(main.Enabled && !guide.Modal && noKey.Reads == 0, "modeless guide without credential read");
            Children(main).OfType<RichTextBox>().Single(c => c.AccessibleName == "原文").Text = "Text views remain usable.";
            Find<Button>(guide, "guideLater").PerformClick(); Require(guideConfig.ServiceGuideSeen && AppConfig.Load().ServiceGuideSeen && main.Enabled, "dismiss persists and reader stays usable");
            main.Hide(); main.Show(); Require(!Application.OpenForms.OfType<ServiceSetupGuide>().Any(), "guide not repeated"); main.Hide();
        }
        var configured = new FakeVault(true);
        using (var main = new MainWindow(new AppConfig { TemplateFile = AppPaths.DefaultTemplate, ShowFloating = false }, true, true, credentialVault: configured))
        { main.Show(); await Task.Delay(100); Require(!Application.OpenForms.OfType<ServiceSetupGuide>().Any() && configured.Reads == 0, "configured service skips guide without reading key"); main.Hide(); }
        var linkConfig = new AppConfig { TemplateFile = AppPaths.DefaultTemplate, ShowFloating = false }; var linkVault = new FakeVault();
        using (var main = new MainWindow(linkConfig, true, true, credentialVault: linkVault))
        {
            main.Show(); await Task.Delay(100); bool opened = false;
            main.BeginInvoke((Action)(() => { var settings = Application.OpenForms.OfType<SettingsDialog>().Single(); opened = Find<ComboBox>(settings, "modelProvider").Items.Count == 2; settings.Close(); }));
            Find<Button>(Application.OpenForms.OfType<ServiceSetupGuide>().Single(), "guideOpenSettings").PerformClick();
            Require(opened && linkConfig.ServiceGuideSeen && linkVault.Reads == 0, "guide links to model service settings without reading key"); main.Hide();
        }
        string previousLanguage = UiText.Language;
        try
        {
            UiText.Language = "en"; var englishVault = new FakeVault(true);
            using var settings = new SettingsDialog(new AppConfig { TemplateFile = AppPaths.DefaultTemplate, UiLanguage = "en" }, useCredentials: false, credentialVault: englishVault, connectionTester: new ConnectionTester(() => new OpenAiCompatibleClient(new ProbeHandler { Status = HttpStatusCode.Unauthorized })));
            settings.Show(); await settings.TestAsync();
            Require(Find<Label>(settings, "connectionTestStatus").Text.StartsWith("401:") && Find<TextBox>(settings, "modelApiKey").Text.Length == 0, "English classified result and no echoed key"); settings.Close();
            UiText.Language = "en";
            using var guide = new ServiceSetupGuide(() => { }, () => { }); guide.Show();
            Require(Find<Button>(guide, "guideLater").Text == "Set up later", "English guide localized"); guide.Close();
        }
        finally { UiText.Language = previousLanguage; }
    }
    static T Find<T>(Control root, string? name, string? caption = null) where T : Control => Children(root).OfType<T>().Single(c => (name == null || c.Name == name) && (caption == null || c.Text == UiText.T(caption)));
    static IEnumerable<Control> Children(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var item in Children(child)) yield return item; } }
}
