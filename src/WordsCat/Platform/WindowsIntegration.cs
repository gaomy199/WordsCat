using System.Runtime.InteropServices;
using System.Text;

namespace ReadDock;

internal static class Native
{
    public const int WM_HOTKEY = 0x0312;
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    public static void DragWindow(IntPtr hwnd) { ReleaseCapture(); SendMessage(hwnd, 0xA1, new IntPtr(2), IntPtr.Zero); }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct HARDWAREINPUT { public uint msg; public ushort low, high; }
    static INPUT Key(ushort vk, bool up = false) => new() { type = 1, U = new() { ki = new() { vk = vk, flags = up ? 2u : 0 } } };
    public static bool IsOurWindow(IntPtr hwnd) { GetWindowThreadProcessId(hwnd, out var pid); return pid == Environment.ProcessId; }
    public static string WindowTitle(IntPtr hwnd) { var sb = new StringBuilder(1024); GetWindowText(hwnd, sb, sb.Capacity); return sb.ToString(); }

    public static async Task<string> CopySelectionAsync(IntPtr source)
    {
        for (int i = 0; i < 35 && new[] { 0x10, 0x11, 0x12 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); i++) await Task.Delay(40);
        if (new[] { 0x10, 0x11, 0x12 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) throw new InvalidOperationException("请松开 Ctrl / Alt / Shift 后再试。");
        if (source == IntPtr.Zero || IsOurWindow(source)) throw new InvalidOperationException("请先在网页 / Zotero / WPS 中选择文字，再按 Ctrl+Alt+D。也可以手动复制后在工具中粘贴。");
        if (GetForegroundWindow() != source) { SetForegroundWindow(source); await Task.Delay(250); }
        if (GetForegroundWindow() != source) throw new InvalidOperationException("无法切回阅读器。请在原阅读器中选中文字，使用 Ctrl+Alt+D，或改用截图翻译。");
        IDataObject? backup = null;
        try { backup = Clipboard.GetDataObject(); } catch { }
        var before = GetClipboardSequenceNumber();
        var inputs = new[] { Key(0x11), Key(0x43), Key(0x43, true), Key(0x11, true) };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != inputs.Length)
            throw new InvalidOperationException("无法发送复制操作。请手动复制文字，或使用截图翻译。");
        uint copiedSequence = 0;
        string text = "";
        try
        {
            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(45);
                if (GetClipboardSequenceNumber() != before && GetForegroundWindow() == source)
                {
                    copiedSequence = GetClipboardSequenceNumber();
                    try { if (Clipboard.ContainsText()) { text = Clipboard.GetText(); break; } } catch (ExternalException) { }
                }
            }
        }
        finally
        {
            if (copiedSequence != 0 && copiedSequence == GetClipboardSequenceNumber())
            {
                try { if (backup != null) Clipboard.SetDataObject(backup, true); else Clipboard.Clear(); } catch { }
            }
        }
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("没有取得选中文字。扫描版 PDF 请用 Ctrl+Alt+S 截图翻译；也可以复制后粘贴到原文框。");
        return text;
    }
}

