using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DXFReaderCore;
using DXFReaderCore.Models;
using DXFReaderDemo.Models;
using LyuExtensions.Aspects;
using LyuWpfHelper.Extensions;
using LyuWpfHelper.Services;
using LyuWpfHelper.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using ZLogger;

namespace DXFReaderDemo.ViewModels;

[Singleton]
public partial class DxfReaderViewModel : ViewModelBase
{
    private static readonly SolidColorBrush DefaultCanvasBackgroundBrush = CreateFrozenBrush(Colors.Black);
    private static readonly SolidColorBrush DefaultSelectedPrimitiveBrush = CreateFrozenBrush(Color.FromRgb(255, 111, 0));

    [Inject]
    private readonly IDxfParserService _dxfParserService;

    [Inject]
    private readonly ILogger<DxfReaderViewModel> _logger;

    [Inject]
    private readonly IBusyService _busyService;

    public ObservableCollection<DxfPrimitiveListItem> Primitives { get; } = [];

    [ObservableProperty]
    private DxfDrawing? _dxfDrawing;

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private string _fileInfoText = "未加载文件";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPrimitiveInfoText))]
    private DxfPrimitive? _selectedPrimitive;

    public string SelectedPrimitiveInfoText
    {
        get
        {
            if (SelectedPrimitive is null)
            {
                return "未选择图元";
            }

            var selectedItem = Primitives.FirstOrDefault(item => ReferenceEquals(item.Primitive, SelectedPrimitive));
            return selectedItem is null
                ? "未选择图元"
                : $"{selectedItem.Title}{Environment.NewLine}{selectedItem.Detail}{Environment.NewLine}{selectedItem.CoordinateText}";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasBackgroundBrush))]
    private Color _canvasBackgroundColor = Colors.Black;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPrimitiveBrush))]
    private Color _selectedPrimitiveColor = Color.FromRgb(255, 111, 0);

    public Brush CanvasBackgroundBrush => CanvasBackgroundColor == Colors.Black
        ? DefaultCanvasBackgroundBrush
        : CreateFrozenBrush(CanvasBackgroundColor);

    public Brush SelectedPrimitiveBrush => SelectedPrimitiveColor == Color.FromRgb(255, 111, 0)
        ? DefaultSelectedPrimitiveBrush
        : CreateFrozenBrush(SelectedPrimitiveColor);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ImportDxfAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DXF 文件 (*.dxf)|*.dxf|所有文件 (*.*)|*.*",
            Title = "选择 DXF 文件"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await LoadDxfFileAsync(dialog.FileName);
    }

    private async Task LoadDxfFileAsync(string filePath)
    {
        try
        {
            _logger.ZLogInformation($"开始加载 DXF 文件: {filePath}");
            FileInfoText = "正在解析并生成预览...";
            Primitives.Clear();
            SelectedPrimitive = null;

            DxfDrawing? drawing = null;
            await _busyService.RunWithBusyAsync(async () =>
            {
                drawing = await Task.Run(() => _dxfParserService.Parse(filePath));
            }, new BusyDisplayOptions
            {
                Title = "导入 DXF",
                Message = "正在解析文件并生成图元..."
            });

            if (drawing == null)
            {
                _logger.ZLogError($"DXF 文件加载失败: {filePath}");
                DxfDrawing = null;
                CurrentFilePath = filePath;
                FileInfoText = "加载失败";
                return;
            }

            DxfDrawing = drawing;
            CurrentFilePath = filePath;
            PopulatePrimitives(drawing);

            var fileName = Path.GetFileName(filePath);
            FileInfoText =
                $"文件: {fileName} | 图层: {drawing.LayerEntityCounts.Count} | 图元: {drawing.Primitives.Count} | 轮廓: {drawing.ContourCount} | 尺寸: {drawing.Bounds.Width:F2} x {drawing.Bounds.Height:F2} | 单位: {drawing.UnitsText}";

            _logger.ZLogInformation($"DXF 文件加载成功: {fileName}, 图层数 {drawing.LayerEntityCounts.Count}, 图元数 {drawing.Primitives.Count}, 轮廓数 {drawing.ContourCount}");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"DXF 文件加载失败: {filePath}");
            DxfDrawing = null;
            CurrentFilePath = filePath;
            Primitives.Clear();
            SelectedPrimitive = null;
            FileInfoText = $"加载失败: {ex.Message}";
        }
    }

    private void PopulatePrimitives(DxfDrawing drawing)
    {
        Primitives.Clear();

        for (var i = 0; i < drawing.Primitives.Count; i++)
        {
            var primitive = drawing.Primitives[i];
            var bounds = GetBounds(primitive);

            Primitives.Add(new DxfPrimitiveListItem
            {
                Primitive = primitive,
                Index = i,
                Title = $"图元 {i + 1}",
                Detail = $"类型: {primitive.SourceType} | 图层: {primitive.LayerName} | 点数: {primitive.Points.Count} | {(primitive.IsClosed ? "闭合" : "开放")} | 线宽: {primitive.LineweightMillimeters:F2} mm",
                CoordinateText = CreateCoordinateText(bounds, primitive)
            });
        }

        SelectedPrimitive = drawing.Primitives.FirstOrDefault();
    }

    private static string CreateCoordinateText(DxfBounds bounds, DxfPrimitive primitive)
    {
        var centerX = bounds.IsEmpty ? 0d : (bounds.MinX + bounds.MaxX) / 2d;
        var centerY = bounds.IsEmpty ? 0d : (bounds.MinY + bounds.MaxY) / 2d;

        return bounds.IsEmpty
            ? $"坐标: {FormatPoint(primitive.Points.FirstOrDefault())}"
            : $"坐标范围: X[{bounds.MinX:F3}, {bounds.MaxX:F3}] Y[{bounds.MinY:F3}, {bounds.MaxY:F3}] | 中心: ({centerX:F3}, {centerY:F3})";
    }

    private static DxfBounds GetBounds(DxfPrimitive primitive)
    {
        var bounds = DxfBounds.Empty;
        foreach (var point in primitive.Points)
        {
            bounds = bounds.Include(point);
        }

        return bounds;
    }

    private static string FormatPoint(DxfPoint? point)
        => point is null ? "-" : $"({point.Value.X:F3}, {point.Value.Y:F3}, {point.Value.Z:F3})";

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
