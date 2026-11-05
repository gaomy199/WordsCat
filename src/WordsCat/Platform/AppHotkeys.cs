namespace ReadDock;

internal enum HotkeyAction { TranslateSelection = 1, CaptureImage = 2, SaveWord = 3, ToggleMain = 4 }

[Flags]
internal enum HotkeyModifiers : uint { Alt = 1, Control = 2, Shift = 4 }

internal sealed record HotkeyBinding(HotkeyAction Action, HotkeyModifiers Modifiers, Keys Key, string CaptionKey)
{
    internal int Id => (int)Action;
    internal uint RegistrationModifiers => (uint)Modifiers | 0x4000; // MOD_NOREPEAT
    internal string Gesture => string.Join("+", new[]
    {
        Modifiers.HasFlag(HotkeyModifiers.Control) ? "Ctrl" : null,
        Modifiers.HasFlag(HotkeyModifiers.Alt) ? "Alt" : null,
        Modifiers.HasFlag(HotkeyModifiers.Shift) ? "Shift" : null,
        Key.ToString()
    }.Where(part => part != null));
    internal string MenuText => UiText.T(CaptionKey) + "          " + Gesture;
}

// One definition for registration, dispatch IDs and displayed shortcuts.
// A future editor can supply bindings here; no user configuration is added yet.
internal static class AppHotkeys
{
    internal static IReadOnlyList<HotkeyBinding> All { get; } = Array.AsReadOnly(new[]
    {
        new HotkeyBinding(HotkeyAction.TranslateSelection, HotkeyModifiers.Alt, Keys.X, "划词翻译"),
        new HotkeyBinding(HotkeyAction.CaptureImage, HotkeyModifiers.Alt, Keys.S, "截图翻译"),
        new HotkeyBinding(HotkeyAction.SaveWord, HotkeyModifiers.Alt, Keys.W, "收藏单词"),
        new HotkeyBinding(HotkeyAction.ToggleMain, HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.R, "显示 / 隐藏主窗口")
    });
    internal static HotkeyBinding Get(HotkeyAction action) => All.First(binding => binding.Action == action);
    internal static string TrayText => UiText.F("单词猫 WordsCat · {0} 截图翻译", Get(HotkeyAction.CaptureImage).Gesture);
    internal static string HelpText => string.Join("\n", All.Select(binding => binding.Gesture + "  " + UiText.T(binding.CaptionKey)))
        + "\n" + UiText.T("单击托盘图标打开主界面。取词时先显示轻量弹窗，再继续后台翻译。");
}
