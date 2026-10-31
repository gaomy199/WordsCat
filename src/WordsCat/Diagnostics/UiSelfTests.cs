using System.Text.Json;

namespace ReadDock;

internal static class UiSelfTests
{
    // Runs on the real STA message loop and actual installed WebView2 runtime, with synthetic text only.
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        Ui.SetAppearance(new AppConfig());
        using var editor = new RichTextBox { Text = "Let $\\theta=q$.\n\n$$H=\\frac{m}{2}(l\\dot q)^2+mgl(1-\\cos q)$$\n\n$$p=\\frac{\\partial T}{\\partial\\dot q}$$\n\n<script>alert('untrusted')</script>" };
        using var view = new MathReadingView(editor, true);
        using var host = new Form { Text = "单词猫 · 离线公式验证", ClientSize = new Size(800, 600) };
        host.Controls.Add(view);
        host.Shown += async (_, _) =>
        {
            try
            {
                string state = "";
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    await Task.Delay(200);
                    state = await view.InspectAsync("({ready:document.readyState,math:document.querySelectorAll('.katex').length,fractions:document.querySelectorAll('.mfrac').length,katex:typeof katex,auto:typeof renderMathInElement,images:document.images.length,text:document.body.innerText})");
                    using var doc = JsonDocument.Parse(state);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("fractions", out var count) && count.GetInt32() >= 2) break;
                }
                File.WriteAllText(Path.Combine(directory, "render-state.json"), SecretRedactor.Redact(state));
                File.WriteAllText(Path.Combine(directory, "navigation.txt"), SecretRedactor.Redact(view.NavigationDiagnostic + "\n" + await view.InspectAsync("document.documentElement.outerHTML")));
                using (var doc = JsonDocument.Parse(state))
                {
                    if (!doc.RootElement.TryGetProperty("fractions", out var count) || count.GetInt32() < 2)
                        throw new Exception("Real formula rendering failed: " + state);
                    if (doc.RootElement.GetProperty("images").GetInt32() != 0) throw new Exception("Untrusted HTML was interpreted as markup.");
                }
                string fonts = "";
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    fonts = await view.InspectAsync("Array.from(document.fonts).filter(f=>f.status==='loaded').map(f=>f.family)");
                    if (fonts.Contains("KaTeX_Math") && fonts.Contains("KaTeX_Main")) break;
                    await Task.Delay(100);
                }
                File.WriteAllText(Path.Combine(directory, "loaded-fonts.json"), fonts);
                if (!fonts.Contains("KaTeX_Math") || !fonts.Contains("KaTeX_Main")) throw new Exception("Math font files did not actually load: " + fonts);
                Ui.SetAppearance(new AppConfig { ThemeMode = "dark" }); view.RefreshAppearance();
                await Task.Delay(600);
                string colors = await view.InspectAsync("getComputedStyle(document.body).backgroundColor");
                if (colors != "\"rgb(32, 32, 32)\"") throw new Exception("Reading theme did not refresh: " + colors);
                Ui.SetAppearance(new AppConfig());
                using (var popup = new TranslationPopup("弹窗高度验证", () => { }, () => { }, () => { }))
                {
                    popup.SetResult("物理智能", "翻译完成 · 输入 92 / 输出 2 tokens"); popup.Show(host); await Task.Delay(100);
                    int shortHeight = popup.Height;
                    popup.SetResult(string.Join("\n", Enumerable.Repeat("这是一行较长的翻译，用于验证纵向滚动和固定高度上限。", 80)), "长译文");
                    await Task.Delay(100);
                    int maxHeight = 330 * popup.DeviceDpi / 96;
                    if (shortHeight >= popup.Height || popup.Height > maxHeight + 3) throw new Exception("Popup height did not adapt or exceeded cap.");
                    var contentBox = Descendants(popup).OfType<RichTextBox>().Single();
                    if (contentBox.ScrollBars != RichTextBoxScrollBars.Vertical || contentBox.GetLineFromCharIndex(contentBox.TextLength) < 70)
                        throw new Exception("Long popup text lost lines or vertical scrolling.");
                    popup.Close();
                }
                string logoFile = Path.Combine(directory, "test-logo.ico");
                using (var stream = File.Create(logoFile)) SystemIcons.Information.Save(stream);
                var appearance = new AppConfig { LogoFile = logoFile };
                Ui.SetAppearance(appearance);
                using (var menu = new ContextMenuStrip())
                using (var dock = new FloatingDock(menu, appearance))
                {
                    Color background = dock.BackColor;
                    appearance.AccentColor = "#00FF00"; Ui.SetAppearance(appearance); dock.RefreshAppearance();
                    if (dock.BackColor != background || dock.BackColor == Ui.Teal) throw new Exception("Theme accent contaminated the custom floating logo.");
                }
                Ui.SetAppearance(new AppConfig());
                using (var dockMenu = new ContextMenuStrip())
                using (var dock = new FloatingDock(dockMenu, new AppConfig { FloatingX = 800, FloatingY = 200 }))
                {
                    int chosen = 0, dragged = 0;
                    dockMenu.Items.Add("离线菜单操作", null, (_, _) => chosen++);
                    dock.PositionSaved += _ => dragged++;
                    dock.Show();
                    void Mouse(string method, MouseButtons button, int x = 20, int y = 20) => typeof(Control).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(dock, [new MouseEventArgs(button, 1, x, y, 0)]);
                    void Click(MouseButtons button = MouseButtons.Left) { Mouse("OnMouseDown", button); Mouse("OnMouseUp", button); }
                    Click();
                    if (!dockMenu.Visible || dockMenu.AutoClose) throw new Exception("Dock click did not open its managed menu.");
                    dock.CheckMenuPointer(new Point(dockMenu.Left + 10, dockMenu.Top + 10), 1000);
                    dock.CheckMenuPointer(new Point(dockMenu.Left + 10, dockMenu.Top + 10), 2000);
                    if (!dockMenu.Visible) throw new Exception("Moving into the menu collapsed it.");
                    Click();
                    if (dockMenu.Visible || !dockMenu.AutoClose) throw new Exception("Second dock click did not close menu and restore tray behavior.");
                    Click();
                    if (!dockMenu.Visible) throw new Exception("Third dock click did not reopen menu.");
                    dock.CheckMenuPointer(Point.Empty, 3000); dock.CheckMenuPointer(Point.Empty, 3200);
                    if (!dockMenu.Visible) throw new Exception("Menu closed before the pointer transit grace period.");
                    dock.CheckMenuPointer(new Point(dockMenu.Left + 10, dockMenu.Top + 10), 3220);
                    dock.CheckMenuPointer(Point.Empty, 4000); dock.CheckMenuPointer(Point.Empty, 4300);
                    if (dockMenu.Visible || !dockMenu.AutoClose) throw new Exception("Leaving dock and menu did not dismiss it.");
                    Click(MouseButtons.Right); dockMenu.Items[0].PerformClick();
                    if (chosen != 1 || dockMenu.Visible) throw new Exception($"Menu action did not execute once and dismiss menu: chosen={chosen}, visible={dockMenu.Visible}, autoClose={dockMenu.AutoClose}.");
                    Mouse("OnMouseDown", MouseButtons.Left); Mouse("OnMouseMove", MouseButtons.Left, 45, 45); Mouse("OnMouseUp", MouseButtons.Left, 45, 45);
                    if (dragged != 1 || dockMenu.Visible) throw new Exception("Dragging should save the position without opening the menu.");
                    dock.Close();
                }
                var settingsConfig = new AppConfig();
                int previewCount = 0;
                using (var settings = new SettingsDialog(settingsConfig, c => { previewCount++; Ui.SetAppearance(c); }, false))
                {
                    settings.Show(host);
                    var theme = Descendants(settings).OfType<ComboBox>().First(c => c.Items.Count == 3 && c.Items[0]?.ToString() == "浅色");
                    theme.SelectedIndex = 1;
                    if (!Ui.IsDark || previewCount == 0) throw new Exception("Theme selection requires an extra confirmation.");
                    settings.Close();
                    if (Ui.IsDark) throw new Exception("Cancelling appearance preview did not restore saved theme.");
                }
                var shrinkHistory = new ReadingHistory(Path.Combine(directory, "settings-history"));
                for (int i = 0; i < 100; i++) shrinkHistory.Add("RECORD " + i, "译文", 100);
                var shrinkConfig = new AppConfig { TemplateFile = AppPaths.DefaultTemplate };
                string warning = "";
                using (var settings = new SettingsDialog(shrinkConfig, null, false, shrinkHistory, message => { warning = message; return false; }))
                {
                    settings.Show(host);
                    Descendants(settings).OfType<NumericUpDown>().Single().Value = 10;
                    Descendants(settings).OfType<Button>().Single(b => b.Text == "保存设置").PerformClick();
                    if (!warning.Contains("90") || !warning.Contains("无法恢复") || shrinkHistory.Records.Count != 100 || shrinkConfig.HistoryLimit != 100)
                        throw new Exception("Reducing history did not warn precisely or denial deleted records.");
                    settings.Close();
                }
                using (var settings = new SettingsDialog(shrinkConfig, null, false, shrinkHistory, _ => true))
                {
                    settings.Show(host); Descendants(settings).OfType<NumericUpDown>().Single().Value = 10;
                    Descendants(settings).OfType<Button>().Single(b => b.Text == "保存设置").PerformClick();
                    if (shrinkHistory.Records.Count != 10 || shrinkConfig.HistoryLimit != 10) throw new Exception("Confirmed history reduction did not take effect.");
                }
                host.Hide();
                var previewHistory = new ReadingHistory(directory);
                previewHistory.Add("HISTORY RESTORE TEST", "历史译文示例", 100);
                using (var main = new MainWindow(new AppConfig { TemplateFile = AppPaths.DefaultTemplate }, true, true))
                {
                    main.Show(); await Task.Delay(400);
                    File.WriteAllText(Path.Combine(directory, "layout-main.json"), JsonSerializer.Serialize(Descendants(main).Where(c => c is FieldFrame or ComboBox or CheckBox || c.Name == "brandTitle").Select(c => new { type = c.GetType().Name, c.Name, c.Bounds, parent = c.Parent?.ClientSize }), AppConfig.Json));
                    foreach (var frame in Descendants(main).OfType<FieldFrame>())
                        if (!frame.ClientRectangle.Contains(frame.Editor.Bounds) || !frame.Parent!.ClientRectangle.Contains(frame.Bounds)) throw new Exception("An input or frame extends beyond its row: " + frame.Editor.GetType().Name + " " + frame.Editor.Bounds + " in " + frame.ClientSize);
                    var historyList = Descendants(main).OfType<ListBox>().Single(c => c.AccessibleName == "最近翻译记录");
                    if (historyList.Items.Count < 1) throw new Exception("History sidebar did not load persisted records.");
                    historyList.SelectedIndex = 0;
                    Descendants(main).OfType<Button>().Single(b => b.Text == "打开记录").PerformClick();
                    if (Descendants(main).OfType<RichTextBox>().Single(r => r.AccessibleName == "原文").Text != "HISTORY RESTORE TEST")
                        throw new Exception("History did not restore original text.");
                    main.WindowState = FormWindowState.Minimized; await Task.Delay(150);
                    if (!main.Visible || !main.ShowInTaskbar || main.WindowState != FormWindowState.Minimized) throw new Exception("Minimize incorrectly hid the taskbar window.");
                    main.ShowMain(); main.Close(); await Task.Delay(150);
                    if (main.Visible || main.IsDisposed) throw new Exception("X should hide the window and keep tray process running.");
                    var originalEditor = Descendants(main).OfType<RichTextBox>().Single(r => r.AccessibleName == "原文");
                    if (originalEditor.Text.Length != 0) throw new Exception("Closing main retained unsaved current input.");
                    main.ShowMain();
                    var input = Descendants(main).OfType<TextBox>().Single(t => t.AccessibleName == "followUpQuestion");
                    var ask = Descendants(main).OfType<Button>().Single(t => t.AccessibleName == "followUpSubmit");
                    var inputFrame = input.Parent!;
                    if (inputFrame.Top != ask.Top || inputFrame.Height != ask.Height) throw new Exception("Question input frame and button are not aligned.");
                    var originalPanel = Descendants(main).OfType<MathReadingView>().First();
                    if (historyList.PointToScreen(Point.Empty).X >= originalPanel.PointToScreen(Point.Empty).X) throw new Exception("History must be left of the source.");
                    var brand = Descendants(main).OfType<Label>().Single(l => l.Name == "brandTitle");
                    if (brand.PointToScreen(Point.Empty).Y >= historyList.PointToScreen(Point.Empty).Y || brand.PointToScreen(Point.Empty).X >= originalPanel.PointToScreen(Point.Empty).X || brand.Height < brand.Font.Height)
                        throw new Exception("Brand must be above history on the left, with enough text height.");
                    if (Descendants(main).OfType<Label>().Any(l => l.Text.StartsWith("仅保留已完成的原文与译文"))) throw new Exception("Sidebar explanation should be removed.");
                    var sidebarToggle = Descendants(main).OfType<Button>().Single(b => b.Name == "sidebarToggle");
                    int expandedSourceLeft = originalPanel.PointToScreen(Point.Empty).X, originalCount = historyList.Items.Count;
                    string preservedSource = originalEditor.Text;
                    for (int toggleRound = 0; toggleRound < 3; toggleRound++)
                    {
                        sidebarToggle.PerformClick();
                        if (brand.Visible || historyList.Visible || !sidebarToggle.Visible || !AppConfig.Load().SidebarCollapsed || originalPanel.PointToScreen(Point.Empty).X >= expandedSourceLeft)
                            throw new Exception("Collapse should hide history, persist state and expand reading width.");
                        for (Control child = sidebarToggle; child.Parent != main; child = child.Parent!)
                            if (!child.Parent!.ClientRectangle.Contains(child.Bounds)) throw new Exception("Collapsed sidebar toggle is clipped or outside its parent: " + child.Bounds + " in " + child.Parent.ClientSize);
                        sidebarToggle.PerformClick();
                        if (!brand.Visible || !historyList.Visible || AppConfig.Load().SidebarCollapsed || historyList.Items.Count != originalCount || originalEditor.Text != preservedSource || originalPanel.PointToScreen(Point.Empty).X != expandedSourceLeft)
                            throw new Exception("Expand should restore sidebar without changing history or source.");
                    }
                    var direction = Descendants(main).OfType<Label>().Single(t => t.AccessibleName == "translationDirection");
                    if (Descendants(main).OfType<ComboBox>().Any(c => c.AccessibleName == "translationDirection")) throw new Exception("Manual direction selection should be removed.");
                    if (Descendants(main).OfType<Label>().Count(l => l.Text is "单词猫" or "WordsCat") != 1) throw new Exception("Brand should appear in the current language only.");
                    foreach (string caption in new[] { "粘贴原文", "打开单词本", "退出" }) if (UiGlyph.For(caption).Length == 0) throw new Exception("Missing action icon: " + caption);
                    originalEditor.Text = "中文原文 $q_1$"; input.Text = "旧追问";
                    if (!direction.Text.Contains("中文 → 英文") || !Descendants(main).OfType<Label>().Any(l => l.Text == "02   英文译文"))
                        throw new Exception("Chinese source must automatically select English translation: " + direction.Text + "; detected=" + TranslationLanguage.DetectDirection(originalEditor.Text) + "; source=" + originalEditor.Text + "; headings=" + string.Join("|", Descendants(main).OfType<Label>().Where(l => l.Text.StartsWith("02")).Select(l => l.Text)));
                    var translatedEditor = Descendants(main).OfType<RichTextBox>().Single(r => r.AccessibleName == "译文");
                    translatedEditor.Text = "Let $q_1$ be constant.";
                    Descendants(main).OfType<Button>().Single(b => b.Text == "交换").PerformClick();
                    if (!direction.Text.Contains("英文 → 中文") || originalEditor.Text != "Let $q_1$ be constant." || translatedEditor.Text.Length != 0 || input.Text.Length != 0)
                        throw new Exception("Swap must detect the new source and clear old translation/context without API calls.");
                    void SwitchLanguage(int index, bool save)
                    {
                        main.BeginInvoke((Action)(() =>
                        {
                            var dialog = Application.OpenForms.OfType<SettingsDialog>().Single();
                            var language = Descendants(dialog).OfType<ComboBox>().Single(c => c.AccessibleName == "uiLanguage");
                            language.SelectedIndex = index;
                            string expectedLanguage = index == 1 ? "en" : "zh-CN";
                            if (UiText.Language != expectedLanguage || !Descendants(main).OfType<Button>().Any(b => b.Text == UiText.T("设置")) || !Descendants(dialog).OfType<Label>().Any(l => l.Text == UiText.T("界面语言")))
                                throw new Exception("Interface language did not preview immediately in both windows.");
                            if (language.Parent is not FieldFrame || language.Parent.Width > 300 * dialog.DeviceDpi / 96)
                                throw new Exception("Language choice must have a compact visible frame.");
                            if (!Descendants(dialog).OfType<DataGridView>().Single().Columns.Cast<DataGridViewColumn>().Any(c => c.HeaderText == UiText.T("名称")))
                                throw new Exception("Grid headings did not follow language preview.");
                            if (save) Descendants(dialog).OfType<Button>().Single(b => b.Text == UiText.T("保存设置")).PerformClick(); else dialog.Close();
                        }));
                        Descendants(main).OfType<Button>().Single(b => b.Text == UiText.T("设置")).PerformClick();
                    }
                    SwitchLanguage(1, false);
                    if (UiText.Language != "zh-CN") throw new Exception("Cancelled language selection took effect.");
                    SwitchLanguage(1, true);
                    if (!Descendants(main).OfType<Button>().Any(b => b.Text == "Settings") || !input.PlaceholderText.StartsWith("Ask about") || AppConfig.Load().UiLanguage != "en")
                        throw new Exception("Saving English language did not update and persist the UI.");
                    if (originalEditor.Text != "Let $q_1$ be constant.") throw new Exception("Interface language modified source data.");
                    if (brand.Text != "WordsCat" || Descendants(main).OfType<Label>().Any(l => l.Text == "单词猫")) throw new Exception("English sidebar should display WordsCat only.");
                    SwitchLanguage(0, true);
                    if (!Descendants(main).OfType<Button>().Any(b => b.Text == "设置") || !Descendants(main).OfType<TabPage>().Any(p => p.Text == "编辑原文"))
                        throw new Exception("Switching back to Chinese failed.");
                    for (int round = 0; round < 3; round++)
                    {
                        main.Hide();
                        Descendants(main).OfType<RichTextBox>().Single(r => r.AccessibleName == "原文").Text = "SELECTION_" + round;
                        main.OpenPopup(new Rectangle(100, 100, 100, 40), "离线窗口切换验证");
                        await Task.Delay(120);
                        var popup = Application.OpenForms.OfType<TranslationPopup>().Single();
                        Descendants(popup).OfType<Button>().Single(b => b.Text == "打开主界面").PerformClick();
                        await Task.Delay(200);
                        if (!main.Visible || !main.Enabled || !popup.IsDisposed || main.WindowState != FormWindowState.Normal)
                            throw new Exception($"Popup to main activation failed in round {round}: visible={main.Visible}, enabled={main.Enabled}, popupDisposed={popup.IsDisposed}, foreground={Native.WindowTitle(Native.GetForegroundWindow())}");
                        await Task.Delay(300);
                        var originalView = Descendants(main).OfType<MathReadingView>().First();
                        string content = "";
                        for (int attempt = 0; attempt < 30; attempt++)
                        {
                            content = await originalView.InspectAsync("document.body.innerText");
                            if (content.Contains("SELECTION_" + round)) break;
                            await Task.Delay(100);
                        }
                        if (!content.Contains("SELECTION_" + round)) throw new Exception("Reading view showed a previous selection: " + content);
                    }
                    main.Hide();
                }
                File.WriteAllText(Path.Combine(directory, "ui-test-results.txt"), "PASS: single-language brand, all main action icons, no manual language selector, automatic Chinese/English headings and swap; framed inputs stay inside rows and align with the follow-up button.\nPASS: language previews immediately in main and settings, compact framed choices, bilingual grid headings, cancel rolls back and save persists; English brand has no duplicate Chinese subtitle.\nPASS: actual WebView2 KaTeX fractions AND loaded KaTeX Main/Math fonts, inert HTML, reading theme and settings preview rollback.\nPASS: short popup shrinks, long popup caps height with vertical scrolling; history sidebar restores original; precise 90-record irreversible-deletion warning; deny preserves data, confirm trims.\nPASS: minimize retains taskbar, X hides and clears current input; three consecutive popup-to-main transitions and latest selection rendering. Foreground activation remains subject to Windows foreground policy. No credentials or model requests.");
                File.AppendAllText(Path.Combine(directory, "ui-test-results.txt"), "\nPASS: dock click toggles open/closed/open, pointer inside menu stays open, outside grace period resets and dismisses, menu action executes once and closes, drag saves position without opening; standard tray AutoClose restored.\nPASS: three sidebar collapse/expand cycles persist state, preserve source/history and restore reading width; collapsed toggle remains within every parent boundary.");
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "ui-test-failure.txt"), SecretRedactor.Redact(ex.ToString())); Environment.ExitCode = 1; }
            finally { host.Close(); }
        };
        Application.Run(host);
    }
    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
