using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DXFReaderCore.Models;

namespace DXFReaderCore.Controls;

public class DxfViewer : Control
{
    private readonly Dictionary<Path, double> _pathStrokeThicknesses = [];
    private readonly Dictionary<Path, DxfPrimitive> _pathPrimitives = [];
    private Canvas? _canvas;
    private ScrollViewer? _scrollViewer;
    private DxfDrawing? _previewDrawing;
    private DxfPreviewResult _preview = DxfPreviewResult.Empty;
    private Path? _selectedPrimitivePath;
    private readonly ScaleTransform _canvasScaleTransform = new(1d, 1d);
    private double _zoomFactor = 1d;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private MouseButton? _panButton;

    private const double ZoomStep = 1.15d;
    private const double MinZoomFactor = 0.1d;
    private const double MaxZoomFactor = 99999d;
    private const double PreviewMargin = 24d;
    private const double MinimumHitTolerance = 4d;
    private const double MaximumHitTolerance = 8d;

    static DxfViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DxfViewer),
            new FrameworkPropertyMetadata(typeof(DxfViewer)));
    }

    public static readonly DependencyProperty DrawingProperty = DependencyProperty.Register(
        nameof(Drawing),
        typeof(DxfDrawing),
        typeof(DxfViewer),
        new PropertyMetadata(null, OnDrawingChanged));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(DxfViewer),
        new PropertyMetadata(1.0, OnStrokeThicknessChanged));

    public static readonly DependencyProperty AutoFitProperty = DependencyProperty.Register(
        nameof(AutoFit),
        typeof(bool),
        typeof(DxfViewer),
        new PropertyMetadata(true, OnAutoFitChanged));

    public static readonly DependencyProperty SelectedPrimitiveProperty = DependencyProperty.Register(
        nameof(SelectedPrimitive),
        typeof(DxfPrimitive),
        typeof(DxfViewer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedPrimitiveChanged));

    public static readonly DependencyProperty SelectedPrimitiveBrushProperty = DependencyProperty.Register(
        nameof(SelectedPrimitiveBrush),
        typeof(Brush),
        typeof(DxfViewer),
        new PropertyMetadata(CreateDefaultSelectedPrimitiveBrush(), OnSelectedPrimitiveBrushChanged));

    public DxfDrawing? Drawing
    {
        get => (DxfDrawing?)GetValue(DrawingProperty);
        set => SetValue(DrawingProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool AutoFit
    {
        get => (bool)GetValue(AutoFitProperty);
        set => SetValue(AutoFitProperty, value);
    }

    public DxfPrimitive? SelectedPrimitive
    {
        get => (DxfPrimitive?)GetValue(SelectedPrimitiveProperty);
        set => SetValue(SelectedPrimitiveProperty, value);
    }

    public Brush SelectedPrimitiveBrush
    {
        get => (Brush)GetValue(SelectedPrimitiveBrushProperty);
        set => SetValue(SelectedPrimitiveBrushProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        if (_canvas is not null)
        {
            _canvas.LayoutTransform = _canvasScaleTransform;
        }

        RebuildPreview(forceRebuild: true);
    }

    public void ResetView()
    {
        _zoomFactor = 1d;
        ApplyAutoFit();
        CenterViewport();
    }

    private static void OnDrawingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DxfViewer viewer)
        {
            viewer.RebuildPreview(forceRebuild: true);
        }
    }

    private static void OnStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DxfViewer viewer)
        {
            viewer.UpdateStrokeThickness();
        }
    }

    private static void OnAutoFitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DxfViewer viewer)
        {
            viewer.ApplyAutoFit();
        }
    }

    private static void OnSelectedPrimitiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DxfViewer viewer)
        {
            viewer.UpdateSelectedPrimitive();
        }
    }

    private static void OnSelectedPrimitiveBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DxfViewer viewer)
        {
            viewer.UpdateSelectedPrimitive();
        }
    }

    private void RebuildPreview(bool forceRebuild)
    {
        if (_canvas == null)
        {
            return;
        }

        _canvas.Children.Clear();
        _canvas.Width = 0d;
        _canvas.Height = 0d;
        _selectedPrimitivePath = null;
        _pathStrokeThicknesses.Clear();
        _pathPrimitives.Clear();

        if (Drawing == null)
        {
            _preview = DxfPreviewResult.Empty;
            _previewDrawing = null;
            SelectedPrimitive = null;
            ResetView();
            return;
        }

        if (forceRebuild || !ReferenceEquals(_previewDrawing, Drawing))
        {
            _preview = DxfPreviewBuilder.Build(Drawing);
            _previewDrawing = Drawing;
        }

        if (_preview.IsEmpty)
        {
            SelectedPrimitive = null;
            ResetView();
            return;
        }

        foreach (var primitive in _preview.Primitives)
        {
            var strokePath = new Path
            {
                Data = primitive.Geometry,
                Stroke = primitive.Brush,
                StrokeThickness = GetEffectiveStrokeThickness(primitive.BaseStrokeThickness),
                Fill = null,
                IsHitTestVisible = false,
            };

            _pathStrokeThicknesses[strokePath] = primitive.BaseStrokeThickness;
            _pathPrimitives[strokePath] = primitive.Primitive;
            _canvas.Children.Add(strokePath);
        }

        _canvas.Width = _preview.Width;
        _canvas.Height = _preview.Height;

        if (SelectedPrimitive is not null && !Drawing.Primitives.Contains(SelectedPrimitive))
        {
            SelectedPrimitive = null;
        }

        UpdateSelectedPrimitive();
        ResetView();
    }

    private void UpdateStrokeThickness()
    {
        if (_canvas == null)
        {
            return;
        }

        foreach (var child in _canvas.Children.OfType<Path>())
        {
            if (child.Stroke is null)
            {
                continue;
            }

            child.StrokeThickness = GetEffectiveStrokeThickness(_pathStrokeThicknesses.GetValueOrDefault(child, StrokeThickness));
        }

        if (_selectedPrimitivePath is not null)
        {
            _selectedPrimitivePath.StrokeThickness = GetEffectiveStrokeThickness(GetSelectedPrimitiveBaseStrokeThickness());
        }
    }

    private void ApplyAutoFit()
    {
        if (_canvas == null)
        {
            return;
        }

        var scale = GetBaseScale() * _zoomFactor;
        _canvasScaleTransform.ScaleX = scale;
        _canvasScaleTransform.ScaleY = scale;
        UpdateStrokeThickness();
    }

    private void UpdateSelectedPrimitive()
    {
        if (_canvas == null)
        {
            return;
        }

        if (_selectedPrimitivePath is not null)
        {
            _canvas.Children.Remove(_selectedPrimitivePath);
            _selectedPrimitivePath = null;
        }

        if (Drawing == null || SelectedPrimitive is null)
        {
            UpdateStrokeThickness();
            return;
        }

        _selectedPrimitivePath = new Path
        {
            Data = DxfPreviewBuilder.BuildPrimitiveGeometry(SelectedPrimitive, Drawing.Bounds),
            Stroke = SelectedPrimitiveBrush,
            StrokeThickness = GetEffectiveStrokeThickness(GetSelectedPrimitiveBaseStrokeThickness()),
            Fill = null,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false,
        };

        _canvas.Children.Add(_selectedPrimitivePath);
        UpdateStrokeThickness();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (!_preview.IsEmpty)
        {
            ApplyAutoFit();
        }
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);

        if (_scrollViewer == null || _preview.IsEmpty)
        {
            return;
        }

        e.Handled = true;

        var oldScale = _canvasScaleTransform.ScaleX <= 0d ? 1d : _canvasScaleTransform.ScaleX;
        var mousePosition = e.GetPosition(_scrollViewer);
        var horizontalOffset = _scrollViewer.HorizontalOffset;
        var verticalOffset = _scrollViewer.VerticalOffset;
        var zoomMultiplier = e.Delta > 0 ? ZoomStep : 1d / ZoomStep;

        _zoomFactor = Math.Clamp(_zoomFactor * zoomMultiplier, MinZoomFactor, MaxZoomFactor);
        ApplyAutoFit();

        Dispatcher.BeginInvoke(() =>
        {
            if (_scrollViewer == null)
            {
                return;
            }

            var newScale = _canvasScaleTransform.ScaleX <= 0d ? 1d : _canvasScaleTransform.ScaleX;
            var scaleFactor = newScale / oldScale;

            _scrollViewer.ScrollToHorizontalOffset(Math.Max(0d, (horizontalOffset + mousePosition.X) * scaleFactor - mousePosition.X));
            _scrollViewer.ScrollToVerticalOffset(Math.Max(0d, (verticalOffset + mousePosition.Y) * scaleFactor - mousePosition.Y));
        }, DispatcherPriority.Loaded);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (_preview.IsEmpty)
        {
            return;
        }

        var hitPrimitive = HitTestPrimitive(e.GetPosition(this));
        if (hitPrimitive is not null)
        {
            SelectedPrimitive = hitPrimitive;
            e.Handled = true;
            return;
        }

        e.Handled = false;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        if (_scrollViewer == null || _preview.IsEmpty)
        {
            return;
        }

        BeginPan(MouseButton.Middle, e.GetPosition(_scrollViewer));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isPanning || _scrollViewer == null)
        {
            return;
        }

        var currentPoint = e.GetPosition(_scrollViewer);
        var delta = currentPoint - _panStartPoint;

        _scrollViewer.ScrollToHorizontalOffset(Math.Max(0d, _panStartHorizontalOffset - delta.X));
        _scrollViewer.ScrollToVerticalOffset(Math.Max(0d, _panStartVerticalOffset - delta.Y));
        e.Handled = true;
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);

        if (!_isPanning || _panButton != e.ChangedButton)
        {
            return;
        }

        EndPan();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndPan();
    }

    private double GetBaseScale()
    {
        if (!AutoFit || _preview.IsEmpty)
        {
            return 1d;
        }

        var viewportWidth = _scrollViewer?.ViewportWidth ?? 0d;
        var viewportHeight = _scrollViewer?.ViewportHeight ?? 0d;

        if (viewportWidth <= 0d)
        {
            viewportWidth = Math.Max(0d, ActualWidth);
        }

        if (viewportHeight <= 0d)
        {
            viewportHeight = Math.Max(0d, ActualHeight);
        }

        if (viewportWidth <= 0d || viewportHeight <= 0d)
        {
            return 1d;
        }

        return Math.Min(viewportWidth / _preview.Width, viewportHeight / _preview.Height);
    }

    private double GetEffectiveStrokeThickness(double baseStrokeThickness)
    {
        var scale = _canvasScaleTransform.ScaleX;
        if (scale <= 0d)
        {
            return baseStrokeThickness;
        }

        return Math.Max(baseStrokeThickness / scale, 0.01d);
    }

    private void CenterViewport()
    {
        if (_scrollViewer == null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (_scrollViewer == null)
            {
                return;
            }

            var horizontalOffset = Math.Max(0d, (_scrollViewer.ExtentWidth - _scrollViewer.ViewportWidth) / 2d);
            var verticalOffset = Math.Max(0d, (_scrollViewer.ExtentHeight - _scrollViewer.ViewportHeight) / 2d);

            _scrollViewer.ScrollToHorizontalOffset(horizontalOffset);
            _scrollViewer.ScrollToVerticalOffset(verticalOffset);
        }, DispatcherPriority.Loaded);
    }

    private void EndPan()
    {
        _isPanning = false;
        _panButton = null;
        Cursor = Cursors.Arrow;

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void BeginPan(MouseButton button, Point position)
    {
        if (_scrollViewer == null)
        {
            return;
        }

        _isPanning = true;
        _panButton = button;
        _panStartPoint = position;
        _panStartHorizontalOffset = _scrollViewer.HorizontalOffset;
        _panStartVerticalOffset = _scrollViewer.VerticalOffset;
        Cursor = Cursors.Hand;
        CaptureMouse();
    }

    private static Brush CreateDefaultSelectedPrimitiveBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(255, 111, 0));
        brush.Freeze();
        return brush;
    }

    private double GetSelectedPrimitiveBaseStrokeThickness()
        => SelectedPrimitive is null
            ? StrokeThickness
            : DxfPreviewBuilder.GetBaseStrokeThickness(SelectedPrimitive);

    private DxfPrimitive? HitTestPrimitive(Point viewPoint)
    {
        if (_canvas == null || Drawing == null)
        {
            return null;
        }

        DxfPrimitive? bestPrimitive = null;
        var bestDistanceSquared = double.MaxValue;

        foreach (var (path, primitive) in _pathPrimitives)
        {
            if (!_pathStrokeThicknesses.TryGetValue(path, out var baseStrokeThickness))
            {
                continue;
            }

            var tolerance = GetHitTolerance(baseStrokeThickness);
            var distanceSquared = GetDistanceSquaredToPrimitive(primitive, viewPoint, Drawing.Bounds);
            if (distanceSquared <= tolerance * tolerance && distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestPrimitive = primitive;
            }
        }

        return bestPrimitive;
    }

    private double GetDistanceSquaredToPrimitive(DxfPrimitive primitive, Point point, DxfBounds bounds)
    {
        return primitive.Kind switch
        {
            DxfPrimitiveKind.Point => primitive.Points.Count == 0
                ? double.MaxValue
                : GetDistanceSquared(point, ToViewPoint(primitive.Points[0], bounds)),
            DxfPrimitiveKind.Polyline => GetDistanceSquaredToPolyline(primitive, point, bounds),
            _ => double.MaxValue,
        };
    }

    private double GetDistanceSquaredToPolyline(DxfPrimitive primitive, Point point, DxfBounds bounds)
    {
        if (primitive.Points.Count == 0)
        {
            return double.MaxValue;
        }

        var minDistanceSquared = double.MaxValue;
        for (var i = 1; i < primitive.Points.Count; i++)
        {
            minDistanceSquared = Math.Min(
                minDistanceSquared,
                GetDistanceSquaredToSegment(
                    point,
                    ToViewPoint(primitive.Points[i - 1], bounds),
                    ToViewPoint(primitive.Points[i], bounds)));
        }

        if (primitive.IsClosed && primitive.Points.Count > 2)
        {
            minDistanceSquared = Math.Min(
                minDistanceSquared,
                GetDistanceSquaredToSegment(
                    point,
                    ToViewPoint(primitive.Points[^1], bounds),
                    ToViewPoint(primitive.Points[0], bounds)));
        }

        return minDistanceSquared;
    }

    private static double GetDistanceSquaredToSegment(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
        {
            return GetDistanceSquared(point, start);
        }

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0d, 1d);

        var closest = new Point(start.X + dx * t, start.Y + dy * t);
        return GetDistanceSquared(point, closest);
    }

    private static double GetDistanceSquared(Point left, Point right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return dx * dx + dy * dy;
    }

    private static double GetHitTolerance(double baseStrokeThickness)
    {
        var tolerance = baseStrokeThickness + 3d;
        return Math.Clamp(tolerance, MinimumHitTolerance, MaximumHitTolerance);
    }

    private Point ToViewPoint(DxfPoint point, DxfBounds bounds)
    {
        if (_canvas == null)
        {
            return default;
        }

        return _canvas.TranslatePoint(ToPreviewPoint(point, bounds), this);
    }

private static Point ToPreviewPoint(DxfPoint point, DxfBounds bounds) => new(
    point.X - bounds.MinX + PreviewMargin,
    bounds.MaxY - point.Y + PreviewMargin);
}
