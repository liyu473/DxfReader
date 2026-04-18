using System.Windows.Media;
using DXFReaderCore.Models;

namespace DXFReaderCore;

internal readonly record struct DxfPreviewResult(
    double Width,
    double Height,
    IReadOnlyList<DxfPrimitiveVisualItem> Primitives)
{
    public bool IsEmpty => Primitives.Count == 0 || Width <= 0 || Height <= 0;

    public static DxfPreviewResult Empty { get; } = new(640, 480, Array.Empty<DxfPrimitiveVisualItem>());
}

internal readonly record struct DxfPrimitiveVisualItem(
    DxfPrimitive Primitive,
    SolidColorBrush Brush,
    double BaseStrokeThickness,
    Geometry Geometry);
