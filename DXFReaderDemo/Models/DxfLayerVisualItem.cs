using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace DXFReaderDemo.Models;

public partial class DxfLayerVisualItem : ObservableObject
{
    public DxfLayerVisualItem(string layerName, int entityCount, Brush stroke, Geometry geometry)
    {
        LayerName = layerName;
        EntityCount = entityCount;
        Stroke = stroke;
        Geometry = geometry;
    }

    public string LayerName { get; }

    public int EntityCount { get; }

    public Brush Stroke { get; }

    public Geometry Geometry { get; }

    [ObservableProperty]
    private bool _isVisible = true;
}
