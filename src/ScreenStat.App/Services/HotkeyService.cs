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

    public bool RegisterDefault()
    {
        // Ctrl + Shift + X
        return Register(NativeMethods.ModControl | NativeMethods.ModShift | NativeMethods.ModNorepeat, (uint)KeyToVirtualKey(System.Windows.Input.Key.X));
    }

    public bool Register(uint modifiers, uint virtualKey)
    {
        EnsureMessageWindow();
        if (_source?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (_registered)
        {
            NativeMethods.UnregisterHotKey(hwnd, _hotkeyId);
            _registered = false;
        }

        _registered = NativeMethods.RegisterHotKey(hwnd, _hotkeyId, modifiers, virtualKey);
        return _registered;
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

    private static uint KeyToVirtualKey(System.Windows.Input.Key key)
    {
        return (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
    }
}

