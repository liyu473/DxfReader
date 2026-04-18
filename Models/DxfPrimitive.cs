using System.Collections.ObjectModel;

namespace DXFReaderCore.Models;

public enum DxfPrimitiveKind
{
    Polyline,
    Point,
}

public sealed class DxfPrimitive
{
    public DxfPrimitive(
        DxfPrimitiveKind kind,
        IReadOnlyList<DxfPoint> points,
        bool isClosed,
        string layerName,
        string colorHex,
        string sourceType,
        double lineweightMillimeters)
    {
        Kind = kind;
        Points = points;
        IsClosed = isClosed;
        LayerName = layerName;
        ColorHex = colorHex;
        SourceType = sourceType;
        LineweightMillimeters = lineweightMillimeters;
    }

    public DxfPrimitiveKind Kind { get; }

    public IReadOnlyList<DxfPoint> Points { get; }

    public bool IsClosed { get; }

    public string LayerName { get; }

    public string ColorHex { get; }

    public string SourceType { get; }

    public double LineweightMillimeters { get; }

    public static IReadOnlyList<DxfPoint> FreezePoints(IEnumerable<DxfPoint> points)
        => new ReadOnlyCollection<DxfPoint>([.. points]);
}
