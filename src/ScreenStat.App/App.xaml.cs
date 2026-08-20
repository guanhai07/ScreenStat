using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ScreenStat.App.Infrastructure;
using ScreenStat.App.Resources;
using ScreenStat.App.Services;
using ScreenStat.App.ViewModels;
using ScreenStat.App.Views;
using ScreenStat.Core.Abstractions;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;

namespace ScreenStat.App;

public partial class App : WpfApplication
{
    private Forms.NotifyIcon? _trayIcon;
    private HotkeyService? _hotkeyService;
    private CaptureWorkflowService? _workflow;
    private ILayoutOcrService? _layoutOcrService;
    private ClipboardService? _clipboardService;
    private AppSettingsService? _settings;
    // Explicit null rather than a bare declaration: release builds never assign
    // this, and the compiler would warn about a never-assigned field.
    private CaptureDatasetRecorder? _datasetRecorder = null;
    private SystemThemeService? _themeService;
    private Window? _hiddenWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log("DispatcherUnhandledException: " + args.Exception);
            WpfMessageBox.Show(args.Exception.ToString(), "ScreenStat 未处理异常", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log("UnhandledException: " + args.ExceptionObject);
        };

        try
        {
            // Match the system light/dark setting before any window is shown,
            // otherwise the first one flashes in the wrong palette.
            _themeService = new SystemThemeService(this);
            _themeService.Apply();
            WindowThemeHelper.Initialize(_themeService);

            // Keep a hidden window so WPF message loop / clipboard / hotkeys stay reliable.
            _hiddenWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                AllowsTransparency = true,
                Opacity = 0,
                ShowActivated = false,
                Title = "ScreenStatHost"
            };
            _hiddenWindow.Show();
            _hiddenWindow.Hide();

            _clipboardService = new ClipboardService();

            // Settings drive the language, so they have to load before any
            // window or menu is built.
            _settings = new AppSettingsService();
            LocalizationService.Apply(_settings.Language);
#if DEBUG
            // Dataset collection is a development tool: it feeds the regression
            // suite, and the samples an end user would produce are of no use to
            // anyone. Release builds ship without it entirely.
            _datasetRecorder = new CaptureDatasetRecorder(_settings);
#endif
            _layoutOcrService = new FallbackLayoutOcrService(
                new RapidLayoutOcrService(),
                new WindowsLayoutOcrService());
            _workflow = new CaptureWorkflowService(_layoutOcrService, _clipboardService, _datasetRecorder);

