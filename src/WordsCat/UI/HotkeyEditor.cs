namespace ReadDock;

// Keep native focus and accessibility while recording a single shortcut chord.
internal sealed class HotkeyEditor : TextBox
{
    internal HotkeyEditor(HotkeyBinding binding)
    {
        Text = binding.Gesture; ReadOnly = true; ShortcutsEnabled = false;
        Font = Ui.Font(); Dock = DockStyle.Fill;
        Name = "hotkey_" + binding.Action;
        AccessibleName = UiText.T(binding.CaptionKey);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Tab and Escape keep their normal navigation/cancel behavior.
        Keys key = keyData & Keys.KeyCode;
        if (key is Keys.Tab or Keys.Escape) return base.ProcessCmdKey(ref msg, keyData);
        HotkeyModifiers modifiers = 0;
        if ((keyData & Keys.Control) != 0) modifiers |= HotkeyModifiers.Control;
        if ((keyData & Keys.Alt) != 0) modifiers |= HotkeyModifiers.Alt;
        if ((keyData & Keys.Shift) != 0) modifiers |= HotkeyModifiers.Shift;
        if (AppHotkeys.IsValid(modifiers, key))
        {
            Text = new HotkeyBinding(HotkeyAction.TranslateSelection, modifiers, key, "").Gesture;
            SelectionStart = TextLength; SelectionLength = 0;
        }
        return true;
    }
}
