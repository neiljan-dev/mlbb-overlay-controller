using System.Windows.Input;

namespace MlbbOverlay.Services;

/// <summary>A key plus modifiers, for example Ctrl+Z.</summary>
public readonly record struct HotkeyGesture(Key Key, ModifierKeys Modifiers)
{
    /// <summary>Reads text like "Ctrl+Shift+F5". Returns false if it isn't valid.</summary>
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var modifiers = ModifierKeys.None;
        var key = Key.None;

        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": modifiers |= ModifierKeys.Control; break;
                case "alt": modifiers |= ModifierKeys.Alt; break;
                case "shift": modifiers |= ModifierKeys.Shift; break;
                default:
                    if (part.All(char.IsDigit) || !Enum.TryParse(part, true, out key)) return false;
                    break;
            }
        }

        if (key == Key.None) return false;
        gesture = new HotkeyGesture(key, modifiers);
        return true;
    }

    /// <summary>The text that is saved in settings.json.</summary>
    public override string ToString() => Join(Key.ToString());

    /// <summary>The text shown to you (for example "Ctrl+1" instead of "Ctrl+D1").</summary>
    public string Display => Join(KeyName(Key));

    private string Join(string keyText)
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(keyText);
        return string.Join("+", parts);
    }

    private static string KeyName(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9) return ((int)key - (int)Key.D0).ToString();
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num " + ((int)key - (int)Key.NumPad0);

        return key switch
        {
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Return => "Enter",
            Key.Prior => "Page Up",
            Key.Next => "Page Down",
            _ => key.ToString()
        };
    }
}