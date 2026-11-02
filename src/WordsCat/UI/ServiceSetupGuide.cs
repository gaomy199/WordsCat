namespace ReadDock;

internal sealed class ServiceSetupGuide : Form
{
    internal ServiceSetupGuide(Action openSettings, Action dismissed)
    {
        Text = "设置模型服务"; Name = "serviceSetupGuide"; Font = Ui.Font(); AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(480, 220); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.Controls.Add(Ui.Label("先设置一个模型服务", true, 14));
        root.Controls.Add(Ui.Label("尚未配置 API Key。可前往设置选择服务并保存密钥。\n你也可以稍后设置，继续使用文本视图。", false, 10));
        var settings = Ui.Button("打开设置", () => { Close(); openSettings(); }, true, 130); settings.Name = "guideOpenSettings";
        var later = Ui.Button("稍后设置", Close, false, 120); later.Name = "guideLater";
        root.Controls.Add(Ui.Row(settings, later)); Controls.Add(root);
        CancelButton = later; FormClosed += (_, _) => dismissed(); Ui.FinishLayout(this);
    }
    internal static bool ShouldShow(AppConfig config, bool hasKey) => !config.ServiceGuideSeen && !hasKey;
}
