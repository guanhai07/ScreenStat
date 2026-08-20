using System.Diagnostics;
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
    private readonly ILayoutOcrService _ocrService;
    private readonly ClipboardService _clipboardService;

    /// <summary>Null in slim builds, which ship without the collection tooling.</summary>
    private readonly CaptureDatasetRecorder? _datasetRecorder;

    private readonly object _gate = new();
    private bool _isRunning;
    private List<SelectionWindow> _overlays = new();

    public CaptureWorkflowService(
        ILayoutOcrService ocrService,
        ClipboardService clipboardService,
        CaptureDatasetRecorder? datasetRecorder)
    {
        _ocrService = ocrService;
        _clipboardService = clipboardService;
        _datasetRecorder = datasetRecorder;
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

            var viewModel = new ResultViewModel(_clipboardService);
            viewModel.ShowLoading();
            var session = _datasetRecorder?.TryBeginCapture(bitmap, width, height);
            if (session is not null)
            {
                viewModel.AttachDatasetSession(session);
            }

            var window = new ResultWindow(viewModel);
            window.Show();

            var stopwatch = Stopwatch.StartNew();
            var ocr = await _ocrService.RecognizeLayoutAsync(pixels, width, height).ConfigureAwait(true);
            stopwatch.Stop();
            if (!ocr.Success)
            {
                viewModel.ApplyLayoutFailure(ocr, stopwatch.Elapsed);
                return;
            }

            viewModel.ApplyLayoutSuccess(ocr, stopwatch.Elapsed);
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

