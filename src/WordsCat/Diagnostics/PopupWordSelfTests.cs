using System.Net;
using System.Text;
using System.Text.Json;

namespace ReadDock;

internal static class PopupWordSelfTests
{
    const string Original = "A robust method stays stable.";
    sealed class WordHandler : HttpMessageHandler
    {
        internal readonly List<string> Words = [];
        internal string Stage = "not called";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Stage = "authorization";
            Require(request.Headers.Authorization?.Parameter == SecuritySelfTests.FakeCredential, "fixed fake credential only");
            Stage = "request JSON";
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Stage = "word JSON";
            using var input = JsonDocument.Parse(payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Stage = "selected word";
            string word = input.RootElement.GetProperty("selected_word").GetString()!;
            Stage = "source context";
            Require(input.RootElement.GetProperty("source_text").GetString() == Original, "current recognized source used as context");
            Words.Add(word); await Task.Delay(30, token);
            string entry = JsonSerializer.Serialize(new { word, lemma = word, part_of_speech = "adjective", meaning = "local mock meaning", example = Original, example_translation = "本地模拟原句译文" });
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = entry } } } }), Encoding.UTF8, "application/json") };
        }
    }
    static void Require(bool value, string name) { if (!value) throw new Exception("Popup collection regression: " + name); }
    static IEnumerable<Control> Children(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var nested in Children(c)) yield return nested; } }
    internal static async Task RunAsync()
    {
        var handler = new WordHandler(); var vault = new ServiceSetupSelfTests.FakeVault(true);
        string notebook = Path.Combine(AppPaths.DataDirectory, "popup-collection-" + Guid.NewGuid().ToString("N") + ".md");
        using var main = new MainWindow(new AppConfig { TemplateFile = AppPaths.DefaultTemplate, TargetFile = notebook, ShowFloating = false, ServiceGuideSeen = true }, true, false, new OpenAiCompatibleClient(handler), vault);
        main.Show(); await Task.Delay(150);
        var source = Children(main).OfType<RichTextBox>().Single(c => c.AccessibleName == "原文");
        source.Text = Original; source.Select(0, 1); main.Hide();
        main.OpenPopup(new Rectangle(100, 100, 180, 40), "本地收藏回归");
        var popup = Application.OpenForms.OfType<TranslationPopup>().Single();
        popup.SetResult("The translated robust method.", "本地模拟结果", Original);
        foreach (string word in new[] { "robust", "stable" })
        {
            if (word == "stable") Children(popup).OfType<Button>().Single(c => c.Name == "popupOriginalToggle").PerformClick();
            bool selected = false;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                await Task.Delay(80);
                await popup.InspectReadingAsync("(()=>{const w=" + JsonSerializer.Serialize(word) + ";const t=document.createTreeWalker(document.getElementById('content'),NodeFilter.SHOW_TEXT);let n;while(n=t.nextNode()){const i=n.textContent.indexOf(w);if(i>=0){const r=document.createRange();r.setStart(n,i);r.setEnd(n,i+w.length);const s=window.getSelection();s.removeAllRanges();s.addRange(r);return true;}}return false;})()");
                await Task.Delay(40);
                if (popup.SelectedText == word) { selected = true; break; }
            }
            Require(selected, "rendered selection reaches popup in both views");
            Exception? failure = null; bool checkedDialog = false;
            main.BeginInvoke((Action)(async () =>
            {
                var dialog = Application.OpenForms.OfType<WordDialog>().Single();
                try
                {
                    Require(!popup.Visible && !main.Visible && dialog.TopMost && dialog.Owner == popup, "modal editor above preserved hidden popup without showing main");
                    var fields = Children(dialog).OfType<TextBox>().Where(c => c.Name.StartsWith("wordField_")).ToDictionary(c => c.Name);
                    Require(fields["wordField_word"].Text == word, "popup selection prefills word instead of stale main selection");
                    for (int attempt = 0; attempt < 60 && fields["wordField_meaning"].Text.Length == 0; attempt++) await Task.Delay(50);
                    Require(fields["wordField_meaning"].Text == "local mock meaning" && fields["wordField_example"].Text == Original, "automatic enrichment completes using local mock; stage=" + handler.Stage + ", calls=" + handler.Words.Count + ", keyReads=" + vault.Reads);
                    checkedDialog = true;
                    if (word == "stable") Children(dialog).OfType<Button>().Single(c => c.Text == UiText.T("保存到单词本")).PerformClick();
                }
                catch (Exception ex) { failure = ex; }
                finally { dialog.Close(); }
            }));
            Children(popup).OfType<Button>().Single(c => c.Text == UiText.T("收藏单词")).PerformClick();
            if (failure != null) throw failure;
            Require(checkedDialog && popup.Visible && !popup.IsDisposed && !main.Visible, "save/cancel restores same popup and keeps main hidden");
        }
        Require(handler.Words.SequenceEqual(new[] { "robust", "stable" }) && vault.Writes == 0, "one local enrichment per selected word and no credential writes");
        Require(File.ReadAllText(notebook).Contains("## stable") && !File.ReadAllText(notebook).Contains("## robust"), "save writes selected entry and cancel does not");
        popup.SetLoading("新选区"); Require(popup.SelectedText.Length == 0, "new capture clears stale selection");
        popup.PreserveOnClose = true; popup.Close();
    }
}
