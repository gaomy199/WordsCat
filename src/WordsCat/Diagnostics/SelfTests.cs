using System.Drawing.Imaging;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReadDock;

internal static class SelfTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); }
    static void Throws<T>(Action a, string name) where T : Exception { try { a(); } catch (T) { return; } throw new Exception("FAIL: " + name); }
    public static async Task RunAsync(string directory, string? regressionDirectory = null)
    {
        Directory.CreateDirectory(directory);
        var report = new StringBuilder("WordsCat 0.3.4 verification\n" + DateTimeOffset.Now.ToString("O") + "\n\n");
        await SecuritySelfTests.RunAsync(directory);
        report.AppendLine("PASS: secret detection/redaction, exception and HTTP boundaries, settings JSON and protected history; fixed fake credential and local handlers only");
        var template = VocabularyTemplate.Load(AppPaths.DefaultTemplate);
        Check(template.Fields.Any(f => f.Key == "example"), "default template includes provenance");
        Check(!template.Fields.Any(f => f.Key is "source" or "page" or "notes") && !template.EntryTemplate.Contains("{{date}}"), "default card omits unwanted metadata");
        var config = new AppConfig();
        var original = "The proposed method is robust to measurement noise.";
        var entry = new WordEntry { Values = template.Fields.ToDictionary(f => f.Key, _ => "") };
        entry.Values["word"] = "robust"; entry.Values["meaning"] = "稳健的"; entry.Values["example"] = original;
        entry.Values["meaning"] += " a | b\n<!-- readdock:fake --> [link](x)";
        var md = template.Render(entry);
        Check(md.Contains("稳健的") && md.Contains("&lt;!--") && !md.Contains("<!-- readdock:fake"), "render escapes marker injection and markdown");
        Check(Prompts.CleanPdfText("mea-\nsurement\nnoise\n\nNext paragraph") == "measurement noise\n\nNext paragraph", "PDF hyphen and paragraph normalization");
        var ai = JsonSerializer.Serialize(new { word = "wrong", meaning = "稳健的", example = original, example_translation = "所提出的方法对测量噪声具有稳健性。", source = "invented", page = "999", notes = "invented" });
        var parsed = Prompts.ParseWord(ai, template, "robust", original);
        Check(parsed["word"] == "robust" && !parsed.ContainsKey("source") && !parsed.ContainsKey("page") && !parsed.ContainsKey("notes"), "protect selected word and omit unconfigured metadata");
        Check(parsed["example"] == original, "accept exact source excerpt");
        var invented = Prompts.ParseWord(ai.Replace(original, "A robust approach works very well."), template, "robust", original);
        Check(invented["example"] == "" && invented["example_translation"] == "", "reject generated example sentences");
        Throws<JsonException>(() => Prompts.ParseWord("not json", template, "robust", original), "reject malformed model JSON");
        Throws<InvalidDataException>(() => Prompts.ParseWord("{\"meaning\":[]}", template, "robust", original), "reject non-string model fields");
        var customPath = Path.Combine(directory, "custom-template.json");
        var custom = new VocabularyTemplate { Fields = [new() { Key = "word", Required = true }, new() { Key = "my_field", Label = "自定义" }], EntryTemplate = "### {{word}}\n{{my_field}}\n{{date}}" };
        AtomicFile.Write(customPath, JsonSerializer.Serialize(custom, AppConfig.Json));
        var loaded = VocabularyTemplate.Load(customPath); Check(loaded.Fields.Count == 2, "custom fields supported");
        custom.EntryTemplate = "{{word}} {{undefined}}"; AtomicFile.Write(customPath, JsonSerializer.Serialize(custom, AppConfig.Json));
        Throws<InvalidDataException>(() => VocabularyTemplate.Load(customPath), "unknown placeholders rejected");
        report.AppendLine("PASS: templates, custom fields, Markdown escaping, PDF normalization, structured output and source validation");
        entry.Values["example"] = "The variables $q_1$, $p_k$, and $\\alpha_i$ are defined.";
        Check(template.Render(entry).Contains("$q_1$") && template.Render(entry).Contains("$\\alpha_i$"), "LaTeX is preserved in Obsidian Markdown");
        entry.Values["example"] = original;

        var legacy = new AppConfig { TargetFile = Path.Combine(directory, "legacy.md") }; legacy.NormalizeNotebooks();
        Check(legacy.Notebooks.Count == 1 && legacy.ActiveNotebook.File == legacy.TargetFile, "legacy single notebook migration");
        var mathBook = new NotebookTarget { Name = "mathematics", File = Path.Combine(directory, "mathematics.md") };
        var vlaBook = new NotebookTarget { Name = "vla", File = Path.Combine(directory, "vla.md") };
        var books = new AppConfig { Notebooks = [mathBook, vlaBook], DefaultNotebookId = vlaBook.Id }; books.NormalizeNotebooks();
        Check(books.ActiveNotebook.Id == vlaBook.Id, "default notebook selected at startup");
        books.ActiveNotebookId = mathBook.Id;
        var serializedBooks = JsonSerializer.Serialize(books, AppConfig.Json);
        var reloadedBooks = JsonSerializer.Deserialize<AppConfig>(serializedBooks, AppConfig.Json)!; reloadedBooks.NormalizeNotebooks();
        Check(reloadedBooks.ActiveNotebook.Id == vlaBook.Id, "active choice does not change default");
        var multiStore = new VocabularyStore(); multiStore.Save(mathBook.File, template, entry, false);
        Check(!multiStore.HasWord(vlaBook.File, "robust"), "duplicate scope is per notebook");
        multiStore.Save(vlaBook.File, template, entry, false);
        Check(File.ReadAllText(mathBook.File).Contains("robust") && File.ReadAllText(vlaBook.File).Contains("robust"), "selected notebook routes writes independently");
        books.Notebooks.Add(new NotebookTarget { Name = "duplicate path", File = vlaBook.File });
        Throws<InvalidDataException>(() => books.NormalizeNotebooks(), "duplicate notebook path rejected");
        if (regressionDirectory != null && File.Exists(Path.Combine(regressionDirectory, "vocabulary-v0.1.json")))
        {
            string old = Path.Combine(directory, "old-default.json"); File.Copy(Path.Combine(regressionDirectory, "vocabulary-v0.1.json"), old, true);
            Check(AppConfig.MigrateBundledTemplate(old) && File.Exists(old + ".v0.1.backup"), "stock old template migrated with backup");
            Check(!VocabularyTemplate.Load(old).Fields.Any(f => f.Key is "source" or "page" or "notes"), "migrated stock template is minimal");
            AtomicFile.Write(old, File.ReadAllText(old) + "\n ");
            Check(!AppConfig.MigrateBundledTemplate(old), "custom template not overwritten");
        }
        Check(PopupPlacement.Calculate(new Rectangle(100, 100, 400, 100), new Size(600, 330), new Rectangle(0, 0, 1920, 1080)).Top == 212, "popup appears below selection");
        var edge = PopupPlacement.Calculate(new Rectangle(1800, 950, 100, 80), new Size(600, 330), new Rectangle(0, 0, 1920, 1080));
        Check(edge.Right <= 1920 && edge.Bottom <= 1080 && edge.Top < 950, "popup flips and clamps near screen edges");
        var negativeMonitor = new Rectangle(-1920, 0, 1920, 1080);
        Check(negativeMonitor.Contains(PopupPlacement.Calculate(new Rectangle(-50, 1000, 40, 50), new Size(600, 330), negativeMonitor)), "negative-coordinate monitor placement");
        Check(MathDisplay.ForReading("$q_1, q_2, \\ldots, q_k$; $p_1$; $\\alpha_i$").Contains("q₁") && MathDisplay.ForReading("$\\alpha_i$") == "αᵢ", "math variables and Greek display without losing subscripts");
        var appearance = new AppConfig { ThemeMode = "dark", AccentColor = "#4455AA", BackgroundBrightness = 10 };
        Ui.SetAppearance(appearance); int darker = Ui.Paper.R; appearance.BackgroundBrightness = 90; Ui.SetAppearance(appearance);
        Check(Ui.IsDark && Ui.Ink.R > Ui.Paper.R && Ui.Paper.R > darker && Ui.Teal.B == 170, "dark theme, brightness and custom accent");
        string ico = Path.Combine(directory, "custom-logo.ico"); using (var f = File.Create(ico)) SystemIcons.Information.Save(f);
        appearance.LogoFile = ico; Ui.SetAppearance(appearance); Check(Ui.Logo != null && Ui.HasCustomLogo, "custom ICO supported");
        using (var exclusive = new FileStream(ico, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(exclusive.Length > 0, "logo loaded without locking original file");
        Ui.SetAppearance(config);
        Check(Ui.Logo != null && Ui.Logo.Width >= 64 && !Ui.HasCustomLogo, "bundled default ICO restored after custom logo");
        using (var bundledIcon = Ui.CreateAppIcon()) Check(bundledIcon.Handle != IntPtr.Zero, "embedded default ICO loads for window and tray");
        var html = ReadingContent.Payload("</script><img src=https://example.com>\n\n$$\\frac{m}{2}\\dot q^2$$");
        Check(!html.Contains("</script><img") && html.Contains("\\u003C/script\\u003E"), "reading preview treats model text as inert text");
        string reader = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "assets", "math", "reader.html"));
        Check(reader.Contains("connect-src 'none'") && reader.Contains("font-src 'self'") && reader.Contains("white-space:pre-wrap"), "reading preview is offline, same-origin and preserves lines");
        Check(File.Exists(Path.Combine(AppContext.BaseDirectory, "assets", "math", "katex.min.js")) && File.Exists(Path.Combine(AppContext.BaseDirectory, "assets", "math", "fonts", "KaTeX_Main-Regular.woff2")), "offline math assets shipped");
        report.AppendLine("PASS: reading preview text injection protection, offline resource policy, paragraph preservation and bundled math fonts");
        string historyDirectory = Path.Combine(directory, "history-tests");
        var history = new ReadingHistory(historyDirectory);
        for (int i = 0; i < 105; i++) history.Add("HISTORY-" + i.ToString("D3"), "译文 " + i, 100);
        Check(history.Records.Count == 100 && history.Records[0].Original == "HISTORY-104" && history.Records[^1].Original == "HISTORY-005", "history evicts oldest at default limit");
        string historyFile = Path.Combine(historyDirectory, "reading-history.dat");
        Check(File.ReadAllBytes(historyFile).AsSpan().IndexOf(Encoding.UTF8.GetBytes("HISTORY-104")) < 0, "history is encrypted on disk");
        Check(new ReadingHistory(historyDirectory).Records[0].Original == "HISTORY-104", "protected history survives restart");
        Check(history.RemovalCount(10) == 90, "history shrink warning computes exact deletion count");
        history.Trim(10);
        var reduced = new ReadingHistory(historyDirectory);
        Check(reduced.Records.Count == 10 && reduced.Records[^1].Original == "HISTORY-095", "100 to 10 permanently discards oldest 90 records");
        reduced.Trim(0); reduced.Add("NOT_RETAINED", "无", 0);
        Check(!File.Exists(historyFile) && new ReadingHistory(historyDirectory).Records.Count == 0, "zero retention deletes history file and disables writes");
        File.WriteAllText(historyFile, "corrupt protected history");
        var damaged = new ReadingHistory(historyDirectory);
        Check(damaged.LoadError != null, "corrupt history is reported");
        Throws<InvalidDataException>(() => damaged.Add("x", "y", 10), "corrupt history is not silently overwritten");
        damaged.Clear();
        Check(!Directory.EnumerateFiles(historyDirectory).Any(), "history deletion leaves no data sidecars");
        var cacheDirectory = Path.Combine(directory, "cache-tests"); Directory.CreateDirectory(cacheDirectory);
        string stale = Path.Combine(cacheDirectory, "Cache", "WebView2-2147483647-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "cache.txt"), "transient");
        string active = Path.Combine(cacheDirectory, "Cache", $"WebView2-{Environment.ProcessId}-{Guid.NewGuid():N}"); Directory.CreateDirectory(active);
        string capture = Path.Combine(cacheDirectory, "capture-" + Guid.NewGuid().ToString("N") + ".png"); File.WriteAllText(capture, "temporary");
        File.WriteAllText(Path.Combine(cacheDirectory, "capture-user.png"), "user content");
        ReadingRuntime.CleanupStale(cacheDirectory, false);
        Check(!Directory.Exists(stale) && Directory.Exists(active) && !File.Exists(capture) && File.Exists(Path.Combine(cacheDirectory, "capture-user.png")), "cleanup targets stale app caches and exact temporary capture names only");
        Check(JsonSerializer.Deserialize<AppConfig>("{}", AppConfig.Json)!.HistoryLimit == 100, "legacy settings gain history default without losing compatibility");
        report.AppendLine("PASS: protected history restart, 100-to-10 pruning, disabled history, corruption protection and precise transient-cache cleanup");

        var legacyLanguage = JsonSerializer.Deserialize<AppConfig>("{}", AppConfig.Json)!;
        Check(legacyLanguage.UiLanguage == "zh-CN" && legacyLanguage.TranslationDirection == "auto", "legacy language and automatic direction defaults");
        legacyLanguage.TranslationInstruction = AppConfig.LegacyTranslationInstruction; legacyLanguage.NormalizeNotebooks();
        Check(legacyLanguage.TranslationInstruction == AppConfig.DefaultTranslationInstruction, "only built-in old instruction is migrated");
        var bilingual = new AppConfig { UiLanguage = "en", TranslationDirection = "zh-en", TranslationInstruction = "保留专有名词。" };
        bilingual.NormalizeNotebooks();
        Check(bilingual.TranslationDirection == "auto", "old manual direction migrates to automatic recognition");
        var chineseInput = "设变量 $q_1$ 为常量。";
        Check(Prompts.Translation(bilingual, chineseInput)[0].Content.Contains("译文必须为英文") && Prompts.Translation(bilingual, chineseInput)[1].Content == chineseInput, "Chinese to English keeps source and selects English");
        var visualPrompt = Prompts.Image(bilingual, [1])[0].Content;
        Check(visualPrompt.Contains("英文正文自动翻译为简体中文") && visualPrompt.Contains("中文正文自动翻译为英文"), "vision recognizes language without a manual choice");
        Check(Prompts.FollowUp(bilingual, chineseInput).Contains("用英文") && !Prompts.FollowUp(bilingual, chineseInput).Contains("译文必须") && bilingual.TranslationInstruction == "保留专有名词。", "follow-up language and custom instructions are preserved");
        bilingual.TranslationDirection = "zh-en";
        Check(Prompts.Translation(bilingual, "test")[0].Content.Contains("译文必须为简体中文"), "UI language is independent of translation direction");
        foreach (var (input, direction) in new[]
        {
            ("The proposed method is robust to noise.", "en-zh"), ("robust", "en-zh"), ("偏导数", "zh-en"),
            ("该方法对 measurement noise 具有 robustness。", "zh-en"), ("This method was tested in 中国.", "en-zh"),
            ("令 a b c d e f g h i j k 均为正数。", "zh-en"),
            ("设变量 $q_1$，且 $$H=\\frac{m}{2}\\dot q^2$$。", "zh-en"),
            ("The variable $\\text{中文注记}$ is constant.", "en-zh"), ("$$q_1+p_2=0$$", "en-zh")
        }) Check(TranslationLanguage.DetectDirection(input) == direction, "automatic source language: " + input);
        var oldRecord = JsonSerializer.Deserialize<ReadingRecord>("{\"Id\":\"legacy\",\"CreatedAt\":\"2026-10-29T00:00:00Z\",\"Original\":\"text\",\"Translation\":\"译文\"}")!;
        Check(oldRecord.Direction == "en-zh", "old history defaults to English to Chinese");
        var directionalHistory = new ReadingHistory(Path.Combine(directory, "direction-history"));
        directionalHistory.Add(chineseInput, "Let $q_1$ be constant.", 100, "zh-en");
        Check(new ReadingHistory(Path.Combine(directory, "direction-history")).Records.Single().Direction == "zh-en", "history retains translation direction after restart");
        var bilingualRoundtrip = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(bilingual, AppConfig.Json), AppConfig.Json)!;
        Check(bilingualRoundtrip.UiLanguage == "en" && bilingualRoundtrip.TranslationInstruction == "保留专有名词。", "language settings and custom instructions round-trip");
        report.AppendLine("PASS: automatic Chinese/English direction, mixed prose and math/code handling, automatic vision prompt, independent UI language, custom instructions and legacy history compatibility");
        report.AppendLine("PASS: lean default template, preserved LaTeX, legacy settings, multiple notebook routing and duplicate isolation, popup placement, theme brightness and custom logo");

        var target = Path.Combine(directory, "notes", "vocabulary.md"); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var initial = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("# 已有笔记\r\n\r\n我的原有内容。\r\n")).ToArray();
        File.WriteAllBytes(target, initial);
        var store = new VocabularyStore();
        store.Save(target, template, entry, false);
        Check(File.ReadAllBytes(target).Take(initial.Length).SequenceEqual(initial), "append preserves original bytes including BOM");
        Check(store.HasWord(target, " ROBUST "), "normalized duplicate lookup");
        Throws<DuplicateWordException>(() => store.Save(target, template, entry, false), "duplicate rejected unless confirmed");
        store.Undo(); Check(File.ReadAllBytes(target).SequenceEqual(initial), "undo restores exact bytes");
        var receipt = store.Save(target, template, entry, false);
        File.AppendAllText(target, "\nExternal edit");
        Throws<InvalidOperationException>(() => store.Undo(), "undo refuses to overwrite external edits");
        Check(File.ReadAllText(target).Contains("External edit"), "external changes retained");
        store.Save(target, template, entry, true);
        Check(File.ReadAllText(target).Split("<!-- readdock:").Length == 3, "duplicate context can be kept as a second card");
        using (var held = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            Throws<IOException>(() => store.Save(target, template, entry, true), "concurrent write excluded");
        var empty = new WordEntry { Values = new() { ["word"] = "test" } };
        Throws<InvalidOperationException>(() => store.Save(target, template, empty, true), "required fields enforced");
        var utf16 = Path.Combine(directory, "utf16.md"); File.WriteAllText(utf16, "existing", Encoding.Unicode);
        Throws<InvalidDataException>(() => store.Save(utf16, template, entry, false), "reject UTF16 instead of corrupting note");
        report.AppendLine("PASS: append preservation, duplicate handling, file lock, required fields, UTF-8 protection, undo and external-edit conflict");

        var mock = new MockApiHandler(); using var client = new DeepSeekClient(mock);
        await client.CompleteAsync(config, "test-api-key-not-real", Prompts.Translation(config, "FIRST_SELECTION"), false, CancellationToken.None);
        await client.CompleteAsync(config, "test-api-key-not-real", Prompts.Translation(config, "SECOND_SELECTION"), false, CancellationToken.None);
        var second = mock.Requests[1]; Check(second.GetProperty("messages").GetArrayLength() == 2 && !second.ToString().Contains("FIRST_SELECTION"), "new translation carries no previous history");
        Check(!second.TryGetProperty("conversation_id", out _) && !second.TryGetProperty("thread_id", out _), "no persistent conversation identity");
        Check(second.GetProperty("thinking").GetProperty("type").GetString() == "disabled", "translation disables reasoning for DeepSeek");
        await client.CompleteAsync(config, "test-api-key-not-real", Prompts.Word(template, "robust", original), true, CancellationToken.None);
        Check(mock.Requests[2].GetProperty("response_format").GetProperty("type").GetString() == "json_object", "word generation requests JSON output");
        mock.Status = HttpStatusCode.Unauthorized;
        try { await client.CompleteAsync(config, "test-api-key-not-real", Prompts.Translation(config, "test"), false, CancellationToken.None); throw new Exception("401 was not rejected"); }
        catch (InvalidOperationException ex) { Check(ex.Message.Contains("401") && !ex.Message.Contains("test-api-key-not-real"), "error does not expose key"); }
        mock.Status = HttpStatusCode.OK; mock.FinishReason = "length";
        try { await client.CompleteAsync(config, "test-api-key-not-real", Prompts.Translation(config, "test"), false, CancellationToken.None); throw new Exception("truncation not rejected"); }
        catch (InvalidDataException) { }
        mock.FinishReason = "stop";
        mock.Content = JsonSerializer.Serialize(new { original = "The variables $q_1$, $p_k$ and $\\alpha_i$ are defined.", translation = "定义变量 $q_1$、$p_k$ 和 $\\alpha_i$。", uncertainty = "" });
        byte[] png;
        using (var b = new Bitmap(20, 20)) { using var ms = new MemoryStream(); b.Save(ms, ImageFormat.Png); png = ms.ToArray(); }
        var vision = await client.TranslateImageAsync(config, "test-api-key-not-real", png, CancellationToken.None);
        var imageRequest = mock.Requests.Last(); var imageMessages = imageRequest.GetProperty("messages");
        Check(imageMessages.GetArrayLength() == 2 && imageMessages[1].GetProperty("content")[1].GetProperty("image_url").GetProperty("url").GetString() == "data:image/png;base64," + Convert.ToBase64String(png), "vision request sends selected image directly without OCR corruption");
        Check(imageMessages[1].GetProperty("content")[1].GetProperty("image_url").GetProperty("detail").GetString() == "original", "vision retains image detail");
        Check(vision.Original.Contains("q_1") && vision.Original.Contains("p_k") && vision.Translation.Contains("\\alpha_i"), "structured vision output preserves variable notation");
        Throws<InvalidDataException>(() => Prompts.ParseImage(new ApiResult("{\"original\":\"\",\"translation\":\"x\",\"uncertainty\":\"\"}", 0, 0)), "empty visual transcription rejected");
        report.AppendLine("PASS: API request contract, independent translations, JSON mode, authentication error and truncated-output handling (local simulated responses; no network)");
        mock.Content = "offline translated example";
        await client.CompleteAsync(config, "test-api-key-not-real", Prompts.Translation(config, "An independent English selection."), false, CancellationToken.None);
        Check(mock.Requests.Last().GetProperty("messages")[0].GetProperty("content").GetString()!.Contains("译文必须为简体中文"), "serialized English request automatically targets Chinese");
        await client.CompleteAsync(config, "test-api-key-not-real", Prompts.Translation(config, "这是另一个独立的中文选区。"), false, CancellationToken.None);
        var autoRequest = mock.Requests.Last().GetProperty("messages");
        Check(autoRequest[0].GetProperty("content").GetString()!.Contains("译文必须为英文") && autoRequest[1].GetProperty("content").GetString() == "这是另一个独立的中文选区。" && !autoRequest.ToString().Contains("An independent English selection."), "next Chinese request targets English without previous selection");
        report.AppendLine("PASS: consecutive serialized English-to-Chinese and Chinese-to-English requests use the detected source, without manual configuration or prior selection context");

        var ocrImage = Path.Combine(directory, "ocr-test.png");
        using (var bmp = new Bitmap(1450, 220))
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font("Arial", 27))
        {
            g.Clear(Color.White); g.DrawString("The method is robust to measurement noise.", font, Brushes.Black, new PointF(30, 45)); bmp.Save(ocrImage, ImageFormat.Png);
        }
        var recognized = await OcrService.RunAsync(ocrImage, CancellationToken.None);
        Check(recognized.Contains("robust", StringComparison.OrdinalIgnoreCase) && recognized.Contains("measurement", StringComparison.OrdinalIgnoreCase), "real Windows OCR recognizes English text");
        report.AppendLine("PASS: real Windows OCR on a generated English image\nOCR result: " + recognized);
        if (regressionDirectory != null && File.Exists(Path.Combine(regressionDirectory, "math-screenshot.png")))
        {
            string preparedPath = Path.Combine(directory, "math-local-prepared.png");
            using (var image = new Bitmap(Path.Combine(regressionDirectory, "math-screenshot.png")))
            using (var prepared = OcrService.PrepareLocal(image))
            { Check(prepared.Width <= 2600 && prepared.Height <= 2600, "local OCR dimensions within system limits"); prepared.Save(preparedPath, ImageFormat.Png); }
            var mathLocal = await OcrService.RunAsync(preparedPath, CancellationToken.None);
            report.AppendLine("Local OCR comparison on user math screenshot (not a vision-quality assertion): " + mathLocal);
        }
        report.AppendLine("\nNo real DeepSeek API Key was used. Live translation quality and billing were not tested.");
        File.WriteAllText(Path.Combine(directory, "test-results.txt"), SecretRedactor.Redact(report.ToString()));
    }

    private sealed class MockApiHandler : HttpMessageHandler
    {
        public List<JsonElement> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string FinishReason { get; set; } = "stop";
        public string Content { get; set; } = "模拟响应";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)); Requests.Add(doc.RootElement.Clone());
            Check(request.RequestUri?.AbsoluteUri == "https://api.deepseek.com/chat/completions", "API path");
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = FinishReason, message = new { content = Content } } }, usage = new { prompt_tokens = 20, completion_tokens = 10 } }))
            };
        }
    }
}
