using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenStat.App.Infrastructure;

namespace ScreenStat.App.Views;

public partial class SelectionWindow : Window
{
    private readonly MonitorInfo _monitor;
    private bool _selecting;
    private NativeMethods.Point _startPx;
    private NativeMethods.Point _currentPx;

    public event EventHandler<ScreenRegion>? SelectionCompleted;
    public event EventHandler? SelectionCanceled;

    public SelectionWindow(MonitorInfo monitor)
    {
        _monitor = monitor;
        InitializeComponent();

        Left = monitor.PixelBounds.Left / monitor.ScaleX;
        Top = monitor.PixelBounds.Top / monitor.ScaleY;
        Width = monitor.PixelBounds.Width / monitor.ScaleX;
        Height = monitor.PixelBounds.Height / monitor.ScaleY;

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDimPath(null);
        Activate();
        Focus();
        Keyboard.Focus(this);
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            SelectionCanceled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        NativeMethods.GetCursorPos(out _startPx);
        _currentPx = _startPx;
        _selecting = true;
        CaptureMouse();
        HintBorder.Visibility = Visibility.Collapsed;
        UpdateSelectionVisual();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        NativeMethods.GetCursorPos(out _currentPx);
        UpdateSelectionVisual();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selecting = false;
        NativeMethods.GetCursorPos(out _currentPx);
        ReleaseMouseCapture();

        var region = ScreenRegion.FromPoints(_startPx, _currentPx);
        if (region.Width < 3 || region.Height < 3)
        {
            SelectionCanceled?.Invoke(this, EventArgs.Empty);
            return;
        }

        SelectionCompleted?.Invoke(this, region);
        e.Handled = true;
    }

    private void UpdateSelectionVisual()
    {
        var region = ScreenRegion.FromPoints(_startPx, _currentPx);
        if (region.IsEmpty)
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            SizeBadge.Visibility = Visibility.Collapsed;
            UpdateDimPath(null);
            return;
        }

        var x = (region.X - _monitor.PixelBounds.Left) / _monitor.ScaleX;
        var y = (region.Y - _monitor.PixelBounds.Top) / _monitor.ScaleY;
        var w = region.Width / _monitor.ScaleX;
        var h = region.Height / _monitor.ScaleY;

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = Math.Max(w, 0);
        SelectionRectangle.Height = Math.Max(h, 0);
        SelectionRectangle.Visibility = Visibility.Visible;

        UpdateSizeBadge(region, x, y);
        UpdateDimPath(new Rect(x, y, w, h));
    }

    /// <summary>
    /// Reports the selection in physical pixels — the units the capture and the
    /// recognizer actually work in, not the DPI-scaled ones on screen.
    /// </summary>
    private void UpdateSizeBadge(ScreenRegion region, double x, double y)
    {
        SizeText.Text = $"{region.Width} × {region.Height}";
        SizeBadge.Visibility = Visibility.Visible;

        // Measure before placing, or the first frame lands with a stale height.
        SizeBadge.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var badgeHeight = SizeBadge.DesiredSize.Height;

        // Sits above the selection, and flips inside it near the top edge.
        var top = y - badgeHeight - 6;
        if (top < 0)
        {
            top = y + 6;
        }

        Canvas.SetLeft(SizeBadge, Math.Max(x, 0));
        Canvas.SetTop(SizeBadge, top);
    }

    private void UpdateDimPath(Rect? clearRect)
    {
        var bounds = new Rect(0, 0, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
        var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
        geometry.Children.Add(new RectangleGeometry(bounds));
        if (clearRect is { } rect && rect.Width > 0 && rect.Height > 0)
        {
            geometry.Children.Add(new RectangleGeometry(rect));
        }

        DimPath.Data = geometry;
        RootGrid.Background = System.Windows.Media.Brushes.Transparent;
    }
}

