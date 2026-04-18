using System.Windows;
using System.Windows.Media;
using DXFReaderCore.Models;

namespace DXFReaderCore;

internal static class DxfPreviewBuilder
{
    private const double PreviewMargin = 24d;
    private const double PointMarkerHalfSize = 2.5d;
    private const double FallbackStrokeThickness = 1d;
    private const double LineweightPixelsPerMillimeter = 4d;
    private const double MinRenderedStrokeThickness = 0.35d;
    private const double MaxRenderedStrokeThickness = 6d;
    private static readonly Color FallbackStrokeColor = Color.FromRgb(232, 232, 232);

    public static DxfPreviewResult Build(DxfDrawing drawing)
    {
        if (drawing.Primitives.Count == 0 || drawing.Bounds.IsEmpty)
        {
            return DxfPreviewResult.Empty;
        }

        var width = Math.Max(drawing.Bounds.Width, 1d) + PreviewMargin * 2;
        var height = Math.Max(drawing.Bounds.Height, 1d) + PreviewMargin * 2;

        var primitives = drawing.Primitives
            .Select(primitive =>
            {
                var geometry = BuildPrimitiveGeometry(primitive, drawing.Bounds);
                var brush = CreatePrimitiveBrush(primitive);
                brush.Freeze();

                return new DxfPrimitiveVisualItem(
                    primitive,
                    brush,
                    GetBaseStrokeThickness(primitive),
                    geometry);
            })
            .ToArray();

        return new DxfPreviewResult(width, height, primitives);
    }

    public static Geometry BuildPrimitiveGeometry(DxfPrimitive primitive, DxfBounds bounds)
        => BuildGeometry([primitive], bounds);

    public static Geometry BuildGeometry(IEnumerable<DxfPrimitive> primitives, DxfBounds bounds)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            foreach (var primitive in primitives)
            {
                AppendPrimitiveGeometry(context, primitive, bounds);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    public static double GetBaseStrokeThickness(DxfPrimitive primitive)
    {
        if (primitive.LineweightMillimeters <= 0d)
        {
            return FallbackStrokeThickness;
        }

        return Math.Clamp(
            primitive.LineweightMillimeters * LineweightPixelsPerMillimeter,
            MinRenderedStrokeThickness,
            MaxRenderedStrokeThickness);
    }

    private static void AppendPrimitiveGeometry(StreamGeometryContext context, DxfPrimitive primitive, DxfBounds bounds)
    {
        switch (primitive.Kind)
        {
            case DxfPrimitiveKind.Polyline:
                AppendPolylineGeometry(context, primitive, bounds);
                break;
            case DxfPrimitiveKind.Point:
                AppendPointGeometry(context, primitive, bounds);
                break;
        }
    }

    private static void AppendPolylineGeometry(StreamGeometryContext context, DxfPrimitive primitive, DxfBounds bounds)
    {
        if (primitive.Points.Count < 2)
        {
            return;
        }

        context.BeginFigure(ToPreviewPoint(primitive.Points[0], bounds), isFilled: false, isClosed: primitive.IsClosed);
        context.PolyLineTo(
            [.. primitive.Points.Skip(1).Select(point => ToPreviewPoint(point, bounds))],
            isStroked: true,
            isSmoothJoin: false);
    }

    private static void AppendPointGeometry(StreamGeometryContext context, DxfPrimitive primitive, DxfBounds bounds)
    {
        if (primitive.Points.Count == 0)
        {
            return;
        }

        var point = ToPreviewPoint(primitive.Points[0], bounds);
        context.BeginFigure(new Point(point.X - PointMarkerHalfSize, point.Y), isFilled: false, isClosed: false);
        context.LineTo(new Point(point.X + PointMarkerHalfSize, point.Y), isStroked: true, isSmoothJoin: false);
        context.BeginFigure(new Point(point.X, point.Y - PointMarkerHalfSize), isFilled: false, isClosed: false);
        context.LineTo(new Point(point.X, point.Y + PointMarkerHalfSize), isStroked: true, isSmoothJoin: false);
    }

    private static Point ToPreviewPoint(DxfPoint point, DxfBounds bounds) => new(
        point.X - bounds.MinX + PreviewMargin,
        bounds.MaxY - point.Y + PreviewMargin);

    private static SolidColorBrush CreatePrimitiveBrush(DxfPrimitive primitive)
    {
        if (!string.IsNullOrWhiteSpace(primitive.ColorHex)
            && ColorConverter.ConvertFromString(primitive.ColorHex) is Color color)
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(FallbackStrokeColor);
    }
}