            _hotkeyService = new HotkeyService();
            _hotkeyService.HotkeyPressed += (_, _) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try { _workflow?.Start(); }
                    catch (Exception ex)
                    {
                        Log(ex.ToString());
                        WpfMessageBox.Show(ex.Message, Strings.AppName, WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
                    }
                });
            };

            var hotkey = _settings.Hotkey;
            var hotkeyOk = _hotkeyService.TryRegister(hotkey);
            CreateTrayIcon(hotkeyOk);

            Log($"Startup OK. Hotkey={hotkey} Registered={hotkeyOk}");
            if (!hotkeyOk)
            {
                // Not fatal: the tray menu still triggers a capture, and the
                // hotkey can be changed in Settings.
                WpfMessageBox.Show(
                    string.Format(Strings.StartupHotkeyFailed, hotkey),
                    Strings.AppName,
                    WpfMessageBoxButton.OK,
                    WpfMessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            WpfMessageBox.Show(
                string.Format(Strings.StartupFailed, ex.Message),
                Strings.AppName,
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _hotkeyService?.Dispose();
            _themeService?.Dispose();
            if (_layoutOcrService is IDisposable disposableOcrService)
            {
                disposableOcrService.Dispose();
            }
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _hiddenWindow?.Close();
        }
        catch (Exception ex)
        {
            Log("Exit cleanup failed: " + ex);
        }

        base.OnExit(e);
    }

    private void CreateTrayIcon(bool hotkeyOk)
    {
        var menu = new Forms.ContextMenuStrip();
        if (_themeService is not null)
        {
            // WinForms ignores the WPF resource dictionaries, so the tray menu
            // has to be told about the theme separately — and repainted when it
            // changes, since the menu outlives every window.
            menu.Renderer = new ThemedToolStripRenderer(_themeService);
            menu.BackColor = _themeService.IsDark
                ? Drawing.Color.FromArgb(43, 43, 43)
                : Drawing.Color.FromArgb(249, 249, 249);
            _themeService.ThemeChanged += (_, _) =>
            {
                menu.BackColor = _themeService.IsDark
                    ? Drawing.Color.FromArgb(43, 43, 43)
                    : Drawing.Color.FromArgb(249, 249, 249);
                menu.Invalidate();
            };
        }

        menu.Items.Add(Strings.TrayCapture, null, (_, _) => Dispatcher.Invoke(() => _workflow?.Start()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Strings.TraySettings, null, (_, _) => Dispatcher.Invoke(ShowSettings));
#if DEBUG
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(CreateDatasetCaptureMenuItem());
        menu.Items.Add("打开测试数据目录", null, (_, _) => OpenDatasetDirectory());
#endif
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Strings.TrayAbout, null, (_, _) =>
        {
            WpfMessageBox.Show(
                string.Format(Strings.AboutBody, CurrentHotkeyText),
                Strings.TrayAbout,
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Information);
        });
        menu.Items.Add(Strings.TrayExit, null, (_, _) => Dispatcher.Invoke(Shutdown));

        _trayIcon = new Forms.NotifyIcon
        {
            Visible = true,
            Icon = CreateTrayIconImage(),
            Text = TrayTooltip(hotkeyOk),
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => _workflow?.Start());
    }

    private string CurrentHotkeyText =>
        (_hotkeyService?.Current ?? _settings?.Hotkey ?? HotkeyDefinition.Default).ToString();

    private string TrayTooltip(bool hotkeyOk) => hotkeyOk
        ? string.Format(Strings.TrayTooltip, CurrentHotkeyText)
        : Strings.TrayTooltipHotkeyUnavailable;

    /// <summary>
    /// Opens Settings. The tray menu is rebuilt afterwards because its items
    /// are plain WinForms strings — a language change does not reach them, and
    /// the tooltip has to reflect a new hotkey.
    /// </summary>
    private void ShowSettings()
    {
        if (_settings is null || _hotkeyService is null)
        {
            return;
        }

        var viewModel = new SettingsViewModel(
            _settings,
            hotkey => _hotkeyService.TryRegister(hotkey),
            _hotkeyService.IsRegistered);

        var window = new SettingsWindow(viewModel);
        window.ShowDialog();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        CreateTrayIcon(_hotkeyService.IsRegistered);
    }

#if DEBUG
    /// <summary>
    /// Turns everyday captures into labeled regression samples. Off by default;
    /// the choice is remembered across runs.
    /// </summary>
    private Forms.ToolStripMenuItem CreateDatasetCaptureMenuItem()
    {
        var item = new Forms.ToolStripMenuItem("采集测试数据")
        {
            CheckOnClick = true,
            Checked = _settings?.IsDatasetCaptureEnabled ?? false,
            Enabled = !(_settings?.IsDatasetCaptureForced ?? false),
            ToolTipText = _datasetRecorder is null ? null : $"保存到 {_datasetRecorder.Root}"
        };

        item.CheckedChanged += (_, _) =>
        {
            if (_settings is not null)
            {
                _settings.IsDatasetCaptureEnabled = item.Checked;
            }
        };

        return item;
    }

    private void OpenDatasetDirectory()
    {
        if (_datasetRecorder is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_datasetRecorder.Root);
            Process.Start(new ProcessStartInfo(_datasetRecorder.Root) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log("Open dataset directory failed: " + ex);
            WpfMessageBox.Show(
                $"无法打开测试数据目录：\n{_datasetRecorder.Root}\n\n{ex.Message}",
                "ScreenStat",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Warning);
        }
    }
#endif

    private static Icon CreateTrayIconImage()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(255, 30, 136, 229));
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 12f, Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var text = "S";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, textBrush, (32 - size.Width) / 2f, (32 - size.Height) / 2f);
        }

        var handle = bitmap.GetHicon();
        using var temp = Icon.FromHandle(handle);
        // Clone so icon survives after bitmap/handle lifetime ends.
        return (Icon)temp.Clone();
    }

    private static void Log(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "ScreenStat-startup.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore logging failures
        }
    }
}
