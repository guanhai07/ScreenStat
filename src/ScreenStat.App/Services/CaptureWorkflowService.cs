using System.IO;
using System.Windows;
using ScreenStat.App.Infrastructure;
using ScreenStat.App.ViewModels;
using ScreenStat.App.Views;
using ScreenStat.Core.Abstractions;

namespace ScreenStat.App.Services;

internal sealed class CaptureWorkflowService
{
    private readonly ScreenCaptureService _captureService = new();
    private readonly IOcrService _ocrService;
    private readonly ClipboardService _clipboardService;
    private readonly object _gate = new();
    private bool _isRunning;
    private List<SelectionWindow> _overlays = new();

    public CaptureWorkflowService(IOcrService ocrService, ClipboardService clipboardService)
    {
        _ocrService = ocrService;
        _clipboardService = clipboardService;
    }

    public async void Start()
    {
        lock (_gate)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
        }

        try
        {
            var region = await SelectRegionAsync().ConfigureAwait(true);
            if (region is null || region.Value.IsEmpty)
            {
                return;
            }

            // Wait a tick so overlay windows are fully gone before BitBlt.
            await Task.Delay(50).ConfigureAwait(true);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            var bitmap = _captureService.Capture(region.Value);
            var pixels = ScreenCaptureService.ToBgra32Pixels(bitmap, out var width, out var height);

            // Debug original crop.
            try
            {
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var fs = System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ScreenStat-last-capture.png"));
                enc.Save(fs);
            }
            catch
            {
                // ignore
            }

            var viewModel = new ResultViewModel(_clipboardService);
            viewModel.ShowLoading();
            var window = new ResultWindow(viewModel);
            window.Show();

            var ocr = await _ocrService.RecognizeAsync(pixels, width, height).ConfigureAwait(true);
            if (!ocr.Success)
            {
                viewModel.ApplyFailure(ocr.ErrorMessage ?? "OCR 失败");
                return;
            }

            viewModel.ApplyOcrSuccess(ocr);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"截图统计失败：{ex.Message}", "ScreenStat", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CloseOverlays();
            lock (_gate)
            {
                _isRunning = false;
            }
        }
    }

    private Task<ScreenRegion?> SelectRegionAsync()
    {
        var tcs = new TaskCompletionSource<ScreenRegion?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitors = VirtualScreenHelper.GetMonitors();
        if (monitors.Count == 0)
        {
            tcs.TrySetResult(null);
            return tcs.Task;
        }

        _overlays = new List<SelectionWindow>();
        var settled = new[] { 0 };

        void Complete(ScreenRegion? region)
        {
            if (Interlocked.Exchange(ref settled[0], 1) == 1)
            {
                return;
            }

            CloseOverlays();
            tcs.TrySetResult(region);
        }

        foreach (var monitor in monitors)
        {
            var overlay = new SelectionWindow(monitor);
            overlay.SelectionCompleted += (_, region) => Complete(region);
            overlay.SelectionCanceled += (_, _) => Complete(null);
            _overlays.Add(overlay);
            overlay.Show();
        }

        return tcs.Task;
    }

    private void CloseOverlays()
    {
        foreach (var overlay in _overlays.ToArray())
        {
            try
            {
                overlay.Close();
            }
            catch
            {
                // ignore
            }
        }

        _overlays.Clear();
    }
}

