using System.Collections.ObjectModel;

namespace DXFReaderCore.Models;

public sealed class DxfContour
{
    public DxfContour(
        int index,
        IReadOnlyList<DxfPrimitive> primitives,
        DxfBounds bounds,
        bool isClosed)
    {
        Index = index;
        Primitives = primitives;
        Bounds = bounds;
        IsClosed = isClosed;
    }

    public int Index { get; }

    public IReadOnlyList<DxfPrimitive> Primitives { get; }

    public DxfBounds Bounds { get; }

    public bool IsClosed { get; }

    public int SegmentCount => Primitives.Count;

    public int PointCount => Primitives.Sum(static primitive => primitive.Points.Count);

    public string PrimaryLayerName => Primitives.FirstOrDefault()?.LayerName ?? "0";

    public static IReadOnlyList<DxfPrimitive> FreezePrimitives(IEnumerable<DxfPrimitive> primitives)
        => new ReadOnlyCollection<DxfPrimitive>([.. primitives]);
}