internal static class CredentialStore
{
    private const string Target = ModelPresets.CredentialTarget;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDENTIAL
    {
        public uint Flags, Type;
        public string TargetName, Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias, UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredWrite(ref CREDENTIAL credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr ptr);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredDelete(string target, uint type, uint flags);
    static bool MatchesScope(CREDENTIAL credential, string baseUrl) =>
        CredentialScope.Matches(credential.Comment, baseUrl);
    public static bool HasSavedKey(string baseUrl)
    {
        if (!CredRead(Target, 1, 0, out var ptr))
        {
            if (Marshal.GetLastWin32Error() == 1168) return false;
            throw new InvalidOperationException("无法检查 Windows 凭据管理器。");
        }
        // Presence and origin metadata only. Never decode the secret for UI display.
        try { var c = Marshal.PtrToStructure<CREDENTIAL>(ptr); return c.CredentialBlobSize > 0 && MatchesScope(c, baseUrl); }
        finally { CredFree(ptr); }
    }
    public static string ReadFor(string baseUrl)
    {
        if (!CredRead(Target, 1, 0, out var ptr)) return "";
        try
        {
            var c = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            if (!MatchesScope(c, baseUrl)) return "";
            string value = Marshal.PtrToStringUni(c.CredentialBlob, (int)c.CredentialBlobSize / 2) ?? "";
            SecretRedactor.Register(value); return value;
        }
        finally { CredFree(ptr); }
    }
    public static string Read() => ReadFor(ModelPresets.BaseUrl);
    public static void Save(string key) => SaveFor(key, ModelPresets.BaseUrl);
    public static void SaveFor(string key, string baseUrl)
    {
        SecretRedactor.Register(key);
        if (string.IsNullOrWhiteSpace(key)) { CredDelete(Target, 1, 0); return; }
        if (key.Length > 1000) throw new InvalidOperationException("API Key 长度异常。");
        if (CredentialScope.Origin(baseUrl).Length == 0) throw new InvalidOperationException("请输入有效的 HTTPS 基础地址。");
        var blob = Marshal.StringToCoTaskMemUni(key);
        try
        {
            var c = new CREDENTIAL { Type = 1, TargetName = Target, Comment = CredentialScope.Prefix + CredentialScope.Origin(baseUrl), CredentialBlobSize = (uint)Encoding.Unicode.GetByteCount(key), CredentialBlob = blob, Persist = 2, UserName = Environment.UserName, TargetAlias = "" };
            if (!CredWrite(ref c, 0)) throw new InvalidOperationException("Windows 凭据管理器保存失败，错误码：" + Marshal.GetLastWin32Error());
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(blob); }
    }
}

internal sealed class CaptureOverlay : Form
{
    private readonly Bitmap snapshot;
    private Point start;
    private Rectangle selection;
    private bool dragging;
    public Bitmap? SelectedImage { get; private set; }
    public Rectangle SelectedScreenBounds { get; private set; }
    public CaptureOverlay()
    {
        Text = UiText.T("单词猫 · 框选截图");
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false; TopMost = true; KeyPreview = true;
        StartPosition = FormStartPosition.Manual; Bounds = SystemInformation.VirtualScreen;
        snapshot = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(snapshot)) g.CopyFromScreen(Bounds.Location, Point.Empty, Bounds.Size);
        DoubleBuffered = true; Cursor = Cursors.Cross;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; } if (e.Button != MouseButtons.Left) return; dragging = true; start = e.Location; Capture = true; };
        MouseMove += (_, e) => { if (!dragging) return; selection = Rectangle.Intersect(ClientRectangle, Rectangle.FromLTRB(Math.Min(start.X, e.X), Math.Min(start.Y, e.Y), Math.Max(start.X, e.X), Math.Max(start.Y, e.Y))); Invalidate(); };
        MouseUp += (_, e) =>
        {
            if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false; Capture = false;
            if (selection.Width < 8 || selection.Height < 8) { selection = Rectangle.Empty; Invalidate(); return; }
            SelectedScreenBounds = new Rectangle(Left + selection.X, Top + selection.Y, selection.Width, selection.Height);
            // Windows OCR accepts images up to 2600 pixels; downscale very large selections.
            var scale = Math.Min(1.0, 2500.0 / Math.Max(selection.Width, selection.Height));
            SelectedImage = new Bitmap(Math.Max(1, (int)(selection.Width * scale)), Math.Max(1, (int)(selection.Height * scale)));
            using (var g = Graphics.FromImage(SelectedImage)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(snapshot, new Rectangle(Point.Empty, SelectedImage.Size), selection, GraphicsUnit.Pixel); }
            DialogResult = DialogResult.OK; Close();
        };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(snapshot, 0, 0);
        using var dim = new SolidBrush(Color.FromArgb(135, 13, 27, 34)); e.Graphics.FillRectangle(dim, ClientRectangle);
        if (selection.Width > 0)
        {
            e.Graphics.DrawImage(snapshot, selection, selection, GraphicsUnit.Pixel);
            using var pen = new Pen(Color.FromArgb(83, 220, 188), 2); e.Graphics.DrawRectangle(pen, selection);
        }
        var hint = UiText.T("框选文字区域 · 松开鼠标完成 · Esc / 右键取消");
        var hintRect = new Rectangle(24, 24, 530, 48);
        using var background = new SolidBrush(Color.FromArgb(245, 18, 39, 46)); e.Graphics.FillRectangle(background, hintRect);
        using var font = new Font("Microsoft YaHei UI", 12); TextRenderer.DrawText(e.Graphics, hint, font, hintRect, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }
    protected override void Dispose(bool disposing) { if (disposing) snapshot.Dispose(); base.Dispose(disposing); }
}

