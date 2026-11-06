using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ReadDock;

// The editable text remains the source of truth; the browser only typesets a local preview.
internal sealed class MathReadingView : UserControl
{
    readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    readonly RichTextBox editor;
    readonly ThemedTabs tabs = new(34) { Dock = DockStyle.Fill };
    readonly PictureBox picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 180 };
    bool initialized, starting, documentReady;
    bool rendering, renderQueued;
    string? renderedPayload;
    readonly bool compact;
    readonly ReadingZoom zoom;
    internal double ZoomFactor => zoom.Factor;
    internal event Action? Rendered;
    const string DocumentUrl = "https://readdock-assets.invalid/reader.html";
    public string SelectedText { get; private set; } = "";
    public string? RenderError { get; private set; }
    internal string NavigationDiagnostic { get; private set; } = "not initialized";

    public MathReadingView(RichTextBox editor, bool original, bool compact = false, ReadingZoom? zoom = null)
    {
        this.compact = compact;
        this.zoom = zoom ?? new ReadingZoom(new AppConfig(), () => { });
        this.editor = editor; Dock = DockStyle.Fill; Margin = Padding.Empty;
        ApplyZoom();
        this.zoom.Changed += ApplyZoom;
        browser.ZoomFactorChanged += (_, _) =>
        {
            if (documentReady && !IsDisposed) this.zoom.SetFactor(browser.ZoomFactor);
        };
        editor.MouseWheel += (_, _) =>
        {
            if ((ModifierKeys & Keys.Control) != 0 && editor.IsHandleCreated)
                editor.BeginInvoke((Action)(() => { if (!IsDisposed) this.zoom.SetFactor(editor.ZoomFactor); }));
        };
        DoubleBuffered = true; BackColor = Ui.Surface; browser.DefaultBackgroundColor = Ui.Surface;
        var reading = new TabPage("阅读"); reading.Controls.Add(browser);
        var raw = new TabPage(original ? "编辑原文" : "文本"); raw.Controls.Add(editor);
        tabs.TabPages.AddRange([reading, raw]);
        if (original) { var image = new TabPage("原始截图"); image.Controls.Add(picture); tabs.TabPages.Add(image); }
        if (compact)
        {
            reading.Controls.Remove(browser);
            raw.Controls.Remove(editor);
            editor.ReadOnly = true; editor.Dock = DockStyle.Fill;
            // Paint native text immediately; swap to formulas after the first local render.
            browser.Visible = false; editor.Visible = true; Controls.Add(browser); Controls.Add(editor);
        }
        else Controls.Add(tabs);
        browser.CreationProperties = new CoreWebView2CreationProperties
        { UserDataFolder = ReadingRuntime.Folder, IsInPrivateModeEnabled = true };
        editor.TextChanged += (_, _) => { SelectedText = ""; refresh.Stop(); refresh.Start(); };
        editor.Enter += (_, _) => SelectedText = "";
        refresh.Tick += (_, _) => { refresh.Stop(); Render(); };
        Load += async (_, _) => { await Task.Yield(); if (!IsDisposed) await InitializeAsync(); };
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
                if (!e.Cancel) documentReady = false;
                NavigationDiagnostic = e.Cancel ? "unexpected navigation cancelled" : "local document starting";
            };
            core.NavigationCompleted += (_, e) =>
            {
                NavigationDiagnostic += " completed " + e.IsSuccess + " " + e.WebErrorStatus;
                documentReady = e.IsSuccess;
                renderedPayload = null;
                if (documentReady) { ApplyZoom(); Render(); }
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
    void ApplyZoom()
    {
        if (IsDisposed) return;
        if (Math.Abs(browser.ZoomFactor - zoom.Factor) >= 0.0001) browser.ZoomFactor = zoom.Factor;
        editor.ZoomFactor = (float)zoom.Factor;
        Rendered?.Invoke();
    }
    internal void RefreshContent() { refresh.Stop(); Render(); }
    public void RefreshLanguage() => UiText.Apply(this);
    internal Task<string> InspectAsync(string script) => initialized
        ? browser.ExecuteScriptAsync(script) : Task.FromResult(JsonSerializer.Serialize(new { error = RenderError ?? "initializing" }));
    async void Render()
    {
        if (!initialized || !documentReady || IsDisposed) return;
        if (rendering) { renderQueued = true; return; }
        rendering = true;
        try
        {
            do
            {
                renderQueued = false;
                string payload = ReadingContent.Payload(editor.Text, compact);
                if (payload == renderedPayload) break;
                SelectedText = ""; browser.DefaultBackgroundColor = Ui.Surface;
                await browser.ExecuteScriptAsync("window.setReading(" + payload + ");");
                if (IsDisposed) return;
                renderedPayload = payload;
                if (compact && !browser.Visible)
                {
                    SuspendLayout();
                    browser.Visible = true; browser.BringToFront(); editor.Visible = false;
                    ResumeLayout(true);
                }
                Rendered?.Invoke();
            } while (renderQueued && !IsDisposed);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or System.Runtime.InteropServices.COMException) { RenderError = ex.Message; }
        finally { rendering = false; }
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
        if (disposing) { zoom.Changed -= ApplyZoom; refresh.Dispose(); picture.Image?.Dispose(); if (compact) { tabs.Dispose(); picture.Dispose(); } }
        base.Dispose(disposing);
    }
}

internal static class ReadingContent
{
    public static string Payload(string text, bool compact = false) => JsonSerializer.Serialize(new { text, compact, ink = ColorTranslator.ToHtml(Ui.Ink), surface = ColorTranslator.ToHtml(Ui.Surface) });
}
