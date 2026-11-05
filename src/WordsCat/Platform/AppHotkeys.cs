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
        Key is >= Keys.D0 and <= Keys.D9 ? ((int)Key - (int)Keys.D0).ToString() : Key.ToString()
    }.Where(part => part != null));
    internal string MenuText => UiText.T(CaptionKey) + "          " + Gesture;
}

// One definition for registration, dispatch IDs and displayed shortcuts.
internal static class AppHotkeys
{
    internal static IReadOnlyList<HotkeyBinding> Defaults { get; } = Array.AsReadOnly(new[]
    {
        new HotkeyBinding(HotkeyAction.TranslateSelection, HotkeyModifiers.Alt, Keys.X, "划词翻译"),
        new HotkeyBinding(HotkeyAction.CaptureImage, HotkeyModifiers.Alt, Keys.S, "截图翻译"),
        new HotkeyBinding(HotkeyAction.SaveWord, HotkeyModifiers.Alt, Keys.W, "收藏单词"),
        new HotkeyBinding(HotkeyAction.ToggleMain, HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.R, "显示 / 隐藏主窗口")
    });
    internal static IReadOnlyList<HotkeyBinding> All { get; private set; } = Defaults;
    internal static void Configure(AppConfig config) => All = Resolve(config.Hotkeys);
    internal static IReadOnlyList<HotkeyBinding> Resolve(IReadOnlyDictionary<string, string> values)
    {
        var bindings = Defaults.Select(binding => values.TryGetValue(binding.Action.ToString(), out string? gesture)
            ? Parse(binding, gesture) : binding).ToArray();
        if (bindings.Select(binding => (binding.Modifiers, binding.Key)).Distinct().Count() != bindings.Length)
            throw new InvalidDataException("快捷键不能重复，请为每个功能设置不同的组合键。");
        return Array.AsReadOnly(bindings);
    }
    internal static HotkeyBinding Parse(HotkeyBinding definition, string? gesture)
    {
        var parts = (gesture ?? "").Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        HotkeyModifiers modifiers = 0;
        if (parts.Length < 2) throw new InvalidDataException("快捷键须包含 Ctrl 或 Alt，以及一个字母、数字、功能键或方向键。");
        foreach (string part in parts[..^1])
            modifiers |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Control,
                "ALT" => HotkeyModifiers.Alt,
                "SHIFT" => HotkeyModifiers.Shift,
                _ => throw new InvalidDataException("快捷键须包含 Ctrl 或 Alt，以及一个字母、数字、功能键或方向键。")
            };
        string name = parts[^1];
        if (name.Length == 1 && char.IsAsciiDigit(name[0])) name = "D" + name;
        if (!Enum.TryParse<Keys>(name, true, out var key) || !IsValid(modifiers, key))
            throw new InvalidDataException("快捷键须包含 Ctrl 或 Alt，以及一个字母、数字、功能键或方向键。");
        return definition with { Modifiers = modifiers, Key = key };
    }
    internal static bool IsValid(HotkeyModifiers modifiers, Keys key) =>
        (modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt)) != 0
        && (key is >= Keys.A and <= Keys.Z or >= Keys.D0 and <= Keys.D9 or >= Keys.NumPad0 and <= Keys.NumPad9
            or >= Keys.F1 and <= Keys.F24 or Keys.Left or Keys.Right or Keys.Up or Keys.Down
            or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Insert or Keys.Delete
            or Keys.Space or Keys.Back or Keys.Enter or Keys.Oemcomma or Keys.OemPeriod or Keys.OemMinus or Keys.Oemplus);
    internal static HotkeyBinding Get(HotkeyAction action) => All.First(binding => binding.Action == action);
    internal static string TrayText => UiText.F("单词猫 WordsCat · {0} 截图翻译", Get(HotkeyAction.CaptureImage).Gesture);
    internal static string HelpText => string.Join("\n", All.Select(binding => binding.Gesture + "  " + UiText.T(binding.CaptionKey)))
        + "\n" + UiText.T("单击托盘图标打开主界面。取词时先显示轻量弹窗，再继续后台翻译。");
}
