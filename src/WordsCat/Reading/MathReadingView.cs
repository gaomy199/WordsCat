using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ReadDock;

// The editable text remains the source of truth; the browser only typesets a local preview.
internal sealed class MathReadingView : UserControl
{
    readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    readonly RichTextBox editor;
    readonly ThemedTabs tabs = new() { Dock = DockStyle.Fill };
    readonly PictureBox picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 180 };
    bool initialized, starting, documentReady;
    readonly bool compact;
    internal event Action? Rendered;
    const string DocumentUrl = "https://readdock-assets.invalid/reader.html";
    public string SelectedText { get; private set; } = "";
    public string? RenderError { get; private set; }
    internal string NavigationDiagnostic { get; private set; } = "not initialized";

    public MathReadingView(RichTextBox editor, bool original, bool compact = false)
    {
        this.compact = compact;
        this.editor = editor; Dock = DockStyle.Fill;
        var reading = new TabPage("阅读"); reading.Controls.Add(browser);
        var raw = new TabPage(original ? "编辑原文" : "文本"); raw.Controls.Add(editor);
        tabs.TabPages.AddRange([reading, raw]);
        if (original) { var image = new TabPage("原始截图"); image.Controls.Add(picture); tabs.TabPages.Add(image); }
        if (compact)
        {
            reading.Controls.Remove(browser);
            raw.Controls.Remove(editor);
            editor.ReadOnly = true; editor.Dock = DockStyle.Fill;
            editor.Visible = false; Controls.Add(editor); Controls.Add(browser);
        }
        else Controls.Add(tabs);
        browser.CreationProperties = new CoreWebView2CreationProperties
        { UserDataFolder = ReadingRuntime.Folder, IsInPrivateModeEnabled = true };
        editor.TextChanged += (_, _) => { SelectedText = ""; refresh.Stop(); refresh.Start(); };
        editor.Enter += (_, _) => SelectedText = "";
        refresh.Tick += (_, _) => { refresh.Stop(); Render(); };
        Load += async (_, _) => await InitializeAsync();
        VisibleChanged += (_, _) => { if (Visible) Render(); };
    }
    async Task InitializeAsync()
    {
        if (starting || IsDisposed) return; starting = true;
        try
        {
            await browser.EnsureCoreWebView2Async();
            if (IsDisposed) return;
            var core = browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false; core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreHostObjectsAllowed = false; core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            ReadingRuntime.Track(core);
            core.SetVirtualHostNameToFolderMapping("readdock-assets.invalid", Path.Combine(AppContext.BaseDirectory, "assets", "math"), CoreWebView2HostResourceAccessKind.Deny);
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) =>
            {
                if (!e.Request.Uri.StartsWith("https://readdock-assets.invalid/", StringComparison.Ordinal))
                    e.Response = core.Environment.CreateWebResourceResponse(null, 403, "Local preview only", "");
            };
            core.NavigationStarting += (_, e) =>
            {
                e.Cancel = e.Uri != DocumentUrl;
                NavigationDiagnostic = e.Cancel ? "unexpected navigation cancelled" : "local document starting";
            };
            core.NavigationCompleted += (_, e) =>
            {
                NavigationDiagnostic += " completed " + e.IsSuccess + " " + e.WebErrorStatus;
                documentReady = e.IsSuccess;
                if (documentReady) Render();
            };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += (_, e) => { SelectedText = e.TryGetWebMessageAsString(); };
            initialized = true; core.Navigate(DocumentUrl);
        }
        catch (Exception ex)
        {
            if (IsDisposed) return;
            RenderError = SecretRedactor.Redact(ex.Message);
            if (compact) { browser.Visible = false; editor.Visible = true; editor.BringToFront(); Rendered?.Invoke(); return; }
            tabs.SelectedIndex = 1;
            tabs.TabPages[0].Controls.Add(new Label { Dock = DockStyle.Fill, Text = "公式阅读视图未能启动，请安装 Microsoft Edge WebView2 Runtime。可继续使用文本页。", ForeColor = Ui.Ink });
        }
    }
    public void RefreshAppearance() { browser.DefaultBackgroundColor = Ui.Surface; Render(); }
    internal void RefreshContent() { refresh.Stop(); Render(); }
    public void RefreshLanguage() => UiText.Apply(this);
    internal Task<string> InspectAsync(string script) => initialized
        ? browser.ExecuteScriptAsync(script) : Task.FromResult(JsonSerializer.Serialize(new { error = RenderError ?? "initializing" }));
    async void Render()
    {
        if (!initialized || !documentReady || IsDisposed) return;
        SelectedText = "";
        browser.DefaultBackgroundColor = Ui.Surface;
        try
        {
            string size = compact ? "document.body.style.fontSize='16px';" : "";
            await browser.ExecuteScriptAsync(size + "window.setReading(" + ReadingContent.Payload(editor.Text) + ");");
            if (!IsDisposed) Rendered?.Invoke();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or System.Runtime.InteropServices.COMException) { RenderError = ex.Message; }
    }
    public void SetCapture(Image? image)
    {
        var old = picture.Image; picture.Image = image == null ? null : new Bitmap(image); old?.Dispose();
    }
    public void ClearTransientContent()
    {
        refresh.Stop(); SelectedText = ""; SetCapture(null); Render();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { refresh.Dispose(); picture.Image?.Dispose(); if (compact) { tabs.Dispose(); picture.Dispose(); } }
        base.Dispose(disposing);
    }
}

internal static class ReadingContent
{
    public static string Payload(string text) => JsonSerializer.Serialize(new { text, ink = ColorTranslator.ToHtml(Ui.Ink), surface = ColorTranslator.ToHtml(Ui.Surface) });
}
