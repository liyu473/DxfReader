using System.Collections.ObjectModel;
using System.IO;

namespace DXFReaderCore.Models;

public sealed class DxfDrawing
{
    public DxfDrawing(
        string sourcePath,
        IReadOnlyList<DxfPrimitive> primitives,
        IReadOnlyList<DxfContour> contours,
        DxfBounds bounds,
        IReadOnlyDictionary<string, int> layerEntityCounts,
        IReadOnlyDictionary<string, int> sourceTypeCounts,
        string unitsText)
    {
        SourcePath = sourcePath;
        Primitives = primitives;
        Contours = contours;
        Bounds = bounds;
        LayerEntityCounts = layerEntityCounts;
        SourceTypeCounts = sourceTypeCounts;
        UnitsText = unitsText;
    }

    public string SourcePath { get; }

    public string FileName => Path.GetFileName(SourcePath);

    public IReadOnlyList<DxfPrimitive> Primitives { get; }

    public IReadOnlyList<DxfContour> Contours { get; }

    public DxfBounds Bounds { get; }

    public IReadOnlyDictionary<string, int> LayerEntityCounts { get; }

    public IReadOnlyDictionary<string, int> SourceTypeCounts { get; }

    public string UnitsText { get; }

    public int EntityCount => Primitives.Count;

    public int ContourCount => Contours.Count;

    public static DxfDrawing Empty(string sourcePath) => new(
        sourcePath,
        [],
        [],
        DxfBounds.Empty,
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)),
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)),
        "Unitless / Unspecified");
}