internal sealed class FloatingDock : Form
{
    private readonly ContextMenuStrip menu;
    private readonly System.Windows.Forms.Timer hover = new() { Interval = 350 };
    private readonly System.Windows.Forms.Timer leave = new() { Interval = 75 };
    private Point dragStart;
    private bool dragging, moved, dockMenuOpen, suppressHover, closeOnRelease;
    private long outsideSince;
    public event Action<Point>? PositionSaved;
    public FloatingDock(ContextMenuStrip menu, AppConfig config)
    {
        this.menu = menu; AutoScaleMode = AutoScaleMode.None; Text = "单词猫 · 悬浮图标";
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        ClientSize = new Size(56, 56); BackColor = LogoBackground; Cursor = Cursors.Hand; DoubleBuffered = true;
        StartPosition = FormStartPosition.Manual;
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = config.FloatingX >= 0 || config.FloatingY >= 0 ? new Point(config.FloatingX, config.FloatingY) : new Point(screen.Right - 90, screen.Top + screen.Height / 3);
        if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(Bounds))) Location = new Point(screen.Right - 90, screen.Top + 200);
        using var path = new System.Drawing.Drawing2D.GraphicsPath(); path.AddEllipse(1, 1, 54, 54); Region = new Region(path);
        hover.Tick += (_, _) => { hover.Stop(); if (Bounds.Contains(Cursor.Position) && !dragging && !menu.Visible && !suppressHover) ShowDockMenu(); };
        leave.Tick += (_, _) => CheckMenuPointer(Cursor.Position, Environment.TickCount64);
        menu.Closing += MenuClosing;
        menu.Closed += MenuClosed;
        menu.KeyDown += MenuKeyDown;
        menu.ItemClicked += MenuItemClicked;
        MouseEnter += (_, _) => { if (!suppressHover) hover.Start(); };
        MouseLeave += (_, _) => { hover.Stop(); suppressHover = false; };
        MouseDown += (_, e) =>
        {
            if (e.Button is not (MouseButtons.Left or MouseButtons.Right)) return;
            hover.Stop();
            closeOnRelease = menu.Visible;
            dragging = true; moved = false; dragStart = e.Location; Capture = true;
        };
        MouseMove += (_, e) =>
        {
            if (!dragging) return;
            var dx = e.X - dragStart.X; var dy = e.Y - dragStart.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 3) moved = true;
            if (moved) { hover.Stop(); if (dockMenuOpen) menu.Close(); Location = new Point(Left + dx, Top + dy); }
        };
        MouseUp += (_, e) =>
        {
            if (!dragging) return;
            dragging = false; Capture = false;
            if (moved) PositionSaved?.Invoke(Location);
            else if (closeOnRelease) { suppressHover = true; menu.Close(); }
            else ShowDockMenu();
        };
        MouseCaptureChanged += (_, _) => { if (Capture) return; dragging = false; };
        VisibleChanged += (_, _) => { if (!Visible) { hover.Stop(); if (dockMenuOpen) menu.Close(); } };
    }
    void ShowDockMenu()
    {
        hover.Stop(); suppressHover = false; outsideSince = 0;
        // ToolStrip's modal outside-click handler otherwise dismisses then reopens
        // on the same icon click. The dock owns dismissal while this menu is open;
        // tray menus regain the standard AutoClose behavior when it closes.
        menu.AutoClose = false;
        menu.Show(this, new Point(-menu.PreferredSize.Width, Height));
        dockMenuOpen = menu.Visible;
        if (dockMenuOpen) leave.Start();
    }
    void MenuClosing(object? sender, ToolStripDropDownClosingEventArgs e)
    {
        if (!dockMenuOpen) return;
        suppressHover = Bounds.Contains(Cursor.Position);
    }
    void MenuClosed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        dockMenuOpen = false; outsideSince = 0; leave.Stop(); menu.AutoClose = true;
        // Closing a non-modal ToolStrip can reorder its source window behind the
        // main form. Keep the floating entry visible without activating it.
        if (Visible && !IsDisposed) { TopMost = true; BringToFront(); Invalidate(); }
    }
    void MenuKeyDown(object? sender, KeyEventArgs e) { if (dockMenuOpen && e.KeyCode == Keys.Escape) { e.Handled = true; menu.Close(); } }
    void MenuItemClicked(object? sender, ToolStripItemClickedEventArgs e) { if (dockMenuOpen) menu.Close(); }
    internal void CheckMenuPointer(Point pointer, long now)
    {
        if (!dockMenuOpen || !menu.Visible) return;
        if (dragging || Rectangle.Inflate(Bounds, 4, 4).Contains(pointer) || Rectangle.Inflate(menu.Bounds, 4, 4).Contains(pointer)) { outsideSince = 0; return; }
        // A short grace period lets the pointer cross from the round icon to the
        // adjacent menu without collapsing it along the way.
        if (outsideSince == 0) outsideSince = now;
        else if (now - outsideSince >= 250) menu.Close();
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80; return cp; } }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(Ui.Logo == null ? Ui.Border : Color.FromArgb(212, 224, 223), 2); e.Graphics.DrawEllipse(pen, 3, 3, 49, 49);
        if (Ui.Logo != null) Ui.DrawLogo(e.Graphics, Ui.Logo, new Rectangle(7, 7, 42, 42));
        else { using var font = new Font("Microsoft YaHei UI", 19, FontStyle.Bold); TextRenderer.DrawText(e.Graphics, "猫", font, ClientRectangle, Ui.OnAccent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }
    }
    static Color LogoBackground => Ui.Logo == null ? Ui.Teal : Color.FromArgb(246, 248, 247);
    public void RefreshAppearance() { BackColor = LogoBackground; Invalidate(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { hover.Stop(); leave.Stop(); if (dockMenuOpen && !menu.IsDisposed) menu.Close(); hover.Dispose(); leave.Dispose(); menu.Closing -= MenuClosing; menu.Closed -= MenuClosed; menu.KeyDown -= MenuKeyDown; menu.ItemClicked -= MenuItemClicked; }
        base.Dispose(disposing);
    }
}
