namespace ReadDock;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try { Run(args); }
        finally { ReadingRuntime.Cleanup(); }
    }
    static void Run(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var dataIndex = Array.IndexOf(args, "--data-dir");
        AppPaths.DataDirectory = dataIndex >= 0 && dataIndex + 1 < args.Length
            ? Path.GetFullPath(args[dataIndex + 1])
            : Path.Combine(AppContext.BaseDirectory, "Data");
        int cleanupIndex = Array.IndexOf(args, "--cleanup-cache");
        if (cleanupIndex >= 0) { if (cleanupIndex + 1 < args.Length) ReadingRuntime.CleanupAfterExit(AppPaths.DataDirectory, args[cleanupIndex + 1]); return; }
        if (args.Contains("--render-self-test")) { UiSelfTests.Run(AppPaths.DataDirectory); return; }
        if (args.Contains("--self-test"))
        {
            int regressionIndex = Array.IndexOf(args, "--regression-dir");
            try { SelfTests.RunAsync(AppPaths.DataDirectory, regressionIndex >= 0 && regressionIndex + 1 < args.Length ? args[regressionIndex + 1] : null).GetAwaiter().GetResult(); Environment.ExitCode = 0; }
            catch (Exception ex)
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                File.WriteAllText(Path.Combine(AppPaths.DataDirectory, "test-failure.txt"), SecretRedactor.Redact(ex.ToString()));
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.Contains("--vision-check"))
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(Path.Combine(AppPaths.DataDirectory, "vision-check.txt"), "DISABLED: developer diagnostics never read production credentials or call model services.");
            return;
        }
        bool offlinePreview = args.Contains("--offline-preview");
        if (offlinePreview && dataIndex < 0) AppPaths.DataDirectory = Path.Combine(AppContext.BaseDirectory, "PreviewData");
        using var mutex = new Mutex(true, offlinePreview ? @"Local\ReadDock.OfflinePreview.v1" : @"Local\ReadDock.Desktop.v1", out bool isNew);
        if (!isNew) { UiText.Show("单词猫已经在运行，请查看屏幕悬浮图标或系统托盘。", "单词猫 WordsCat"); return; }
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var config = AppConfig.Load();
            UiText.Language = config.UiLanguage;
            ReadingRuntime.CleanupStale(AppPaths.DataDirectory, !offlinePreview);
            if (offlinePreview) { config.ScreenshotEngine = "local"; config.ShowOnStartup = true; }
            Ui.SetAppearance(config);
            Application.Run(new MainWindow(config, args.Contains("--preview") || offlinePreview, offlinePreview));
        }
        catch (Exception ex) { UiText.Show(UiText.T("启动失败：") + SecretRedactor.Redact(UiText.T(ex.Message)) + UiText.T("\n请将整个文件夹解压到可写入的位置后运行。"), "单词猫 WordsCat"); }
    }
}

internal static class AppPaths
{
    public static string DataDirectory { get; set; } = "";
    public static string ConfigFile => Path.Combine(DataDirectory, "settings.json");
    public static string DefaultTemplate => Path.Combine(AppContext.BaseDirectory, "vocabulary.template.json");
}
