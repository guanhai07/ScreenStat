using System.Windows;
using System.Windows.Interop;
using ScreenStat.App.Infrastructure;

namespace ScreenStat.App.Services;

internal sealed class HotkeyService : IDisposable
{
    private readonly int _hotkeyId = 0x5301;
    private HwndSource? _source;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    /// <summary>The combination currently registered, or null if none is.</summary>
    public HotkeyDefinition? Current { get; private set; }

    public bool IsRegistered => _registered;

    /// <summary>
    /// Registers <paramref name="hotkey"/>, keeping the previous one if the new
    /// combination is refused. Windows gives no reason for a refusal, but in
    /// practice it means another program owns that combination.
    /// </summary>
    public bool TryRegister(HotkeyDefinition hotkey)
    {
        if (!hotkey.IsValid)
        {
            return false;
        }

        EnsureMessageWindow();
        if (_source?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
        {
            return false;
        }

        var previous = Current;
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(hwnd, _hotkeyId);
            _registered = false;
            Current = null;
        }

        if (NativeMethods.RegisterHotKey(hwnd, _hotkeyId, hotkey.ToWin32Modifiers(), hotkey.ToVirtualKey()))
        {
            _registered = true;
            Current = hotkey;
            return true;
        }

        // Put the old one back, so a failed change does not also lose the
        // hotkey that was working a moment ago.
        if (previous is not null &&
            NativeMethods.RegisterHotKey(hwnd, _hotkeyId, previous.ToWin32Modifiers(), previous.ToVirtualKey()))
        {
            _registered = true;
            Current = previous;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_source is not null)
        {
            if (_registered)
            {
                NativeMethods.UnregisterHotKey(_source.Handle, _hotkeyId);
                _registered = false;
            }

            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    private void EnsureMessageWindow()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("ScreenStatHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = unchecked((int)0x80000000) // WS_POPUP
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}

