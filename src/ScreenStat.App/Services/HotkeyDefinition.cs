using System.Text;
using System.Windows.Input;
using ScreenStat.App.Infrastructure;

namespace ScreenStat.App.Services;

/// <summary>
/// A global hotkey as the user configured it. Stored as a WPF key plus
/// modifiers and converted to Win32 values only at registration time, so the
/// settings file stays readable.
/// </summary>
public sealed record HotkeyDefinition(ModifierKeys Modifiers, Key Key)
{
    public static HotkeyDefinition Default { get; } =
        new(ModifierKeys.Control | ModifierKeys.Shift, Key.X);

    /// <summary>
    /// True when this combination is safe to register. Windows will happily
    /// take a bare letter and then swallow that key everywhere, so a modifier
    /// is required; a lone modifier is not a hotkey at all.
    /// </summary>
    public bool IsValid =>
        Modifiers != ModifierKeys.None &&
        Key is not (Key.None
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin
            or Key.System);

    public uint ToWin32Modifiers()
    {
        uint value = NativeMethods.ModNorepeat;
        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            value |= NativeMethods.ModControl;
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            value |= NativeMethods.ModShift;
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            value |= NativeMethods.ModAlt;
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            value |= NativeMethods.ModWin;
        }

        return value;
    }

    public uint ToVirtualKey() => (uint)KeyInterop.VirtualKeyFromKey(Key);

    /// <summary>Display form, for example "Ctrl+Shift+X".</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            builder.Append("Ctrl+");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            builder.Append("Alt+");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            builder.Append("Shift+");
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            builder.Append("Win+");
        }

        builder.Append(Key);
        return builder.ToString();
    }

    /// <summary>Round-trips through the settings file, for example "Control+Shift, X".</summary>
    public string Serialize() => $"{Modifiers}|{Key}";

    public static HotkeyDefinition Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Default;
        }

        var parts = text.Split('|');
        if (parts.Length != 2 ||
            !Enum.TryParse<ModifierKeys>(parts[0], ignoreCase: true, out var modifiers) ||
            !Enum.TryParse<Key>(parts[1], ignoreCase: true, out var key))
        {
            return Default;
        }

        var parsed = new HotkeyDefinition(modifiers, key);
        // A hand-edited settings file could hold something unusable; falling
        // back beats starting with a hotkey that can never register.
        return parsed.IsValid ? parsed : Default;
    }
}
