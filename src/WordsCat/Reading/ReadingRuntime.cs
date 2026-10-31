using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace ReadDock;

internal static class ReadingRuntime
{
    static string? folder;
    static readonly HashSet<int> processes = [];
    public static string Folder => folder ??= Path.Combine(AppPaths.DataDirectory, "Cache", $"WebView2-{Environment.ProcessId}-{Guid.NewGuid():N}");
    public static void Track(CoreWebView2 core) => processes.Add(checked((int)core.BrowserProcessId));
    public static void Cleanup()
    {
        foreach (int id in processes)
            try { using var process = Process.GetProcessById(id); if (process.ProcessName == "msedgewebview2") process.WaitForExit(600); }
            catch (ArgumentException) { }
            catch (System.ComponentModel.Win32Exception) { }
        if (folder == null || TryDelete(folder)) return;
        // Chromium can retain the profile lock until its parent exits. This short-lived copy only cleans our named cache after exit.
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var arg in new[] { "--cleanup-cache", Path.GetFileName(folder), "--data-dir", AppPaths.DataDirectory }) start.ArgumentList.Add(arg);
            using var helper = Process.Start(start);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }
    public static void CleanupAfterExit(string dataDirectory, string name)
    {
        var parts = name.Split('-');
        if (parts.Length != 3 || parts[0] != "WebView2" || !int.TryParse(parts[1], out int parent) || parent == Environment.ProcessId || !Guid.TryParseExact(parts[2], "N", out _)) return;
        try { using var process = Process.GetProcessById(parent); if (process.ProcessName is "WordsCat" or "ReadDock") process.WaitForExit(15000); }
        catch (ArgumentException) { }
        catch (System.ComponentModel.Win32Exception) { }
        string cache = Path.Combine(dataDirectory, "Cache");
        if (!Directory.Exists(cache) || IsLink(cache)) return;
        string target = Path.Combine(cache, name);
        for (int attempt = 0; attempt < 30 && !TryDelete(target); attempt++) Thread.Sleep(200);
    }
    public static void CleanupStale(string dataDirectory, bool includeLegacy)
    {
        if (Directory.Exists(dataDirectory))
            foreach (var file in Directory.EnumerateFiles(dataDirectory, "capture-*.png"))
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file)[8..], "N", out _))
                    try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        var cache = Path.Combine(dataDirectory, "Cache");
        if (Directory.Exists(cache) && !IsLink(cache))
            foreach (var candidate in Directory.EnumerateDirectories(cache, "WebView2-*"))
            {
                var parts = Path.GetFileName(candidate).Split('-');
                if (parts.Length != 3 || !int.TryParse(parts[1], out int id) || !Guid.TryParseExact(parts[2], "N", out _)) continue;
                try { using var active = Process.GetProcessById(id); if (!active.HasExited) continue; } catch (ArgumentException) { }
                catch (System.ComponentModel.Win32Exception) { continue; }
                TryDelete(candidate);
            }
        if (includeLegacy) TryDelete(Path.Combine(dataDirectory, "WebView2"));
    }
    static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    static bool TryDelete(string path)
    {
        try
        {
            // Only called with application-owned cache folders; never follow junctions.
            if (Directory.Exists(path) && !IsLink(path) && !HasLinks(path))
                Directory.Delete(path, true);
            return !Directory.Exists(path);
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
        return false;
    }
    static bool HasLinks(string path) => Directory.EnumerateDirectories(path).Any(child => IsLink(child) || HasLinks(child));
}
